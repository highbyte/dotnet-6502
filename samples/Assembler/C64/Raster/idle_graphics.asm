;ACME assembler
;!to "./idle_graphics.prg"

; VIC-II display enable (DEN) test: the same cycle-counted colour loop with the display on and off.
;
; DEN is bit 4 of $d011. The VIC-II does not read it continuously: it samples it during raster
; line 48 ($30), and what it saw there decides for the whole frame whether any line can be a bad
; line, that is whether character rows are fetched and displayed at all. Separately, DEN gates the
; vertical border: the border flip-flop opens at line 51 only if DEN is set at that moment.
;
; With the display on, every 8th line of the display area is a bad line: the VIC-II takes the bus
; for about 40 cycles in the middle of it and the CPU stops at its next opcode fetch. Nothing a
; program does inside those lines can avoid that. With DEN clear during line 48 there are no bad
; lines, the CPU owns every cycle of the frame, and cycle-counted code runs straight through the
; display area. That is what this program shows, by running the same loop in every phase:
;
;   200 iterations of exactly one raster line (63 cycles on PAL, 65 on NTSC; the model is detected
;   at start), each writing eight background colours 6 cycles apart, so the display area shows
;   48-pixel vertical colour columns. The loop is entered from a line-clock sync (see
;   raster_columns.asm), so the columns land on the same cycle every frame.
;
; Four phases. The first stays until the space bar is pressed; the other three follow at about
; two seconds each, after which the first waits for space again:
;
;   1. DEN set all frame: the text screen over the columns. Every bad line halts the loop for
;      about 40 cycles, so from there on the colour writes are 40 cycles late and the columns
;      shear sideways, 25 times down the screen.
;   2. DEN cleared on line 40 and set again a few cycles into line 48: the same picture, because a
;      write during line 48 counts and the frame has its bad lines.
;   3. DEN cleared until line 49: the border opens at 51, but the frame has no bad lines, so the
;      columns are straight. No character row is fetched either: the display area shows "idle
;      graphics", the byte at $3fff in every cell, black over the background colour. The loop
;      writes one row of an 8x8 glyph to $3fff on every line, so all 1000 cells show the same
;      glyph without any screen RAM or character set being read.
;   4. DEN cleared all frame: the border never opens, so the loop writes the border colour instead
;      and the columns run edge to edge.
;
; Phases 3 and 4 are why loaders and demo parts switch the display off: the ~1000 cycles per
; frame of bad lines come back. An emulator that reads DEN live shows sheared columns and the
; text screen in phase 3, with its status line saying "3".
;
; Timing inside the loop: the idle byte is written after the previous line's display window and
; the first colour before the next line's visible part begins, so both the idle graphics and the
; phase 4 border columns have straight top and left edges.

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

SCREEN_RAM      = $0400
COLOR_RAM       = $d800

SCREEN_CONTROL_REGISTER_1 = $d011
SCREEN_RASTER_LINE = $d012
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021

CIA1_TIMER_A_LO = $dc04
CIA1_TIMER_A_HI = $dc05
CIA1_CONTROL_A = $dc0e

IDLE_BYTE_ADDRESS = $3fff       ; the byte the VIC-II displays in idle state

; $d011 values: raster bit 8 clear, 25 rows, YSCROLL 3; with DEN on and off.
D011_DEN_ON  = %00011011
D011_DEN_OFF = %00001011

IRQ_LINE          = 40          ; top border on both models; well before line 48
DEN_SAMPLE_LINE   = 48          ; the line during which the VIC-II samples DEN
SYNC_LINE         = 50          ; the loop is entered from this line, one above the display area
FIRST_SCREEN_LINE = 51
SCREEN_LINES      = 200

CYCLES_PER_LINE_PAL = 63
CYCLES_PER_LINE_NTSC = 65
LOOP_CYCLES = 63                ; one iteration of the column loop without filler

PHASES       = 4
PHASE_FRAMES = 100              ; frames per phase 2-4 (2 s PAL, 1.7 s NTSC)
STATUS_ROW   = 24

