;ACME assembler
;!to "./sprite_enable.prg"

; When a sprite's enable bit and Y are read: the VIC-II's DMA start and display decision.
;
; A sprite's DMA is switched on by a compare in cycle 55 or 56 of a line (56 or 57 on the 6567R8):
; enable bit set, Y equal to the line, DMA off. In cycle 58 (59) the chip decides the display for
; the next line: a sprite whose DMA is on, whose enable bit is set and whose Y equals the line is
; displayed from the next line on, and a sprite whose DMA is off stops being displayed. A CPU write
; in a cycle is seen by the compare of the cycle after it, so what a write does depends on where
; in the line it lands, to the cycle. Three cases, each on its own line (VICE's spriteenable and
; spriterestart test programs):
;
; Sprites 0 (white) and 1 (cyan): enabled by a write in cycle 55 of their Y line, after the first
; compare has read the register and before the second. Both start at the second compare, and for
; sprite 0 that is one cycle too late: its data fetch begins two cycles on, in cycle 58, but the
; bus request (BA) needs three cycles to stop the CPU, so the first byte of its first row is read
; while the CPU still drives the bus and comes back as $FF: a bar at the sprite's top left. Sprite
; 1's fetch is two cycles later and clean.
;
; Sprite 2 (yellow): enabled before its Y line, so the compares start its DMA, and cleared again by
; the second write of a read-modify-write in cycle 57, before the display decision: the sprite is
; fetched for 21 lines but never shown. Its place stays empty.
;
; Sprite 1 again: on the last line of its run its DMA ends in cycle 16, so the compare in cycle 55
; can start it again when Y names that line. Y is set to that line in advance and written back in
; cycle 56, after the compare and before the display decision: the decision finds the DMA on and
; leaves the display as it was, on, although Y no longer matches, and the sprite shows all its
; rows a second time, right below the first.
;
; The lines used (208, 212 and 233) carry no bad line, so the timed writes are placed with the
; line clock of the side-border sample. Sprite 2's line comes first: once sprites 0 and 1 fetch,
; their bus requests hold the CPU at the end of every line, and a poll cannot wait for a line
; change from there. The lower border is opened with the 24/25 row switch so the second copy of
; sprite 1 shows whole.

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
SPRITE_ENABLE = $d015
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021
SPRITE_1_Y = $d003

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

IDLE_BYTE_ADDRESS = $3fff       ; shown in the opened border: blank

COLUMNS_40 = %11001000          ; $d016: 40 columns, XSCROLL 0
D011_25_ROWS = %00011011        ; DEN on, 25 rows, YSCROLL 3
D011_24_ROWS = %00010011        ; DEN on, 24 rows, YSCROLL 3

SPRITE_Y_01 = 212               ; sprites 0 and 1: their Y line, rows on 213-233 (no bad line at 212-214)
SPRITE_Y_2 = 208                ; sprite 2: its Y line, before the others' so no sprite DMA is on yet
SPRITE_1_LAST_LINE = SPRITE_Y_01 + 21   ; 233: the last line of sprite 1's run, where it is restarted
IRQ_LINE = 200                  ; before the first timed line, after the text
BORDER_OPEN_LINE = 248          ; after the 24 row compare line (247), before the 25 row one (251)
BORDER_CLOSE_LINE = 255         ; after sprite 1's second run (234-254)
SPRITE_POINTERS = $07f8
SPRITE_SHAPES = $3000           ; block $c0
SPRITE_SHAPE = SPRITE_SHAPES / 64

SPRITE_0_X = 72
SPRITE_1_X = 136
SPRITE_2_X = 200

ENABLE_2 = %00000100            ; sprite 2 alone, as every frame begins
ENABLE_01 = %00000011           ; sprites 0 and 1, written in cycle 55 of line 212
                                ; an LSR of ENABLE_2 in line 208 gives %010: sprite 2 cleared

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

FILLER       = $02      ; zero page: scratch, also the byte the timing filler reads
LINE_CYCLES  = $03      ; zero page: 63 on PAL, 65 on NTSC
SAVE_X       = $06      ; zero page: the caller's X across SyncToLine
SLIDE_CENTER = $07      ; zero page: this model's slide entry for a nominal poll
IS_PAL       = $fb      ; zero page: 1 on PAL, 0 on NTSC
LAST_LINE_LO = $fc      ; zero page: scratch for the model detection
CALIB        = $fd      ; zero page: line clock value 9 cycles into a raster line (see CalibrateLineClock)
TARGET_LINE  = $fe      ; zero page: raster line SyncToLine waits for

; Lines the calibration polls: in the top border, above any bad line, no sprites there.
CALIB_FIRST_LINE = 10
CALIB_LINES = 16

