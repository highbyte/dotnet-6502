;ACME assembler
;!to "./cia_synced_split.prg"

; A raster split placed by a CIA timer: the 6526's start pipeline, made visible.
;
; CIA 2's timer A is started, by a write in a known cycle of a raster line, to count one raster
; line per period (latch + 1 cycles, continuously). From then on its value at any cycle of any
; line is known: on the 6526 the counter holds through the two cycles after the start write and
; shows its first decrement on the third (with a force load: the latch shows two cycles after the
; write, holds one cycle, and counts from the fourth). Every frame the raster interrupt reads the
; timer and delays by an amount that grows one cycle per count, so the code after the delay lands
; in the same cycle of a line whatever the interrupt's jitter and the KERNAL's entry cost were:
; the classic CIA timer stabiliser. That code paints a yellow band with a background colour
; split, ten lines tall, in the lower border opened with the 24/25 row switch, where no bad line
; and no sprite fetch can move it.
;
; Two white sprites, one above and one below the band, mark the pixel column where the band's
; left edge belongs with the 6526's pipeline. A timer that counted from its start write itself
; would read three counts lower, the delay would come out three cycles shorter, and the edge would
; sit 24 pixels to the left of the marks. (VICE's spritesteal and vsp-tester test programs and the
; cia-timer test's hardware dumps are where the pipeline comes from.)
;
; The timer is started with the line clock of the side-border sample (CIA 1's timer A, calibrated
; against the raster), which gets the start write into a known cycle; that clock is not used for
; the split itself.

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

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e
CIA2_TIMER_A_LO = $dd04
CIA2_TIMER_A_HI = $dd05
CIA2_CONTROL_A = $dd0e

IDLE_BYTE_ADDRESS = $3fff       ; shown in the opened border: blank, so the band is the background colour

COLUMNS_40 = %11001000          ; $d016: 40 columns, XSCROLL 0
D011_25_ROWS = %00011011        ; DEN on, 25 rows, YSCROLL 3
D011_24_ROWS = %00010011        ; DEN on, 24 rows, YSCROLL 3

TIMER_START_LINE = 200          ; the line CIA 2's timer is started in: no bad line, no sprite fetch there
IRQ_LINE = 245                  ; before the 24 row compare line (247): 25 rows are put back so it is not made
BORDER_OPEN_LINE = 248          ; 24 rows written here: the 24 row compare is past, the 25 row one is never made
BAND_LINES = 10                 ; lines 249-258 on both models (the 6567R8 has 263 lines)
BAND_WIDTH_CYCLES = 20          ; cycles between the yellow and the blue write: 160 pixels
SPLIT_DELAY_CYCLES = 6          ; from the synced cycle to the band loop: puts the edge left of the middle

SPRITE_POINTERS = $07f8
SPRITE_SHAPES = $3000           ; block $c0
SPRITE_SHAPE = SPRITE_SHAPES / 64
MARK_ABOVE_Y = 226              ; sprite 0: lines 226-246, above the band, its fetches over before the sync
MARK_BELOW_Y = 3                ; sprite 1: Y matches a line's low byte, so lines 259-279 (and 3-23 of the next frame): right below the band, its fetches after it
; Where the band's left edge lands, in sprite X coordinates, per model: the yellow write is in
; cycle index 24 on the 6569 and 22 on the 6567R8 (see the band loop), and the background colour
; changes one pixel into that cycle's eight (X 88-95 for index 24, 72-79 for 22: 89 and 73). The marks are
; set to these at start.
MARK_X_PAL = 89
MARK_X_NTSC = 73

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
; 51 on the 6569 and 52 on the 6567R8.
SLIDE_CENTER_PAL = 8
SLIDE_CENTER_NTSC = 7
SLIDE_RANGE = 7

COLOR_BLACK = 0
COLOR_WHITE = 1
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
	lda #%01111111       ; switch off interrupt signals from CIA-1 and CIA-2 (CIA-2's would be NMIs)
	sta $dc0d
	sta $dd0d

	and SCREEN_CONTROL_REGISTER_1 ; clear most significant bit of VIC's raster register
	sta SCREEN_CONTROL_REGISTER_1

	lda $dc0d            ; acknowledge pending interrupts from CIA-1
	lda $dd0d            ; acknowledge pending interrupts from CIA-2

	jsr DetectModel

	; Border and background the same: the vertical border, left open below the display, shows
	; the background colour there and above the display, and so nothing changes.
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
	jsr PatchForModel

	; Line clock: CIA 1's timer A counts one raster line per period, continuously.
	lda #0
	sta CIA1_TIMER_A_HI
	lda LINE_CYCLES
	sec
	sbc #1
	sta CIA1_TIMER_A_LO
	lda #%00010001       ; force load + start, continuous
	sta CIA1_CONTROL_A
	jsr CalibrateLineClock
	jsr BuildSlideEntryTable

	; CIA 2's timer A: the same period, started by a write in a known cycle of TIMER_START_LINE.
	; SyncToLine returns with the next opcode fetched in cycle index 51 (52 on the 6567R8); STY
	; abs writes in its fourth cycle: index 54 (55). With the force load the latch is in the
	; counter from index 56 (57), holds through 57 (58), and the counter reads latch - 1 in 58
	; (59): its value in cycle index c of any later line is (56 - c) mod 63 on the 6569 and
	; (57 - c) mod 65 on the 6567R8.
	lda #0
	sta CIA2_TIMER_A_HI
	lda LINE_CYCLES
	sec
	sbc #1
	sta CIA2_TIMER_A_LO
	lda #TIMER_START_LINE
	sta TARGET_LINE
	ldy #%00010001       ; force load + start, continuous
	jsr SyncToLine       ; index 51 (52)
	sty CIA2_CONTROL_A   ; 4   the write in index 54 (55)

	jsr SetupSprites

	lda IS_PAL
	bne +
	+print SCREEN_RAM + 16 * 40, TextNtsc
+
	; Raster IRQ on the 24 row compare line
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

; The model's line length into the interrupt's timer arithmetic and the band loop's line length,
; and the marks' X.
PatchForModel:
	lda LINE_CYCLES
	sta SyncLineCycles + 1
	lda #MARK_X_NTSC
	ldx IS_PAL
	beq +
	lda #MARK_X_PAL
+	sta MarkX
	rts
MarkX:	!byte 0

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

; Sprites 0 and 1: a one-pixel white line in the leftmost column of the shape, at the X the
; band's edge belongs at, one above the band and one below it. Neither is fetched on the lines
; the interrupt syncs and paints on, so no bus request moves that code.
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
	lda #COLOR_WHITE
	sta $d027
	sta $d028
	lda MarkX
	sta $d000
	sta $d002
	lda #MARK_ABOVE_Y
	sta $d001
	lda #MARK_BELOW_Y
	sta $d003
	lda #0
	sta $d010            ; X below 256 for both
	sta $d017            ; not Y expanded
	sta $d01c            ; single colour
	sta $d01d            ; not X expanded
	sta $d01b            ; in front of the graphics
	lda #%00000011
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
	+print SCREEN_RAM + 7 * 40, Text7
	+print SCREEN_RAM + 9 * 40, Text9
	+print SCREEN_RAM + 10 * 40, Text10
	+print SCREEN_RAM + 11 * 40, Text11
	+print SCREEN_RAM + 12 * 40, Text12
	+print SCREEN_RAM + 13 * 40, Text13
	+print SCREEN_RAM + 14 * 40, Text14
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
;Raster interrupt: open the lower border, sync on CIA 2's timer, paint the band
;------------------------------------------------------------

Irq:
	; 25 rows again before the 24 row compare line, so that compare is not made this frame
	; either, and the flip-flop, cleared at the top of the display, is never set: the vertical
	; border stays open below the display.
	lda #D011_25_ROWS
	sta SCREEN_CONTROL_REGISTER_1
	+wait_line BORDER_OPEN_LINE
	lda #D011_24_ROWS
	sta SCREEN_CONTROL_REGISTER_1

	; The stabiliser. The timer's value v in cycle index r (the SBC's read) is (56 - r) mod 63,
	; so A = 63 - v; STA (4) and the taken BNE (3) follow, then the field: entered A bytes in, its
	; 65 - A remaining bytes of $80 (NOP #imm, two cycles for every two bytes, whatever the
	; alignment) and the $04 $EA tail (NOP zp for an even remainder, NOP #$04 then NOP for an odd
	; one) take 65 - A + 3 cycles. The next opcode is fetched 1 + 4 + 3 + 68 - A = 13 + v cycles
	; after the read, i.e. 13 + 56 - r cycles after cycle r: in cycle index 69 - 63 = 6 of the next
	; line, for any r up to 56 (the read lands well before that here: the poll above gets out
	; within 9 cycles of the line's start, and the SBC's read is 14 cycles on). On the 6567R8 the
	; arithmetic is 65 - v with v = (57 - r) mod 65, and the next opcode is fetched in index 3.
	jmp SyncLineCycles              ; over the alignment padding
	!align 255, 0                   ; the sync code, the field and its tail in one page: the branch never crosses one
