; ACME assembler
; color_cycle.asm
;
; Simple VIC-20 machine-code demo for "Load & start binary".
; Cycles the VIC-I background/border color register forever.

* = $1200

VIC_BACKGROUND_BORDER = $900f

start:
    ldx #$00

main_loop:
    lda colors,x
    sta VIC_BACKGROUND_BORDER
    jsr delay
    inx
    cpx #color_count
    bne main_loop

    ldx #$00
    jmp main_loop

delay:
    lda #$20

delay_outer:
    ldy #$ff

delay_inner:
    dey
    bne delay_inner
    sec
    sbc #$01
    bne delay_outer
    rts

color_count = 8
colors:
    ; VIC-I $900F = background (bits 7-4) | reverse (bit 3) | border (bits 2-0)
    !byte $18, $29, $3a, $4b, $5c, $6d, $7e, $8f
