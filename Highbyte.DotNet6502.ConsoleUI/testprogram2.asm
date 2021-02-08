;code start address
* = $c000

;VSCode extension VS64 will automatially set output path and filename to the .cache directory
;!to "./testprogram2.prg"

;copy $1000-10ff to $2000-200ff via subroutine
	ldx #0
loop:
	jsr copymem
	inx
	bne loop
	brk

copymem:
	lda $1000,x
	sta $2000,x
	rts
