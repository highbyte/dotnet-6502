;ACME assembler
;!to "./build/sprite_collision.prg"

;Example below is from https://github.com/C64CD/Collisions-C64/blob/master/collisions.asm
;
; SIMPLE HARDWARE SPRITE COLLISION EXAMPLE
;

; ; Select an output filename
; 		!to "collisions.prg",cbm


; Label assignments
sprite_x	= $50
sprite_dir	= $51

coll_delay	= $52
sprite_coll	= $53
bgnd_coll	= $54


; Add a BASIC startline
		* = $0801
		!word code_start-2
		!byte $40,$00,$9e
		!text "2066"
		!byte $00,$00,$00


; Entry point for the code
		* = $0812

; Stop the interrupts
code_start	sei

; Clear the screen RAM
		ldx #$00
		lda #$20
screen_clear	sta $0400,x
		sta $0500,x
		sta $0600,x
		sta $06e8,x
		inx
		bne screen_clear

; Put the two blobs onto the screen and set their colours
		lda #$51
		sta $04ca
		sta $04e1

		lda #$0e
		sta $d8ca
		sta $d8e1

; Fill the sprite at $3000 with $ff...
		ldx #$00
		lda #$ff
sprite_gen	sta $3000,x
		inx
		cpx #$40
		bne sprite_gen

; ...and set the sprite data pointers
		lda #$c0
		sta $07f8
		sta $07f9

; Set sprite colours
		lda #$01
		sta $d027
		lda #$03
		sta $d028

; Set up the initial sprite X and Y positions
		lda #$50
		sta sprite_x
		sta $d000

		lda #$7d
		sta $d002

		lda #$50
		sta $d001
		sta $d003

; Initialise the remaining labels
		lda #$01
		sta sprite_dir

		lda #$00
		sta coll_delay

; Enable the first two hardware sprites
		lda #$03
		sta $d015


; Main loop - wait for raster line $fc for timing
main_loop	lda $d012
		cmp #$fc
		bne main_loop

; Grab the collision registers
		lda $d01e
		sta sprite_coll

		lda $d01f
		sta bgnd_coll

; Update sprite 0's position
		lda sprite_x
		clc
		adc sprite_dir
		sta sprite_x
		sta $d000

; Check to see if the background collisions need testing
		lda coll_delay
		beq bgnd_coll_check

; No, so decrease the timer and skip over the actual check
		dec coll_delay
		jmp bgnd_no_coll

; Read the background collision register
bgnd_coll_check	lda bgnd_coll
		cmp #$01
		bne bgnd_no_coll

; If there's a collision, reverse direction and set the delay
		ldy #$ff
		lda sprite_dir
		cmp #$01
		beq *+$04
		ldy #$01
		sty sprite_dir

		lda #$14
		sta coll_delay

; Reset sprite 1's Y position for the next pass
		lda $d003
		sec
		sbc #$17
		sta $d003

; See if there's a sprite to sprite collision happening
bgnd_no_coll	lda sprite_coll
		cmp #$03
		bne sprite_no_coll

; The sprites have collided so move sprite 1 down a pixel
		inc $d003

; A small delay to make sure the code only runs once per frame
sprite_no_coll	ldx #$00
		inx
		bne sprite_no_coll+$02

; All of the updates are done, so restart the loop
		jmp main_loop