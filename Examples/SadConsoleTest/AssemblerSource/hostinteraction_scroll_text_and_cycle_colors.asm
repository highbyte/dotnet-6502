;VSCode extension VS64 (ACME cross-assembler) will automatially set output path and filename to the .cache directory
;!to "./hostinteraction_scroll_text.prg"

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
;80 columns and 25 rows, 1 byte per character = 2000 (0x07d0) bytes
;Laid out in memory as appears on screen.
SCREEN_MEM = 0x0400			;0x400 - 0xbcf
SCREEN_MEM_COLS = 80
SCREEN_MEM_ROWS = 25
;Colors, one byte per character = 2000 (0x07d0) bytes
SCREEN_COLOR_MEM = 0xd800	;0xd800 - 0xdfcf
;Byte with status flags to communicate with emulator host. When host new frame, emulator done for frame, etc.
SCREEN_REFRESH_STATUS = 0xd000
;Border color address
SCREEN_BORDER_COLOR_ADDRESS = 0xd020
;Bg color address for entire screen
SCREEN_BACKGROUND_COLOR_ADDRESS = 0xd021

;Currently pressed key on host (ASCII byte). If no key is pressed, value is 0x00
KEY_PRESSED_ADDRESS = 0xe000
;Currently down key on host (ASCII byte). If no key is down, value is 0x00
KEY_DOWN_ADDRESS = 0xe001
;Currently released key on host (ASCII byte). If no key is down, value is 0x00
KEY_RELEASED_ADDRESS = 0xe002

;------------------------------------------------------------
;ZP memory locations used for calculations
;------------------------------------------------------------
;Zero Page address that will hold an 16 bit address (text start in memory + current scroll offset)
;Little endian:
;	0x40 will contain least significant byte, that is used in Indirect Indexed addressing mode
;	0x41 will contain most significant byte.
ZP_SCROLL_TEXT_ADDRESS = 0x40

;Index to where in text color list we are
ZP_TEXT_COLOR_CYCLE_INDEX = 0x42

;Index to where in background color list we are
ZP_BG_COLOR_CYCLE_INDEX = 0x43

;Index to where in border color list we are
ZP_BORDER_COLOR_CYCLE_INDEX = 0x44

;Frame counters
ZP_SCROLL_FRAME_COUNT = 0x50
ZP_COLOR_CYCLE_FRAME_COUNT = 0x51

;------------------------------------------------------------
;Code start
;------------------------------------------------------------
;Set screen background color
	lda #$0
	sta SCREEN_BACKGROUND_COLOR_ADDRESS

;Set border color
	lda #$0
	sta SCREEN_BORDER_COLOR_ADDRESS	

;Init bg color cycle index
	lda #2
	sta ZP_BG_COLOR_CYCLE_INDEX

;Init border color cycle index
	lda #0
	sta ZP_BORDER_COLOR_CYCLE_INDEX

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
	jsr waitforrefresh

;Cycle background color if key is pressed
;	jsr cyclebackgroundifkeyispressed
;Cycle border color if key is pressed
	jsr cycleborderifkeyispressed


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


;We're done for this frame (emulator host checks this flag if it should continue with rendering the result from memory)
	jsr markdoneflag
	;brk	; In emulator, setup hitting brk instruction to stop	
	jmp mainloop
	;brk	; In emulator, setup hitting brk instruction to stop
;-----------------

!zone waitforrefresh
waitforrefresh:
.loop
	lda SCREEN_REFRESH_STATUS
	;tax ; Store copy of current screen status in X
	and #%00000001	;Bit 0 set signals it time to refresh screen
	beq .loop	;Loop if bit 1 is not set (AND results in 0, then zero flag set, BEQ branches zero flag is set)
	; txa ;Transfer original screen status back to A
	; and %11111110 ;Clear bit 1. TODO: Clearing the flag in memory should probably be done by the host instead?
	; sta SCREEN_REFRESH_STATUS ;Update status to memory
	rts
;-----------------	

!zone markdoneflag
markdoneflag:
	lda SCREEN_REFRESH_STATUS
	ora #%00000010	;Bit 1 set signals that emulator is currently done
	sta SCREEN_REFRESH_STATUS ;Update status to memory
	rts
;-----------------

!zone printstatictext
printstatictext:
	ldx #0
	ldy #0
.loop:
	lda STATIC_TEXT, X
	sta SCREEN_MEM + (SCREEN_MEM_COLS * STATIC_TEXT_ROW) , X	; Print character. A will contain current character to print, and X the column
	beq .endoftext
	inx
	jmp .loop
.endoftext
	rts

;-----------------
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

;-----------------
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

;-----------------
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
;-----------------

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
;-----------------

!zone cyclebackgroundifkeyispressed
cyclebackgroundifkeyispressed:

