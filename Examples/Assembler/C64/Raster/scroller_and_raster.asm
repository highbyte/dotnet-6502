;ACME assembler
;!to "./scroller_and_raster.prg"

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------
STATIC_TEXT_ROW = 8;
COLOR_CYCLE_EACH_X_FRAME = 2;

SCROLLER_ROW = 14;
SCROLL_EACH_X_FRAME = 4;

;------------------------------------------------------------
;Memory address shared with emulator host for updating screen
;------------------------------------------------------------
;40 columns and 25 rows, 1 byte per character = 1000 (0x03e8) bytes
;Laid out in memory as appears on screen.
SCREEN_MEM = 0x0400			;0x0400 - 0x07e7
SCREEN_MEM_COLS = 40
SCREEN_MEM_ROWS = 25
;Colors, one byte per character = 1000 (0x03e8) bytes
SCREEN_COLOR_MEM = 0xd800	;0xd800 - 0xdbe7

;Bit 8 (highest bit) of the current video scan line is stored in bit #7 in this register
SCREEN_CONTROL_REGISTER_1 = 0xd011
;Bits 0-7 the current video scan line bit
SCREEN_RASTER_LINE = 0xd012

;Border color address
SCREEN_BORDER_COLOR_ADDRESS = 0xd020
;Background color address for main screen
SCREEN_BACKGROUND_COLOR_ADDRESS = 0xd021

;Check keyboard status
CIA1_DATAB = 0xdc01

;------------------------------------------------------------
;ZP memory locations used for calculations
;------------------------------------------------------------
;Zero Page address that will hold an 16 bit address (text start in memory + current scroll offset)
;Little endian:
;	0xfb will contain least significant byte, that is used in Indirect Indexed addressing mode
;	0xfc will contain most significant byte.
ZP_SCROLL_TEXT_ADDRESS = 0xfb

;Index to where in text color list
ZP_TEXT_COLOR_CYCLE_INDEX = 0xfd

;Index to where in background color list 
ZP_BG_COLOR_CYCLE_INDEX = 0xfe

;Index to where in border color list
ZP_BORDER_COLOR_CYCLE_INDEX = 0x02

;Frame counters
ZP_SCROLL_FRAME_COUNT = 0x03
ZP_COLOR_CYCLE_FRAME_COUNT = 0x04

;Index in raster sine table
RASTER_COUNTER = 0x05

;Macros
!macro wait_vblank {
;Wait for vblank (if raster pos 255 already been waited for, set X to no-zero)
	cpx #0
	bne .wait_vblank2
.wait_vblank1
	bit SCREEN_CONTROL_REGISTER_1
	bpl .wait_vblank1
.wait_vblank2
	bit SCREEN_CONTROL_REGISTER_1
	bmi .wait_vblank2
}

;------------------------------------------------------------------
;Code start
;------------------------------------------------------------------
	sei			;Disable interrupts;

;Set default border and background color
	lda #0x0b
	sta defaultBorderColor
	lda #0x00
	sta defaultBackgroundColor

;Init bg color cycle index
	lda #2
	sta ZP_BG_COLOR_CYCLE_INDEX

;Init border color cycle index
	lda #0
	sta ZP_BORDER_COLOR_CYCLE_INDEX

;Clear text screen and set text color
	jsr cleartextscreen
	lda #1	;text color
	jsr settextcolors

;Initialize scroll text address to start of text.
	jsr initscroll
	jsr initscrollframecount

;Initialize static text and color cycle
	jsr printstatictext
	jsr initcolorcycleframecount
	jsr initcolorcycle

!zone mainloop
mainloop:
;Wait for new frame (flag set by emulator host)
	ldx #0
	+wait_vblank

;Cycle background color if key is pressed
;	jsr cyclebackgroundifkeyispressed
;Cycle border color if key is pressed
;	jsr cycleborderifkeyispressed

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

;Raster bars
	jsr rasterbars

	;brk	; In emulator, setup hitting brk instruction to stop	
	jmp mainloop
	;brk	; In emulator, setup hitting brk instruction to stop
;------------------------------------------------------------------

