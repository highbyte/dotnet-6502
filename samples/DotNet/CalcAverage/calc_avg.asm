;Calculate the average of two values stored in memory locations, and store the result in another memory location.
;Code written in 6502 assembler using ACME cross assembler syntax.
;Assemble with:
;  acme -f cbm -o calc_avg.prg calc_avg.asm

;code load address
* = $c000

;start of code
	lda $d000
	clc
	adc $d001
	ror
	sta $d002
;In emulator, setup stop on hitting brk instruction.
	brk
