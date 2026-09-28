;ACME assembler
;!to "./row_latch.prg"

; VIC-II character row latch test.
;
; The VIC-II reads a character row's 40 screen codes and colour RAM nibbles once,
; during the row's first raster line (the "bad line"), and reuses them for the
; remaining seven lines. A program that writes screen RAM or colour RAM while the
; raster is inside the row does not change what is displayed until the next frame.
;
; This program writes to rows the raster is currently drawing and restores them in
; the bottom border. On real hardware, and in an emulator that latches the row,
; demo rows 1-3 look solid. A renderer that re-reads memory on every line shows
; row 1 cut in two, row 2 in two colours and row 3 as stripes of different glyphs.
; Demo row 4 changes the background colour register instead; that is not latched,
; so it splits mid-row everywhere.
;
; Works on PAL and NTSC (all raster lines used are inside the 51-250 screen area
; or the bottom border at 251).

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

SCREEN_RAM      = $0400
COLOR_RAM       = $d800

;Bit 8 (highest bit) of the current video scan line is stored in bit #7 in this register
SCREEN_CONTROL_REGISTER_1 = $d011
;Bits 0-7 the current video scan line bit
SCREEN_RASTER_LINE = $d012
;Border color address
SCREEN_BORDER_COLOR_ADDRESS = $d020
;Bg color address for entire screen
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021

; Screen rows used by the demo. With the default vertical scroll (3), character
; row R starts on raster line 51 + 8*R.
DEMO1_ROW = 6           ; screen codes cleared on line 3 of the row
DEMO2_ROW = 9           ; colour RAM set to red on line 3 of the row
DEMO3_ROW = 12          ; screen codes a-g written on lines 1-7 of the row
DEMO4_ROW = 15          ; background colour changed on line 3 of the row

DEMO1_SCREEN = SCREEN_RAM + DEMO1_ROW * 40
DEMO1_COLOR  = COLOR_RAM  + DEMO1_ROW * 40
DEMO2_SCREEN = SCREEN_RAM + DEMO2_ROW * 40
DEMO2_COLOR  = COLOR_RAM  + DEMO2_ROW * 40
DEMO3_SCREEN = SCREEN_RAM + DEMO3_ROW * 40
DEMO3_COLOR  = COLOR_RAM  + DEMO3_ROW * 40

DEMO1_LINE = 51 + DEMO1_ROW * 8         ; first raster line of each demo row
DEMO2_LINE = 51 + DEMO2_ROW * 8
DEMO3_LINE = 51 + DEMO3_ROW * 8
DEMO4_LINE = 51 + DEMO4_ROW * 8

IRQ_LINE     = DEMO1_LINE + 1           ; handler starts inside demo row 1
RESTORE_LINE = 251                      ; first line of the bottom border

DEMO3_COLUMNS = 12      ; 12 unrolled STA (48 cycles) fit inside one raster line

BLOCK_CHAR      = $a0   ; reverse space = solid block
SPACE_CHAR      = $20

COLOR_BLACK      = 0
COLOR_WHITE      = 1
COLOR_RED        = 2
COLOR_BLUE       = 6
COLOR_YELLOW     = 7
COLOR_DARK_GREY  = 11
COLOR_LIGHT_GREEN = 13

;------------------------------------------------------------
;Macros
;------------------------------------------------------------

; Busy-wait until the raster reaches .line (0-255).
!macro wait_line .line {
	lda #.line
-	cmp SCREEN_RASTER_LINE
	bne -
}

