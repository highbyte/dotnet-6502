;ACME assembler
;!to "./sprite_x.prg"

; Where a sprite appears on a line: the VIC-II's X compare, pixel by pixel.
;
; The chip compares every sprite's X register with the beam's position at every pixel and starts
; shifting the sprite's row out where they match. A program can write the register in the middle
; of a line, and then it matters exactly where the beam is: a write in a cycle counts from that
; cycle's fifth pixel on. Written one pixel before the sprite's compare, the new X counts and the
; sprite appears there; one pixel after, the old X has already matched and the sprite appears at
; the old place, and its row is spent for that line.
;
; The sprite's own data fetch sets three more rules (VIC-II article, sections 3.6.3 and 3.8.1).
; Sprite 0's pointer and data are read in cycles 58 and 59 of every line it is on, sprite 1's two
; cycles later, and so on. From the pixel before the pointer read until the fetch has ended (X 355
; to 366 for sprite 0 on the 6569, 371 to 382 for sprite 1; 8 further on on the 6567R8) the
; sprite cannot start. A sprite that is still shifting when its fetch begins repeats its last
; pixel for seven pixels and stops. And the fetch loads the next row, which can start on the same
; line: a sprite whose X lies beyond its fetch shows every row one raster line higher than a
; sprite to the left of it.
;
; This program puts all eight sprites on one band of lines in the lower border (no bad lines
; there, so the line loop is cycle-exact; the vertical border is opened with the 24/25 row
; switch and the side borders with the 38/40 column write of the side-border sample). Sprites 3
; and 4 show the write timing: every line the loop writes both a new X, 4 in the cycle whose
; fifth pixel is its compare, 3 one pixel after its compare. Sprites 0 and 1 sit in the right
; border near their fetch; the space bar steps them through four positions. Sprite 2 stands
; still as a reference for "one line higher". The collision register is read once per frame and
; shown. Sprites 5-7 are blank and only keep the sprite DMA the same on every line.

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
SPRITE_COLLISION = $d01e
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021
SPRITE_3_X = $d006
SPRITE_4_X = $d008

CIA1_PORT_A = $dc00
CIA1_PORT_B = $dc01
CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

IDLE_BYTE_ADDRESS = $3fff       ; shown in the opened borders: blank

COLUMNS_40 = %11001000          ; $d016: 40 columns, XSCROLL 0 (DEC of it selects 38 columns)
D011_25_ROWS = %00011011        ; DEN on, 25 rows, YSCROLL 3
D011_24_ROWS = %00010011        ; DEN on, 24 rows, YSCROLL 3

SPRITE_Y = 250                  ; the compare on the display's last line: sprites on lines 251-271
BAND_LINES = 21
IRQ_LINE = 248                  ; after the 24 row compare line (247), before the 25 row one (251)
SPRITE_POINTERS = $07f8
SPRITE_SHAPES = $3000           ; blocks $c0 (ruler), $c1 (solid), $c2 (blank)
SPRITE_SHAPE_RULER = SPRITE_SHAPES / 64
SPRITE_SHAPE_SOLID = SPRITE_SHAPES / 64 + 1
SPRITE_SHAPE_BLANK = SPRITE_SHAPES / 64 + 2

SPRITE_2_X = 296                ; the reference, X high bit set
SPRITE_3_X_HOME = 151           ; sprite 3 as each line begins
SPRITE_3_X_NEW = 231            ; written one pixel after its compare: never shown there
SPRITE_4_X_HOME = 120           ; sprite 4 as each line begins
SPRITE_4_X_NEW = 200            ; written in the cycle of its compare: shown there
STEPS = 4

SPACE_ROW_SELECT = %01111111    ; the space bar is row 7, column 4
SPACE_COLUMN_BIT = %00010000

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

FILLER       = $02      ; zero page: scratch, also the byte the timing filler reads
LINE_CYCLES  = $03      ; zero page: 63 on PAL, 65 on NTSC
ENTRY        = $04      ; zero page, 2 bytes: the band loop for this model
SAVE_X       = $06      ; zero page: the caller's X across SyncToLine
SPRITE_3_HOME = $07     ; zero page: SPRITE_3_X_HOME, read by the loop (a 3-cycle load)
STEP         = $08      ; zero page: which of the four positions sprites 0 and 1 are at
COLLISION    = $09      ; zero page: the collision register, read after the band
COLLISION_SHOWN = $0a   ; zero page: the value on screen
STEP_TABLE   = $0c      ; zero page, 2 bytes: this model's position table
IS_PAL       = $fb      ; zero page: 1 on PAL, 0 on NTSC
LAST_LINE_LO = $fc      ; zero page: scratch for the model detection
CALIB        = $fd      ; zero page: line clock value 9 cycles into a raster line (see CalibrateLineClock)
TARGET_LINE  = $fe      ; zero page: raster line SyncToLine waits for

