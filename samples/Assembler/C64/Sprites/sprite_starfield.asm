;ACME assembler
;!to "./build/8spritefield.prg"

;Example below is from https://codebase64.org/doku.php?id=base:8_sprite_starfield

;===========================================================================
;Simple sprite starfield by Richard Bayliss
;===========================================================================

sync = $0340
starpos = $0350

			;!to "8spritefield.prg",cbm
			;* = $0900

*=$0800
; encode SYS 2064 ($0810) line
; in BASIC program space
!byte $00 ,$0c, $08, $0a, $00, $9e, $20, $32 
!byte $30, $36, $34, $00, $00, $00, $00, $00

			sei
			jsr $ff81 ; Clear the screen
			lda #$00  ;Black border + screen
			sta $d020
			sta $d021
			lda #$ff
			sta $d015 ;Turn on all sprites
			lda #$00  
			sta $d017 ;No sprite expansion X
			sta $d01b ;Sprites in front of chars
			sta $d01d ;No sprite expansion Y
			ldx #$00
clr2000	                lda #$00
			sta $2000,x ;Fill $2000 with zero
			inx
			bne clr2000
			lda #$01    ;Create a dot for the sprite starfield
			sta $2000
			ldx #$00
setsprs	                lda #$80    ;Sprite object data from $2000-$2080
			sta $07f8,x
			lda #$01    ;All sprites are white
			sta $d027,x
			inx
			cpx #$08    ;Do the sprite creation 8 times
			bne setsprs
			ldx #$00
positions	        lda postable,x ;Read label postable
			sta starpos+0,x ;Create data memory for current sprite position
			inx
			cpx #$10
			bne positions
			
			lda #<irq ;You should know this bit already ;)
			sta $0314
			lda #>irq
			sta $0315
			lda #$00
			sta $d012
			lda #$7f
			sta $dc0d
			lda #$1b
			sta $d011
			lda #$01
			sta $d01a
			cli
mainloop	        lda #$00 ;Synchronize the routines outside IRQ so that all routines run outside IRQ
			sta sync ;correctly
			lda sync
waitsync	        cmp sync
			bne cont
			jmp waitsync
cont		        jsr expdpos     ;Call label xpdpos for sprite position x expansion
			jsr movestars   ;Call label movestars for virtual sprite movement
			jmp mainloop
			
expdpos	                ldx #$00
xpdloop	                lda starpos+1,x ;Read virtual memory from starpos (odd number values)
			sta $d001,x     ;Write memory to the actual sprite y position
			lda starpos+0,x ;Read virtual memory from starpos (odd number values)
			asl
			ror $d010 ;increase the screen limit for sprite x position
			sta $d000,x ;Write memory to the actual sprite x position
			inx
			inx
			cpx #$10
			bne xpdloop
			rts
			
movestars       	ldx #$00
moveloop	        lda starpos+0,x ;Read from data table (starpos)
			clc
			adc starspeed+0,x
			sta starpos+0,x
			inx ; Add 2 to each value of the loop
			inx ;
			cpx #$10 ;Once reached 16 times rts else repeat moveloop
			bne moveloop
			rts
			
irq			inc $d019 ;You should also know this bit already
			lda #$00
			sta $d012
			lda #$01
			sta sync
			jmp $ea31
			
;Data tables for the sprite positions
                             ; x    y
postable
			!byte $00,$38 ;We always keep x as zero, y is changeable
			!byte $00,$40
			!byte $00,$48
			!byte $00,$50
			!byte $00,$58
			!byte $00,$60
			!byte $00,$68
			!byte $00,$70
			!byte $00,$78
			
;Data tables for speed of the moving stars (erm dots)
                             ;x     y
starspeed
			!byte $04,$00 ;Important. Remember that Y should always be zero. X is changable for
			!byte $05,$00 ;varied speeds of the moving stars. :)
			!byte $06,$00
			!byte $07,$00
			!byte $06,$00
			!byte $04,$00
			!byte $07,$00
			!byte $05,$00
			!byte $00,$00