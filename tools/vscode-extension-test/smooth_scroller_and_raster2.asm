; ca65 assembler

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------
COLOR_CYCLE_EACH_X_FRAME = 2

SCROLLER_ROW = 12
SCROLL_EACH_X_FRAME = 1

RASTER_DEFAULT_BORDER_COLOR = $00
RASTER_DEFAULT_BACKGROUND_COLOR = $00

RASTER_BACKGROUND_DISABLED_START = 59 + (10*8) 	;First raster line of main screen, NTSC & PAL = 51

RASTER_BACKGROUND_DISABLED_END = 251 - 8 - (10*8)	;Last raster line of main screen, NTSC & PAL = 250.

ColorBlack = $00
ColorWhite = $01
ColorRed = $02
ColorCyan = $03
ColorViolet = $04
ColorGreen = $05
ColorBlue = $06
ColorYellow = $07
ColorOrange = $08
ColorBrown = $09
ColorLightRed = $0a
ColorDarkGrey = $0b
ColorGrey = $0c
ColorLightGreen = $0d
ColorLightBlue = $0e
ColorLightGrey = $0f

;------------------------------------------------------------
;Memory address shared with emulator host for updating screen
;------------------------------------------------------------
;40 columns and 25 rows, 1 byte per character = 1000 ($03e8) bytes
;Laid out in memory as appears on screen.
SCREEN_MEM = $0400			;$0400 - $07e7
SCREEN_MEM_COLS = 40
SCREEN_MEM_ROWS = 25
;Colors, one byte per character = 1000 ($03e8) bytes
SCREEN_COLOR_MEM = $d800	;$d800 - $dbe7

;Bit 8 (highest bit) of the current video scan line is stored in bit #7 in this register
SCREEN_CONTROL_REGISTER_1 = $d011
;Bits 0-7 the current video scan line bit
SCREEN_RASTER_LINE = $d012

;Bits 0-2 is the horizontal X scroll value (0-7)
SCROLLX = $d016

;Border color address
SCREEN_BORDER_COLOR_ADDRESS = $d020
;Background color address for main screen
SCREEN_BACKGROUND_COLOR_ADDRESS = $d021

;Check keyboard status
CIA1_DATAB = $dc01

;------------------------------------------------------------
;ZP memory locations used for calculations
;------------------------------------------------------------
;Zero Page address that will hold an 16 bit address (text start in memory + current scroll offset)
;Little endian:
;	$fb will contain least significant byte, that is used in Indirect Indexed addressing mode
;	$fc will contain most significant byte.
ZP_SCROLL_TEXT_ADDRESS = $fb

;Index to where in text color list
ZP_TEXT_COLOR_CYCLE_INDEX = $fd

;Frame counters
ZP_SCROLL_FRAME_COUNT = $fe
ZP_COLOR_CYCLE_FRAME_COUNT = $02

;Index in raster sine table
RASTER_COUNTER = $03

CURRENT_RASTER_LINE = $04
;PREVIOUS_RASTER_LINE = $05
RASTER_BAR_DIRECTION = $06	;Down = 0, Up = 1

XSHIFT = $05

;Macros
.macro set_irq irqhandler, line

	; Set next IRQ raster line
	lda #<(line) ; Bits 0-7 of current raster line
	sta SCREEN_RASTER_LINE	; $d012
	lda #>(line) ; 8th bit of current raster line
	cmp #0
	beq :+
	;Set bit 7 of $d011, which is the 8th bit of the current raster line
	lda SCREEN_CONTROL_REGISTER_1 ; $d011
	ora #128
	sta SCREEN_CONTROL_REGISTER_1 ; $d011
	jmp :++
:	;no_highbit
	;Clear bit 7 of $d011, which is the 8th bit of the current raster line
	lda SCREEN_CONTROL_REGISTER_1 ; $d011
	and #127
	sta SCREEN_CONTROL_REGISTER_1 ; $d011
:	;irq_addr_cont

	; The handler that will be called during the IRQ
	lda #<(irqhandler)
	sta $0314
	lda #>(irqhandler)
	sta $0315
.endmacro

