; Simple 6502 test program
; This is a minimal program for testing the debugger
; Syntax compatible with ACME assembler.

*=$c000       ; Start at address $c000

start:
    LDA #$01  ; Load 1 into accumulator
    STA $00   ; Store at zero page address $00
    
    LDX #$05  ; Load 5 into X register
loop:
    DEX       ; Decrement X
    BNE loop  ; Branch if not zero
    
    LDA $00   ; Load from $00
    CLC       ; Clear carry
    ADC #$02  ; Add 2
    
    BRK       ; Break (stop execution)
