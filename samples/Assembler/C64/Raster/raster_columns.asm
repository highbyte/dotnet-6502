;ACME assembler
;!to "./raster_columns.prg"

; VIC-II colour columns: how finely the border colour can be changed along a raster line.
;
; The VIC-II applies a write to $D020 from the cycle the write lands on, so the picture has
; 8-pixel (one cycle) granularity. What limits a program is the CPU: the fastest 6502 store to
; an absolute address takes 4 cycles, so the highest column resolution is
;
;   4 cycles = 32 pixels = 4 character cells, using STA/STX/STY back to back.
;
; That rate needs the colours already in A, X and Y, so at most three colours repeat. Loading a
; new colour costs 2 cycles more, giving
;
;   6 cycles = 48 pixels = 6 character cells, with any colour per column.
;
; Two bands in the top border show both. Every line of a band runs the same store sequence and
; takes exactly one raster line (63 cycles on PAL, 65 on NTSC; the model is detected at start), so
; the colour changes land at the same position on every line and the columns are vertical. The
; store sequence is written out for every line of a band rather than looped, because a loop's own
; counter and branch would cost 8 cycles per line, a 64-pixel column of nothing. A raster line is
; still not a whole number of columns, so one column per line is wider by the remainder: 3 cycles
; on PAL, 5 on NTSC.
;
; The effect runs in the border to keep it simple. With the display on, one raster line in eight is
; a bad line: the VIC-II takes the bus for about 40 cycles in the middle of it to fetch the video
; matrix, and the CPU stops at its next opcode fetch. That window covers the whole visible part of
; that one line, so it comes out in a single colour; the other seven lines are untouched and take
; columns at the same rate as the border does. Keeping those seven lines in step across the stolen
; cycles is the work, and screen_columns.asm does it. Above raster line 48 no bad line can occur,
; whatever YSCROLL is, so the top border needs none of that.
;
; A raster interrupt alone is not accurate enough to place columns: it arrives a few cycles late
; depending on the instruction it interrupts, and the raster line poll that follows is only
; accurate to its 7-cycle loop, so the columns would jump sideways from frame to frame. CIA 1
; timer A is therefore run as a free line clock (period = one raster line), force-loaded once at a
; fixed offset from a raster poll. Every later poll reads the timer at the matching offset, and the
; value tells how early or late this frame is; a 1-cycle-resolution delay slide absorbs it.

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

SCREEN_CONTROL_REGISTER_1 = $d011
SCREEN_RASTER_LINE = $d012
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

SCREEN_RAM = $0400
COLOR_RAM = $d800

IS_PAL = $fb            ; zero page: 1 on PAL, 0 on NTSC
LAST_LINE_LO = $fc      ; zero page: scratch for the model detection
CALIB = $fd             ; zero page: line clock value of the reference poll (see SyncToLine)
TARGET_LINE = $fe       ; zero page: raster line SyncToLine waits for
FILLER = $02            ; zero page: scratch, also the byte the timing filler reads

; Both bands sit below raster line 50, where no bad line can occur with the default YSCROLL of 3,
; and inside the part of the top border that both models actually show: PAL's visible top border
; starts at raster line 9 but NTSC's only at 34.
; A band starts partway into its first line and so ends partway into the line after its last, which
; is why the second band's first line is two lines after the first band's last: its wait would
; otherwise be for a line that has just gone by, and would sit out a whole frame.
BAND_A_LINE = 34        ; first raster line of the 4-cycle band
BAND_A_LINES = 7
BAND_B_LINE = 43        ; first raster line of the 6-cycle band
BAND_B_LINES = 7

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65

BAND_A_COLUMNS = 15     ; 15 * 4 = 60 cycles of columns
BAND_B_COLUMNS = 10     ; 10 * 6 = 60 cycles of columns

; Slide entry for a frame exactly as early as the reference frame. The poll loop is 7 cycles, so a
; frame differs from the reference by at most 6 cycles either way and the entry stays within 3-15.
SLIDE_CENTER = 9

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

; Wait for the raster line in TARGET_LINE, then read the line clock. Written once and used both by
; the calibration in Init and by SyncToLine, so the clock is always read at the same offset after
; the poll and the two values can be compared directly.
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

; The 4-cycle band, written out line by line: 15 stores from the three preloaded colour registers,
; then the remainder of the raster line. 15 is a multiple of 3, so every line starts on the same
; colour and the columns line up vertically.
!macro band_a .fill, .lines, ~.entry {
.Start:
	!for .line, 1, .lines {
		!for .group, 1, BAND_A_COLUMNS / 3 {
			sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
			stx SCREEN_BORDER_COLOR_ADDRESS     ; 4
			sty SCREEN_BORDER_COLOR_ADDRESS     ; 4
		}
		+delay_cycles .fill
	}
	.entry = .Start
}

; One line of the 6-cycle band: 10 columns, each loading its own colour first.
!macro band_b_line {
	lda #2                              ; 2   red
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #8                              ; 2   orange
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #7                              ; 2   yellow
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #5                              ; 2   green
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #13                             ; 2   light green
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #3                              ; 2   cyan
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #14                             ; 2   light blue
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #6                              ; 2   blue
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #4                              ; 2   purple
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
	lda #1                              ; 2   white
	sta SCREEN_BORDER_COLOR_ADDRESS     ; 4
}

