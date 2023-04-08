;ACME assembler

;code start address
* = $c000

;!to "./testprogram.prg"

;copy $1000-10ff to $2000-200ff
	ldx #0
loop:
	lda $1000,x
	sta $2000,x
	inx
	bne loop
	
;In emulator, setup hitting brk instruction to stop
	brk