!zone cleartextscreen
cleartextscreen:
	lda #$20
	ldx #0
.loop:
	sta SCREEN_MEM,x
	sta SCREEN_MEM + 0x100,x
	sta SCREEN_MEM + 0x200,x
	sta SCREEN_MEM + 0x300,x
	dex
	bne .loop
	rts

!zone settextcolors
;Set color for each character on screen to the value in A
settextcolors:
	ldx #0
.loop:
	sta SCREEN_COLOR_MEM,x
	sta SCREEN_COLOR_MEM + 0x100,x
	sta SCREEN_COLOR_MEM + 0x200,x
	sta SCREEN_COLOR_MEM + 0x300,x
	dex
	bne .loop
	rts


!zone printstatictext
printstatictext:
	ldx #0
	ldy #0
.loop:
	lda STATIC_TEXT, X
	beq .endoftext
	sta SCREEN_MEM + (SCREEN_MEM_COLS * STATIC_TEXT_ROW) , X	; Print character. A will contain current character to print, and X the column
	inx
	jmp .loop
.endoftext
	rts

;------------------------------------------------------------------
!zone colorcycle_statictext
colorcycle_statictext:
	ldx #0
	lda ZP_TEXT_COLOR_CYCLE_INDEX
	tay
.loop:
	lda STATIC_TEXT_COLOR, Y
	cmp #$ff
	bne .notendofcolorlist
	ldy #0
	jmp .loop
.notendofcolorlist
	iny
	sta SCREEN_COLOR_MEM + (SCREEN_MEM_COLS * STATIC_TEXT_ROW) , X	; Change color of character. A will contain current color to print, and X the column
	inx
	cpx #SCREEN_MEM_COLS
	bne .loop	;Loop until we changed color for entire row of 80 characters

	;Increase color cycle index starting point.
	inc ZP_TEXT_COLOR_CYCLE_INDEX
	lda ZP_TEXT_COLOR_CYCLE_INDEX
	tay
	;Check if we reached end, then reset
	lda STATIC_TEXT_COLOR, Y
	cmp #$ff
	bne .notendofcolorlist2
	jsr initcolorcycle
.notendofcolorlist2
	rts

;------------------------------------------------------------------
!zone initcolorcycle
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
!zone scrolltext
scrolltext:
	ldx #0
	ldy #0
.loop:
	lda (ZP_SCROLL_TEXT_ADDRESS), Y
	bne .notendofscroll
	jsr initscroll					; Reset scroll pointer to start of text
	ldy #0
	jmp .loop
.notendofscroll
	iny
	sta SCREEN_MEM + (SCREEN_MEM_COLS * SCROLLER_ROW) , X				; Print character. A will contain current character to print, and X the column
	inx
	cpx #SCREEN_MEM_COLS
	bne .loop						;Loop until we printed 80 characters

	inc ZP_SCROLL_TEXT_ADDRESS		;Increase scroll start pointer lowbyte
	bne .nohighbyteincrease			;Check if we reach 00 (wrap around), then Zero flag is set, which means we should also increase highbyte
	inc ZP_SCROLL_TEXT_ADDRESS + 1	;Increase scroll start pointer highbyte if we got carry from lowbyte
.nohighbyteincrease
	rts
;------------------------------------------------------------------

!zone initscroll
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
!zone rasterbars
rasterbars:
	ldx RASTER_COUNTER
	lda rasterSinusTable,x	;Grab new rasterline value  
.rasterwait:
	cmp SCREEN_RASTER_LINE	;from the table and wait
    bne .rasterwait		;for raster the line

	ldy #10				;Loose time to hide the
.idle1	
	dey					;flickering at the beginning 
	bne .idle1			;of the effect

; Main Loop to print raster bars
	ldx #00		
.loop	
	lda rasterColorTable,x	;assign background and border
   	sta SCREEN_BORDER_COLOR_ADDRESS
	sta SCREEN_BACKGROUND_COLOR_ADDRESS

	ldy rasterDelayTable,x	;Loose time to hide the
.idle2	
	dey					;flickering at the end
	bne .idle2			;of the effect


	inx 		
	cpx #09
	bne .loop
