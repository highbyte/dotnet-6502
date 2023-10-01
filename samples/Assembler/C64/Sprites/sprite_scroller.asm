;ACME assembler
;!to "./build/sprite_scroller.prg"

;Example below is based on code from from https://codebase64.org/doku.php?id=base:scrolltext_using_sprites
;and adapted to work as a complete program.

	; ----------------------------------------------------------------------------------------------------
	;
	;	Sprite-Scroller Routine
	;	----------------------------
	;
	;	coding: testicle/payday
	;	logo: fabu/payday
	;
	;
	;	contact and payday-releases:
	;	------------------------------------
	;
	;	daniel@popelganda.de
	;	www.popelganda.de
	;
	;
	;	this source code is part of an intro, so many code is missing here,
	;	while only the interesting parts for the sprite scroller are shown.
	;	it shows how to use sprites for text scrolling, so the scroll text
	;	can easily be placed above pictures.
	;
	;	this sourcecode is best view with the font "tahoma", font size 9.
	;	you can compile this code using the ACME crossassembler.
	;
	;	the code was written with Relaunch64, the c64-crossassembler-tool
	;	for windows-pc. grab it at www.popelganda.de!
	;
	; ----------------------------------------------------------------------------------------------------

;--------------------------------------------------
;----- Paragraph @Globale Variablen@ -----
;--------------------------------------------------

scrolldelay = 1 	;how many frames to wait before scrolling, the higher the slower
scrolldelaycounter = $fa
charsetromlocation = $d000
charsetramlocation = $3000
spritexpos = 128
spriteypos = 140	;sprite y-position
spritechar = $3300	;here's the char located, that "rolls" into the spritescroller

;*=$c000
*=$0800
; encode SYS 2064 ($0810) line
; in BASIC program space
!byte $00 ,$0c, $08, $0a, $00, $9e, $20, $32 
!byte $30, $36, $34, $00, $00, $00, $00, $00
 
;--------------------------------------------------
;----- Paragraph @Includes@ -----
;--------------------------------------------------

; disable interrupts during irq setup
		sei					 

;	init text pointer

		lda #<text
		sta $50
		lda #>text
		sta $51

;--------------------------------------------------
;----- Paragraph @clear sprite-memory@ -----
;--------------------------------------------------

		ldx #0
		lda #$00
.loop4	sta $3800,x
		inx
		bne .loop4
		ldx #0
		lda #$00
.loop6	sta $3900,x
		inx
		cpx #64
		bne .loop6

		ldx #7
		lda #0
.loop5	sta spritechar,x
		dex
		bpl .loop5


;--------------------------------------------------
;----- Paragraph @copy charset@ -----
;--------------------------------------------------
		jsr copycharset	;jump to subroutine to copy charset from rom $d000 to ram $3000

;--------------------------------------------------
;----- Paragraph @init scroll variables@ -----
;--------------------------------------------------
		jsr sprscrollinit	;jump to subroutine to initialize sprite positions

		;Set scroll speed (how many frames to wait before scrolling, the higher the slower)
		lda #scrolldelay	;load scrollspeed
		sta scrolldelaycounter

;--------------------------------------------------
;----- Paragraph @init irq hander@ -----
;--------------------------------------------------

		;Init IRQ
		lda #%01111111       ; switch off interrupt signals from cia-1
		sta $dc0d

		and $d011            ; clear most significant bit of vic's raster register
		sta $d011

		lda $dc0d            ; acknowledge pending interrupts from cia-1
		lda $dd0d            ; acknowledge pending interrupts from cia-2

		lda #%00000001       ; enable raster interrupt signals from vic
		sta $d01a

		lda #<irq
		sta $0314	;Lowbyte of IRQ handler address
		lda #>irq
		sta $0315	;Highbyte of IRQ handler address

;enable interrupts after setup
		cli			

waitloop
		lda #0
		beq waitloop	;endless branch, until lda #0 instruction "waitloop" changes it's operand to other than 0
		rts

irq		;Irq handler called once each frame
		;Check how often we should scroll (every x frame)
		dec scrolldelaycounter
	 	bne skipscroll
		jsr spritescroll	;jump to the main routine
		lda #scrolldelay	;reset scrollspeed delay
		sta scrolldelaycounter

skipscroll
		asl $d019            ; acknowledge the interrupt by clearing the VIC's interrupt flag
		;jmp $ea31            ; jump into KERNAL's standard interrupt service routine to handle keyboard scan, cursor display etc.	
		jmp $ea81            ; jump into shorter ROM routine to only restore registers from the stack etc

