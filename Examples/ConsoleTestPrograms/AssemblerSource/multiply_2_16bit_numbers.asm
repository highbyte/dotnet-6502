;Example based on code from http://www.obelisk.me.uk/6502/maclib.inc and modified for ACME assembler syntax

;VSCode extension VS64 (ACME cross-assembler) will automatially set output path and filename to the .cache directory
;!to "./multiply_2_16bit_numbers.prg"

;code start address
* = $c000

;Input value A. Values is 16-bit unsigned, lowbyte first
INPUT16A = $d000
;Input value B. Value is 16-bit unsigned, lowbyte first
INPUT16B = $d002
;Calculation result store address. Value is 16-bit unsigned, lowbyte first. Any overflow over 16 bits during the calculation is lost.
RESULT16 = $d004


;Define macros
!macro clr16 .mem {
	lda #0
	sta .mem+0
	sta .mem+1
}

!macro mul16 .vla, .vlb, .res {
	+clr16 .res
	ldx #15
.loop	
	+asl16 .res, .res
	+asl16 .vla, .vla
	bcc .next
	+add16 .vlb, .res, .res
.next 
	dex
	bpl .loop
}

!macro asl16 .vla, .res {
	!if (.vla != .res) {
		lda .vla+0
		asl A
		sta .res+0
		lda .vla+1
		rol A
		sta .res+1
	} else {
		asl .vla+0
		rol .vla+1
	}
}

!macro add16 .vla, .vlb, .res {
	!if (.vla != .vlb) {	
		clc
		lda .vla+0
		adc .vlb+0
		sta .res+0
		lda .vla+1
		adc .vlb+1
		sta .res+1
	} else {
		+asl16 .vla, .res
	}
}

;Run calculation
	+mul16 INPUT16A, INPUT16B, RESULT16

;When running code through emulator, a convinent way to end execution is to confiure brk instruction to stop execution.
	brk
