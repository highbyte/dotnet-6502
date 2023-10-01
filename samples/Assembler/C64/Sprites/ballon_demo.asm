;ACME assembler
;!to "./build/ballon_demo.prg"

;Example below is from https://codebase64.org/doku.php?id=base:ballon_demo_from_manual, adapted to work with ACME assembler.

CLRSCN = $E544	; Clear Screen

VIC = $D000		; VIC Basis 53248
MIB_X2 = VIC+4		;
MIB_Y2 = VIC+5		;
MIB_Y_MSB = VIC+16

MIB_ENABLE = VIC+21 	; register Sprite Enable 53269
MIB_POINTER = $07F8	; Memory pointer Basis 2040

MIB_MEM_SP2 = $0340	; begin memory area sprite2


*=$0800
	
; encode SYS 2064 ($0810) line
; in BASIC program space

!byte $00 ,$0c, $08, $0a, $00, $9e, $20, $32 
!byte $30, $36, $34, $00, $00, $00, $00, $00


init	
	lda #$04	; Sprite 2
	sta MIB_ENABLE	; Sprite enable register
	
	lda #MIB_MEM_SP2/64	; Store startaddress of Pointer 2
	sta MIB_POINTER+2	; to Sprite pointer register
	
	ldx #$3e	; max of sprite value => 63
x0	lda spr0,x	; load sprite byte
	sta MIB_MEM_SP2,x; store to spritememory
	dex		; x--
	bne x0		; last byte?
	dex		; x--
	stx MIB_X2	; set Sprite position x to zero minus one
	stx MIB_Y2	; set Sprite position y to zero minus one
	
	jsr CLRSCN	; C64 ROM Clear Screen
	
y0	inc MIB_X2	; Sprite position x++
	inc MIB_Y2	; Sprite position y++
	
	
	; delay for sprite move
	ldx #$05	; set prescaler outer loop
y11	ldy #$ff	; set prescaler inner loop
y1	dey		; y--
	bne y1		; no reached of zero
	dex		; x--
	bne y11		;
	
	lda MIB_X2	; Sprite position x
	cmp #$c8	; Sprite position x are 200?
	bne y0		; no, next position
	
	rts
	
	
spr0:
	;!byte 128,127,0,1,255,192,3,255,224,3,231,224
	!byte 0,127,0,1,255,192,3,255,224,3,231,224
	!byte 7,217,240,7,223,240,2,217,240,3,231,224
	!byte 3,255,224,3,255,224,2,255,160,1,127,64
	!byte 1,62,64,0,156,128,0,156,128,0,73,0,0,73,0,0
	!byte 62,0,0,62,0,0,62,0,0,28,0	