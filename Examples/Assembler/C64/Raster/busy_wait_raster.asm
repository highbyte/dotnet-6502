;ACME assembler
;!to "./busy_wait_raster.prg"

;code start address
* = $c000

;------------------------------------------------------------
;Program settings
;------------------------------------------------------------
; NTSC new, RSEL1 (25 lines)
;WAIT_LINE1 = 0;   // Raster line 0 is within the bottom border...
;WAIT_LINE1 = 11;  // Last NORMALLY VISIBLE line of bottom border
;WAIT_LINE1 = 28;  // First NORMALLY VISIBLE line of top border
;WAIT_LINE1 = 50;  // Last line of top border
;WAIT_LINE1 = 51;  // First line of screen
;WAIT_LINE1 = 250; // Last line of screen
;WAIT_LINE1 = 251; // Fist line of bottom border

; NTSC new, RSEL2 (24 lines)
;WAIT_LINE1 = 0;   // Raster line 0 is within the bottom border...
;WAIT_LINE1 = 11;  // Last NORMALLY VISIBLE line of bottom border
;WAIT_LINE1 = 28;  // First NORMALLY VISIBLE line of top border
;WAIT_LINE1 = 54;  // Last line of top border
;WAIT_LINE1 = 55;  // First line of screen
;WAIT_LINE1 = 246; // Last line of screen
;WAIT_LINE1 = 247; // Fist line of bottom border

; PAL new, RSEL1 (25 lines)
;WAIT_LINE1 = 0;   // Raster line 0 within vertical blank area (not visible)
;WAIT_LINE1 = 16;  // First NORMALLY VISIBLE line of top border
;WAIT_LINE1 = 50;  // Last line of top border
;WAIT_LINE1 = 51;  // First line of screen
;WAIT_LINE1 = 250; // Last line of screen
;WAIT_LINE1 = 251; // Fist line of bottom border
;WAIT_LINE1 = 287; // Last NORMALLY VISIBLE line of bottom border

; WAIT_LINE1 = (262 - 256); // NTSC Last raster line before wraparound back to 0
; WAIT_LINE_HIGHBIT1 = 1

; WAIT_LINE1 = 120;			// NTSC/PAL some line in the screen
; WAIT_LINE_HIGHBIT1 = 0;

; WAIT_LINE1 = 3;			// NTSC some line in the bottom border (before raster starts over to 0, also within bottom border)
; WAIT_LINE_HIGHBIT1 = 1;

; WAIT_LINE1 = (311-256);		// PAL last real(?) line, seen in Vice 64 VIC2 Debug mode
; WAIT_LINE_HIGHBIT1 = 1;

; WAIT_LINE1 = (287 - 256);	// PAL last line of bottom border
; WAIT_LINE_HIGHBIT1 = 1;

WAIT_LINE1 = 150;
WAIT_LINE_HIGHBIT1 = 0;

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

;Macros
!macro wait_vblank {
;Wait for vblank (if raster pos 255 already been waited for, set X to no-zero)
	cpx #0
	bne .wait_vblank2
.wait_vblank1
	bit SCREEN_CONTROL_REGISTER_1
	bpl .wait_vblank1
.wait_vblank2
	bit SCREEN_CONTROL_REGISTER_1
	bmi .wait_vblank2
}

!macro wait_line {
;Wait for line (low byte in A, high byte in X)
.wait_line
	cpx #0
	beq .wait_lower
.wait_higher
	bit SCREEN_CONTROL_REGISTER_1	; Bit 7 is set when raster line is 256 or higher
	bpl .wait_higher					; Bit 7 clear = positive number
.wait_lower
	cmp SCREEN_RASTER_LINE
	bne .wait_lower
}

;------------------------------------------------------------
;Code start
;------------------------------------------------------------

	sei

	; Set 25 (default) line mode by setting bit 3 of D011
	lda SCREEN_CONTROL_REGISTER_1
	ora #$08
	sta SCREEN_CONTROL_REGISTER_1

	; OR Set 24 line mode by clearing bit 3 of D011
	; lda SCREEN_CONTROL_REGISTER_1
	; and #$f7
	; sta SCREEN_CONTROL_REGISTER_1

mainloop:
	ldx #0
	+wait_vblank
	lda #BORDER_COLOR_AFTER_VBLANK
	sta SCREEN_BORDER_COLOR_ADDRESS

	lda #WAIT_LINE1
	ldx #WAIT_LINE_HIGHBIT1
	+wait_line
	lda #BORDER_COLOR_BAR
	sta SCREEN_BORDER_COLOR_ADDRESS	

	lda #WAIT_LINE1+3
	ldx #WAIT_LINE_HIGHBIT1
	+wait_line
	lda #BORDER_COLOR_AFTER_BAR
	sta SCREEN_BORDER_COLOR_ADDRESS	

	jmp mainloop	