; Lines the calibration polls: in the top border, above any bad line, no sprites there.
CALIB_FIRST_LINE = 10
CALIB_LINES = 16

; SyncToLine returns 50 cycles into the line, which is where the DEC has to start for its second
; write to be in cycle index 55. (The centre must not be below SLIDE_RANGE: the table's entry for
; the difference -SLIDE_RANGE is SLIDE_CENTER - SLIDE_RANGE.)
SLIDE_CENTER = 9
SLIDE_RANGE = 8

COLOR_BLACK = 0
COLOR_WHITE = 1
COLOR_CYAN = 3
COLOR_GREEN = 5
COLOR_BLUE = 6
COLOR_YELLOW = 7
COLOR_LIGHT_GREEN = 13
COLOR_LIGHT_GREY = 15
SPACE_CHAR = $20

STEP_ROW = 16
COLLISION_ROW = 18
COLLISION_COLUMN = 27

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

; One band line, entered .resume + 5 cycles into the line when the sprite DMA hold releases the
; CPU (after the DEX and the branch). The two X writes are placed by .lead: the write to sprite 4
; lands in cycle index .lead + 12, the cycle whose fifth pixel is X 120 (index 27 on the 6569, 28
; on the 6567R8), the write to sprite 3 four cycles later, one pixel after its compare at 151.
; Then 40 columns (the 335 compare must see them), sprite 4 back to 120 after its match at 200,
; and the DEC timed so its second write lands in cycle 56 (index 55); the DEX after it is halted
; by the next line's sprite DMA. Y holds sprite 3's new X throughout.
!macro band_loop .resume, .lead, ~.entry {
.entry
	!if .lead - .resume - 5 {
		+delay_cycles .lead - .resume - 5
	}
	lda SPRITE_3_HOME               ; 3   at .lead: sprite 3 back to 151 for this line's compare
	sta SPRITE_3_X                  ; 4
	lda #SPRITE_4_X_NEW             ; 2
	sta SPRITE_4_X                  ; 4   the write is in the fourth cycle: index .lead + 12
	sty SPRITE_3_X                  ; 4   index .lead + 16
	lda #COLUMNS_40                 ; 2
	sta SCREEN_CONTROL_REGISTER_2   ; 4
	lda #SPRITE_4_X_HOME            ; 2
	sta SPRITE_4_X                  ; 4   index .lead + 28, after sprite 4's match at 200
	+delay_cycles 50 - (.lead + 29)
	dec SCREEN_CONTROL_REGISTER_2   ; 6   starts at cycle index 50: writes in 54 (40 columns
	                                ;     again) and 55 (38 columns)
	dex                             ; 2   halted on its first cycle until the sprite DMA ends
	bne .entry                      ; 3
	lda #COLUMNS_40
	sta SCREEN_CONTROL_REGISTER_2
	jmp BandDone
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

	; The band loop and the position table for this model
	lda #<NtscBand
	sta ENTRY
	lda #>NtscBand
	sta ENTRY + 1
	lda #<StepsNtsc
	sta STEP_TABLE
	lda #>StepsNtsc
	sta STEP_TABLE + 1
	lda IS_PAL
	beq +
	lda #<PalBand
	sta ENTRY
	lda #>PalBand
	sta ENTRY + 1
	lda #<StepsPal
	sta STEP_TABLE
	lda #>StepsPal
	sta STEP_TABLE + 1
	jmp ++
+	+print SCREEN_RAM + 21 * 40, TextNtsc
++
	lda #SPRITE_3_X_HOME
	sta SPRITE_3_HOME
	lda #0
	sta STEP
	sta COLLISION
	lda #$ff
	sta COLLISION_SHOWN
	jsr ShowStep

	; Raster IRQ on the line after the 24 row compare
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

; Show the collision register as the interrupt read it, and step the positions on the space bar.
Main:
	lda COLLISION
	cmp COLLISION_SHOWN
	beq +
	sta COLLISION_SHOWN
	jsr ShowCollision
+	jsr SpacePressed
	bcc Main
	inc STEP
	lda STEP
	cmp #STEPS
	bcc +
	lda #0
	sta STEP
+	jsr ShowStep
	jmp Main

; Carry set on a new press of the space bar, reading the keyboard matrix directly (the KERNAL's
; scan is off with the CIA interrupt). A held key counts once.
SpacePressed:
	lda #SPACE_ROW_SELECT
	sta CIA1_PORT_A
	lda CIA1_PORT_B
	and #SPACE_COLUMN_BIT
	bne +
	lda SpaceWasDown
	bne ++
	lda #1
	sta SpaceWasDown
	sec
	rts
+	lda #0
	sta SpaceWasDown
++	clc
	rts
SpaceWasDown:
	!byte 0

; Sprites 0 and 1 to the step's positions (X high bits set for both), and the step's text.
ShowStep:
	lda STEP
	asl
	tay
	lda (STEP_TABLE),y
	sta $d000
	iny
	lda (STEP_TABLE),y
	sta $d002
	lda STEP
	asl
	tay
	lda StepTexts,y
	sta FILLER
	lda StepTexts + 1,y
	sta FILLER + 1
	ldy #0
-	lda (FILLER),y
	cmp #$ff
	beq +
	sta SCREEN_RAM + STEP_ROW * 40,y
	iny
	bne -
+	rts

; The collision register's value as two hex digits.
ShowCollision:
	lda COLLISION_SHOWN
	lsr
	lsr
	lsr
	lsr
	tax
	lda HexDigits,x
	sta SCREEN_RAM + COLLISION_ROW * 40 + COLLISION_COLUMN
	lda COLLISION_SHOWN
	and #$0f
	tax
	lda HexDigits,x
	sta SCREEN_RAM + COLLISION_ROW * 40 + COLLISION_COLUMN + 1
	rts
HexDigits:
	!scr "0123456789abcdef"

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

; All eight sprites on the band's lines: 0, 1 and 2 with the ruler shape (0 and 1 placed by the
; step, 2 the reference), 3 and 4 solid blocks at their home X, 5-7 blank.
SetupSprites:
	ldx #0