CIA1_PORT_A = $dc00             ; keyboard matrix row select
CIA1_PORT_B = $dc01             ; keyboard matrix column read
SPACE_ROW_SELECT = %01111111    ; the space bar is row 7, column 4
SPACE_COLUMN_BIT = %00010000

FILLER       = $02      ; zero page: scratch, also the byte the timing filler reads
START        = $03      ; zero page: set by the main loop when space starts the cycle
ENTRY        = $f7      ; zero page, 2 bytes: the column loop to run this frame
PHASE        = $f9      ; zero page: current phase 0-3
FRAME        = $fa      ; zero page: frames spent in the current phase
IS_PAL       = $fb      ; zero page: 1 on PAL, 0 on NTSC
LAST_LINE_LO = $fc      ; zero page: scratch for the model detection
CALIB        = $fd      ; zero page: line clock value of the reference poll (see SyncToLine)
TARGET_LINE  = $fe      ; zero page: raster line SyncToLine waits for

; Slide entry for a frame exactly as early as the reference frame. The poll loop is 7 cycles, so a
; frame differs from the reference by at most 6 cycles either way and the entry stays within 3-15.
SLIDE_CENTER = 9

COLOR_BLACK     = 0
COLOR_WHITE     = 1
COLOR_DARK_GREY = 11
SPACE_CHAR      = $20

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

