;code start address
* = $c000

;VSCode extension VS64 will automatially set output path and filename to the .cache directory
;!to "./testprogram.prg"

;copy $1000-10ff to $2000-200ff
	ldx #0
loop:
	lda $1000,x
	sta $2000,x
	inx
	bne loop
	brk
