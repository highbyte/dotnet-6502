;ACME assembler
;!to "./dma_delay.prg"

; Scrolling the whole screen with the VIC-II's counters: DMA delay and FLD.
;
; The VIC-II reads a text row's screen codes on the row's first raster line, the bad line, into
; an internal line of 40 entries, and its video counter VC decides which screen memory the row
; comes from. Both are driven by the "bad line condition": raster line $30-$F7 whose low three
; bits equal YSCROLL, with DEN seen during line $30 (VIC-II article, sections 3.5, 3.7.2, 3.14).
; A program that controls when the condition appears controls where the rows come from:
;
;   FLD        keep the condition away for a few lines before the first row (YSCROLL written
;              on every line so it never matches) and the rows start that many lines later.
;   DMA delay  create the condition in the middle of an idle line, some cycles after cycle 14:
;              the fetches start there, VC advances by fewer than 40 on that line, and from then
;              on every row begins that many characters into its screen memory. The whole
;              screen appears shifted to the right, the columns pushed off the right edge come
;              back at the left of the next row, and no byte of screen memory has moved.
;
; This program does both every frame once the space bar is pressed, with the shift and the line
; count changing frame by frame, so the text bounces around the screen for one full sweep and
; then stands still until the next press. The delay line's own row (row 0, blank here) shows the
; seam: the first three fetches after the CPU loses the bus read $FF as screen code and the low
; nibble of the CPU's next opcode as colour, so the instruction after the $d011 write is chosen
; to have a 6 in that nibble, the background colour. 24 row mode hides the rest.
;
; The DMA delay write has to land on an exact cycle; the line clock sync of the other raster
; samples puts it there, and a NOP slide entered at a computed point provides the frame's delay.

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

SCREEN_RAM      = $0400
COLOR_RAM       = $d800

SCREEN_CONTROL_REGISTER_1 = $d011
SCREEN_RASTER_LINE = $d012
SCREEN_CONTROL_REGISTER_2 = $d016
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021

CIA1_PORT_A = $dc00
CIA1_PORT_B = $dc01
CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

IDLE_BYTE_ADDRESS = $3fff       ; shown by idle lines: blank

COLUMNS_40 = %11001000          ; $d016: 40 columns, XSCROLL 0
D011_BASE = %00010000           ; DEN on, 24 rows (the delay line's seam and the rows' edges are hidden)
YSCROLL_IDLE = 7                ; no bad line on 48-54 with this; the FLD writes take over from 51

FIRST_ROW_LINE = 51             ; the first row's bad line with YSCROLL 3 and no FLD
IRQ_LINE = 40
MAX_SHIFT = 38                  ; characters: the condition can be created up to cycle 14 + 38
SPACE_ROW_SELECT = %01111111    ; the space bar is row 7, column 4
SPACE_COLUMN_BIT = %00010000
SLIDE_NOPS = 32                 ; the NOP slide covers delays up to 64 cycles

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

FILLER       = $02      ; zero page: scratch, also the byte the timing filler reads
LINE_CYCLES  = $03      ; zero page: 63 on PAL, 65 on NTSC
SAVE_X       = $06      ; zero page: the caller's X across SyncToLine
SHIFT        = $07      ; zero page: this frame's DMA delay, 1-MAX_SHIFT characters
SHIFT_DIR    = $08      ; zero page: 1 or -1
FLD_LINES    = $09      ; zero page: this frame's FLD, half the shift in lines
RUNNING      = $0a      ; zero page: 1 while the sweep runs, set by the main loop, cleared by the interrupt
ODD          = $0b      ; zero page: 1 if the delay is odd
ROWS_D011    = $0e      ; zero page: $d011 for the rows, the line after the delay line's low bits
DELAY_LINE   = $0c      ; zero page: the raster line the condition is created on
FRAME        = $0d      ; zero page: frame counter, the motion advances every fourth frame
IS_PAL       = $fb      ; zero page: 1 on PAL, 0 on NTSC
LAST_LINE_LO = $fc      ; zero page: DetectModel's scratch
CALIB        = $fd      ; zero page: line clock value 9 cycles into a raster line (see CalibrateLineClock)
TARGET_LINE  = $fe      ; zero page: raster line SyncToLine waits for