; Busy-wait until the raster reaches .line (0-255). Exits 2-8 cycles into the line.
!macro wait_line .line {
	lda #.line
-	cmp SCREEN_RASTER_LINE
	bne -
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

; The column loop: 200 iterations of exactly one raster line. Iteration x runs on line 50 + x and
; prepares line 51 + x: its idle byte (written 60-66 cycles into the line, after the display window
; has ended), then eight colours 6 cycles apart, the first of them 3-9 cycles into the next line,
; before its visible part. The loop's own counter and branch take the last 7 cycles. Afterwards the
; border and background go back to their resting colours, the border write timed to land in the
; unseen pixels after line 250's visible right border.
!macro column_loop .colour_addr, .fill, ~.entry {
	!align 127, 0        ; the loop is under 128 bytes: no page crossing on the branch back
.entry
.Line:
	lda IdleBytes,x                     ; 4
	sta IDLE_BYTE_ADDRESS               ; 4
	lda #COLOR_WHITE                    ; 2
	sta .colour_addr                    ; 4
	lda #7                              ; yellow
	sta .colour_addr
	lda #8                              ; orange
	sta .colour_addr
	lda #10                             ; light red
	sta .colour_addr
	lda #13                             ; light green
	sta .colour_addr
	lda #3                              ; cyan
	sta .colour_addr
	lda #14                             ; light blue
	sta .colour_addr
	lda #15                             ; light grey
	sta .colour_addr
	!if .fill {
		+delay_cycles .fill
	}
	inx                                 ; 2
	cpx #SCREEN_LINES                   ; 2
	bne .Line                           ; 3 taken / 2 falling through
	nop
	nop
	lda #COLOR_DARK_GREY                ; the border write lands 62-68 cycles into line 250
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #COLOR_BLACK
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	jmp ColumnsDone
}

; 8x8 glyphs as idle bytes, one row per raster line; five glyphs, repeated down the screen.
!macro heart {
	!byte %00000000, %01100110, %11111111, %11111111, %01111110, %00111100, %00011000, %00000000
}
!macro smiley {
	!byte %00000000, %00111100, %01000010, %10100101, %10011001, %01000010, %00111100, %00000000
}
!macro diamond {
	!byte %00000000, %00011000, %00111100, %01111110, %01111110, %00111100, %00011000, %00000000
}
!macro arrow {
	!byte %00000000, %00011000, %00111100, %01111110, %00011000, %00011000, %00011000, %00000000
}
!macro cross {
	!byte %00000000, %01000010, %00100100, %00011000, %00011000, %00100100, %01000010, %00000000
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
	lda #COLOR_BLACK
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	sta IDLE_BYTE_ADDRESS
	sta PHASE
	sta FRAME
	sta START

	jsr ClearScreen
	jsr DrawScreen
	jsr PrintStatus

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
	; compared against this, so the loop aligns to this frame whatever the code layout is.
	lda #SYNC_LINE
	sta TARGET_LINE
	+poll_and_read_clock
	sta CALIB

	; Raster IRQ in the top border, before the DEN sample line
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

; Main loop: while phase 1 is showing, a press of the space bar (after it has been released) starts
; the cycle; the interrupt takes it from there and the loop waits for phase 1 to come round again.
Main:
-	lda PHASE
	bne -
	jsr WaitSpace
	lda #1
	sta START
-	lda PHASE
	beq -
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
	; on either model.
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

; Fill screen RAM with spaces and colour RAM with black, so the text reads over the columns.
ClearScreen:
	ldx #0
	lda #SPACE_CHAR
-	sta SCREEN_RAM,x
	sta SCREEN_RAM + $100,x
	sta SCREEN_RAM + $200,x
	sta SCREEN_RAM + $300,x
	inx
	bne -
	lda #COLOR_BLACK
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
	+print SCREEN_RAM + 7 * 40, Text7
	+print SCREEN_RAM + 8 * 40, Text8
	+print SCREEN_RAM + 9 * 40, Text9
	+print SCREEN_RAM + 11 * 40, Text11
	+print SCREEN_RAM + 12 * 40, Text12
	+print SCREEN_RAM + 13 * 40, Text13
	+print SCREEN_RAM + 14 * 40, Text14
	+print SCREEN_RAM + 15 * 40, Text15
	+print SCREEN_RAM + 16 * 40, Text16
	+print SCREEN_RAM + 17 * 40, Text17
	+print SCREEN_RAM + 18 * 40, Text18
	+print SCREEN_RAM + 19 * 40, Text19
	+print SCREEN_RAM + 20 * 40, Text20
	+print SCREEN_RAM + 22 * 40, Text22
	+print SCREEN_RAM + 23 * 40, Text23
	rts

; Status line for the current phase. Only phases 1 and 2 ever show it on hardware: in phase 3
; the text screen is not fetched and in phase 4 the border covers it.
PrintStatus:
	lda PHASE
	cmp #1
	beq PrintStatus2
	cmp #2
	beq PrintStatus3
	cmp #3
	beq PrintStatus4
	+print SCREEN_RAM + STATUS_ROW * 40, Status1
	rts
PrintStatus2:
	+print SCREEN_RAM + STATUS_ROW * 40, Status2
	rts
PrintStatus3:
	+print SCREEN_RAM + STATUS_ROW * 40, Status3
	rts
PrintStatus4:
	+print SCREEN_RAM + STATUS_ROW * 40, Status4
	rts

; Wait for the raster line in TARGET_LINE and return at the same cycle offset into that line on
; every frame. Uses A; leaves X and Y alone.
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

;------------------------------------------------------------
;Raster interrupt, entered in the top border on line 40
;------------------------------------------------------------

Irq:
	lda PHASE
	beq Phase1
	cmp #1
	beq Phase2
	cmp #2
	beq Phase3

	; Phase 4: DEN off for the whole frame; the border never opens, so the columns go to $d020.
	lda #D011_DEN_OFF
	sta SCREEN_CONTROL_REGISTER_1
	ldx #2
	jmp RunColumns

Phase3:
	; DEN off through line 48, on again on line 49: no bad lines, border open, idle graphics.
	lda #D011_DEN_OFF
	sta SCREEN_CONTROL_REGISTER_1
	+wait_line DEN_SAMPLE_LINE + 1
	lda #D011_DEN_ON
	sta SCREEN_CONTROL_REGISTER_1
	ldx #0
	jmp RunColumns

Phase2:
	; DEN off from line 40, set again a few cycles into line 48: the VIC-II sees it during the
	; line, so this frame displays normally, bad lines included.
	lda #D011_DEN_OFF
	sta SCREEN_CONTROL_REGISTER_1
	+wait_line DEN_SAMPLE_LINE
	lda #D011_DEN_ON
	sta SCREEN_CONTROL_REGISTER_1
Phase1:
	ldx #0

; X = 0 for background columns, 2 for border columns. Picks the loop for the model, syncs to
; line 50 and enters it; the loop is written for the return to land a fixed number of cycles
; before the first idle byte write.
RunColumns:
	lda IS_PAL
	beq +
	inx
	inx
	inx
	inx
+	lda EntryTable,x
	sta ENTRY
	lda EntryTable + 1,x
	sta ENTRY + 1
	lda #SYNC_LINE
	sta TARGET_LINE
	jsr SyncToLine                  ; returns 44-50 cycles into line 50
	ldx #0                          ; 2
	jmp (ENTRY)                     ; 5

ColumnsDone:
	lda #D011_DEN_ON                ; DEN on for whichever phase comes next
	sta SCREEN_CONTROL_REGISTER_1

	lda PHASE
	bne Counting
	lda START            ; phase 1 stays until the main loop has seen the space bar
	beq Done
	lda #0
	sta START
	beq Advance
Counting:
	inc FRAME
	lda FRAME
	cmp #PHASE_FRAMES
	bne Done
Advance:
	lda #0
	sta FRAME
	inc PHASE
	lda PHASE
	cmp #PHASES
	bne +
	lda #0
	sta PHASE
+	jsr PrintStatus
Done:
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

EntryTable:
	!word NtscBackgroundColumns, NtscBorderColumns
	!word PalBackgroundColumns, PalBorderColumns

	+column_loop SCREEN_BACKGROUND_COLOR_ADDRESS, CYCLES_PER_LINE_PAL - LOOP_CYCLES, ~PalBackgroundColumns
	+column_loop SCREEN_BORDER_COLOR_ADDRESS, CYCLES_PER_LINE_PAL - LOOP_CYCLES, ~PalBorderColumns
	+column_loop SCREEN_BACKGROUND_COLOR_ADDRESS, CYCLES_PER_LINE_NTSC - LOOP_CYCLES, ~NtscBackgroundColumns
	+column_loop SCREEN_BORDER_COLOR_ADDRESS, CYCLES_PER_LINE_NTSC - LOOP_CYCLES, ~NtscBorderColumns

;------------------------------------------------------------
;Tables
;------------------------------------------------------------

; One idle byte per display line: 25 groups of 8 glyph rows. Page aligned so that the indexed
; read in the loop never crosses a page and costs an extra cycle.
	!align 255, 0
IdleBytes:
	!for .group, 1, 5 {
		+heart
		+smiley
		+diamond
		+arrow
		+cross
	}

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:	!scr "vic-ii display enable (den) test", $ff
Text2:	!scr "den (bit 4 of $d011) is sampled once", $ff
Text3:	!scr "per frame, during raster line 48. it", $ff
Text4:	!scr "decides if the frame has bad lines and", $ff
Text5:	!scr "lets the border open at line 51.", $ff
Text7:	!scr "every phase runs the same cycle-counted", $ff
Text8:	!scr "loop over the 200 screen lines: eight", $ff
Text9:	!scr "background colours per line = columns.", $ff
Text11:	!scr "1. den set all frame: every 8th line is", $ff
Text12:	!scr "   a bad line that halts the cpu for 40", $ff
Text13:	!scr "   cycles, so the columns shear.", $ff
Text14:	!scr "2. den cleared on line 40, set again", $ff
Text15:	!scr "   inside line 48: counts, still shears", $ff
Text16:	!scr "3. den cleared until line 49: no bad", $ff
Text17:	!scr "   lines, straight columns. no rows are", $ff
Text18:	!scr "   fetched: the black glyph is the byte", $ff
Text19:	!scr "   at $3fff, written once per line.", $ff
Text20:	!scr "4. den cleared all frame: border only.", $ff
Text22:	!scr "press space to run phases 2-4 and back.", $ff
Text23:	!scr "phase now:", $ff

Status1:	!scr "1. den set all frame: columns shear   ", $ff
Status2:	!scr "2. den off on line 40, on inside 48   ", $ff
Status3:	!scr "3. den off until line 49: no bad lines", $ff
Status4:	!scr "4. den off all frame: border only     ", $ff
