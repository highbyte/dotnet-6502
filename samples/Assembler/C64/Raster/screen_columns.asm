;ACME assembler
;!to "./screen_columns.prg"

; VIC-II colour columns across the main screen area, with bad lines handled.
;
; The background colour register is written back to back, giving a colour change every 4 cycles =
; 32 pixels, the same resolution as in the border (see raster_columns.asm). The screen is blank, so
; the display area shows nothing but that colour.
;
; The display is on, so every 8th raster line is a bad line: the VIC-II pulls BA low on cycle 12 to
; fetch the video matrix and gives the bus back on cycle 55. The CPU stops at its next read, which
; means it can place no colour change anywhere in the visible part of that line, and the line comes
; out in a single flat colour. That much cannot be avoided; it is how the chip works. What must be
; avoided is the damage afterwards: those ~40 stolen cycles push every following line out of phase,
; and the columns stop being vertical.
;
; The fix does not count the stolen cycles. Each group of 8 raster lines begins by walking into the
; bad line, where the VIC-II holds the CPU and releases it on cycle 55 however late or early the
; group arrived. That is most of the alignment for free. Two details matter. The wait must be sure
; to enter the hold rather than pass it: it waits for the line before the bad line, then for the bad
; line itself, so the match is certainly within the first few cycles of it, and only then walks in.
; Waiting for the bad line alone would sometimes match before cycle 12 and sometimes after cycle 55,
; two arrivals 40 cycles apart, which is what made an earlier version of this sample flicker. And
; the walk in is made of instructions, so it is the walk that gets held, not a chosen instruction,
; leaving a few cycles of slack depending on where the hold caught it. CIA 1 timer A, free running
; with one raster line as its period, measures that slack against a reference taken at start from
; the raster line change itself (CalibrateLineClock), and a 1-cycle-resolution delay slide removes
; it. The lines of columns that follow are then drawn from the same cycle within the line every
; time, and on every run: the reference does not depend on the cycle the program was started on,
; so the picture is comparable with another emulator or a real machine. Aligning takes most of a raster line, and
; the group has to be back in time to catch the next bad line, so five of every eight lines carry
; columns; the other three, the bad line among them, stay flat. No interrupt timing has to be exact.
;
; Line lengths differ per model (63 cycles on PAL, 65 on NTSC), so the model is detected at start
; and the matching line code is used.


;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

SCREEN_CONTROL_REGISTER_1 = $d011
SCREEN_RASTER_LINE = $d012
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021

SCREEN_RAM = $0400
COLOR_RAM = $d800

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

