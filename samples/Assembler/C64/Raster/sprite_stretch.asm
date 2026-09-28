;ACME assembler
;!to "./sprite_stretch.prg"

; Stretching a sprite with the VIC-II's expansion flip-flop.
;
; A sprite's rows come from its data counter MC, reloaded every line from MCBASE in cycle 58.
; MCBASE advances by three in cycles 15 and 16 of a line only if the sprite's expansion flip-flop
; is set. The flip-flop is set as long as the Y-expand bit in $d017 is cleared, and inverted in
; cycle 55 of every line while the bit is set: that is how Y expansion shows each row twice (VIC-II
; article, section 3.8.1).
;
; So a program decides, line by line, whether the next line shows the next row or the same one
; again: clear the bit after cycle 16 (the flip-flop is set at once) and, to hold the row, set it
; again before cycle 55 (the inversion clears the flip-flop, MCBASE stands still on the next line).
; Leave it cleared and the next line moves on. Done on every line, a row can be held for as long as
; wanted: the sprite stretcher of the demos, one 21-row shape drawn as tall as the screen.
;
; The writes have to land in cycles 17-54, so the sprite is shown in a band without bad lines
; (YSCROLL written every line so no line matches, the FLD trick): the band shows the idle graphics,
; blank here, and pushes the character rows below it down. A wave of repeat counts runs down the
; diamond on each press of the space bar; between passes every row is shown once.

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

SCREEN_RAM      = $0400
COLOR_RAM       = $d800
SPRITE_DATA     = $3000         ; sprite pointer $c0
SPRITE_POINTER  = $07f8

SCREEN_CONTROL_REGISTER_1 = $d011
SCREEN_RASTER_LINE = $d012
SPRITE_Y_EXPAND = $d017
SCREEN_BORDER_COLOR_ADDRESS = $d020
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021
CIA1_PORT_A = $dc00
CIA1_PORT_B = $dc01
SPACE_ROW_SELECT = %01111111    ; the space bar is row 7, column 4
SPACE_COLUMN_BIT = %00010000

D011_BASE = %00011000           ; DEN on, 25 rows; low three bits carry YSCROLL
BAND_START = 91                 ; row 5's first line: the band begins where its bad line would be
BAND_LINES = 120                ; the band: lines 91-210, 15 rows' worth; rows 5-9 follow at 211
TAB_LEN = BAND_LINES + 1        ; one entry per line from 90 (the line before the band) to 210
SPRITE_Y = BAND_START - 1       ; the compare on this line starts the sprite on the band's first line
SPRITE_X = 172                  ; centred
IRQ_LINE = 86
WAVE_STEPS = 64
SPRITE_ROWS = 21

ROW          = $02      ; zero page: BuildTab's row
COUNT        = $03      ; zero page: BuildTab's lines left for the row
CUR          = $04      ; zero page: the raster line the handler is on
PHASE        = $05      ; zero page: the wave's position
FRAME        = $06      ; zero page: frame counter of the pass
RUNNING      = $07      ; zero page: 1 while a pass runs
BAND_DONE    = $08      ; zero page: set by the interrupt when the band is over
COLOR_BLUE = 6
COLOR_LIGHT_BLUE = 14
COLOR_WHITE = 1
COLOR_YELLOW = 7
SPACE_CHAR = $20

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
	jsr SetupSprite
	jsr BuildD011Tab
	lda #0
	sta RUNNING
	sta BAND_DONE

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

; Between passes every row is shown once. A press of the space bar runs the wave down the sprite:
; the repeat table is rebuilt after every band, the wave advances every other frame.
Main:
	jsr BuildTab
	jsr WaitSpace
	lda #1
	sta RUNNING
	lda #0
	sta PHASE
	sta FRAME
Pass:
-	lda BAND_DONE
	beq -
	lda #0
	sta BAND_DONE
	inc FRAME
	lda FRAME
	and #1
	bne +
	inc PHASE
+	lda PHASE
	cmp #WAVE_STEPS
	bcc +
	lda #0
	sta RUNNING
	jmp Main
+	jsr BuildTab
	jmp Pass

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

DrawScreen:
	+print SCREEN_RAM + 0 * 40, Text0
	+print SCREEN_RAM + 1 * 40, Text1
	+print SCREEN_RAM + 2 * 40, Text2
	+print SCREEN_RAM + 3 * 40, Text3
	+print SCREEN_RAM + 5 * 40, Text5
	+print SCREEN_RAM + 6 * 40, Text6
	+print SCREEN_RAM + 7 * 40, Text7
	+print SCREEN_RAM + 9 * 40, Text9
	rts

; Sprite 0: the diamond, unexpanded, at the top of the band.
SetupSprite:
	ldx #0
-	lda SpriteShape,x
	sta SPRITE_DATA,x
	inx
	cpx #63
	bne -
	lda #SPRITE_DATA / 64
	sta SPRITE_POINTER
	lda #SPRITE_X
	sta $d000
	lda #SPRITE_Y
	sta $d001
	lda #0
	sta $d010            ; X high bits
	sta SPRITE_Y_EXPAND
	sta $d01b            ; in front of the graphics
	sta $d01c            ; single colour
	sta $d01d            ; not X-expanded
	lda #COLOR_YELLOW
	sta $d027
	lda #1
	sta $d015
	rts

