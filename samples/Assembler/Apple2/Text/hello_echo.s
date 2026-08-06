; HELLO + keyboard echo for the Apple II Plus (ca65 assembler).
;
; Prints a banner via the Monitor ROM's character output, then echoes every typed key
; until Q returns to the Applesoft BASIC prompt. Text mode only — runs on the minimal
; Apple II Plus system in the DotNet 6502 emulator.
;
; Build (cc65 toolchain):
;   ca65 hello_echo.s -o hello_echo.o
;   ld65 -C ../apple2-b.cfg hello_echo.o -o Build/hello_echo.bin
;
; The output is a DOS 3.3 "B" file: 4-byte header (load address $2000 + length),
; then the code. Load it with the emulator's "Load & start binary", or strip the
; header and use the machine code monitor's "l 2000" + "g".

HOME       := $FC58          ; Monitor ROM: clear screen, cursor to top-left
COUT       := $FDED          ; Monitor ROM: output character in A (high bit set)
BASIC_WARM := $E003          ; Applesoft BASIC warm entry (] prompt, program preserved)

KBD        := $C000          ; keyboard data + strobe (bit 7 set = key waiting)
KBDSTRB    := $C010          ; read/write to clear the keyboard strobe

; Quit key. 'Q' rather than ESC: the Monitor ROM's RDKEY consumes ESC for its
; screen-editing escape sequences, and a host window may intercept it too.
QUIT_KEY   = $D1             ; 'Q' with the high bit set, as the encoder latches it

; The EXEHDR segment holds the DOS 3.3 B-file header; ld65 fills in the values
; from the MAIN memory area defined in apple2-b.cfg.
.segment "EXEHDR"
.import __MAIN_START__, __MAIN_LAST__
.addr   __MAIN_START__                       ; load address
.word   __MAIN_LAST__ - __MAIN_START__       ; length

.segment "CODE"

start:
    jsr HOME

    ldx #0
print_banner:
    lda banner,x
    beq echo_loop
    ora #$80                 ; Apple text output wants the high bit set
    jsr COUT
    inx
    bne print_banner

; Poll the keyboard the way the machine's own firmware does: wait for bit 7 of $C000,
; read the ASCII code, then clear the strobe by touching $C010. Deliberately not the
; Monitor's RDKEY, because that consumes ESC for its screen-editing escape sequences.
echo_loop:
    lda KBD
    bpl echo_loop            ; bit 7 clear = no key waiting
    bit KBDSTRB              ; clear the strobe (the read is the side effect)
    cmp #QUIT_KEY
    beq quit
    jsr COUT                 ; echo the typed character
    jmp echo_loop

quit:
    jmp BASIC_WARM

.segment "RODATA"

banner:
    .byte "HELLO FROM CA65 ASSEMBLY!", $0D
    .byte $0D
    .byte "TYPE KEYS TO ECHO THEM.", $0D
    .byte "PRESS Q TO EXIT TO BASIC.", $0D
    .byte $0D
    .byte $00