SyncLineCycles:
	lda #CYCLES_PER_LINE_PAL        ; 2   patched to the model's line length
	sec                             ; 2
	sbc CIA2_TIMER_A_LO             ; 4   the read lands on the 4th cycle: index r
	sta SyncBranch + 1              ; 4
SyncBranch:
	bne SyncField                   ; 3   the operand is A: A bytes into the field
SyncField:
	!fill 65, $80
	!byte $04, $ea

	; From here every cycle is the same on every frame. The band: yellow written in the loop's
	; sixth cycle, blue BAND_WIDTH_CYCLES cycles later, one line per pass. The yellow write lands
	; in cycle index 6 + SPLIT_DELAY_CYCLES + 2 (LDY) + 3 (LDX) + 2 (BEQ not taken) + 2 (LDA) + 3
	; = 24 on the 6569 (3 + 6 + 2 + 3 + 3 + 2 + 3 = 22 on the 6567R8, the BEQ taken): the marks
	; stand where the background colour changes for a write in that cycle.
	+delay_cycles SPLIT_DELAY_CYCLES
	ldy #BAND_LINES                 ; 2
	ldx IS_PAL                      ; 3
	beq BandNtsc                    ; 2 / 3
BandPal:
	lda #COLOR_YELLOW               ; 2
	sta SCREEN_BACKGROUND_COLOR_ADDRESS ; 4
	+delay_cycles BAND_WIDTH_CYCLES - 6
	lda #COLOR_BLUE                 ; 2
	sta SCREEN_BACKGROUND_COLOR_ADDRESS ; 4
	+delay_cycles CYCLES_PER_LINE_PAL - BAND_WIDTH_CYCLES - 6 - 5
	dey                             ; 2
	bne BandPal                     ; 3
	jmp BandDone
