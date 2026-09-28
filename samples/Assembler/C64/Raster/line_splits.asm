;ACME assembler
;!to "./line_splits.prg"

; VIC-II register changes in the middle of a line.
;
; The VIC-II's graphics sequencer reads XSCROLL, the mode bits and the memory pointers as it goes:
; a write in the middle of a line takes effect at the column the chip applies it, not at the next
; line. This program writes a register on every line 36 cycles in (counting from 1), just past the
; middle of the display, and puts the old value back after the right border compare, so every line
; shows the old state on its left half and the new one on its right:
;
;   XSCROLL      the right half is fine scrolled by a wave, the left half stands still
;   $d018        the right half shows the same screen codes from the lowercase character set
;   MCM          the right half shows its cells in multicolour, the left half in hires
;
; Each register change lands where the chip puts it: XSCROLL moves the next load of the shift
; register, a pointer change takes effect at the next fetch, and a mode bit reaches the output four
; pixels into the following cycle (the multicolour decoding a cycle later, which shows as a short
; flash of background colour 2 at the seam).
;
; On the first line of every character row, the bad line, the CPU is halted from cycle 12 to 55
; while the chip fetches the row's screen codes, so nothing can be written there and the line stays
; in the old state: the right half's seam has a gap every eight lines. The halt ends on the same
; cycle every time, which is what keeps the loop in step from row to row; only the first row is
; entered from the line clock sync the other raster samples use.

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
MEMORY_POINTERS = $d018
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021
BACKGROUND_COLOR_1 = $d022
BACKGROUND_COLOR_2 = $d023

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

COLUMNS_40 = %11001000          ; $d016: 40 columns, XSCROLL 0, hires
MULTICOLOR = %00010000          ; $d016 bit 4
CHARSET_UPPER = $15             ; $d018: screen at $0400, character set at $1000 (upper case)
CHARSET_LOWER = $17             ; character set at $1800 (lower case)

D011_BASE = %00011000           ; DEN on, 25 rows; low three bits carry YSCROLL
YSCROLL_NORMAL = 3

; The band: rows 2-18. Rows 2-6 split XSCROLL, 8-12 the character set, 14-18 the mode; rows 7 and
; 13 between them are run through the same loop with writes that change nothing.
FIRST_ROW = 2
ROWS_A = 6                      ; rows 2-7: XSCROLL, then the gap row
ROWS_B = 5                      ; rows 8-12: character set
ROWS_C = 6                      ; row 13, the gap row, then rows 14-18: multicolour
BAND_LINES = (ROWS_A + ROWS_B + ROWS_C) * 8
FIRST_LINE = 51 + FIRST_ROW * 8 ; 67, the bad line of row 2
IRQ_LINE = FIRST_LINE - 7       ; 60: no bad line between it and the sync's line (59 is one)
SYNC_LINE = FIRST_LINE - 1      ; the line before the band: the sync returns 50 cycles into it

