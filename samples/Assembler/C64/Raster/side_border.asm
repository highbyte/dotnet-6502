;ACME assembler
;!to "./side_border.prg"

; VIC-II side borders: opening them, and what it takes.
;
; The VIC-II's border unit has one flip-flop for the left and right border. It is set when the
; X coordinate reaches the right compare value, 344 with 40 columns selected or 335 with 38, and
; reset when X reaches the left one (24 or 31) on a line the vertical border leaves open. A pixel
; is border colour while the flip-flop is set. So a program that has 40 columns selected when
; X passes 335 and 38 columns when it passes 344 misses both compares: the flip-flop stays clear,
; the display window's output continues to the frame's edges on that line and the left border of
; the next, and sprites placed there are shown. The chip evaluates the 335 compare in cycle 56
; and the 344 compare in cycle 57 (counting from 1), each with the registers as written before
; that cycle, so the 38 column write has to land in cycle 56 exactly, on every line the border
; is to stay open.
;
; Two things take the CPU away on such lines. A bad line halts it from cycle 12 to 55, so no
; write can be placed on it: this band therefore avoids bad lines by changing YSCROLL every line
; (FLD), which also means the band's lines show idle output, the background colour, and the rows
; below are pushed down. And sprite DMA, for the eight sprites the band uses, halts the CPU at
; its first read from cycle 55 on until 10 (6569) or 9 (6567R8) cycles into the next line. Writes
; still go through, but a plain store's operand fetch in cycle 55 would be halted, so the 38
; column write is made with DEC: a read-modify-write whose last read is in cycle 54 and whose two
; writes are in cycles 55 and 56, the first putting the 40 column value back and the second
; writing it decremented, which clears the column select bit (and sets XSCROLL to 7, harmless on
; the band's idle lines). The halt's release is what keeps the band's lines in step: every line
; of the band resumes on the same cycle, so the loop is timed from there and needs no other
; synchronisation. Only the first line is entered from the line clock sync.
;
; The band: 21 lines with eight X-expanded sprites in a row across the full width of the frame,
; the outer ones in the side borders, over the background colour running edge to edge.

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

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

IDLE_BYTE_ADDRESS = $3fff       ; shown in the opened borders: blank

COLUMNS_40 = %11001000          ; $d016: 40 columns, XSCROLL 0 (DEC of it selects 38 columns)

D011_BASE = %00011000           ; DEN on, 25 rows; low three bits carry YSCROLL
YSCROLL_NORMAL = 3
YSCROLL_BEFORE_BAND = 5         ; no line from the interrupt to the band's first line is a bad line

SPRITE_Y = 146                  ; sprites on raster lines 147-167 (the last line of row 11 is 146)
BAND_LINES = 21
IRQ_LINE = SPRITE_Y - 3
SPRITE_POINTERS = $07f8
SPRITE_SHAPE_ADDRESS = $3000
SPRITE_SHAPE_BLOCK = SPRITE_SHAPE_ADDRESS / 64

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

FILLER       = $02      ; zero page: scratch, also the byte the timing filler reads
LINE_CYCLES  = $03      ; zero page: 63 on PAL, 65 on NTSC
ENTRY        = $04      ; zero page, 2 bytes: the band loop for this model
SAVE_X       = $06      ; zero page: the caller's X across SyncToLine
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

COLOR_BLUE = 6
COLOR_WHITE = 1
COLOR_DARK_GREY = 11
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


; One band line, entered .resume cycles into the line when the sprite DMA hold releases the CPU.
; 40 columns first (the 335 compare must see them), YSCROLL for the next line (written after this
; line's bad line decision at cycle 14, before the next line's), then the DEC timed so its second
; write lands in cycle 56 (index 55); the DEX after it is halted by the next line's sprite DMA.
!macro band_loop .resume, ~.entry {
.entry
	lda #COLUMNS_40                 ; 2   from .resume + 5 (DEX's second cycle and the branch)
	sta SCREEN_CONTROL_REGISTER_2   ; 4
	lda YscrollTable,x              ; 4   the next line's YSCROLL, indexed by the line counter
	sta SCREEN_CONTROL_REGISTER_1   ; 4
	+delay_cycles 50 - (.resume + 5 + 14)
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

	lda #COLOR_DARK_GREY
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #COLOR_BLUE
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	lda #0
	sta IDLE_BYTE_ADDRESS
	lda #COLUMNS_40
	sta SCREEN_CONTROL_REGISTER_2
	lda #D011_BASE + YSCROLL_NORMAL
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

	; The band loop for this model
	lda #<NtscBand
	sta ENTRY
	lda #>NtscBand
	sta ENTRY + 1
	lda IS_PAL
	beq +
	lda #<PalBand
	sta ENTRY
	lda #>PalBand
	sta ENTRY + 1
+
	; Raster IRQ three lines above the band
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
	jmp *

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

; Eight X-expanded sprites in a row on the band's lines, spread over the full width of the frame:
; the first and the last two sit in the side borders (X below 24 and above 343).
SetupSprites:
	ldx #62
-	lda SpriteShape,x
	sta SPRITE_SHAPE_ADDRESS,x
	dex
	bpl -
	ldx #7
	ldy #14
-	lda #SPRITE_SHAPE_BLOCK
	sta SPRITE_POINTERS,x
	lda SpriteColours,x
	sta $d027,x
	lda SpriteX,x
	sta $d000,y
	lda #SPRITE_Y
	sta $d001,y
	dey
	dey
	dex
	bpl -
	lda #%11000001
	sta $d010            ; X above 255 for sprites 0, 6 and 7
	lda #%11111111
	sta $d01d            ; X expanded
	lda #0
	sta $d017            ; not Y expanded
	sta $d01c            ; single colour
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
	lda #COLOR_WHITE
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
	+print SCREEN_RAM + 8 * 40, Text8
	+print SCREEN_RAM + 9 * 40, Text9
	+print SCREEN_RAM + 10 * 40, Text10
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
;Raster interrupt: the band
;------------------------------------------------------------

Irq:
	; No bad line from here to the band's first line: YSCROLL 5 matches none of lines 144-147.
	lda #D011_BASE + YSCROLL_BEFORE_BAND
	sta SCREEN_CONTROL_REGISTER_1

	lda #SPRITE_Y
	sta TARGET_LINE
	ldx #BAND_LINES                 ; lines the loop runs, 147-167 (X survives the sync)
	jsr SyncToLine                  ; returns 50 cycles into line 146, the line before the sprites
!ifdef MEASURE {
	sta $d020                       ; measurement builds: writes 3, 7, 11, ... cycles after the return
	sta $d020                       ; show where the return is and where the sprite DMA halt begins
	sta $d020
	sta $d020
	sta $d020
	sta $d020
}
	dec SCREEN_CONTROL_REGISTER_2   ; 6   starts at 50: the writes are in cycle index 54 and 55
	jmp (ENTRY)                     ; its opcode fetch, at 56, is halted by the first sprite DMA;
	                                ; the loop is entered 5 cycles after the release, as after DEX+BNE
BandDone:
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

	+band_loop 10, ~PalBand
	+band_loop 9, ~NtscBand

;------------------------------------------------------------
;Tables
;------------------------------------------------------------

; YSCROLL for lines 148-168, written on the line before each: the line's own low three bits plus
; four, so none of them is a bad line; the last (index 1, written on line 167) puts the normal value
; back, and the next row then starts on line 171. Indexed by the loop counter, which is 21 on
; line 147 and 1 on line 167, so entry j is for line 169 - j.
YscrollTable:
	!byte 0                         ; index 0 is never used
	!byte D011_BASE + YSCROLL_NORMAL
	!for .j, 2, BAND_LINES {
		!byte D011_BASE + ((((169 - .j) & 7) + 4) & 7)
	}

; A 24x21 ball, the shape of all eight sprites.
SpriteShape:
	!byte %00000000, %01111110, %00000000
	!byte %00000001, %11111111, %10000000
	!byte %00000011, %11111111, %11000000
	!byte %00000111, %11111111, %11100000
	!byte %00001111, %11111111, %11110000
	!byte %00011111, %11111111, %11111000
	!byte %00011111, %11111111, %11111000
	!byte %00111111, %11111111, %11111100
	!byte %00111111, %11111111, %11111100
	!byte %00111111, %11111111, %11111100
	!byte %00111111, %11111111, %11111100
	!byte %00111111, %11111111, %11111100
	!byte %00111111, %11111111, %11111100
	!byte %00111111, %11111111, %11111100
	!byte %00011111, %11111111, %11111000
	!byte %00011111, %11111111, %11111000
	!byte %00001111, %11111111, %11110000
	!byte %00000111, %11111111, %11100000
	!byte %00000011, %11111111, %11000000
	!byte %00000001, %11111111, %10000000
	!byte %00000000, %01111110, %00000000

SpriteColours:
	!byte 1, 7, 13, 3, 14, 10, 15, 8

; Sprite X (low byte): 50 pixels apart from X -16 (496) to 334; each is 48 pixels wide.
SpriteX:
	!byte <496, 34, 84, 134, 184, 234, <284, <334

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "vic-ii side borders", $ff
Text2:	!scr "the border flip-flop is set when the", $ff
Text3:	!scr "raster reaches x 335 (38 columns) or", $ff
Text4:	!scr "x 344 (40 columns). 40 columns at the", $ff
Text5:	!scr "first and 38 at the second miss both:", $ff
Text6:	!scr "the side borders stay open, the screen", $ff
Text7:	!scr "runs edge to edge and sprites show", $ff
Text8:	!scr "there. the band below does it on 21", $ff
Text9:	!scr "lines, timed by the sprite dma hold,", $ff
Text10:	!scr "with bad lines pushed away (fld).", $ff