;Check if space is pressed, if so cycle background color
	lda KEY_DOWN_ADDRESS
	cmp #$20	;32 ($20) = space
	bne .spacenotpressed
.loop:
	lda ZP_BG_COLOR_CYCLE_INDEX
	tay
	lda BACKGROUND_COLOR, Y
	cmp #$ff
	bne .notendofcolorlist
	lda #0
	sta ZP_BG_COLOR_CYCLE_INDEX	
	jmp .loop
.notendofcolorlist
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
	;Increase bg color cycle index starting point.
	inc ZP_BG_COLOR_CYCLE_INDEX
.spacenotpressed:	
	rts

;-----------------

!zone cycleborderifkeyispressed
cycleborderifkeyispressed:

;Check if space is pressed, if so cycle border color
	lda KEY_DOWN_ADDRESS
	cmp #$20	;32 ($20) = space
	bne .spacenotpressed
.loop:
	lda ZP_BORDER_COLOR_CYCLE_INDEX
	tay
	lda BORDER_COLOR, Y
	cmp #$ff
	bne .notendofcolorlist
	lda #0
	sta ZP_BORDER_COLOR_CYCLE_INDEX	
	jmp .loop
.notendofcolorlist
	sta SCREEN_BORDER_COLOR_ADDRESS
	;Increase bg color cycle index starting point.
	inc ZP_BORDER_COLOR_CYCLE_INDEX
.spacenotpressed:	
	rts

;------------------------------------------------------------
;Data
;------------------------------------------------------------
!zone data

!convtab raw	;Text conversion setting: pet (PetSCII), raw (none), scr (C64 screen code)

STATIC_TEXT:
	!text "       *** 6502 machine code running in Highbyte.DotNet6502 emulator! ***       "
	!by 0 ;End of text indicator

STATIC_TEXT_COLOR:
	;!by 0x0b,0x0b,0x0b,0x0b,0x0c,0x0c,0x0c,0x0c,0x0f,0x0f,0x01,0x01,0x0f,0x0f,0x0c,0x0c,0x0c,0x0c,0x0c,0x0c,0x0c
	;!by 0x02,0x02,0x02,0x02,0x04,0x04,0x04,0x04,0x0a,0x0a,0x0a,0x0a,0x07,0x07,0x07,0x07,0x0a,0x0a,0x0a,0x0a,0x04,0x04,0x04,0x04,0x02,0x02,0x02,0x02
	!by 0x02,0x02,0x02,0x02,0x0a,0x0a,0x0a,0x0a,0x07,0x07,0x07,0x07,0x0a,0x0a,0x0a,0x0a,0x02,0x02,0x02,0x02
	!by 0x06,0x06,0x06,0x06,0x0e,0x0e,0x0e,0x0e,0x01,0x01,0x01,0x01,0x0e,0x0e,0x0e,0x0e,0x06,0x06,0x06,0x06
	!by 0x05,0x05,0x05,0x05,0x0d,0x0d,0x0d,0x0d,0x01,0x01,0x01,0x01,0x0d,0x0d,0x0d,0x0d,0x05,0x05,0x05,0x05
	!by 0xff ;End of color indicator (cannot be 0 which is black)

SCROLL_TEXT:
	!text "                                                                                "
	!text "Highbyte, in 2021, proudly presents... A DotNet 6502 CPU emulator!    "
	!text "This (rather choppy) scroller and color cycler is written in 6502 machine code, updating the emulator host screen indirectly via shared memory.   "
	!text "Hold SPACE to flash border color.   "
	!text "Greetings to all my demo-scene friends from back in the late 80s & early 90s in the groups Them and Virtual!"
	!text "                                                                                "
	!by 0 ;End of text indicator

BACKGROUND_COLOR:
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x0b,0x0b,0x0b,0x0b,0x0b,0x0b,0x0b,0x0b
	!by 0x0c,0x0c,0x0c,0x0c
	!by 0x0f,0x0f,0x0f,0x0f
	!by 0x0c,0x0c,0x0c,0x0c
	!by 0x0b,0x0b,0x0b,0x0b,0x0b,0x0b,0x0b,0x0b
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0xff ;End of color indicator (cannot be 0 which is black)


BORDER_COLOR:
	!by 0x02,0x02,0x02
	!by 0x0a,0x0a,0x0a
	!by 0x0f,0x0f,0x0f
	!by 0x0a,0x0a,0x0a
	!by 0x02,0x02,0x02
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x05,0x05,0x05
	!by 0x0d,0x0d,0x0d
	!by 0x0f,0x0f,0x0f
	!by 0x0d,0x0d,0x0d
	!by 0x05,0x05,0x05
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x06,0x06,0x06
	!by 0x0e,0x0e,0x0e
	!by 0x0f,0x0f,0x0f
	!by 0x0e,0x0e,0x0e
	!by 0x06,0x06,0x06
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00
	!by 0xff ;End of color indicator (cannot be 0 which is black)