; SyncToLine returns 59 - SLIDE_CENTER cycles into the line (a larger entry is a shorter slide):
; 51 on the 6569 and 52 on the 6567R8, where the compares are a cycle later, so that the same
; stores after it land a cycle later too. (The centre must not be below SLIDE_RANGE: the table's
; entry for the difference -SLIDE_RANGE is SLIDE_CENTER - SLIDE_RANGE.)
SLIDE_CENTER_PAL = 8
SLIDE_CENTER_NTSC = 7
SLIDE_RANGE = 7

COLOR_BLACK = 0
COLOR_WHITE = 1
COLOR_CYAN = 3
COLOR_BLUE = 6
COLOR_YELLOW = 7
COLOR_LIGHT_GREY = 15
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

; Wait for a raster line with the plain poll (no cycle exactness needed).
!macro wait_line .line {
	lda #.line
-	cmp SCREEN_RASTER_LINE
	bne -
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

	; Border and background the same: the open vertical border shows the background colour on
	; the lines above the display too, and so nothing changes there.
	lda #COLOR_BLUE
	sta SCREEN_BORDER_COLOR_ADDRESS
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	lda #0
	sta IDLE_BYTE_ADDRESS
	lda #COLUMNS_40
	sta SCREEN_CONTROL_REGISTER_2
	lda #D011_25_ROWS
	sta SCREEN_CONTROL_REGISTER_1

	jsr ClearScreen
	jsr DrawScreen

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
	jsr SetupSprites     ; after the calibration: sprite DMA would stall its polls

	lda IS_PAL
	bne +
	+print SCREEN_RAM + 19 * 40, TextNtsc   ; row 19: the sprites cover rows 20-25
+
	; Raster IRQ before the first timed line
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

Main:
	jmp Main

; PAL or NTSC: after the raster passes line 255, remember the last low byte seen before it wraps
; to 0. NTSC wraps after line 262 (low byte 6), PAL after line 311 (low byte 55).
DetectModel:
	lda #0
	sta LAST_LINE_LO
-	bit SCREEN_CONTROL_REGISTER_1
	bpl -
	; Keep the highest line low byte seen while bit 8 is set, rather than the last one: the raster
	; can wrap to 0 between reading the line and testing bit 8, and the last value would then be 0
	; on either model.
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
	lda #SLIDE_CENTER_NTSC
	sta SLIDE_CENTER
	lda LAST_LINE_LO
	cmp #$20
	bcc +
	inc IS_PAL
	lda #CYCLES_PER_LINE_PAL
	sta LINE_CYCLES
	lda #SLIDE_CENTER_PAL
	sta SLIDE_CENTER
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
	; line change.
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
	lda SLIDE_CENTER
-	sta SlideEntryTable,x
	inx
	bne -
	ldx #<-SLIDE_RANGE              ; difference, as a byte
	lda SLIDE_CENTER
	sec
	sbc #SLIDE_RANGE                ; its entry
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

; Sprites 0-2 with the same shape at their X, sprites 0 and 1 on Y line 212, sprite 2 on 213;
; only sprite 2 enabled as the frame begins. Sprites 3-7 stay off.
SetupSprites:
	ldx #0
-	lda SpriteShape,x
	sta SPRITE_SHAPES,x
	inx
	cpx #64
	bne -
	lda #SPRITE_SHAPE
	sta SPRITE_POINTERS
	sta SPRITE_POINTERS + 1
	sta SPRITE_POINTERS + 2
	lda #COLOR_WHITE
	sta $d027
	lda #COLOR_CYAN
	sta $d028
	lda #COLOR_YELLOW
	sta $d029
	lda #SPRITE_0_X
	sta $d000
	lda #SPRITE_1_X
	sta $d002
	lda #SPRITE_2_X
	sta $d004
	lda #SPRITE_Y_01
	sta $d001
	sta SPRITE_1_Y
	lda #SPRITE_Y_2
	sta $d005
	lda #0
	sta $d010            ; X below 256 for all
	sta $d017            ; not Y expanded
	sta $d01c            ; single colour
	sta $d01d            ; not X expanded
	sta $d01b            ; in front of the graphics
	lda #ENABLE_2
	sta SPRITE_ENABLE
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
	lda #COLOR_LIGHT_GREY
-	sta COLOR_RAM,x
	sta COLOR_RAM + $100,x
	sta COLOR_RAM + $200,x
	sta COLOR_RAM + $300,x
	inx
	bne -
	rts

DrawScreen:
	+print SCREEN_RAM + 0 * 40, Text0
	+print SCREEN_RAM + 2 * 40, Text2
	+print SCREEN_RAM + 3 * 40, Text3
	+print SCREEN_RAM + 4 * 40, Text4
	+print SCREEN_RAM + 5 * 40, Text5
	+print SCREEN_RAM + 6 * 40, Text6
	+print SCREEN_RAM + 8 * 40, Text8
	+print SCREEN_RAM + 9 * 40, Text9
	+print SCREEN_RAM + 10 * 40, Text10
	+print SCREEN_RAM + 12 * 40, Text12
	+print SCREEN_RAM + 13 * 40, Text13
	+print SCREEN_RAM + 14 * 40, Text14
	+print SCREEN_RAM + 15 * 40, Text15
	+print SCREEN_RAM + 17 * 40, Text17
	+print SCREEN_RAM + 18 * 40, Text18
	rts

; Wait for the raster line in TARGET_LINE and return 50 cycles into it, on every frame and on
; every run. Uses A; X and Y are preserved.
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
;Raster interrupt: the three timed writes, then the border
;------------------------------------------------------------

; SyncToLine returns with the next opcode fetched in cycle index 51 on the 6569 and 52 on the
; 6567R8, and every timed store after it is the same on both: STY abs writes in its fourth cycle,
; a read-modify-write on abs in its fifth (the old value) and sixth (the new one). SyncToLine
; keeps X and Y but not A, so the values travel in Y or in the register itself.
Irq:
	lda #SPRITE_Y_01                ; sprite 1's Y back to its line for this frame's start
	sta SPRITE_1_Y

	; Line 208, the second write of an LSR in index 56 (57): sprite 2's bit cleared after both
	; compares saw it and before the display decision in index 57 (58). The LSR moves the bit to
	; sprite 1, which must not be enabled before line 212: the register is cleared right after.
	lda #SPRITE_Y_2
	sta TARGET_LINE
	jsr SyncToLine                  ; index 51 (52)
	lsr SPRITE_ENABLE               ; 6   writes in index 55 and 56 (56 and 57)
	lda #0
	sta SPRITE_ENABLE

	; Line 212, index 54 (55 on the 6567R8): sprites 0 and 1 enabled between the two compares.
	lda #SPRITE_Y_01
	sta TARGET_LINE
	ldy #ENABLE_01
	jsr SyncToLine                  ; index 51 (52)
	sty SPRITE_ENABLE               ; 4   the write in index 54 (55)

	; Sprite 1's Y names its last line in advance; on that line a DEC writes the old value in index
	; 55 (56), after the compare that restarted it, and one below in 56 (57), before the display
	; decision, which then no longer finds Y matching and leaves the display on.
	lda #SPRITE_1_LAST_LINE
	sta SPRITE_1_Y
	sta TARGET_LINE
	jsr SyncToLine                  ; index 51 (52)
	dec SPRITE_1_Y                  ; 6   writes in index 55 and 56 (56 and 57)

	; 24 rows: the bottom compare of 24 rows (line 247) is past and the one of 25 rows (line 251)
	; is not made, so the vertical border stays open below the display for the second copy.
	+wait_line BORDER_OPEN_LINE
	lda #D011_24_ROWS
	sta SCREEN_CONTROL_REGISTER_1

	; Below the copy: 25 rows again for the next frame's display, and only sprite 2 enabled so
	; that the next frame's writes find the same registers.
	+wait_line BORDER_CLOSE_LINE
	lda #D011_25_ROWS
	sta SCREEN_CONTROL_REGISTER_1
	lda #ENABLE_2
	sta SPRITE_ENABLE

	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

;------------------------------------------------------------
;Tables
;------------------------------------------------------------

; Row 0: a dot in the middle byte, so an $FF first byte shows as a bar to its left. Rows 1-20: a
; box open at the top, its right wall thicker, so a second copy below the first reads as one.
SpriteShape:
	!byte %00000000, %00011000, %00000000
	!for .r, 1, 19 {
		!byte %11000000, %00000000, %00000111
	}
	!byte %11111111, %11111111, %11111111
	!byte 0

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "when the vic-ii reads enable and y", $ff
Text2:	!scr "0 (white) and 1 (cyan): enabled by a", $ff
Text3:	!scr "write in cycle 55 of their y line, after", $ff
Text4:	!scr "the first compare. both start at the", $ff
Text5:	!scr "second; 0's fetch comes too soon for ba:", $ff
Text6:	!scr "its first byte reads $ff: a bar. 1: none", $ff
Text8:	!scr "2 (yellow): enabled early, then cleared", $ff
Text9:	!scr "in cycle 57, before the display decision", $ff
Text10:	!scr "of cycle 58: fetched but never shown.", $ff
Text12:	!scr "1 again: on its last line the dma ended", $ff
Text13:	!scr "in cycle 16, y names that line, so the", $ff
Text14:	!scr "compare restarts it. y back in cycle 56:", $ff
Text15:	!scr "the display stays on, rows shown again.", $ff
Text17:	!scr "         0        1        2", $ff
Text18:	!scr "        $ff    clean   nothing", $ff
TextNtsc:	!scr "ntsc: those cycles are one later, 56-59.", $ff
