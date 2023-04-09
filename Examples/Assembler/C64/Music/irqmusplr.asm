;Sid play code from: https://codebase64.org/doku.php?id=base:simple_irq_music_player
;Sid music file from: https://hvsc.c64.org/
;--------------------------------
;JCH, DMC, Whatever IRQ music plr
;================================

;Build with ACME cross assembler.
;
;Build with output and format specified in this file
;!to "irqmusplr.prg",cbm
;acme.exe build/irqmusplr.asm
;
;or specify output and format in command line
;acme.exe -f cbm -o build/irqmusplr.prg irqmusplr.asm

 
;Example for starting with SYS command from Basic
;* = $0810 ;Remember SYS 2064 to enable it

;Example for starting with a pre-build SYS command by entering RUN
* = $0801
sysline:
!byte $0b,$08,$01,$00,$9e,$32,$30,$36,$31,$00,$00,$00 ;= SYS 2061
* = $080d ;=2061 (Instead of $0810 not to waste unnecessary bytes)

             sei
             lda #<irq
             ldx #>irq
             sta $314
             stx $315
             lda #$1b
             ldx #$00
             ldy #$7f 
             sta $d011
             stx $d012
             sty $dc0d
             lda #$01
             sta $d01a
             sta $d019 ; ACK any raster IRQs
             lda #$00
             jsr $1000 ;Initialize SID routine
             cli
hold         jmp hold ;We don't want to do anything else here. :)
                      ; we could also RTS here, when also changing $ea81 to $ea31
irq
             lda #$01
             sta $d019 ; ACK any raster IRQs
             jsr $1003 ;Play the music
             jmp $ea31
            

;.sid files have a header of length $7c bytes. You must remove this header from the sid files before you can place them on $1000 in c64 memory (for sids that should indeed be placed on $1000, which is something like a standard, but not every tune follow this).
;In some assemblers you can skip N number of bytes in the binary files directly, without having to do it with help of a hex editor or a tool such as “dd” or similar. 
;An example for ACME follows:
* = $1000

; Include the file Giana_Mix.sid from the same directory as this .asm file

!binary "Giana_Mix.sid",, $7c+2
;!binary "Great_Giana_Sisters.sid",, $7c+2
;!binary "Raymond.sid",, $7c+2
