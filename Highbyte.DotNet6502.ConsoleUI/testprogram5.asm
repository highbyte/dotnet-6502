;code start address
* = $c000

;VSCode extension VS64 will automatially set output path and filename to the .cache directory
;!to "./testprogram5.prg"

;----------------------------------------------
;Test SBC 0 - (-1) = +1
;With different carry set before
;----------------------------------------------

	LDA #$00
	CLC
	SBC #$ff

	NOP

	LDA #$00
	SEC
	SBC #$ff