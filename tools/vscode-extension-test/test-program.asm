; Simple 6502 test program
; This is a minimal program for testing the debugger
; Syntax compatible with ca65 assembler.

    .setcpu "6502"
    
    ;.org $c000    ; Set load address, doesn't affect .dbg file? Instead use --start-addr when invoking the assembler/linker?
    
start:
    LDA #$01  ; Load 1 into accumulator
    STA $00   ; Store at zero page address $00
    
    ; Test subroutine call where subroutine is not part of the assembly source.
    ; Generate subroutine at $c100 that multiplies A by 2
    LDA #$0A  ; ASL A opcode
    STA $C100 ; Store at $c100
    LDA #$60  ; RTS opcode
    STA $C101 ; Store at $c101

    ; Jump to the subroutine created above
    JSR $C100
   
    LDX #$05  ; Load 5 into X register
loop:
    DEX       ; Decrement X
    BNE loop  ; Branch if not zero
    
    LDA $00   ; Load from $00
    CLC       ; Clear carry
    ADC #$02  ; Add 2
    
    BRK       ; Break (stop execution)