!zone
copycharset
        sei         ; disable interrupts while we copy 
        ldx #$08    ; we loop 8 times (8x255 = 2Kb)
        lda #$33    ; make the CPU see the Character Generator ROM...
        sta $01     ; ...at $D000 by storing %00110011 into location $01

        lda #>charsetromlocation    ; load high byte of $D000
        sta $fc     ; store it in a free location we use as vector
        ldy #<charsetromlocation    ; init counter with 0
        sty $fb     ; store it as low byte in the $FB/$FC vector

        lda #>charsetramlocation    ; load high byte of $3000
        sta $fe     ; store it in a free location we use as vector
        ldy #<charsetramlocation    ; init counter with 0
        sty $fd     ; store it as low byte in the $FD/$FE vector

loop    lda ($fb),y ; read byte from vector stored in $fb/$fc
        sta ($fd),y ; write to the RAM vector stored in $fd/$fe
        iny         ; do this 255 times...
        bne loop    ; ..for low byte $00 to $FF
        inc $fc     ; when we passed $FF increase high byte...
        inc $fe     ; when we passed $FF increase high byte...
        dex         ; ... and decrease X by one before restart
        bne loop    ; We repeat this until X becomes Zero
        lda #$37    ; switch in I/O mapped registers again...
        sta $01     ; ... with %00110111 so CPU can see them
        cli         ; turn off interrupt disable flag
        rts         ; return from subroutine

;--------------------------------------------------
;
;----- Paragraph @init sprites above@ -----
;
;--------------------------------------------------

!zone
sprscrollinit	
		lda #spritexpos
		sta $d000
		lda #spritexpos+24
		sta $d002
		lda #spritexpos+48
		sta $d004
		lda #spritexpos+72
		sta $d006
		lda #spritexpos+96
		sta $d008
		lda #spriteypos
		sta $d001
		sta $d003
		sta $d005
		sta $d007
		sta $d009
		
		lda #%00011111	;switch on 5 sprites
		sta $d015
		lda #0
		sta $d01b
		sta $d01c
		lda #7			;set sprite color
		sta $d027
		sta $d028
		sta $d029
		sta $d02a
		sta $d02b

;--------------------------------------------------
;		sprites at $3800
;--------------------------------------------------
		
		lda #224
		sta $07f8
		lda #225
		sta $07f9
		lda #226
		sta $07fa
		lda #227
		sta $07fb
		lda #228
		sta $07fc
		rts


;--------------------------------------------------
;
;----- Paragraph @Sub-Route: Spritescrolling@ -----
;
;--------------------------------------------------

	;this is the main routine which is responsible for scrolling
	;a text through sprites

!zone
spritescroll	
		dec .cnt+1
.cnt	lda #8			;already 8 pixel moved?
		beq .neuchar		;if yes, read in new char
		jmp .softscroll		;else jump to the softscroller and return to the main routine

.neuchar
		ldy #0			;read new char
		lda ($50),y		;this is the text-pointer
		bne .undlos		;end-sign?

		lda #<text		;if yes, reset text-vector
		sta $50
		lda #>text
		sta $51
		lda #$20

.undlos	clc			;clear carry-bit
		rol			;char-value * 8
		rol			;(this is the offset for the pixeldata of a char in the charset)
		rol
		sta .loop2+1
		bcc .weiter
		inc .loop2+2

.weiter	ldx #7			;read 8 bytes (one char from the charset)
.loop2	lda charsetramlocation,x		;from charset-memory
		sta spritechar,x		;and store to that memory-adress where the char is located,
		dex			;that "roles" next into the spritescroll
		bpl .loop2

		lda #0			;reset adresses
		sta .loop2+1
		lda #$30
		sta .loop2+2

		inc $50			;increase scrolltext-counter
		lda $50
		bne .nixneu
		inc $51

.nixneu	lda #8			;reset scrolltext-counter
		sta .cnt+1

.softscroll	
		ldy #0
		ldx #0

;--------------------------------------------------
;	move chars in sprites
;	to the left (soft-scrolling)
;--------------------------------------------------

.loop1	clc
.origin	rol spritechar		;"read" left bit of new sign
		rol $3902,x		;move sprite-char - sprite5
		rol $3901,x
		rol $3900,x
		rol $38c2,x		;move sprite-char - sprite4
		rol $38c1,x
		rol $38c0,x
		rol $3882,x		;move sprite-char - sprite3
		rol $3881,x
		rol $3880,x
		rol $3842,x		;move sprite-char - sprite2
		rol $3841,x
		rol $3840,x
		rol $3802,x		;move sprite-char - sprite1
		rol $3801,x
		rol $3800,x
		iny
		inc .origin+1		;increase counter and set to next "pixel-row" of that char
		txa
		clc
		adc #3
		tax
		cpy #8
		bne .loop1
		lda #<spritechar	;restore original value
		sta .origin+1
		rts

;--------------------------------------------------
;----- Paragraph @Scrolltext@ -----
;--------------------------------------------------
text:
!ct scr
!tx "     sprite scroller, uses character set copied from rom to scroll in as pixels on sprites. drawn over characters or graphics."
!tx "                    "
!byte 0
