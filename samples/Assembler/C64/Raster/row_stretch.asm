;ACME assembler
;!to "./row_stretch.prg"

; Stretching a character logo with the VIC-II's row counter.
;
; A character row is eight raster lines: on its first line, the bad line, the VIC-II fetches the
; row's screen codes and resets its row counter RC, and RC then selects the line of each character
; shape on the seven lines that follow. When RC has reached 7, the row is over: in cycle 58 of that
; line the video counter base VCBASE moves on by 40, and the next bad line fetches the next row
; (VIC-II article, section 3.7.2).
;
; The bad line condition is only "raster line $30-$F7 whose low three bits equal YSCROLL", in any
; cycle. Make it true before cycle 14 of a row's last line, by writing YSCROLL, and RC is reset
; there instead of reaching 7: VCBASE has not moved, the fetch reads the same row again, and the
; row starts over. Do that as often as wanted and the row is shown as many times: seven lines per
; repeat (its eighth line is the one that restarts it) and eight for the last. For a logo made of
; solid block characters the repeats join seamlessly, and each row can be stretched by its own
; factor. The rows below just move down.
;
; Nothing here needs an exact cycle. One write per bad line is enough: right after the row has
; been fetched, YSCROLL is set for the next bad line, seven or eight lines on, and that value can
; match none of the lines in between. The interrupt handler follows the bad lines down the display.
;
; The program waits for the space bar, runs one pass of the wave (64 frames, from all rows plain
; back to all rows plain), and waits again.

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
CIA1_PORT_A = $dc00
CIA1_PORT_B = $dc01
SPACE_ROW_SELECT = %01111111    ; the space bar is row 7, column 4
SPACE_COLUMN_BIT = %00010000

D011_BASE = %00011000           ; DEN on, 25 rows; low three bits carry YSCROLL

FIRST_ROW_LINE = 51             ; row 0's bad line with YSCROLL 3
LAST_BAD_LINE = $f7             ; no line after this can be a bad line
LOGO_ROW = 1                    ; the logo's first screen row
LOGO_ROWS = 5                   ; a logo row is shown 1-3 times: 8 to 22 lines
IRQ_LINE = 44
WAVE_STEPS = 64                 ; the wave table's length: one pass
WAVE_START = 68                 ; the frame count where every logo row is at a plain step of the wave

FRAME        = $02      ; zero page: frame counter of the pass
RUNNING      = $06      ; zero page: 1 while a pass runs, set by the main loop, cleared by the interrupt
NEXT_BAD     = $03      ; zero page: the next line that is to be a bad line
ROW          = $04      ; zero page: the row that line starts, or restarts
REPEATS_LEFT = $05      ; zero page: restarts still to come for the row being shown
BLOCK_CHAR = $a0                ; reverse space: a solid cell
SPACE_CHAR = $20
COLOR_BLUE = 6
COLOR_LIGHT_BLUE = 14
COLOR_WHITE = 1

;------------------------------------------------------------
;Macros
;------------------------------------------------------------

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

	lda #COLOR_LIGHT_BLUE
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #COLOR_BLUE
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	lda #D011_BASE + 3
	sta SCREEN_CONTROL_REGISTER_1

	jsr ClearScreen
	jsr DrawScreen
	lda #0
	sta RUNNING

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

; Each press of the space bar starts one pass of the wave; the interrupt handler runs it and clears
; RUNNING when the pass is over.
Main:
	jsr WaitSpace
	lda #WAVE_START
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