IS_PAL = $fb            ; zero page: $80 on PAL, 0 on NTSC (bit 7, so BIT can test it)
CALIB = $fd             ; zero page: line clock value 9 cycles into a raster line (see CalibrateLineClock)
LINE_CYCLES = $f8       ; zero page: 63 on PAL, 65 on NTSC
TARGET_LINE = $fe       ; zero page: raster line CalibrateLineClock waits for
SAVE_X = $04            ; zero page: the column colour in X across the alignment
LAST_LINE_LO = $fc      ; zero page: scratch for the model detection
GROUP_COUNT = $f9       ; zero page: groups left to draw this frame
MEAS_INDEX = $fa        ; measurement builds only: next slot in the arrival log at $8000
BEFORE_BAD_LINE_BITS = 2    ; low three bits of the line before a bad line
BAD_LINE_BITS = 3           ; low three bits of a bad line, with the default YSCROLL of 3
INTO_HOLD_CYCLES = 11       ; cycles to walk into the bad line, past the cycle the hold starts on
SLIDE_CENTER = 17           ; slide entry for a typical arrival; the slide has 34 entries
SLIDE_RANGE = 16            ; the entry table covers arrivals this far either side of typical
; Reference minus a typical arrival's reading, as a byte: the arrival's read lands past the line's
; end, 6 cycles into the next line on PAL (63 + 6 - 9 = 60 cycles after the reference, which the
; timer's reload turns into -3) and on the last cycle of the bad line on NTSC (64 - 9 = 55, -10).
; Measured with a MEASURE build (see align_to_reference), the same for every start cycle.
ARRIVAL_OFFSET_PAL = 253
ARRIVAL_OFFSET_NTSC = 246
ARRIVAL_OFFSET = $f7        ; zero page: the model's value

; Lines the calibration polls: in the top border, above any bad line.
CALIB_FIRST_LINE = 10
CALIB_LINES = 16
FILLER = $02            ; zero page: scratch, also the byte the timing filler reads

; With the default YSCROLL of 3, a bad line is any raster line whose low three bits are 3. Each
; group waits on a bad line and then draws the lines after it.
FIRST_BAD_LINE = 51     ; the first bad line of the screen (51 & 7 = 3)
GROUPS = 16             ; 16 groups of 8 raster lines: 51-178
LINES_PER_GROUP = 5     ; lines of columns drawn per group; the alignment takes most of a line

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

COLUMNS = 15            ; 15 * 4 = 60 cycles of columns per line

COLOR_BLACK = 0
COLOR_WHITE = 1

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

; Walk into the VIC-II's hold on the next bad line and read the line clock there. Waiting first for
; the line before the bad line makes the second wait's match certainly early in the bad line, so the
; walk in ends up inside the hold rather than past it. The clock reading is left in A: it says how
; much later than the reference this arrival was. Low three bits identify the lines, so no line
; counter has to be kept between groups.
!macro run_into_hold {
-	lda SCREEN_RASTER_LINE          ; 4
	and #7                          ; 2
	cmp #BEFORE_BAD_LINE_BITS       ; 2
	bne -                           ; 3 taken / 2 falling through
-	lda SCREEN_RASTER_LINE          ; 4
	and #7                          ; 2
	cmp #BAD_LINE_BITS              ; 2
	bne -                           ; 3 taken / 2 falling through
	+delay_cycles INTO_HOLD_CYCLES  ; from here the fetches are inside the hold
	lda CIA1_TIMER_A_LO             ; 4   read once the bus is back
}

; Remove the slack the hold left, by delaying the difference between this arrival and a typical
; one. The reference is the line clock's value at a fixed cycle of a raster line, taken at start
; (CalibrateLineClock), so the columns land on the same cycle on every run, not only on every frame
; of one run; the entry table turns the difference into the slide entry without arithmetic that
; could wrap when the two readings straddle the timer's reload.
!macro align_to_reference {
	stx SAVE_X                      ; 3   the column colour in X survives the table lookup
	sta FILLER                      ; 3   this arrival's clock reading
!ifdef MEASURE {
	sty $8100
	ldy MEAS_INDEX
	lda CALIB
	sec
	sbc FILLER
	sta $8000,y
	inc MEAS_INDEX
	ldy $8100
}
	lda CALIB                       ; 3
	sec                             ; 2
	sbc FILLER                      ; 3   reference minus this arrival: larger the later it is
	tax                             ; 2
	lda SlideEntryTable,x           ; 4   later arrival => larger entry => shorter delay
	sta .SlideJmp + 1               ; 4
.SlideJmp:
	jmp .Slide                      ; 3
	!align 255, 0
.Slide:
	; Entered at offset k (0-33) this takes 35 - k cycles: pairs of $C9 are CMP #$C9 (2 cycles);
	; the tail is either CMP $EA (3 cycles) or CMP #$C5 + NOP (4 cycles) depending on parity.
	!fill 32, $c9
	!byte $c5, $ea
	ldx SAVE_X                      ; 3
}

; The slide entry table, page aligned so the lookup never crosses a page.
!macro slide_entry_table {
	!align 255, 0
SlideEntryTable:
	!fill 256, 0
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

; Seven lines of columns, written out rather than looped: a loop's counter and branch would cost 8
; cycles per line, a 64-pixel column of nothing. Each line is exactly one raster line long, so the
; columns land on the same cycles on every line. 15 is a multiple of 3, so every line starts on the
; same colour.
!macro column_lines .fill, ~.entry {
.Start:
	+align_to_reference
	lda #COLOR_WHITE                ; 2   the third colour; X and Y already hold theirs
	!for .line, 1, LINES_PER_GROUP {
		!for .group, 1, COLUMNS / 3 {
			sta SCREEN_BACKGROUND_COLOR_ADDRESS     ; 4
			stx SCREEN_BACKGROUND_COLOR_ADDRESS     ; 4
			sty SCREEN_BACKGROUND_COLOR_ADDRESS     ; 4
		}
		+delay_cycles .fill
	}
	.entry = .Start
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

	lda #COLOR_BLACK
	sta SCREEN_BORDER_COLOR_ADDRESS
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
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

	; Raster IRQ two lines above the first bad line. Its timing does not have to be exact: the
	; first group aligns itself on the bad line like every other group.
	lda #FIRST_BAD_LINE - 2
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
	lda #ARRIVAL_OFFSET_NTSC
	sta ARRIVAL_OFFSET
	lda LAST_LINE_LO
	cmp #$20
	bcc +
	lda #$80
	sta IS_PAL
	lda #CYCLES_PER_LINE_PAL
	sta LINE_CYCLES
	lda #ARRIVAL_OFFSET_PAL
	sta ARRIVAL_OFFSET
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
; the group alignment needs no arithmetic that could branch or wrap. An arrival's reading is taken
; inside the bad line's hold, ARRIVAL_OFFSET cycles after the reference's cycle on a typical
; arrival, and the entry is SLIDE_CENTER plus how far this arrival is from that. The difference can
; also come out a line period off, when the two readings straddle the timer's reload: those
; entries are filled the same way. Everything else gets the nominal entry.
BuildSlideEntryTable:
	ldx #0
	lda #SLIDE_CENTER
-	sta SlideEntryTable,x
	inx
	bne -
	lda ARRIVAL_OFFSET
	sec
	sbc #SLIDE_RANGE
	tax                             ; difference, as a byte
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
	lda FILLER
	cmp #SLIDE_CENTER + SLIDE_RANGE + 1
	bne -
	rts


; Blank screen: the display area then shows the background colour and nothing else.
ClearScreen:
	ldx #0
	lda #$20
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

; Text below the effect, on the rows the groups do not cover.
DrawScreen:
	+print SCREEN_RAM + 18 * 40, Text0
	+print SCREEN_RAM + 19 * 40, Text1
	+print SCREEN_RAM + 21 * 40, Text2
	+print SCREEN_RAM + 22 * 40, Text3
	+print SCREEN_RAM + 23 * 40, Text4
	+print SCREEN_RAM + 24 * 40, Text5
	rts

; Raster interrupt: one group of 8 raster lines per pass, each aligning itself on the bad line.
Irq:
	lda #GROUPS
	sta GROUP_COUNT
!ifdef MEASURE {
	lda #0
	sta MEAS_INDEX
}
	ldy #6                          ; blue

GroupLoop:
	+run_into_hold
	ldx #7                          ; yellow (A carries the clock reading, so load X here)
	bit IS_PAL                      ; bit 7 set on PAL; leaves A alone
	bmi +
	jmp NtscGroup
+	jmp PalGroup
GroupDone:
	dec GROUP_COUNT
	beq GroupsDone
	jmp GroupLoop

GroupsDone:
	lda #COLOR_BLACK
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

	+column_lines CYCLES_PER_LINE_PAL - COLUMNS * 4, ~PalGroup
	jmp GroupDone
	+column_lines CYCLES_PER_LINE_NTSC - COLUMNS * 4, ~NtscGroup
	jmp GroupDone

;------------------------------------------------------------
;Tables and text
;------------------------------------------------------------

	+slide_entry_table

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "background colour written back to back", $ff
Text1:	!scr "over the screen: 32 pixel columns.", $ff
Text2:	!scr "every 8th line is a bad line: the vic-ii", $ff
Text3:	!scr "holds the bus and that line stays flat.", $ff
Text4:	!scr "each group re-aligns itself in that hold,", $ff
Text5:	!scr "so the columns stay vertical below it.", $ff
