;ACME assembler

;code start address
* = $c000

;!to "./testprogram.prg"

;copy $1000-10ff to $2000-200ff via subroutine
	ldx #0
loop:
	jsr copymem
	inx
	bne loop

;In emulator, setup hitting brk instruction to stop
	brk

copymem:
	lda $1000,x
	sta $2000,x
	rts