-	lda SpriteShapes,x
	sta SPRITE_SHAPES,x
	inx
	cpx #128
	bne -
	lda #0
-	sta SPRITE_SHAPES + 128,x
	inx
	cpx #192
	bne -
	ldx #7
	ldy #14
-	lda SpritePointers,x
	sta SPRITE_POINTERS,x
	lda SpriteColours,x
	sta $d027,x
	lda #SPRITE_Y
	sta $d001,y
	dey
	dey
	dex
	bpl -
	lda #<SPRITE_2_X
	sta $d004
	lda #SPRITE_3_X_HOME
	sta SPRITE_3_X
	lda #SPRITE_4_X_HOME
	sta SPRITE_4_X
	lda #%00000111
	sta $d010            ; X above 255 for sprites 0, 1 and 2
	lda #0
	sta $d017            ; not Y expanded
	sta $d01c            ; single colour
	sta $d01d            ; not X expanded
	sta $d01b            ; in front of the graphics
	lda #%11111111
	sta $d015            ; all eight enabled
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
	+print SCREEN_RAM + COLLISION_ROW * 40, TextCollision
	+print SCREEN_RAM + 20 * 40, Text20
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
;Raster interrupt: the band in the lower border
;------------------------------------------------------------

Irq:
	; 24 rows: the bottom compare of 24 rows (line 247) is past and the one of 25 rows (line 251)
	; is not made, so the vertical border stays open below the display.
	lda #D011_24_ROWS
	sta SCREEN_CONTROL_REGISTER_1

	lda #SPRITE_Y
	sta TARGET_LINE
	ldx #BAND_LINES                 ; lines the loop runs, 251-271 (X survives the sync)
	ldy #SPRITE_3_X_NEW             ; sprite 3's new X, written every line (Y survives the sync)
	jsr SyncToLine                  ; returns 50 cycles into line 250, the line before the sprites
	dec SCREEN_CONTROL_REGISTER_2   ; 6   starts at 50: the writes are in cycle index 54 and 55
	jmp (ENTRY)                     ; its opcode fetch, at 56, is halted by the first sprite DMA;
	                                ; the loop is entered 5 cycles after the release, as after DEX+BNE