; The 6-cycle band, written out line by line.
!macro band_b .fill, .lines, ~.entry {
.Start:
	!for .line, 1, .lines {
		+band_b_line
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

	; Calibration: run the poll once and keep the clock value it produces. Every later poll is
	; compared against this, so the effect aligns to this frame whatever the code layout is.
	lda #BAND_A_LINE
	sta TARGET_LINE
	+poll_and_read_clock
	sta CALIB

	; Raster IRQ two lines above the first band: interrupt latency and the KERNAL's interrupt
	; entry take more than one raster line, and the handler must reach its poll in time.
	lda #BAND_A_LINE - 2
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
	lda LAST_LINE_LO
	cmp #$20
	bcc +
	inc IS_PAL
+	rts

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

DrawScreen:
	+print SCREEN_RAM + 1 * 40, Text0
	+print SCREEN_RAM + 3 * 40, Text1
	+print SCREEN_RAM + 4 * 40, Text2
	+print SCREEN_RAM + 5 * 40, Text3
	+print SCREEN_RAM + 7 * 40, Text4
	+print SCREEN_RAM + 8 * 40, Text5
	+print SCREEN_RAM + 10 * 40, Text6
	+print SCREEN_RAM + 11 * 40, Text7
	+print SCREEN_RAM + 12 * 40, Text8
	+print SCREEN_RAM + 14 * 40, Text9
	+print SCREEN_RAM + 15 * 40, Text10
	+print SCREEN_RAM + 16 * 40, Text11
	rts

; Wait for the raster line in TARGET_LINE and return at the same cycle offset into that line on
; every frame. Uses A; leaves X and Y alone so a band can keep its colours in them.
SyncToLine:
	+poll_and_read_clock            ; poll exits 0-6 cycles after the line began
	sta FILLER                      ; 3   this frame's clock value
	lda CALIB                       ; 3
	sec                             ; 2
	sbc FILLER                      ; 3   reference minus this frame: negative if this frame is late
	clc                             ; 2
	adc #SLIDE_CENTER               ; 2   later frame => larger entry => shorter delay
	and #15                         ; 2   a jump into the slide even if the poll ever missed a frame
	sta SyncSlideJmp + 1            ; 4
SyncSlideJmp:
	jmp SyncSlide                   ; 3
	!align 255, 0
SyncSlide:
	; Entered at offset k (0-17) this takes 19 - k cycles: pairs of $C9 are CMP #$C9 (2 cycles);
	; the tail is either CMP $EA (3 cycles) or CMP #$C5 + NOP (4 cycles) depending on parity.
	!byte $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c9, $c5, $ea
	rts

; Raster interrupt: draw both bands, then leave the border black for the rest of the frame.
Irq:
	; 4-cycle band: three colours held in A, X and Y. The model is selected through A only, so
	; the two colours in X and Y survive into the band.
	lda #BAND_A_LINE
	sta TARGET_LINE
	ldx #2                          ; red
	ldy #14                         ; light blue
	jsr SyncToLine
	lda IS_PAL
	beq +
	lda #COLOR_WHITE
	jmp PalBandA
+	lda #COLOR_WHITE
	jmp NtscBandA
BandADone:
	lda #COLOR_BLACK                ; blank the two lines between the bands
	sta SCREEN_BORDER_COLOR_ADDRESS

	; 6-cycle band: a colour loaded per column.
	lda #BAND_B_LINE
	sta TARGET_LINE
	jsr SyncToLine
	lda IS_PAL
	beq +
	jmp PalBandB
+	jmp NtscBandB
BandBDone:

	lda #COLOR_BLACK
	sta SCREEN_BORDER_COLOR_ADDRESS
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

	+band_a CYCLES_PER_LINE_PAL - BAND_A_COLUMNS * 4, BAND_A_LINES, ~PalBandA
	jmp BandADone
	+band_a CYCLES_PER_LINE_NTSC - BAND_A_COLUMNS * 4, BAND_A_LINES, ~NtscBandA
	jmp BandADone
	+band_b CYCLES_PER_LINE_PAL - BAND_B_COLUMNS * 6, BAND_B_LINES, ~PalBandB
	jmp BandBDone
	+band_b CYCLES_PER_LINE_NTSC - BAND_B_COLUMNS * 6, BAND_B_LINES, ~NtscBandB
	jmp BandBDone

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "vic-ii colour columns", $ff
Text1:	!scr "the border colour takes effect at the", $ff
Text2:	!scr "cycle it is written, 8 pixels wide. how", $ff
Text3:	!scr "close the changes get is up to the cpu.", $ff
Text4:	!scr "upper band: sta/stx/sty $d020 in a row,", $ff
Text5:	!scr "4 cycles = 32 pixels, three colours.", $ff
Text6:	!scr "lower band: lda #col then sta $d020,", $ff
Text7:	!scr "6 cycles = 48 pixels, any colour.", $ff
Text8:	!scr "one column per line is wider: a line", $ff
Text9:	!scr "is not a whole number of columns.", $ff
Text10:	!scr "in the border: the vic-ii steals every", $ff
Text11:	!scr "8th line's middle while display is on.", $ff
