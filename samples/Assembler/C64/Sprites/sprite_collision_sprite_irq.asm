;ACME assembler
;!to "./build/sprite_collision_sprite_irq.prg"

; Simple sprite to srpite collision detection with IRQ, changes background color when collision is detected
; Can be used when running basic programs for testing.
* = $c000

Init:
	SEI                  ; set interrupt bit, make the CPU ignore interrupt requests

	LDA $DC0D            ; acknowledge pending interrupts from CIA-1
	LDA $DD0D            ; acknowledge pending interrupts from CIA-2

	; The handler that will be called during the IRQ
	lda #<spritecollisionirqhandler
	sta $0314
	lda #>spritecollisionirqhandler
	sta $0315

	; Enable sprite to sprite interrupt signals from VIC
	LDA #%00000100
	STA $D01A

	CLI                  ; clear interrupt flag, allowing the CPU to respond to interrupt requests
	RTS

spritecollisionirqhandler:
	; LDA $D019			; Check if the IRQ source is sprite to sprite collision
	; AND #%00000100	;There can be other IRQ sources running at the same time (for example if we run it together with Basic)
	; BEQ .no_collision
	LDA $D01E			; Read sprite to sprite collision register. Can be inspected to see which sprite(s) collided with other sprite(s). Will be cleared by reading.
	CMP #0
	BEQ .no_collision
	; TAX					; Store the value in X register for later use
	; TXA					; Restore sprite to sprite collision register value from X register

	INC $D020			; Change border color

	LDA $D019			; Acknowledge the specific sprite to sprite IRQ by writing 1 to its interrupt flag
	ORA #%00000100
	STA $D019
	jmp .exit_irq

.no_collision:
	ASL $D019          ; TODO: Is this needed here: acknowledge the interrupt by clearing the VIC's interrupt flag

.exit_irq:
	JMP $EA31            ; jump into KERNAL's standard interrupt service routine to handle keyboard scan, cursor display etc.
	;JMP $EA81            ; jump into shorter ROM routine to only restore registers from the stack etc