; Cycle indices (counting from 0) the writes land on: the split 35 (the chip's cycle 36), the
; restore 58, past the right border compare. A read halted by a bad line is made on index 54
; (measured with the MEASURE2 build on both models), so the NOP halted on its opcode fetch ends on
; 55 and the instruction after it starts on RESUME.
SPLIT_CYCLE = 35
RESTORE_CYCLE = 58
RESUME = 56

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

FILLER       = $02      ; zero page: scratch, also the byte the timing filler reads
LINE_CYCLES  = $03      ; zero page: 63 on PAL, 65 on NTSC
ENTRY        = $04      ; zero page, 2 bytes: the band code for this model
SAVE_X       = $06      ; zero page: the caller's X across SyncToLine
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

COLOR_BLACK = 0
COLOR_WHITE = 1
COLOR_RED = 2
COLOR_GREEN = 5
COLOR_BLUE = 6
COLOR_LIGHT_BLUE = 14
COLOR_BROWN = 9
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



; One line of the band, entered at cycle index .entry of the line (negative: that many cycles
; before it begins) and left at index 61: the split write at SPLIT_CYCLE, the restore at
; RESTORE_CYCLE, and the line counter advanced.
!macro split_line .reg, .entry {
	+delay_cycles SPLIT_CYCLE - 7 - .entry
	lda SplitTable,y                ; 4
	sta .reg                        ; 4   the write is in its last cycle: SPLIT_CYCLE
	+delay_cycles RESTORE_CYCLE - SPLIT_CYCLE - 8
	lda RestoreTable,y              ; 4
	sta .reg                        ; 4   RESTORE_CYCLE
	iny                             ; 2   ends at index 60
}

; X character rows writing .reg, one row per pass. A pass begins with the row's bad line: the NOPs
; run into the CPU halt at cycle index 11 and the code after it starts on RESUME whatever the
; entry's exact cycle was; .start is entered 71 cycles after the previous line began (from the
; JMP that starts a band) and .back 3 cycles earlier (from the loop's own jump). The bad line
; cannot be split and keeps the old state; its restore write lands on the first cycle of the next
; line, in the left border, and that line's code is entered 3 (PAL) or 1 (NTSC) cycles in.
!macro row_loop .cycles, .reg, ~.start, ~.back {
.back
	+delay_cycles 3
.start
	+delay_cycles .cycles - 60      ; to index 11 of the bad line
	nop                             ; halted on its first cycle until the row's fetches end
!ifdef MEASURE {
	sta $d020                       ; measurement builds: the write is 3 cycles after RESUME
}
	lda RestoreTable,y              ; 4   from RESUME
	sta .reg                        ; 4   the write is 7 cycles after RESUME
	iny                             ; 2
	+split_line .reg, RESUME + 10 - .cycles
	!for .l, 2, 7 {
		+split_line .reg, 61 - .cycles
	}
	dex                             ; 2   61-62
	beq .done                       ; 2   (3 taken: the band's next part is entered 66 cycles in)
	jmp .back                       ; 3   the next row's bad line began 68 cycles after this line
.done
}

; The whole band for one line length: entered by JMP (ENTRY) 57 cycles into SYNC_LINE.
!macro band .cycles, ~.entry {
.entry
	+delay_cycles 71 - 57 - 3
	jmp .startA                     ; 3   arrives 71 cycles after SYNC_LINE began
	+row_loop .cycles, SCREEN_CONTROL_REGISTER_2, ~.startA, ~.backA
	ldx #ROWS_B                     ; 2   after the loop's DEX and BEQ: 66 cycles after the line began
	jmp .startB                     ; 3   71
	+row_loop .cycles, MEMORY_POINTERS, ~.startB, ~.backB
	ldx #ROWS_C
	jmp .startC
	+row_loop .cycles, SCREEN_CONTROL_REGISTER_2, ~.startC, ~.backC
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

	lda #COLOR_LIGHT_BLUE
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #COLOR_BLUE
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	lda #COLOR_RED
	sta BACKGROUND_COLOR_1
	lda #COLOR_GREEN
	sta BACKGROUND_COLOR_2
	lda #COLUMNS_40
	sta SCREEN_CONTROL_REGISTER_2
	lda #CHARSET_UPPER
	sta MEMORY_POINTERS
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

	; The band code for this model
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
	; Raster IRQ above the band
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

; The title, the three bands' rows and the explanation.
DrawScreen:
	+print SCREEN_RAM + 0 * 40, Text0
	ldx #0
-	lda BarPattern,x                ; rows 2-6: bars every fourth column, so the wave shows
	sta SCREEN_RAM + 2 * 40,x
	sta SCREEN_RAM + 3 * 40,x
	sta SCREEN_RAM + 4 * 40,x
	sta SCREEN_RAM + 5 * 40,x
	sta SCREEN_RAM + 6 * 40,x
	lda LetterPattern,x             ; rows 8-12: letters, upper case from this set
	sta SCREEN_RAM + 8 * 40,x
	sta SCREEN_RAM + 9 * 40,x
	sta SCREEN_RAM + 10 * 40,x
	sta SCREEN_RAM + 11 * 40,x
	sta SCREEN_RAM + 12 * 40,x
	lda CellPattern,x               ; rows 14-18: cells whose colour nibble has bit 3 set
	sta SCREEN_RAM + 14 * 40,x
	sta SCREEN_RAM + 15 * 40,x
	sta SCREEN_RAM + 16 * 40,x
	sta SCREEN_RAM + 17 * 40,x
	sta SCREEN_RAM + 18 * 40,x
	lda #COLOR_BROWN                ; 9: brown in hires, colour 1 (white) as a multicolour cell
	sta COLOR_RAM + 14 * 40,x
	sta COLOR_RAM + 15 * 40,x
	sta COLOR_RAM + 16 * 40,x
	sta COLOR_RAM + 17 * 40,x
	sta COLOR_RAM + 18 * 40,x
	inx
	cpx #40
	bne -
	+print SCREEN_RAM + 20 * 40, Text20
	+print SCREEN_RAM + 21 * 40, Text21
	+print SCREEN_RAM + 22 * 40, Text22
	+print SCREEN_RAM + 23 * 40, Text23
	+print SCREEN_RAM + 24 * 40, Text24
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
	lda #SYNC_LINE
	sta TARGET_LINE
	ldy #0                          ; the band's line counter (Y survives the sync)
	jsr SyncToLine                  ; returns 50 cycles into SYNC_LINE, the line before the band
!ifdef MEASURE {
	sta $d020                       ; measurement builds: writes 3, 7, 11, ... cycles after the return
	sta $d020
	sta $d020
}
!ifdef MEASURE2 {
	!fill 8, $ea                    ; measurement builds: reads up to the bad line's halt (index 11),
	!for .i, 1, 20 {                ; then writes every 4 cycles show where the halt released the CPU
		sta $d020
	}
}
	ldx #ROWS_A                     ; 2   50-51
	jmp (ENTRY)                     ; 5   52-56: the band code is entered 57 cycles into the line
BandDone:
	lda #COLUMNS_40
	sta SCREEN_CONTROL_REGISTER_2
	lda #CHARSET_UPPER
	sta MEMORY_POINTERS
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

	+band CYCLES_PER_LINE_PAL, ~PalBand
	+band CYCLES_PER_LINE_NTSC, ~NtscBand

;------------------------------------------------------------
;Tables
;------------------------------------------------------------

; The value written at SPLIT_CYCLE and the one put back at RESTORE_CYCLE, per line of the band
; (index: line, 0 to BAND_LINES - 1). Rows 0-4 of the band: XSCROLL from a triangle wave; rows 6-10:
; the lower case character set; rows 12-16: multicolour; the gap rows write the normal values.
	!align 255, 0                   ; no page crossing in the indexed loads
SplitTable:
	!for .i, 0, BAND_LINES - 1 {
		!set .row = .i DIV 8
		!set .t = .i & 15
		!if .row < 5 {
			!byte COLUMNS_40 | (.t - ((.t >> 3) * (2 * .t - 15)))
		} else {
			!if (.row >= 6) & (.row <= 10) {
				!byte CHARSET_LOWER
			} else {
				!if .row >= 12 {
					!byte COLUMNS_40 | MULTICOLOR
				} else {
					!byte COLUMNS_40
				}
			}
		}
	}
RestoreTable:
	!for .i, 0, BAND_LINES - 1 {
		!set .row = .i DIV 8
		!if (.row >= 6) & (.row <= 10) {
			!byte CHARSET_UPPER
		} else {
			!byte COLUMNS_40
		}
	}

BarPattern:
	!for .i, 0, 39 {
		!if (.i & 3) = 0 {
			!byte $5d               ; a vertical bar
		} else {
			!byte SPACE_CHAR
		}
	}
LetterPattern:
	!scr "abcdefghijklmnopqrstuvwxyz abcdefghijklm"
CellPattern:
	!scr "multicolour cells: hires left, mc right "

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "vic-ii register splits in mid-line", $ff
Text20:	!scr "every line writes a register 36 cycles", $ff
Text21:	!scr "in and restores it after the display:", $ff
Text22:	!scr "old state left, new state right. bad", $ff
Text23:	!scr "lines halt the cpu, so every eighth", $ff
Text24:	!scr "line cannot be split (the gaps).", $ff
