;ACME assembler

;code start address
* = $c000

;!to "./simple.prg"
;Add values in two memory locations, rotate right, and store in another memory location.
	lda $d000
	clc
	adc $d001
	ror
	sta $d002
;In emulator, setup hitting brk instruction to stop
	brk
