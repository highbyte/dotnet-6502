* = $0801
sysline:
!byte $0b,$08,$01,$00,$9e,$32,$30,$36,$31,$00,$00,$00 ;= SYS 2061
* = $080d ;=2061 (Instead of $0810 not to waste unnecessary bytes)

DD00    = $DD00
ATN     = %00001000
CLK     = %00010000
DATA    = %00100000

start:
        SEI                     ; disable IRQs

        LDA DD00
        AND #%00000111          ; preserve VIC-II bank bits (0–2)
        STA vicBankBase

        ; === Send LISTEN 8 ===
        LDA #$28
        JSR SendByteWithATN

        ; ; === Send secondary address 15 ===
        ; LDA #$6F
        ; JSR SendByte

        ; ; === Send ASCII "I" ===
        ; LDA #'I'
        ; JSR SendByte

        ; ; === Send UNLISTEN ===
        ; LDA #$3F
        ; JSR SendByte

        ; === Release all lines ===
        LDA vicBankBase
        STA DD00

        CLI
        RTS

; -----------------------
; Send a byte with ATN asserted
SendByteWithATN:
        LDA vicBankBase
        ORA #ATN
        STA DD00                ; drive ATN low
        JMP SendByteCommon

; -----------------------
; Send a byte (after ATN)
SendByte:
        LDA vicBankBase
        STA DD00                ; release ATN
SendByteCommon:
        LDY #8
        LDA byteToSend
        STA sendBuffer        ; shift copy (working byte)

SendBitLoop:
        LDA vicBankBase
        STA portValue         ; prepare clean copy

        LDA sendBuffer
        ASL
        STA sendBuffer        ; shift left, result stays
        BCC BitIs0
        LDA portValue
        ORA #DATA             ; set DATA bit high
        STA portValue
BitIs0:
        ; Pull CLK low (bit 4 = 0)
        AND #%11101111        ; make sure CLK bit is 0
        STA DD00

        JSR Wait

        ; Release CLK (bit 4 = 1)
        LDA portValue
        ORA #CLK
        STA DD00

        JSR Wait

        DEY
        BNE SendBitLoop

        RTS

; -----------------------
Wait:
        LDX #50
WaitLoop:
        DEX
        BNE WaitLoop
        RTS
        
; -----------------------
vicBankBase: !by 0       ; preserved VIC bank bits
byteToSend:  !by 0       ; value to send
sendBuffer:  !by 0       ; shifting copy
portValue:   !by 0       ; prepared DD00 output