; Store .value to .count consecutive bytes from .addr, unrolled (4 cycles per byte).
!macro store_unrolled .addr, .value, .count {
	lda #.value
	!for .i, 0, .count - 1 {
		sta .addr + .i
	}
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

	lda #COLOR_DARK_GREY
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #COLOR_BLACK
	sta SCREEN_BACKGROUND_COLOR_ADDRESS

	jsr ClearScreen
	jsr DrawScreen

	; Raster IRQ at the first demo row
	lda #IRQ_LINE
	sta SCREEN_RASTER_LINE
	lda #<Irq
	sta $0314
	lda #>Irq
	sta $0315

	lda #%00000001       ; enable raster interrupt signals from VIC
	sta $d01a

	cli                  ; clear interrupt flag, allowing the CPU to respond to interrupt requests
	jmp *

; Fill screen RAM with spaces and colour RAM with white.
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

; Explanation text and the resting state of the demo rows.
DrawScreen:
	+print SCREEN_RAM + 0 * 40, Text0
	+print SCREEN_RAM + 1 * 40, Text1
	+print SCREEN_RAM + 2 * 40, Text2
	+print SCREEN_RAM + 3 * 40, Text3
	+print SCREEN_RAM + (DEMO1_ROW - 1) * 40, Text_Demo1
	+print SCREEN_RAM + (DEMO2_ROW - 1) * 40, Text_Demo2
	+print SCREEN_RAM + (DEMO3_ROW - 1) * 40, Text_Demo3
	+print SCREEN_RAM + (DEMO4_ROW - 1) * 40, Text_Demo4
	+print SCREEN_RAM + 17 * 40, Text_Result0
	+print SCREEN_RAM + 18 * 40, Text_Result1
	+print SCREEN_RAM + 19 * 40, Text_Result2
	+print SCREEN_RAM + 20 * 40, Text_Result3

	+store_unrolled DEMO1_SCREEN, BLOCK_CHAR, 40
	+store_unrolled DEMO1_COLOR, COLOR_LIGHT_GREEN, 40
	+store_unrolled DEMO2_SCREEN, BLOCK_CHAR, 40
	+store_unrolled DEMO2_COLOR, COLOR_WHITE, 40
	+store_unrolled DEMO3_SCREEN, BLOCK_CHAR, DEMO3_COLUMNS
	+store_unrolled DEMO3_COLOR, COLOR_YELLOW, DEMO3_COLUMNS
	rts

; Raster interrupt: runs from demo row 1 down to the bottom border, doing all
; timed writes with busy-waits on the raster line register, then restores the
; demo rows so every frame starts from the same screen contents.
Irq:
	; Demo 1: clear the row's screen codes on its 3rd line (the VIC-II has already
	; latched the codes on line 0, so the row should stay solid).
	+wait_line DEMO1_LINE + 3
	+store_unrolled DEMO1_SCREEN, SPACE_CHAR, 40

	; Demo 2: change the row's colour RAM on its 3rd line.
	+wait_line DEMO2_LINE + 3
	+store_unrolled DEMO2_COLOR, COLOR_RED, 40

	; Demo 3: write a different screen code on each of the row's lines 1-7.
	+wait_line DEMO3_LINE + 1
	+store_unrolled DEMO3_SCREEN, 1, DEMO3_COLUMNS    ; a
	+wait_line DEMO3_LINE + 2
	+store_unrolled DEMO3_SCREEN, 2, DEMO3_COLUMNS    ; b
	+wait_line DEMO3_LINE + 3
	+store_unrolled DEMO3_SCREEN, 3, DEMO3_COLUMNS    ; c
	+wait_line DEMO3_LINE + 4
	+store_unrolled DEMO3_SCREEN, 4, DEMO3_COLUMNS    ; d
	+wait_line DEMO3_LINE + 5
	+store_unrolled DEMO3_SCREEN, 5, DEMO3_COLUMNS    ; e
	+wait_line DEMO3_LINE + 6
	+store_unrolled DEMO3_SCREEN, 6, DEMO3_COLUMNS    ; f
	+wait_line DEMO3_LINE + 7
	+store_unrolled DEMO3_SCREEN, 7, DEMO3_COLUMNS    ; g

	; Demo 4: the background colour register is not latched per row, so this
	; change is visible from the line it is written on.
	+wait_line DEMO4_LINE + 3
	lda #COLOR_BLUE
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	+wait_line DEMO4_LINE + 8
	lda #COLOR_BLACK
	sta SCREEN_BACKGROUND_COLOR_ADDRESS

	; Restore the demo rows in the bottom border, before the next frame fetches them.
	+wait_line RESTORE_LINE
	+store_unrolled DEMO1_SCREEN, BLOCK_CHAR, 40
	+store_unrolled DEMO2_COLOR, COLOR_WHITE, 40
	+store_unrolled DEMO3_SCREEN, BLOCK_CHAR, DEMO3_COLUMNS

	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

;------------------------------------------------------------
;Text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

Text0:		!scr "vic-ii character row latch test", $ff
Text1:		!scr "the vic-ii reads a row's screen codes", $ff
Text2:		!scr "and colours on the row's first line.", $ff
Text3:		!scr "later writes show on the next frame.", $ff

Text_Demo1:	!scr "1. codes cleared on line 3 of the row:", $ff
Text_Demo2:	!scr "2. colour ram set red on line 3:", $ff
Text_Demo3:	!scr "3. codes a-g written on lines 1-7:", $ff
Text_Demo4:	!scr "4. background colour blue on line 3:", $ff

Text_Result0:	!scr "real hardware and a row-latching", $ff
Text_Result1:	!scr "renderer: rows 1-3 solid, row 4 split.", $ff
Text_Result2:	!scr "a renderer reading memory every line", $ff
Text_Result3:	!scr "splits rows 1-3 too.", $ff
