;ACME assembler
;!to "./irq_raster.prg"

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------

WAIT_LINE1 = 150;

BORDER_COLOR_AFTER_VBLANK = $07;
BORDER_COLOR_BAR = $09;
BORDER_COLOR_AFTER_BAR = $08;

;Bit 8 (highest bit) of the current video scan line is stored in bit #7 in this register
SCREEN_CONTROL_REGISTER_1 = 0xd011
;Bits 0-7 the current video scan line bit
SCREEN_RASTER_LINE = 0xd012
;Border color address
SCREEN_BORDER_COLOR_ADDRESS = 0xd020
;Bg color address for entire screen
SCREEN_BACKGROUND_COLOR_ADDRESS = 0xd021

NEXT_IRQ_BORDER_COLOR = 0xfd	; Unused zero page address

;------------------------------------------------------------
;Code start
;------------------------------------------------------------

!macro set_irq .irqhandler, .line, .color {

	; Set next IRQ raster line
	lda #<.line ; Bits 0-7 of current raster line
	sta SCREEN_RASTER_LINE	; $d012
	lda #>.line ; 8th bit of current raster line
	cmp #0
	beq .no_highbit
	;Set bit 7 of $d011, which is the 8th bit of the current raster line
	lda SCREEN_CONTROL_REGISTER_1 ; $d011
	ora #128
	sta SCREEN_CONTROL_REGISTER_1 ; $d011
	jmp .irq_addr_cont
.no_highbit:
	;Clear bit 7 of $d011, which is the 8th bit of the current raster line
	lda SCREEN_CONTROL_REGISTER_1 ; $d011
	and #127
	sta SCREEN_CONTROL_REGISTER_1 ; $d011
.irq_addr_cont:

	; The handler that will be called during the IRQ
	lda #<.irqhandler
	sta $0314
	lda #>.irqhandler
	sta $0315

	; Color to be set during the IRQ
	lda #.color
	sta NEXT_IRQ_BORDER_COLOR
}

Init:
	SEI                  ; set interrupt bit, make the CPU ignore interrupt requests
	LDA #%01111111       ; switch off interrupt signals from CIA-1
	STA $DC0D

	AND $D011            ; clear most significant bit of VIC's raster register
	STA $D011

	LDA $DC0D            ; acknowledge pending interrupts from CIA-1
	LDA $DD0D            ; acknowledge pending interrupts from CIA-2

	; Setup first IRQ to raster line 0
	+set_irq Irq1, 0, BORDER_COLOR_AFTER_VBLANK

	LDA #%00000001       ; enable raster interrupt signals from VIC
	STA $D01A

	CLI                  ; clear interrupt flag, allowing the CPU to respond to interrupt requests
	JMP *

Irq1:
	LDA NEXT_IRQ_BORDER_COLOR
	STA SCREEN_BORDER_COLOR_ADDRESS           ; change border colour to yellow

	; Setup second IRQ to raster line in the middle
	+set_irq Irq2, WAIT_LINE1, BORDER_COLOR_BAR

	ASL $D019            ; acknowledge the interrupt by clearing the VIC's interrupt flag

	JMP $EA31            ; jump into KERNAL's standard interrupt service routine to handle keyboard scan, cursor display etc.	

Irq2:
	LDA NEXT_IRQ_BORDER_COLOR
	STA SCREEN_BORDER_COLOR_ADDRESS           ; change border colour to yellow

	; Setup third IRQ to raster line a few lines after the middle
	+set_irq Irq3, WAIT_LINE1+3, BORDER_COLOR_AFTER_BAR

	ASL $D019            ; acknowledge the interrupt by clearing the VIC's interrupt flag

	JMP $EA31            ; jump into KERNAL's standard interrupt service routine to handle keyboard scan, cursor display etc.

Irq3:
	LDA NEXT_IRQ_BORDER_COLOR
	STA SCREEN_BORDER_COLOR_ADDRESS           ; change border colour to yellow

	; Setup IRQ back to first one
	+set_irq Irq1, 0, BORDER_COLOR_AFTER_VBLANK

	ASL $D019            ; acknowledge the interrupt by clearing the VIC's interrupt flag

	;JMP $EA31            ; jump into KERNAL's standard interrupt service routine to handle keyboard scan, cursor display etc.
	JMP $EA81            ; jump into shorter ROM routine to only restore registers from the stack etc
	