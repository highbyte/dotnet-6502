* = $0801
sysline:
!byte $0b,$08,$01,$00,$9e,$32,$30,$36,$31,$00,$00,$00 ;= SYS 2061
* = $080d ;=2061 (Instead of $0810 not to waste unnecessary bytes)

start:
        LDX #15            ; logical file number
        LDY #8             ; device number
        LDA #15            ; secondary address (command channel)
        JSR $FFBA          ; SETLFS

        LDA #<cmdstr       ; low byte of command string
        LDY #>cmdstr       ; high byte
        JSR $FFBD          ; SETNAM

        JSR $FFC0          ; OPEN

        LDX #15
        JSR $FFC9          ; CLOSE

        RTS

cmdstr:
        !by "I"
        