;------------------------------------------------------------
initirq:
	LDA #%01111111       ; switch off interrupt signals from CIA-1
	STA $DC0D

	AND $D011            ; clear most significant bit of VIC's raster register
	STA $D011

	LDA $DC0D            ; acknowledge pending interrupts from CIA-1
	LDA $DD0D            ; acknowledge pending interrupts from CIA-2

	; Setup first IRQ to raster line 0
	set_irq vblank_irq, 0

	LDA #%00000001       ; enable raster interrupt signals from VIC
	STA $D01A
	rts
