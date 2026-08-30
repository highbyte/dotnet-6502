; Oric Atmos CB1 VSync raster bars
; --------------------------------
; Target: Oric Atmos 48K, PAL, loaded at $0600.
;
; This is a real race-the-beam effect rather than a static colour pattern:
;
;   1. The optional RGB-sync-to-cassette-input cable supplies a frame edge on
;      VIA CB1.
;   2. VIA Timer 1 delays until shortly before the moving bar reaches the ULA.
;   3. Eight paper-colour attributes are written into upcoming HIRES rows.
;   4. After those rows have been scanned, the attributes are restored to blue.
;
; At the end of every frame video memory contains only the blue background. A
; renderer that samples the whole screen after the frame will therefore miss
; the bars; a real Oric ULA, and the emulator's progressive rasterizer, sees
; the temporary values while scanning the affected rows.
;
; Enable "CB1 VSync compatibility cable" in the Oric configuration before
; loading this TAP. The program deliberately waits for that hardware signal.

#define VIA_ORB                 $0300
#define VIA_T1_COUNTER_LOW      $0304
#define VIA_T1_COUNTER_HIGH     $0305
#define VIA_ACR                 $030b
#define VIA_PCR                 $030c
#define VIA_IFR                 $030d
#define VIA_IER                 $030e

#define VIA_IFR_CB1             $10
#define VIA_IFR_TIMER1          $40

#define HIRES_BASE              $a000
#define BOTTOM_TEXT             $bf68
#define VIDEO_MODE_LATCH        $bfdf
#define TEXT_STANDARD_CHARSET   $b400
#define HIRES_STANDARD_CHARSET  $9800

#define ATTR_PAPER_BLACK        $10
#define ATTR_PAPER_BLUE         $14
#define ATTR_INK_WHITE          $07
#define ATTR_HIRES_50HZ         $1e
#define EMPTY_HIRES_PIXELS      $40

#define BAR_HEIGHT              8

; PAL frame timing is 64 cycles per scanline. The CB1 falling edge occurs 12
; cycles after the frame boundary and the visible region starts at line 44.
; Start painting eight lines early; PaintBar takes roughly six scanlines.
#define FIRST_BAR_DELAY         (44*64-12-8*64)
#define ERASE_DELAY             (18*64)

; Scratch locations in zero page. IRQs are disabled while the demo runs.
#define BAR_POINTER             $50
#define WORK_POINTER            $52

    .text
    *=$0600

Start
    sei

    ; Disable VIA interrupt delivery while retaining IFR flags for polling.
    lda #$7f
    sta VIA_IER
    sta VIA_IFR

    ; Timer 1 one-shot mode and CB1 active on the falling edge.
    lda VIA_ACR
    and #$bf
    sta VIA_ACR
    lda VIA_PCR
    and #$ef
    sta VIA_PCR

    ; The ROM HIRES routine normally copies the standard character set to
    ; $9800. This sample enters HIRES directly, so preserve the font first;
    ; otherwise the bottom three text rows render invalid striped glyphs.
    jsr CopyHiresCharset
    jsr ClearHires
    jsr SetBluePaperRows
    jsr WriteCaption

    ; A HIRES attribute in the final display cell takes effect when the ULA
    ; wraps to the next frame, leaving the bottom three rows in text mode.
    lda #ATTR_HIRES_50HZ
    sta VIDEO_MODE_LATCH

    lda #0
    sta SineIndex
    jsr SetBarPositionFromSine

FrameLoop
    jsr WaitForVSync

    ; Timer 1 positions the update relative to the CB1 frame edge.
    lda BarDelayLow
    sta VIA_T1_COUNTER_LOW
    lda BarDelayHigh
    sta VIA_T1_COUNTER_HIGH
    jsr WaitForTimer1

    ; Count from the paint point until safely after all eight rows have been
    ; fetched. The paint routine runs while this timer is already counting.
    lda #<ERASE_DELAY
    sta VIA_T1_COUNTER_LOW
    lda #>ERASE_DELAY
    sta VIA_T1_COUNTER_HIGH
    jsr PaintBar
    jsr WaitForTimer1
    jsr EraseBar

    jsr AdvanceBar
    jmp FrameLoop