; The $d011 value written on line 90 + x for line 91 + x: two ahead of the line's own low bits, so
; neither that line nor the one it is written on is a bad line. The last entry, for the line after
; the band, is the normal YSCROLL 3: that line is a row boundary and the rows resume there.
BuildD011Tab:
	ldx #0
-	txa
	clc
	adc #BAND_START + 2
	and #7
	ora #D011_BASE
	sta D011Tab,x
	inx
	cpx #BAND_LINES
	bne -
	lda #D011_BASE + 3
	sta D011Tab + BAND_LINES
	rts

; Tab[x] tells the handler, on line 90 + x, whether line 91 + x moves on to the next row (1) or
; shows the same row again (0). Each row r takes 1 + Wave[(PHASE - 2r) & 63] lines while a pass
; runs, one line otherwise; the entries past the sprite's last row move on.
BuildTab:
	ldx #0
	lda #0
	sta ROW
RowLoop:
	lda #1
	ldy RUNNING
	beq +
	lda ROW
	asl
	sta COUNT            ; 2r, borrowed
	lda PHASE
	sec
	sbc COUNT
	and #WAVE_STEPS - 1
	tay
	lda Wave,y
	clc
	adc #1
+	sta COUNT
LineLoop:
	dec COUNT
	lda COUNT
	beq Last
	lda #0               ; more lines of this row to come: the next one repeats it
	beq Store
Last:
	lda #1               ; the row's last line: the next one moves on
Store:
	sta Tab,x
	inx
	lda COUNT
	bne LineLoop
	inc ROW
	lda ROW
	cmp #SPRITE_ROWS
	bne RowLoop
-	cpx #TAB_LEN
	bcs +
	lda #1
	sta Tab,x
	inx
	bne -
+	rts

;------------------------------------------------------------
;Raster interrupt: the band, line by line
;------------------------------------------------------------

; From line 90 (the line before the band, where the sprite's compare is made) to line 210, one
; pass per line: write the next line's YSCROLL, clear the Y-expand bit (after cycle 16: the line
; change is seen within seven cycles and this store comes about twenty cycles after that), set it
; again if the next line is to repeat the row (before cycle 55), then wait for the line to end.
Irq:
	lda #SPRITE_Y
-	cmp SCREEN_RASTER_LINE
	bne -
	sta CUR
	ldx #0
LineLoop2:
	lda D011Tab,x
	sta SCREEN_CONTROL_REGISTER_1
	lda #0
	sta SPRITE_Y_EXPAND  ; the flip-flop is set
	lda Tab,x
	bne +
	lda #1
	sta SPRITE_Y_EXPAND  ; cycle 55 clears it again: the next line holds the row
+	inc CUR
	lda CUR
-	cmp SCREEN_RASTER_LINE
	bne -
	inx
	cpx #TAB_LEN
	bne LineLoop2
	inc BAND_DONE
	asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

;------------------------------------------------------------
;Tables and text (screen codes, $ff terminated; lowercase source shows as uppercase)
;------------------------------------------------------------

; Extra lines per row as the wave passes: a bump 23 steps wide, 0 elsewhere.
Wave:
	!byte 0, 1, 1, 2, 2, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4
	!byte 4, 4, 3, 3, 2, 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0
	!byte 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
	!byte 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0

Tab:
	!fill TAB_LEN, 1
D011Tab:
	!fill TAB_LEN, 0

; 24 x 21 diamond, solid on even rows and outlined on odd ones, so stretched rows show as bands.
SpriteShape:
	!byte $00, $3c, $00
	!byte $00, $66, $00
	!byte $00, $ff, $00
	!byte $01, $81, $80
	!byte $03, $ff, $c0
	!byte $06, $00, $60
	!byte $0f, $ff, $f0
	!byte $18, $00, $18
	!byte $3f, $ff, $fc
	!byte $60, $00, $06
	!byte $ff, $ff, $ff
	!byte $60, $00, $06
	!byte $3f, $ff, $fc
	!byte $18, $00, $18
	!byte $0f, $ff, $f0
	!byte $06, $00, $60
	!byte $03, $ff, $c0
	!byte $01, $81, $80
	!byte $00, $ff, $00
	!byte $00, $66, $00
	!byte $00, $3c, $00

Text0:	!scr "sprite stretch: the expansion flip-flop", $ff
Text1:	!scr "decides each line whether the next one", $ff
Text2:	!scr "shows the next sprite row or the same.", $ff
Text3:	!scr "clear $d017 after cycle 16, set it again", $ff
Text5:	!scr "before cycle 55 and the row is held.", $ff
Text6:	!scr "the band has no bad lines (fld), so the", $ff
Text7:	!scr "writes fit; rows below are pushed down.", $ff
Text9:	!scr "press space for one pass of the wave.", $ff