; Macro to convert text to C64 screen codes (equivalent to ACME's !convtab scr + !text)
; Uses character constants instead of numeric literals to work regardless of charmap (ASCII/PETSCII)
.macro scrcode str
    .repeat .strlen(str), i
        .if .strat(str, i) >= 'a' .and .strat(str, i) <= 'z'
            .byte .strat(str, i) - 'a' + 1
        .elseif .strat(str, i) >= 'A' .and .strat(str, i) <= 'Z'
            .byte .strat(str, i) - 'A' + 1
        .else
            .byte .strat(str, i)
        .endif
    .endrepeat
.endmacro

;------------------------------------------------------------------
;Code start
;------------------------------------------------------------------
	sei			;Disable interrupts;

;Clear text screen and set text color
	jsr cleartextscreen

;Initialize scroll text address to start of text.
	jsr initscroll
	jsr initscrollframecount

;Initialize static text and color cycle
	jsr initcolorcycleframecount
	jsr initcolorcycle

; Init IRQ and first IRQ handler
	jsr initirq

; Clear interrupt flag, allowing the CPU to respond to interrupt requests
	cli

mainloop:
	;Raster bars
	jsr rasterbars

	jmp mainloop

;------------------------------------------------------------------
vblank_irq:
;Color cycle (evry frame)
	;Check how often we should scroll (every x frame)
 	dec ZP_COLOR_CYCLE_FRAME_COUNT
 	bne skipcolorcycle
 	jsr colorcycle_statictext
 	jsr initcolorcycleframecount
skipcolorcycle:

;Scroller
	;Check how often we should scroll (every x frame)
 	dec ZP_SCROLL_FRAME_COUNT
 	bne skipscroll
 	jsr scrolltext
 	jsr initscrollframecount
skipscroll:

	ASL $D019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
	;JMP $EA31            ; jump into KERNAL's standard interrupt service routine to handle keyboard scan, cursor display etc.
	JMP $EA81            ; jump into shorter ROM routine to only restore registers from the stack etc

.proc cleartextscreen
	lda #$20
	ldx #0
loop:
	sta SCREEN_MEM,x
	sta SCREEN_MEM + $100,x
	sta SCREEN_MEM + $200,x
	sta SCREEN_MEM + $300,x
	dex
	bne loop
	rts
.endproc

;------------------------------------------------------------------
.proc colorcycle_statictext
	ldx #0
	lda ZP_TEXT_COLOR_CYCLE_INDEX
	tay
loop:
	lda SCROLLER_TEXT_COLOR, Y
	cmp #$ff
	bne notendofcolorlist
	ldy #0
	jmp loop
notendofcolorlist:
	iny
	sta SCREEN_COLOR_MEM + (SCREEN_MEM_COLS * SCROLLER_ROW) , X	; Change color of character. A will contain current color to print, and X the column
	inx
	cpx #SCREEN_MEM_COLS
	bne loop	;Loop until we changed color for entire row of 80 characters

	;Increase color cycle index starting point.
	inc ZP_TEXT_COLOR_CYCLE_INDEX
	lda ZP_TEXT_COLOR_CYCLE_INDEX
	tay
	;Check if we reached end, then reset
	lda SCROLLER_TEXT_COLOR, Y
	cmp #$ff
	bne notendofcolorlist2
	jsr initcolorcycle
notendofcolorlist2:
	rts
.endproc

;------------------------------------------------------------------
initcolorcycle:
	lda #0
	sta ZP_TEXT_COLOR_CYCLE_INDEX
	rts

initcolorcycleframecount:
	;Init framecounter (decrease from number to 0)
	lda #COLOR_CYCLE_EACH_X_FRAME
	sta ZP_COLOR_CYCLE_FRAME_COUNT
	rts

;------------------------------------------------------------------
.proc scrolltext

;Change the smooth scrolling register from 7 to 0 and back again
	dec XSHIFT
	bpl storescrollxreg
	lda #7
	sta XSHIFT
	tax
storescrollxreg:
	lda XSHIFT
	;ora #$08	;Bit 3: 1 = 40 characters per line, 0 = 38 characters per line
	sta SCROLLX	;Update smooth scroll X register

;If we haven't wrapped arounb to scroll pos 7, then skip moving any text
	cpx #7
	beq movecharactersleft
	rts

;Move all characters one position to the left and print new character at end
movecharactersleft:
	ldx #0
	ldy #0
loop:
	lda (ZP_SCROLL_TEXT_ADDRESS), Y
	bne notendofscroll
	jsr initscroll					; Reset scroll pointer to start of text
	ldy #0
	jmp loop
notendofscroll:
	iny
	sta SCREEN_MEM + (SCREEN_MEM_COLS * SCROLLER_ROW) , X				; Print character. A will contain current character to print, and X the column
	inx
	cpx #SCREEN_MEM_COLS
	bne loop						;Loop until we printed 80 characters

	inc ZP_SCROLL_TEXT_ADDRESS		;Increase scroll start pointer lowbyte
	bne nohighbyteincrease			;Check if we reach 00 (wrap around), then Zero flag is set, which means we should also increase highbyte
	inc ZP_SCROLL_TEXT_ADDRESS + 1	;Increase scroll start pointer highbyte if we got carry from lowbyte
nohighbyteincrease:
	rts
.endproc
;------------------------------------------------------------------

initscroll:
	lda #<SCROLL_TEXT	;Load lowbyte of scroll text start in memory. We start at -1 because code will start by increasing the address by 1
	sta ZP_SCROLL_TEXT_ADDRESS
	lda #>SCROLL_TEXT	;Load highbyte of scroll text start in memory. We start at -1 because code will start by increasing the address by 1
	sta ZP_SCROLL_TEXT_ADDRESS + 1
	rts

initscrollframecount:
	;Init framecounter (decrease from number to 0)
	lda #SCROLL_EACH_X_FRAME
	sta ZP_SCROLL_FRAME_COUNT
	rts

;------------------------------------------------------------------
; Raster bars
;------------------------------------------------------------------
.proc rasterbars
	ldx RASTER_COUNTER
	lda rasterSinusTable,x	;Grab new rasterline value
	lsr 					;Divide by 2 to get smaller sine wave
	clc
	adc #70  			    ;Adjust to middle of screen
rasterwait:
	cmp SCREEN_RASTER_LINE	;from the table and wait
    bne rasterwait			;for raster the line

	ldy #10					;Loose time to hide the
idle1:
	dey						;flickering at the beginning
	bne idle1				;of the effect


 	sta CURRENT_RASTER_LINE	;Save current rasterbars start position
	cmp PREVIOUS_RASTER_LINE	;Compare with previous rasterline start position
	sta PREVIOUS_RASTER_LINE	;Remember the current raster start as previous
	bcc goingdown 			;Previous less than -> going down
	lda #1					;1 = going up
	sta RASTER_BAR_DIRECTION
	jmp afterdirection
goingdown:
	lda #0					;0 = going down
	sta RASTER_BAR_DIRECTION
afterdirection:
; Main Loop to print raster bars
	ldx #00
loop:
  	lda CURRENT_RASTER_LINE
 	cmp #RASTER_BACKGROUND_DISABLED_START
 	bcc backgroundAndBorderRaster	;Less than
 	cmp #RASTER_BACKGROUND_DISABLED_END
 	bcs backgroundAndBorderRaster	;Greater than or equal

;Within main defined area where raster in background should be on or off (depending on direction check above)
	lda RASTER_BAR_DIRECTION
	cmp #0
	beq backgroundAndBorderRaster	;If going down, show both background and border raster
	;Only show border raster, and background to same color as default background
	lda rasterColorTable,x	;assign border
   	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #RASTER_DEFAULT_BACKGROUND_COLOR 	;assign border to default background color
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	jmp cont

backgroundAndBorderRaster:
	lda rasterColorTable,x	;assign background and border
   	sta SCREEN_BORDER_COLOR_ADDRESS
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
cont:

; 	ldy rasterDelayTable,x	;Loose time to hide the
; idle2:
; 	dey						;flickering at the end
; 	bne idle2				;of the effect

	inc CURRENT_RASTER_LINE
	inc CURRENT_RASTER_LINE
	lda CURRENT_RASTER_LINE
waitnextline:
	cmp SCREEN_RASTER_LINE
	bne waitnextline

	inx
	cpx #rasterLength
	bne loop
; End of main loop

	inc RASTER_COUNTER
	inc RASTER_COUNTER

	;Raster bar done, reset border and background color
	lda #RASTER_DEFAULT_BORDER_COLOR
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda #RASTER_DEFAULT_BACKGROUND_COLOR
	sta SCREEN_BACKGROUND_COLOR_ADDRESS

	rts
.endproc

.include "smooth_scroller_and_raster2_init.asm"

.include "smooth_scroller_and_raster2_data.asm"