; Lines the calibration polls: in the top border, above any bad line, no sprites there.
CALIB_FIRST_LINE = 10
CALIB_LINES = 16

; SyncToLine returns 50 cycles into the line. (The centre must not be below SLIDE_RANGE: the
; table's entry for the difference -SLIDE_RANGE is SLIDE_CENTER - SLIDE_RANGE.)
SLIDE_CENTER = 9
SLIDE_RANGE = 8

COLOR_WHITE = 1
COLOR_BLUE = 6
COLOR_LIGHT_BLUE = 14
COLOR_YELLOW = 7
SPACE_CHAR = $20

;------------------------------------------------------------
;Macros
;------------------------------------------------------------

; Burn .cycles cycles (>= 2). NOPs in pairs, with a 3-cycle BIT for an odd count.
!macro delay_cycles .cycles {
	!if .cycles & 1 {
		bit FILLER
		!fill (.cycles - 3) / 2, $ea
	} else {
		!fill .cycles / 2, $ea
	}
}

; Wait for the raster line in TARGET_LINE, then read the line clock, always 3 cycles after the
; poll got out of its loop, so the reading says when that was.
!macro poll_and_read_clock {
	lda TARGET_LINE                 ; 3
-	cmp SCREEN_RASTER_LINE          ; 4
	bne -                           ; 3 taken / 2 falling through
	lda CIA1_TIMER_A_LO             ; 4   (the read lands on the 4th cycle)
}

; Copy a $ff-terminated screen-code string to screen memory.
!macro print .screenaddr, .text {
	ldx #0
-	lda .text,x
	cmp #$ff
	beq +
	sta .screenaddr,x
	inx
	bne -
+
}




;------------------------------------------------------------
;Code start
;------------------------------------------------------------

Init:
	sei                  ; set interrupt bit, make the CPU ignore interrupt requests
	lda #%01111111       ; switch off interrupt signals from CIA-1
	sta $dc0d

	and SCREEN_CONTROL_REGISTER_1 ; clear most significant bit of VIC's raster register
	sta SCREEN_CONTROL_REGISTER_1

	lda $dc0d            ; acknowledge pending interrupts from CIA-1
	lda $dd0d            ; acknowledge pending interrupts from CIA-2

	jsr DetectModel

	lda #COLOR_LIGHT_BLUE
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #COLOR_BLUE
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	lda #0
	sta IDLE_BYTE_ADDRESS
	lda #COLUMNS_40
	sta SCREEN_CONTROL_REGISTER_2
	lda #D011_BASE + 3
	sta SCREEN_CONTROL_REGISTER_1

	jsr ClearScreen
	jsr DrawScreen

	lda #1
	sta SHIFT
	sta SHIFT_DIR
	lda #0
	sta FLD_LINES
	sta FRAME
	sta RUNNING

	; Line clock: timer A counts one raster line per period (latch + 1 cycles), continuously.
	lda #0
	sta CIA1_TIMER_A_HI
	lda #CYCLES_PER_LINE_NTSC - 1
	ldx IS_PAL
	beq +
	lda #CYCLES_PER_LINE_PAL - 1
+	sta CIA1_TIMER_A_LO
	lda #%00010001       ; force load + start, continuous
	sta CIA1_CONTROL_A
	jsr CalibrateLineClock
	jsr BuildSlideEntryTable

	; Raster IRQ above the display window
	lda #IRQ_LINE
	sta SCREEN_RASTER_LINE
	lda #<Irq
	sta $0314
	lda #>Irq
	sta $0315

	lda #%00001111       ; clear any pending VIC interrupt flag
	sta $d019
	lda #%00000001       ; enable raster interrupt signals from VIC
	sta $d01a

	cli                  ; clear interrupt flag, allowing the CPU to respond to interrupt requests

; Main loop: a press of the space bar (after it has been released) starts a sweep; the interrupt
; runs it and clears RUNNING when the shift is back where it started.
Main:
	jsr WaitSpace
	lda #1
	sta SHIFT
	sta SHIFT_DIR
	lda #0
	sta FLD_LINES
	sta FRAME
	lda #1
	sta RUNNING
-	lda RUNNING
	bne -
	jmp Main

; Wait for the space bar to be up, then down, reading the keyboard matrix directly (the KERNAL's
; scan is off with the CIA interrupt).
WaitSpace:
	lda #SPACE_ROW_SELECT
	sta CIA1_PORT_A
-	lda CIA1_PORT_B
	and #SPACE_COLUMN_BIT
	beq -                ; still held from before
-	lda CIA1_PORT_B
	and #SPACE_COLUMN_BIT
	bne -                ; not pressed yet
	rts

; PAL or NTSC: after the raster passes line 255, remember the last low byte seen before it wraps
; to 0. NTSC wraps after line 262 (low byte 6), PAL after line 311 (low byte 55).
DetectModel:
	lda #0
	sta LAST_LINE_LO
-	bit SCREEN_CONTROL_REGISTER_1
	bpl -
	; Keep the highest line low byte seen while bit 8 is set, rather than the last one: the raster
	; can wrap to 0 between reading the line and testing bit 8, and the last value would then be 0
	; on either model. That misread makes the effect use the wrong line length on PAL.
-	lda SCREEN_RASTER_LINE
	cmp LAST_LINE_LO
	bcc +
	sta LAST_LINE_LO
+	bit SCREEN_CONTROL_REGISTER_1
	bmi -
	lda #0
	sta IS_PAL
	lda #CYCLES_PER_LINE_NTSC
	sta LINE_CYCLES
	lda LAST_LINE_LO
	cmp #$20
	bcc +
	inc IS_PAL
	lda #CYCLES_PER_LINE_PAL
	sta LINE_CYCLES
+	rts

; Read the line clock at a fixed cycle of a raster line, whatever cycle this program was started
; on. A poll loop can only get out of its loop a whole loop length after the line began, and where
; in the loop the line change falls depends on the start cycle. This loop is 11 cycles, which
; neither 63 nor 65 is a multiple of, so its position drifts by 8 (PAL) or 10 (NTSC) cycles per
; line (7 on PAL, 5 on NTSC, counting the work between two lines' polls) and within 11 lines
; every position has come up once, including the earliest: the poll whose compare read the new
; line on its first cycle and so got out 7 cycles in, whose timer read (the 4th cycle of the LDA,
; 10 cycles in) gives the largest value, since the timer counts down. That value is the reference.
CalibrateLineClock:
	lda #CALIB_FIRST_LINE
	sta TARGET_LINE
	; Enter the first poll on the line before the first polled one, so that every poll waits for a
	; line change. Entered on the polled line itself, a poll gets out at once, anywhere in the line,
	; and so does the next one, and those two readings are then not the timer's value near the
	; line's start.
-	bit SCREEN_CONTROL_REGISTER_1
	bmi -
	lda SCREEN_RASTER_LINE
	cmp #CALIB_FIRST_LINE - 1
	bne -
	ldx #0
-	lda TARGET_LINE                 ; 3
--	cmp SCREEN_RASTER_LINE          ; 4   (the read lands on the 4th cycle)
	nop                             ; 2
	nop                             ; 2
	bne --                          ; 3 taken / 2 falling through
	lda CIA1_TIMER_A_LO             ; 4   (the read lands on the 4th cycle)
	sta CalibReadings,x             ; 5   every reading is kept, so the work between two lines'
	inx                             ; 2   polls is always the same and the loop's position drifts
	inc TARGET_LINE                 ; 5   by the same amount each line (7 on PAL, 5 on NTSC)
	lda TARGET_LINE                 ; 3
	cmp #CALIB_FIRST_LINE + CALIB_LINES ; 2
	bne -                           ; 3
	; The reading from the earliest exit is the largest, the others lie within 10 below it. If the
	; readings straddle the timer's reload, the ones from the later exits have wrapped to the top
	; of the period and the earliest is then the largest of those below 11.
	lda #255
	sta FILLER
	jsr LargestCalibReading
	sta CALIB
	sec
	sbc FILLER                      ; largest minus smallest
	cmp #11
	bcc +
	lda #11
	sta FILLER                      ; only readings below 11 count
	jsr LargestCalibReading
	sta CALIB
+	rts

; A = the largest reading below FILLER's value on entry (FILLER = 255 for all of them); FILLER
; leaves with the smallest reading.
LargestCalibReading:
	ldx #CALIB_LINES - 1
	lda #0
	sta TARGET_LINE                 ; largest so far
	lda #255
	sta LINE_CYCLES_SAVE            ; smallest so far
-	lda CalibReadings,x
	cmp FILLER
	bcs +                           ; at or above the limit: skip
	cmp TARGET_LINE
	bcc ++
	sta TARGET_LINE
++	cmp LINE_CYCLES_SAVE
	bcs +
	sta LINE_CYCLES_SAVE
+	dex
	bpl -
	lda LINE_CYCLES_SAVE
	sta FILLER
	lda TARGET_LINE
	rts
CalibReadings:
	!fill CALIB_LINES, 0
LINE_CYCLES_SAVE:
	!byte 0

; The slide entry for every possible timer difference (reference minus this poll's reading), so
; SyncToLine needs no arithmetic that could branch or wrap. SyncToLine's poll gets out 3-9 cycles
; into the line and reads the timer 3 cycles later, 6-12 cycles in against the reference's 10, so
; the difference is -4 to +2 for a poll that made it, and the entry is SLIDE_CENTER plus that. The
; difference can also come out a line period off, when the two readings straddle the timer's
; reload: those entries are filled the same way. Everything else gets the nominal entry.
BuildSlideEntryTable:
	ldx #0
	lda #SLIDE_CENTER
-	sta SlideEntryTable,x
	inx
	bne -
	ldx #<-SLIDE_RANGE              ; difference, as a byte
	lda #SLIDE_CENTER - SLIDE_RANGE ; its entry
	sta FILLER
-	lda FILLER
	sta SlideEntryTable,x           ; difference as it is
	txa
	clc
	adc LINE_CYCLES
	tay
	lda FILLER
	sta SlideEntryTable,y           ; difference plus a period
	txa
	sec
	sbc LINE_CYCLES
	tay
	lda FILLER
	sta SlideEntryTable,y           ; difference minus a period
	inc FILLER
	inx
	cpx #SLIDE_RANGE + 1
	bne -
	rts


ClearScreen:
	ldx #0
	lda #SPACE_CHAR
-	sta SCREEN_RAM,x
	sta SCREEN_RAM + $100,x
	sta SCREEN_RAM + $200,x
	sta SCREEN_RAM + $300,x
	inx
	bne -
	lda #COLOR_WHITE
-	sta COLOR_RAM,x
	sta COLOR_RAM + $100,x
	sta COLOR_RAM + $200,x
	sta COLOR_RAM + $300,x
	inx
	bne -
	rts

; Rows 1 and 22: a column ruler, so the shift can be read off. Rows 3-19: the text. Rows 0 and 23
; stay blank: row 0 is the delay line's row and row 23's tail wraps into row 0 of the next frame's
; first row.
DrawScreen:
	ldx #0
-	lda Ruler,x
	sta SCREEN_RAM + 1 * 40,x
	sta SCREEN_RAM + 22 * 40,x
	lda #COLOR_YELLOW
	sta COLOR_RAM + 1 * 40,x
	sta COLOR_RAM + 22 * 40,x
	inx
	cpx #40
	bne -
	+print SCREEN_RAM + 3 * 40, Text3
	+print SCREEN_RAM + 4 * 40, Text4
	+print SCREEN_RAM + 5 * 40, Text5
	+print SCREEN_RAM + 6 * 40, Text6
	+print SCREEN_RAM + 8 * 40, Text8
	+print SCREEN_RAM + 9 * 40, Text9
	+print SCREEN_RAM + 10 * 40, Text10
	+print SCREEN_RAM + 11 * 40, Text11
	+print SCREEN_RAM + 13 * 40, Text13
	+print SCREEN_RAM + 14 * 40, Text14
	+print SCREEN_RAM + 15 * 40, Text15
	+print SCREEN_RAM + 16 * 40, Text16
	+print SCREEN_RAM + 18 * 40, Text18
	+print SCREEN_RAM + 19 * 40, Text19
	+print SCREEN_RAM + 21 * 40, Text21
	rts

; Wait for the raster line in TARGET_LINE and return 50 cycles into it,
; on every frame and on every run. Uses A; X and Y are preserved so a caller can keep colours or counters in them.
SyncToLine:
	stx SAVE_X
	+poll_and_read_clock            ; poll exits 3-9 cycles after the line began
	eor #$ff                        ; 2   reference minus this poll's clock value (larger the later
	sec                             ; 2   the poll got out): CALIB + (255 - value) + 1
	adc CALIB                       ; 3
	tax                             ; 2
	lda SlideEntryTable,x           ; 4   later poll => larger entry => shorter delay
	sta SyncSlideJmp + 1            ; 4
SyncSlideJmp:
	jmp SyncSlide                   ; 3
	!align 255, 0
SlideEntryTable:
	!fill 256, 0
SyncSlide:
	; Entered at offset k (0-17) this takes 19 - k cycles: pairs of $C9 are CMP #$C9 (2 cycles);
	; the tail is either CMP $EA (3 cycles) or CMP #$C5 + NOP (4 cycles) depending on parity.
	!byte $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c5, $ea
	ldx SAVE_X                      ; 3
	rts                             ; 6


;------------------------------------------------------------
;Raster interrupt: FLD, then the DMA delay line
;------------------------------------------------------------

Irq:
	lda RUNNING
	bne +
	; Waiting: the plain screen, rows from line 51.
	lda #D011_BASE + 3
	sta SCREEN_CONTROL_REGISTER_1
	jmp IrqDone
+
	; This frame's motion: every fourth frame the shift takes a step, out to MAX_SHIFT and back;
	; the line count follows it at half the rate. Back at 1 the sweep is over.
	inc FRAME
	lda FRAME
	and #3
	bne ++
	lda SHIFT
	clc
	adc SHIFT_DIR
	sta SHIFT
	cmp #MAX_SHIFT
	bne +
	lda #$ff                        ; turn: -1
	sta SHIFT_DIR
	bne ++
+	cmp #1
	bne ++
	lda #0                          ; back at the start: done, the plain screen from the next frame
	sta RUNNING
++	lda SHIFT
	sec
	sbc #1
	lsr
	sta FLD_LINES
	; The delay line is the first idle line after the FLD lines, and its condition is created
	; SHIFT cycles after cycle 14: the $d011 write lands on cycle index 12 + SHIFT. From the sync's
	; return (index 50 of the line before) that is LINE_CYCLES - 50 + 12 + SHIFT cycles: PLA (4),
	; LSR (5), BCS (2, 3 if the delay is odd), JMP (3), the slide's NOPs, and STA with the write on
	; its fourth cycle, so the slide has to take LINE_CYCLES - 55 + SHIFT cycles.
	lda #FIRST_ROW_LINE
	clc
	adc FLD_LINES
	sta DELAY_LINE
	lda LINE_CYCLES
	sec
	sbc #55
	clc
	adc SHIFT
	lsr                             ; the delay in NOP pairs, its odd cycle into the carry
	tax
	lda #0
	rol
	sta ODD
	lda #SLIDE_NOPS
	stx FILLER
	sec
	sbc FILLER
	sta SlideJmp + 1                ; enter the slide SLIDE_NOPS - pairs NOPs before its end

	; FLD: on the lines from FIRST_ROW_LINE up to two before the delay line, YSCROLL is set three
	; ahead of the line's low bits, so the condition holds on none of them: not on the line
	; written, not at the start of the next where the value is still in force, and not on the
	; delay line either, which the last write's value must also miss until the delay write on it.
	; (Two ahead, the usual FLD choice, would make the last value the delay line's own.) Before
	; those lines YSCROLL_IDLE keeps the condition away from lines 48-54. The line before the delay
	; line gets no write: the sync below has to be entered before it begins.
	lda DELAY_LINE
	sec
	sbc #1
	sta TARGET_LINE                 ; the line before the delay line: the sync's line
	lda DELAY_LINE
	clc
	adc #1
	and #7
	ora #D011_BASE
	sta ROWS_D011
	lda #D011_BASE + YSCROLL_IDLE
	sta SCREEN_CONTROL_REGISTER_1
	ldx #FIRST_ROW_LINE
--	cpx TARGET_LINE                 ; up to two lines before the delay line
	bcs +
	txa
	clc
	adc #3
	and #7
	ora #D011_BASE
	tay
-	cpx SCREEN_RASTER_LINE          ; wait for the line
	bne -
	sty SCREEN_CONTROL_REGISTER_1
	inx
	bne --
+
	; The DMA delay: the condition on DELAY_LINE (its own low bits into YSCROLL) at the computed cycle.
	lda DELAY_LINE
	and #7
	ora #D011_BASE
	sta FILLER                      ; the value to write, ready in A after the slide
	pha
	jsr SyncToLine                  ; returns 50 cycles into TARGET_LINE (uses A)
	pla                             ; 4   (A is the value again)
!ifdef MEASURE {
	sta $d020                       ; measurement builds: writes 7, 11, 15 cycles after the return
	sta $d020
	sta $d020
}
	lsr ODD                         ; 5   the odd cycle into the carry
	bcs *+2                         ; 2 / 3
SlideJmp:
	jmp NopSlide                    ; 3
	!align 255, 0
NopSlide:
	!fill SLIDE_NOPS, $ea           ; 2 per NOP left to the end
	sta SCREEN_CONTROL_REGISTER_1   ; 4   the write in its last cycle: the condition holds from the next
	ldx FILLER                      ; $A6: halted on its opcode fetch until the fetches end, and the
	                                ; low nibble the seam's $FF cells take as colour is 6, the background
	; The rows: YSCROLL becomes the next line's low bits, written in this line's last cycles
	; (after the halt, cycle 55 on), so that line is the first row's bad line (RC reset there) and
	; every eighth line after it is one too.
	lda ROWS_D011                   ; 3
	sta SCREEN_CONTROL_REGISTER_1   ; 4
IrqDone:
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

;------------------------------------------------------------
;Tables and text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Ruler:
	!scr "0123456789012345678901234567890123456789"

Text3:	!scr "the vic-ii counts its way through the", $ff
Text4:	!scr "screen: a bad line fetches a row, and", $ff
Text5:	!scr "the video counter says which memory the", $ff
Text6:	!scr "row comes from.", $ff
Text8:	!scr "creating the bad line condition in the", $ff
Text9:	!scr "middle of a line delays the fetches (dma", $ff
Text10:	!scr "delay) and every row after it begins", $ff
Text11:	!scr "that many characters into its memory.", $ff
Text13:	!scr "keeping the condition away for some", $ff
Text14:	!scr "lines (fld) starts the rows lower down.", $ff
Text15:	!scr "this screen bounces in both directions", $ff
Text16:	!scr "with no byte of screen memory moved.", $ff
Text18:	!scr "the columns pushed off the right edge", $ff
Text19:	!scr "come back at the left of the next row.", $ff
Text21:	!scr "press space for one sweep.", $ff
