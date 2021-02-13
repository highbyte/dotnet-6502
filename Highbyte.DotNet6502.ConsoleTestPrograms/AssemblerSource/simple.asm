;code start address
* = $c000

;VSCode extension VS64 (ACME cross-assembler) will automatially set output path and filename to the .cache directory
;!to "./simple.prg"
;Add values in two memory locations, rotate right, and store in another memory location.
	lda $d000
	clc
	adc $d001
	ror
	sta $d002
;In emulator, setup hitting brk instruction to stop
	brk