BandNtsc:
	lda #COLOR_YELLOW               ; 2
	sta SCREEN_BACKGROUND_COLOR_ADDRESS ; 4
	+delay_cycles BAND_WIDTH_CYCLES - 6
	lda #COLOR_BLUE                 ; 2
	sta SCREEN_BACKGROUND_COLOR_ADDRESS ; 4
	+delay_cycles CYCLES_PER_LINE_NTSC - BAND_WIDTH_CYCLES - 6 - 5
	dey                             ; 2
	bne BandNtsc                    ; 3
BandDone:
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

;------------------------------------------------------------
;Tables
;------------------------------------------------------------

; A one-pixel line down the shape's leftmost column.
SpriteShape:
	!for .r, 1, 21 {
		!byte %10000000, %00000000, %00000000
	}
	!byte 0

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "cia timer synced split", $ff
Text2:	!scr "cia 2's timer a is started by a write in", $ff
Text3:	!scr "a known cycle and counts one line per", $ff
Text4:	!scr "period. each frame the raster interrupt", $ff
Text5:	!scr "reads it and delays one cycle per count,", $ff
Text6:	!scr "so what follows lands in the same cycle", $ff
Text7:	!scr "of a line whatever the interrupt jitter.", $ff
Text9:	!scr "the yellow band below is split that way.", $ff
Text10:	!scr "the white marks show where its left edge", $ff
Text11:	!scr "belongs when the timer's first count", $ff
Text12:	!scr "comes three cycles after the start write", $ff
Text13:	!scr "as on the 6526. a timer counting from the", $ff
Text14:	!scr "write would put the edge 24 pixels left.", $ff
TextNtsc:	!scr "ntsc: 65-cycle lines, the same rule.", $ff