; Copy the 1 KB standard text character set into the location selected by the
; ULA for the three text rows below a HIRES display. ClearHires subsequently
; overwrites the original $B400 copy because it lies inside bitmap memory.
CopyHiresCharset
    lda #<TEXT_STANDARD_CHARSET
    sta BAR_POINTER
    lda #>TEXT_STANDARD_CHARSET
    sta BAR_POINTER+1
    lda #<HIRES_STANDARD_CHARSET
    sta WORK_POINTER
    lda #>HIRES_STANDARD_CHARSET
    sta WORK_POINTER+1
    ldx #4
CopyCharsetPage
    ldy #0
CopyCharsetByte
    lda (BAR_POINTER),y
    sta (WORK_POINTER),y
    iny
    bne CopyCharsetByte
    inc BAR_POINTER+1
    inc WORK_POINTER+1
    dex
    bne CopyCharsetPage
    rts

; Clear the 8000-byte, 200x40 HIRES bitmap to empty six-pixel data bytes.
ClearHires
    lda #<HIRES_BASE
    sta WORK_POINTER
    lda #>HIRES_BASE
    sta WORK_POINTER+1
    lda #EMPTY_HIRES_PIXELS
    ldx #31
ClearPage
    ldy #0
ClearPageByte
    sta (WORK_POINTER),y
    iny
    bne ClearPageByte
    inc WORK_POINTER+1
    dex
    bne ClearPage

    ; $A000-$BEFF is 31 pages; finish the first 64 bytes of page $BF.
    ldy #0
ClearTailByte
    sta (WORK_POINTER),y
    iny
    cpy #64
    bne ClearTailByte
    rts

; Put a blue paper attribute at the beginning of every bitmap row.
SetBluePaperRows
    lda #<HIRES_BASE
    sta WORK_POINTER
    lda #>HIRES_BASE
    sta WORK_POINTER+1
    ldx #200
SetBlueRow
    ldy #0
    lda #ATTR_PAPER_BLUE
    sta (WORK_POINTER),y
    jsr Add40ToWorkPointer
    dex
    bne SetBlueRow
    rts

WriteCaption
    lda #" "
    ldx #119
ClearCaption
    sta BOTTOM_TEXT,x
    dex
    bpl ClearCaption

    lda #ATTR_PAPER_BLACK
    sta BOTTOM_TEXT
    sta BOTTOM_TEXT+40
    sta BOTTOM_TEXT+80
    lda #ATTR_INK_WHITE
    sta BOTTOM_TEXT+1
    sta BOTTOM_TEXT+41
    sta BOTTOM_TEXT+81

    ldx #0
WriteCaptionLine1
    lda CaptionLine1,x
    beq WriteCaptionLine2Start
    sta BOTTOM_TEXT+2,x
    inx
    bne WriteCaptionLine1
WriteCaptionLine2Start
    ldx #0
WriteCaptionLine2
    lda CaptionLine2,x
    beq WriteCaptionLine3Start
    sta BOTTOM_TEXT+42,x
    inx
    bne WriteCaptionLine2
WriteCaptionLine3Start
    ldx #0
WriteCaptionLine3
    lda CaptionLine3,x
    beq CaptionDone
    sta BOTTOM_TEXT+82,x
    inx
    bne WriteCaptionLine3
CaptionDone
    rts

WaitForVSync
    ; Reading ORB clears a previously latched CB1 transition.
    lda VIA_ORB
WaitForVSyncLoop
    lda VIA_IFR
    and #VIA_IFR_CB1
    beq WaitForVSyncLoop
    rts

WaitForTimer1
    lda VIA_IFR
    and #VIA_IFR_TIMER1
    beq WaitForTimer1
    rts

PaintBar
    lda BAR_POINTER
    sta WORK_POINTER
    lda BAR_POINTER+1
    sta WORK_POINTER+1
    ldx #0
PaintBarRow
    ldy #0
    lda BarColours,x
    sta (WORK_POINTER),y
    jsr Add40ToWorkPointer
    inx
    cpx #BAR_HEIGHT
    bne PaintBarRow
    rts

