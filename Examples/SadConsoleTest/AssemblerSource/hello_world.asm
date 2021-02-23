;hello_world.asm
;Written with ACME cross-assembler using VSCode extension VS64. Extension will compile on save to .cache directory.

;Code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------
STATIC_TEXT_ROW = 10;

;------------------------------------------------------------
;Memory address shared with emulator host for updating screen
;------------------------------------------------------------
;80 columns and 25 rows, 1 byte per character = 2000 (0x03e8) bytes. Laid out in memory as appears on screen.
SCREEN_MEM = 0x0400					;0x0400 - 0x07e7
SCREEN_MEM_COLS	= 80
SCREEN_MEM_ROWS	= 25
;Colors, one byte per character = 1000 (0x03e8) bytes
SCREEN_COLOR_MEM = 0xd800			;0xd800 - 0xdbe7
;Byte with status flags to communicate with emulator host. When host new frame, emulator done for frame, etc.
SCREEN_REFRESH_STATUS = 0xd000
;Border color address
SCREEN_BORDER_COLOR_ADDRESS = 0xd020
;Bg color address for entire screen
SCREEN_BACKGROUND_COLOR_ADDRESS = 0xd021

;Currently pressed key on host (ASCII byte). If no key is pressed, value is 0x00
KEY_PRESSED_ADDRESS = 0xd030
;Currently down key on host (ASCII byte). If no key is down, value is 0x00
KEY_DOWN_ADDRESS = 0xd031
;Currently released key on host (ASCII byte). If no key is down, value is 0x00
KEY_RELEASED_ADDRESS = 0xd031

;------------------------------------------------------------
;Code start
;------------------------------------------------------------
;Set screen background color
	lda #$06
	sta SCREEN_BACKGROUND_COLOR_ADDRESS
;Set border color
	lda #$0e
	sta SCREEN_BORDER_COLOR_ADDRESS	
;Initialize static text at row defined in STATIC_TEXT_ROW
	ldx #0
.printchar:
	lda STATIC_TEXT, X
	sta SCREEN_MEM + (SCREEN_MEM_COLS * STATIC_TEXT_ROW), X
	lda STATIC_TEXT_2, X
	sta SCREEN_MEM + (SCREEN_MEM_COLS * (STATIC_TEXT_ROW + 2)), X
	beq .endoftext
	inx
	jmp .printchar
.endoftext

mainloop:
;Wait for emulator indicating a new frame
.waitfornextframe
	lda SCREEN_REFRESH_STATUS
	and #%00000001					;Bit 0 set signals it time to refresh screen
	beq .waitfornextframe			;Loop if bit 1 is not set

;If space is pressed, cycle corder color
	lda KEY_DOWN_ADDRESS			;Load currently down key
	cmp #$20						;32 ($20) = space
	bne .spacenotpressed
	ldx SCREEN_BORDER_COLOR_ADDRESS ;Get current border color
	inx								;Next color
	cpx #$10						;Passed highest color (#$0f)?
	bne .notreachedhighestcolor		;If we haven't reached max color value
	ldx #$00						;Reset to lowest color (0)
.notreachedhighestcolor
	stx SCREEN_BORDER_COLOR_ADDRESS	;Update border color
.spacenotpressed:

;Set bit flag that tells emulator that this 6502 code is done for current frame
	lda SCREEN_REFRESH_STATUS
	ora #%00000010					;Bit 1 set signals that emulator is currently done
	sta SCREEN_REFRESH_STATUS 		;Update status to memory

;Loop forever
	jmp mainloop

;------------------------------------------------------------
;Data
;------------------------------------------------------------
STATIC_TEXT:
	!text "                     ***** DotNet6502 + SadConsole !! *****                     "
	!by 0 							;End of text indicator	
STATIC_TEXT_2:
	!text "                        Press SPACE to cycle border color                       "
	!by 0 							;End of text indicator