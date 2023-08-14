; Example by https://github.com/wizofwor/
; Below code copied from :https://github.com/wizofwor/C64-assembly-examples/blob/master/tutorials/raster-effects/color-bar-02.asm


;!to "build/color-raster-02.prg",cbm

; * = $0801                               ; BASIC start address (#2049)
; !byte $0d,$08,$dc,$07,$9e,$20,$34,$39   ; BASIC loader to start at $c000...
; !byte $31,$35,$32,$00,$00,$00           ; puts BASIC line 2012 SYS 49152

* = $c000
COUNTER	= $02

	sei
main	
	ldx COUNTER		
	lda sinusTable,x	;Grab new rasterline value  
rasterwait
	cmp $D012			;from the table and wait
    bne rasterwait		;for raster the line

	ldy #10				;Loose time to hide the
idle1	
	dey					;flickering at the beginning 
	bne idle1			;of the effect

;------------------------------------------------------------------
; Main Loop to print raster bars
;------------------------------------------------------------------
	ldx #00		
loop	
	lda colorTable,x	;assign background and border
   	sta $d020			;colors
	sta $d021

	ldy delayTable,x	;Loose time to hide the
idle2	
	dey					;flickering at the end
	bne idle2			;of the effect


	inx 		
	cpx #09
	bne loop
;------------------------------------------------------------------
; End of main loop
;------------------------------------------------------------------

	lda #$0e			;Assign default colors
	sta $d020
	lda #$06
	sta $d021
	
	inc COUNTER
	JMP main

colorTable:
!by 09,08,12,13,01,13,12,08,09

delayTable:
!by 08,08,09,08,12,08,08,08,09

sinusTable:
!by 140,143,145,148,151,154,156,159,162,164,167,169,172,175,177,180,182,185,187,190
!by 192,194,197,199,201,204,206,208,210,212,214,216,218,220,222,224,225,227,229,230
!by 232,233,235,236,237,238,240,241,242,243,244,245,245,246,247,247,248,248,249,249
!by 250,250,250,250,250,250,250,250,249,249,249,248,248,247,247,246,245,244,243,242
!by 241,240,239,238,237,235,234,232,231,229,228,226,224,223,221,219,217,215,213,211
!by 209,207,205,202,200,198,196,193,191,188,186,183,181,178,176,173,171,168,166,163
!by 160,158,155,152,149,147,144,141,139,136,133,131,128,125,122,120,117,114,112,109
!by 107,104,102,99,97,94,92,89,87,84,82,80,78,75,73,71,69,67,65,63
!by 61,59,57,56,54,52,51,49,48,46,45,43,42,41,40,39,38,37,36,35
!by 34,33,33,32,32,31,31,31,30,30,30,30,30,30,30,30,31,31,32,32
!by 33,33,34,35,35,36,37,38,39,40,42,43,44,45,47,48,50,51,53,55
!by 56,58,60,62,64,66,68,70,72,74,76,79,81,83,86,88,90,93,95,98
!by 100,103,105,108,111,113,116,118,121,124,126,129,132,135,137,140