EraseBar
    lda BAR_POINTER
    sta WORK_POINTER
    lda BAR_POINTER+1
    sta WORK_POINTER+1
    ldx #BAR_HEIGHT
EraseBarRow
    ldy #0
    lda #ATTR_PAPER_BLUE
    sta (WORK_POINTER),y
    jsr Add40ToWorkPointer
    dex
    bne EraseBarRow
    rts

AdvanceBar
    inc SineIndex

SetBarPositionFromSine
    ldx SineIndex
    lda SineTable,x
    sta BarTopLine

    ; BAR_POINTER = HIRES_BASE + BarTopLine * 40. Keep line*8 in
    ; BAR_POINTER, shift WORK_POINTER twice more to line*32, then add them.
    sta WORK_POINTER
    lda #0
    sta WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    lda WORK_POINTER
    sta BAR_POINTER
    lda WORK_POINTER+1
    sta BAR_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    clc
    lda BAR_POINTER
    adc WORK_POINTER
    sta BAR_POINTER
    lda BAR_POINTER+1
    adc WORK_POINTER+1
    clc
    adc #>HIRES_BASE
    sta BAR_POINTER+1

    ; BarDelay = FIRST_BAR_DELAY + BarTopLine * 64 cycles.
    lda BarTopLine
    sta WORK_POINTER
    lda #0
    sta WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    asl WORK_POINTER
    rol WORK_POINTER+1
    clc
    lda WORK_POINTER
    adc #<FIRST_BAR_DELAY
    sta BarDelayLow
    lda WORK_POINTER+1
    adc #>FIRST_BAR_DELAY
    sta BarDelayHigh
    rts

Add40ToWorkPointer
    clc
    lda WORK_POINTER
    adc #40
    sta WORK_POINTER
    bcc Add40Done
    inc WORK_POINTER+1
Add40Done
    rts

BarColours
    .byt $11,$13,$12,$16,$14,$15,$11,$17

; One 256-frame vertical cycle, centered at line 96 with an 88-line radius.
; Values stay within 8-184, keeping the complete eight-line bar in HIRES.
SineTable
    .byt 96,98,100,102,105,107,109,111,113,115,117,119,122,124,126,128
    .byt 130,132,134,136,137,139,141,143,145,147,148,150,152,153,155,157
    .byt 158,160,161,163,164,165,167,168,169,170,171,173,174,175,176,176
    .byt 177,178,179,180,180,181,181,182,182,183,183,183,184,184,184,184
    .byt 184,184,184,184,184,183,183,183,182,182,181,181,180,180,179,178
    .byt 177,176,176,175,174,173,171,170,169,168,167,165,164,163,161,160
    .byt 158,157,155,153,152,150,148,147,145,143,141,139,137,136,134,132
    .byt 130,128,126,124,122,119,117,115,113,111,109,107,105,102,100,98
    .byt 96,94,92,90,87,85,83,81,79,77,75,73,70,68,66,64
    .byt 62,60,58,56,55,53,51,49,47,45,44,42,40,39,37,35
    .byt 34,32,31,29,28,27,25,24,23,22,21,19,18,17,16,16
    .byt 15,14,13,12,12,11,11,10,10,9,9,9,8,8,8,8
    .byt 8,8,8,8,8,9,9,9,10,10,11,11,12,12,13,14
    .byt 15,16,16,17,18,19,21,22,23,24,25,27,28,29,31,32
    .byt 34,35,37,39,40,42,44,45,47,49,51,53,55,56,58,60
    .byt 62,64,66,68,70,73,75,77,79,81,83,85,87,90,92,94

CaptionLine1
    .asc "CB1 VSYNC RASTER BARS",0
CaptionLine2
    .asc "TEMPORARY VIDEO MEMORY WRITES",0
CaptionLine3
    .asc "STOP EMULATOR TO EXIT",0

BarTopLine
    .byt 0
SineIndex
    .byt 0
BarDelayLow
    .byt <FIRST_BAR_DELAY
BarDelayHigh
    .byt >FIRST_BAR_DELAY