; The logo: five rows of solid cells drawn from the pattern below (# is a cell), one colour per
; row, then the explanation.
DrawScreen:
	ldx #0
-	lda LogoPattern,x
	cmp #'#'
	bne +
	lda #BLOCK_CHAR
	!byte $2c                       ; BIT abs: skips the LDA below
+	lda #SPACE_CHAR
	sta SCREEN_RAM + LOGO_ROW * 40,x
	inx
	cpx #LOGO_ROWS * 40
	bne -
	ldx #0
	ldy #0                          ; Y: the logo row, X: the cell
-	lda LogoColours,y
	sta COLOR_RAM + LOGO_ROW * 40,x
	inx
	txa
	cmp RowEnds,y                   ; the first cell of the next row
	bne -
	iny
	cpy #LOGO_ROWS
	bne -
	+print SCREEN_RAM + 7 * 40, Text7
	+print SCREEN_RAM + 8 * 40, Text8
	+print SCREEN_RAM + 9 * 40, Text9
	+print SCREEN_RAM + 10 * 40, Text10
	+print SCREEN_RAM + 12 * 40, Text12
	+print SCREEN_RAM + 13 * 40, Text13
	+print SCREEN_RAM + 14 * 40, Text14
	+print SCREEN_RAM + 15 * 40, Text15
	+print SCREEN_RAM + 17 * 40, Text17
	rts

;------------------------------------------------------------
;Raster interrupt: the bad lines from the top of the display down
;------------------------------------------------------------

; The handler waits for each bad line to begin. The VIC-II halts the CPU from cycle 12 of it to
; cycle 55 while it fetches the row, so the code after the wait runs, and writes, once that is
; over: the value for the next bad line, seven lines on for a restart (its low bits then match
; none of the six lines in between) or eight for the next row (matching none of the seven).
Irq:
	lda RUNNING
	beq Plain
	lda FRAME
	cmp #WAVE_START + WAVE_STEPS    ; the pass is over: back to plain rows
	bne Pass
	lda #0
	sta RUNNING
Plain:
	lda #D011_BASE + 3              ; no writes: every row eight lines
	sta SCREEN_CONTROL_REGISTER_1
	jmp IrqDone
Pass:
	lda #D011_BASE + 3              ; row 0 starts on line 51
	sta SCREEN_CONTROL_REGISTER_1
	lda #0
	sta ROW
	jsr RowRepeats
	sta REPEATS_LEFT
	ldx #FIRST_ROW_LINE
NextBadLine:
-	cpx SCREEN_RASTER_LINE          ; wait for the bad line to begin
	bne -
	jsr RowRestartOrNext            ; reads while the VIC-II fetches the row: resumes after cycle 55
	lda NEXT_BAD
	and #7
	ora #D011_BASE
	sta SCREEN_CONTROL_REGISTER_1
	ldx NEXT_BAD
	cpx #LAST_BAD_LINE + 1
	bcc NextBadLine
	inc FRAME
IrqDone:
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

; A bad line at the line in X: a restart of the current row seven lines on if it has repeats left,
; else the next row eight lines on. Uses A and Y.
RowRestartOrNext:
	lda REPEATS_LEFT
	beq +
	dec REPEATS_LEFT
	txa
	clc
	adc #7
	sta NEXT_BAD
	rts
+	txa
	clc
	adc #8
	sta NEXT_BAD
	inc ROW
	jsr RowRepeats
	sta REPEATS_LEFT
	rts

; The restarts the row in ROW gets this frame: a logo row follows the wave, by its position and
; the frame; any other row none. The pass starts and ends where all rows are at a plain step
; (WAVE_START is the first count where that holds for the five phases). Returns A = restarts (0-3). Uses Y.
RowRepeats:
	lda ROW
	cmp #LOGO_ROW
	bcc +
	cmp #LOGO_ROW + LOGO_ROWS
	bcs +
	sec
	sbc #LOGO_ROW
	tay
	lda RowPhase,y                  ; the wave runs down the logo
	clc
	adc FRAME                       ; and advances one step per frame
	and #63
	tay
	lda Wave,y
	rts
+	lda #0
	rts

;------------------------------------------------------------
;Tables and text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

; Restarts per wave step, 64 steps round.
Wave:
	!byte 0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2
	!byte 2,2,2,2,2,2,2,2,1,1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0

LogoPattern:
	!text "    ####    ####     ###     ####       "
	!text "    #       #       #   #       #       "
	!text "    ####    ####    #   #    ####       "
	!text "    #   #      #    #   #    #          "
	!text "    ####    ####     ###     ####       "

LogoColours:
	!byte 7, 13, 3, 14, 4

RowPhase:
	!byte 0, 59, 54, 49, 44         ; each row five steps behind the one above

RowEnds:
	!byte 40, 80, 120, 160, 200

Text7:	!scr "each logo row is one character row,", $ff
Text8:	!scr "shown as many times as the wave says:", $ff
Text9:	!scr "a bad line condition before cycle 14 of", $ff
Text10:	!scr "its last line resets the row counter and", $ff
Text12:	!scr "fetches the same row again, since the", $ff
Text13:	!scr "row pointer has not moved on. seven", $ff
Text14:	!scr "lines per repeat, no exact timing needed,", $ff
Text15:	!scr "and the rows below simply move down.", $ff
Text17:	!scr "press space for one pass of the wave.", $ff
