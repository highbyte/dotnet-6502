;code start address
* = $c000

;VSCode extension VS64 will automatially set output path and filename to the .cache directory
;!to "./testprogram3.prg"

;----------------------------------------------
;Test SBC overflow.
;----------------------------------------------

	LDA #$FE	; -2
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$7F 	; -127, After SBC, A will be #$7F (+127) (which is incorrect, it should have been -129 which cannot be contained in a signed byte, min -128), and Overflow = 1

	NOP

	LDA #$01	; 1
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$81 	; -127, After SBC, A will be #$80 (-128) (which is incorrect, it should have been +128 which cannot be contained in a signed byte. max +127), and Overflow = 1

	NOP

	LDA #$FE	; -2
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$7E 	; 126, After SBC, A will be #$80 (-128), and Overflow = 0

	NOP

	LDA #$01	; 1
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$82 	; -126, After SBC, A will be #$7F (127) , and Overflow = 0

	NOP

	BRK