; End of main loop

	;Assign default border and background colors
	lda defaultBorderColor			
	sta SCREEN_BORDER_COLOR_ADDRESS
	lda defaultBackgroundColor
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	
	inc RASTER_COUNTER

	rts

;------------------------------------------------------------
;Data
;------------------------------------------------------------
!zone data

!convtab scr	;Text conversion setting: pet (PetSCII), raw (none), scr (C64 screen code)

STATIC_TEXT:
	!text "C64 code running in dotnet6502 emulator"
	!by 0 ;End of text indicator

STATIC_TEXT_COLOR:
	;!by 0x0b,0x0b,0x0b,0x0b,0x0c,0x0c,0x0c,0x0c,0x0f,0x0f,0x01,0x01,0x0f,0x0f,0x0c,0x0c,0x0c,0x0c,0x0c,0x0c,0x0c
	;!by 0x02,0x02,0x02,0x02,0x04,0x04,0x04,0x04,0x0a,0x0a,0x0a,0x0a,0x07,0x07,0x07,0x07,0x0a,0x0a,0x0a,0x0a,0x04,0x04,0x04,0x04,0x02,0x02,0x02,0x02
	!by 0x02,0x02,0x02,0x02,0x0a,0x0a,0x0a,0x0a,0x07,0x07,0x07,0x07,0x0a,0x0a,0x0a,0x0a,0x02,0x02,0x02,0x02
	!by 0x06,0x06,0x06,0x06,0x0e,0x0e,0x0e,0x0e,0x01,0x01,0x01,0x01,0x0e,0x0e,0x0e,0x0e,0x06,0x06,0x06,0x06
	!by 0x05,0x05,0x05,0x05,0x0d,0x0d,0x0d,0x0d,0x01,0x01,0x01,0x01,0x0d,0x0d,0x0d,0x0d,0x05,0x05,0x05,0x05
	!by 0xff ;End of color indicator (cannot be 0 which is black)

SCROLL_TEXT:
	;!text "                                                                                "
	!text "                                        "
	!text "highbyte, in 2023, proudly presents... a dotnet 6502 cpu emulator!    "
	!text "these raster bars and the (rather choppy) scroller is written in 6502 machine code for C64.   "
	;!text "hold space to flash border color.   "
	!text "greetings to all my demo-scene friends from back in the late 80s & early 90s in the groups them and virtual!"
	;!text "                                                                                "
	!text "                                        "
	!by 0 ;End of text indicator


defaultBorderColor:
	!by 0x0b

defaultBackgroundColor:
	!by 0x00

rasterColorTable:
!by 09,08,12,13,01,13,12,08,09

rasterDelayTable:
!by 08,08,09,08,12,08,08,08,09

rasterSinusTable:
!by 140,143,145,148,151,154,156,159,162,164,167,169,172,175,177,180,182,185,187,190
!by 192,194,197,199,201,204,206,208,210,212,214,216,218,220,222,224,225,227,229,230
!by 232,233,235,236,237,238,240,241,242,243,244,245,245,246,247,247,248,248,249,249
!by 250,250,250,250,250,250,250,250,249,249,249,248,248,247,247,246,245,244,243,242
!by 241,240,239,238,237,235,234,232,231,229,228,226,224,223,221,219,217,215,213,211
!by 209,207,205,202,200,198,196,193,191,188,186,183,181,178,176,173,171,168,166,163
!by 160,158,155,152,149,147,144,141,139,136,133,131,128,125,122,120,117,114,112,109
!by 107,104,102,99,97,94,92,89,87,84,82,80,78,75,73,71,69,67,65,63
!by 61,59,57,56,54,52,51,49,48,46,45,43,42,41,40,39,38,37,36,35
!by 34,33,33,32,32,31,31,31,30,30,30,30,30,30,30,30,31,31,32,32
!by 33,33,34,35,35,36,37,38,39,40,42,43,44,45,47,48,50,51,53,55
!by 56,58,60,62,64,66,68,70,72,74,76,79,81,83,86,88,90,93,95,98
!by 100,103,105,108,111,113,116,118,121,124,126,129,132,135,137,140

