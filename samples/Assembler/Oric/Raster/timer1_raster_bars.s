; Oric Atmos Timer 1 raster-bar diagnostic (no CB1 cable)
; -------------------------------------------------------
; Target: Oric Atmos 48K, PAL, loaded at $0900.
;
; This diagnostic deliberately does not read VIA CB1. It adapts the software
; synchronization technique used by Oricium's calibration screen:
;
;   1. VIA Timer 1 free-runs slightly faster than the 19,968-cycle PAL frame.
;   2. A fast cyan-paper sweep races down the first column of the text screen.
;   3. A slower blue-paper sweep follows and restores memory in the same frame.
;   4. The one-scanline period difference moves the visible band through every
;      possible phase, including phases that began in vertical blanking.
;
; At the end of each iteration all row attributes are blue. A renderer that
; only snapshots memory after a frame therefore cannot display the cyan band;
; a progressive raster renderer can. No VSync compatibility cable is needed.

#define VIA_T1_COUNTER_LOW      $0304
#define VIA_T1_COUNTER_HIGH     $0305
#define VIA_ACR                 $030b
#define VIA_IFR                 $030d
#define VIA_IER                 $030e

#define VIA_IFR_TIMER1          $40

#define TEXT_SCREEN             $bb80
#define TEXT_SCREEN_END_HIGH    $c0

#define ATTR_PAPER_BLUE         $14
#define ATTR_PAPER_CYAN         $16
#define ATTR_INK_WHITE          $07
#define SPACE                   $20

; One PAL raster line shorter than the 19,968-cycle frame. Via6522 counts
; latch+1 cycles, so 19,903 produces a 19,904-cycle period.
#define TIMER_PERIOD            19903

#define SCREEN_POINTER          $50

    .text
    *=$0900

Start
    sei

    ; Disable interrupt delivery; this program polls Timer 1's IFR flag.
    lda #$7f
    sta VIA_IER
    sta VIA_IFR

    jsr PrepareScreen
    jsr WriteCaptions

    ; Timer 1 continuous mode. Writing the counter high byte transfers the
    ; latch into the counter and starts it.
    lda VIA_ACR
    ora #$40
    sta VIA_ACR
    lda #<TIMER_PERIOD
    sta VIA_T1_COUNTER_LOW
    lda #>TIMER_PERIOD
    sta VIA_T1_COUNTER_HIGH

FrameLoop
    jsr WaitForTimer1

    lda #ATTR_PAPER_CYAN
    ldx #1
    jsr SweepPaper

    lda #ATTR_PAPER_BLUE
    ldx #18
    jsr SweepPaper
    jmp FrameLoop

WaitForTimer1
    lda VIA_IFR
    and #VIA_IFR_TIMER1
    beq WaitForTimer1
    ; Reading T1C-L clears the flag without disturbing continuous mode.
    lda VIA_T1_COUNTER_LOW
    rts

; Fill 28 text rows with blue paper, white ink and spaces.
PrepareScreen
    lda #<TEXT_SCREEN
    sta SCREEN_POINTER
    lda #>TEXT_SCREEN
    sta SCREEN_POINTER+1
    ldx #28
PrepareRow
    ldy #0
    lda #ATTR_PAPER_BLUE
    sta (SCREEN_POINTER),y
    iny
    lda #ATTR_INK_WHITE
    sta (SCREEN_POINTER),y
    iny
    lda #SPACE
PrepareRowByte
    sta (SCREEN_POINTER),y
    iny
    cpy #40
    bne PrepareRowByte
    jsr Add40ToScreenPointer
    dex
    bne PrepareRow
    rts

WriteCaptions
    ldx #0
WriteCaptionLine1
    lda CaptionLine1,x
    beq WriteCaptionLine2Start
    sta TEXT_SCREEN+42,x
    inx
    bne WriteCaptionLine1
WriteCaptionLine2Start
    ldx #0
WriteCaptionLine2
    lda CaptionLine2,x
    beq WriteCaptionLine3Start
    sta TEXT_SCREEN+82,x
    inx
    bne WriteCaptionLine2
WriteCaptionLine3Start
    ldx #0
WriteCaptionLine3
    lda CaptionLine3,x
    beq WriteCaptionDone
    sta TEXT_SCREEN+1042,x
    inx
    bne WriteCaptionLine3
WriteCaptionDone
    rts

; Write a paper attribute to the first column of each row. A is the colour;
; X controls the per-row delay. Self-modifying immediates keep the inner loop
; close to the timing structure of Oricium's calibration routine.
SweepPaper
    sta SweepColour+1
    stx SweepDelay+1
    lda #<TEXT_SCREEN
    sta SCREEN_POINTER
    lda #>TEXT_SCREEN
    sta SCREEN_POINTER+1
    ldy #0
    clc
SweepRow
SweepColour
    lda #ATTR_PAPER_BLUE
    sta (SCREEN_POINTER),y
SweepDelay
    ldx #1
SweepDelayLoop
    dex
    bne SweepDelayLoop
    tya
    adc #40
    tay
    bcc SweepRow
    lda #0
    adc SCREEN_POINTER+1
    sta SCREEN_POINTER+1
    cmp #TEXT_SCREEN_END_HIGH
    bcc SweepRow
    rts

Add40ToScreenPointer
    clc
    lda SCREEN_POINTER
    adc #40
    sta SCREEN_POINTER
    bcc Add40Done
    inc SCREEN_POINTER+1
Add40Done
    rts

CaptionLine1
    .asc "TIMER 1 RASTER BARS - NO CB1",0
CaptionLine2
    .asc "CYAN BAND PROVES PROGRESSIVE DRAWING",0
CaptionLine3
    .asc "PHASE SWEEPS THROUGH VERTICAL BLANK",0