BandDone:
	lda #D011_25_ROWS               ; 25 rows again for the next frame's display
	sta SCREEN_CONTROL_REGISTER_1
	lda SPRITE_COLLISION            ; the band's collisions (the read clears the register)
	sta COLLISION
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

	+band_loop 10, 15, ~PalBand     ; 6569: released 10 cycles in, sprite 4's write in index 27
	+band_loop 9, 16, ~NtscBand     ; 6567R8: released 9 cycles in, the write in index 28

;------------------------------------------------------------
;Tables
;------------------------------------------------------------

; Sprite 0 and 1 X (low bytes, the high bit is set) per step. On the 6567R8 the fetch is two
; cycles later in a line whose first cycle starts at X 412 instead of 404 and that wraps at 520
; instead of 504: everything sits 8 to the right (sprite 0's fetch at X 363-374, sprite 1's at
; 379-390).
StepsPal:
	!byte <323, <347            ; both whole: their last pixels (346, 370) are before their fetches
	!byte <340, <361            ; 0 cut at 355 with pixel 14 repeated to 361, where it meets 1's first
	                            ; pixel; 1 cut at 371 (pixel 9, blank, repeated: nothing more shows)
	!byte <356, <372            ; both inside their fetch: not shown
	!byte <367, <340            ; 0 beyond its fetch: a line higher; 1 whole (its fetch is at 371)
StepsNtsc:
	!byte <331, <355
	!byte <348, <369
	!byte <364, <380
	!byte <375, <348

StepTexts:
	!word StepText0, StepText1, StepText2, StepText3

SpritePointers:
	!byte SPRITE_SHAPE_RULER, SPRITE_SHAPE_RULER, SPRITE_SHAPE_RULER
	!byte SPRITE_SHAPE_SOLID, SPRITE_SHAPE_SOLID
	!byte SPRITE_SHAPE_BLANK, SPRITE_SHAPE_BLANK, SPRITE_SHAPE_BLANK

SpriteColours:
	!byte COLOR_WHITE, COLOR_CYAN, COLOR_LIGHT_GREY, COLOR_YELLOW, COLOR_LIGHT_GREEN, 0, 0, 0

; The band's visible part is 17 lines (251-267), so the shapes use rows 0-16. The ruler: a solid
; top row, then stripes in columns 2, 6, 10, 14, 18 and 22, so a cut and a repeated pixel show
; (column 14 is a stripe: cut at X 355 from 340, that pixel repeats as a bar). The block: solid.
SpriteShapes:
	!byte %11111111, %11111111, %11111111
	!for .r, 1, 16 {
		!byte %00100010, %00100010, %00100010
	}
	!fill 4 * 3 + 1, 0
	!for .r, 1, 17 {
		!byte %11111111, %11111111, %11111111
	}
	!fill 4 * 3 + 1, 0

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "the vic-ii compares sprite x per pixel", $ff
Text2:	!scr "in the lower border, 3 (yellow) starts", $ff
Text3:	!scr "each line at x 151, 4 (green) at 120.", $ff
Text4:	!scr "every line 4 is written x 200 in the", $ff
Text5:	!scr "cycle its compare reaches 120: the new x", $ff
Text6:	!scr "counts, 4 shows at 200. 3 is written 231", $ff
Text7:	!scr "one pixel after its compare: stays 151.", $ff
Text9:	!scr "0 (white) and 1 (cyan) sit in the right", $ff
Text10:	!scr "border near their data fetch, where no", $ff
Text11:	!scr "sprite can start (0: x 355-366, 1: 371-", $ff
Text12:	!scr "382), a running one repeats its last", $ff
Text13:	!scr "pixel and stops, and beyond it the next", $ff
Text14:	!scr "row shows a line higher. 2 (grey): 296.", $ff
StepText0:	!scr "step 0: 0 at 323, 1 at 347, both whole  ", $ff
StepText1:	!scr "step 1: 0 at 340 bar to 361, 1: 361 cut ", $ff
StepText2:	!scr "step 2: 0 at 356, 1 at 372: not shown   ", $ff
StepText3:	!scr "step 3: 0 at 367 one up, 1 at 340 whole ", $ff
TextCollision:	!scr "collision register $d01e: $", $ff
Text20:	!scr "space: next step", $ff
TextNtsc:	!scr "ntsc: sprite 0/1 x are 8 further right", $ff
