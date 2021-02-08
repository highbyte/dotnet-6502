;code start address
* = $c000

;VSCode extension VS64 will automatially set output path and filename to the .cache directory
;!to "./testprogram3.prg"

;----------------------------------------------
;Test SBC with carry set before
;----------------------------------------------

	LDA #$20
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$10 	; After SBC, A will be #$10 and Carry 1, Negative 0, Zero = 0

	NOP

	LDA #$10
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$20 	; After SBC, A will be #$F0 and Carry 0, Negative 1, Zero = 0

	NOP

	LDA #$10
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$10 	; After SBC, A will be #$00 and Carry 1, Negative 0, Zero = 1

	NOP 

	LDA #$10
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$FF 	; After SBC, A will be #$11 and Carry 0 (because subtracting a neg nr? compare with #$20-#$10 which get Carry 1), Negative 0, Zero = 0

	NOP 

	LDA #$F0
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$FF 	; After SBC, A will be #$F1 and Carry 0 (because subtracting a neg nr? compare with #$20-#$10 which get Carry 1), Negative 1, Zero = 0

	NOP 

	LDA #$F0
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$EF 	; After SBC, A will be #$01 and Carry 1, Negative 0, Zero = 0

	NOP 

	LDA #$F0
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$01 	; After SBC, A will be #$EF and Carry 1, Negative 1, Zero = 0

	NOP 

	LDA #$10
	SEC			; Must set carry flag before doing subtraction. The carry flag is an input to the SBC instruction, set it to 1 perform a subtraction without borrow.
	SBC #$00 	; After SBC, A will be #$10 and Carry 1, Negative 0, Zero = 0

;----------------------------------------------
;Test SBC with carry clear before
;----------------------------------------------

	NOP

	LDA #$20
	CLC			; If carry is clear, #$10 - #$20 will not work correctly
	SBC #$10 	; After SBC, A will be #$0F (not #$10), and Carry 1, Negative 0, Zero 0

	NOP

	LDA #$10
	CLC			; If carry is clear, #$20 - #$10 will not work correctly
	SBC #$20 	; After SBC, A will be #$EF (not #$F0), and Carry 0, Negative 1, Zero 0

	NOP

	LDA #$10
	CLC			; If carry is clear, #$10 - #$10 will not work correctly
	SBC #$10 	; After SBC, A will be #$FF and Carry 0, Negative 1, Zero 0

	NOP

	LDA #$10
	CLC			; If carry is clear before subtracting a negative number, #$10 - #$FF will not work correctly
	SBC #$FF 	; After SBC, A will be #$10 (should have been #$11) and Carry 0, Negative 0, Zero = 0

	NOP 

	LDA #$F0
	CLC			; If carry is clear before subtracting a negative number, #$F0 - #$FF will not work correctly
	SBC #$FF 	; After SBC, A will be #$F0 and Carry 0, Negative 1, Zero = 0

	NOP 

	LDA #$F0
	CLC			; If carry is clear before subtracting a negative number, #$F0 - #$EF will not work correctly
	SBC #$EF 	; After SBC, A will be #$00 and Carry 1 , Negative 0, Zero = 1

	NOP 

	LDA #$F0
	CLC			; If carry is clear before subtracting a negative number, #$F0 - #01 will not work correctly
	SBC #$01 	; After SBC, A will be #$EE and Carry 0, Negative 1, Zero = 0

	NOP 

	LDA #$10
	CLC			; If carry is clear before subtracting a negative number, #$10 - #$00 will not work correctly
	SBC #$00 	; After SBC, A will be #$=0F (should have been #$10) and Carry 1, Negative 0, Zero = 0

	NOP

	BRK
