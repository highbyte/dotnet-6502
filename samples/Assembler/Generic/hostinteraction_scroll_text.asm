;ACME assembler
;!to "./hostinteraction_scroll_text.prg"

;code start address
* = $c000

;Memory location where interact with host to update a Console screen.
;Console assumptions
;- 80 columns and 25 rows, 1 byte per character = 2000 (0x07d0) bytes
;- Laid out in memory as appears on screen.
SCREEN_MEM = 0x1000
SCREEN_MEM_COLS = 80
SCREEN_MEM_ROWS = 25

SCREEN_REFRESH_STATUS = 0xf000

;Zero Page address that will hold an 16 bit address (text start in memory + current scroll offset)
;Little endian:
;	0x40 will contain least significant byte, that is used in Indirect Indexed addressing mode
;	0x41 will contain most significant byte.
ZP_SCROLL_TEXT_ADDRESS = 0x40

;-----------------
;Code start
;-----------------
;Initialize scroll text address to start of text.
	jsr initscroll
!zone mainloop
mainloop:
	jsr waitforrefresh
	jsr scrolltext
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

!zone scrolltext
scrolltext:
	ldx #0
	ldy #0
.loop:
	lda (ZP_SCROLL_TEXT_ADDRESS), Y
	bne .notendofscroll
	jsr initscroll					; Reset scroll pointer to start of text
.nothighbyteincrease2:
	ldy #0
	jmp .loop
.notendofscroll
	iny
	sta SCREEN_MEM, X				; Print character. A will contain current character to print, and Y the column
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
;-----------------

;-----------------
;Data
;-----------------
!zone data

!convtab raw	;Text conversion setting: pet (PetSCII), raw (none), scr (C64 screen code)
SCROLL_TEXT:
	!text "                                                                                "
	!text "Highbyte, in 2024, proudly presents... A DotNet 6502 CPU emulator!    "
	!text "This scroller is written in 6502 machine code, updating the emulator host screen indirectly via shared memory.   "
	!text "Greetings to all my demo-scene friends from back in the late 80s & early 90s in the groups Them and Virtual!"
	!text "                                                                                "
	!by 0 ;End of scroll indicator	
