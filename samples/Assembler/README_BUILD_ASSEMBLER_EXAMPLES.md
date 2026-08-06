# Build .asm source files to .prg binaries with ACME cross assembler

Syntax to compile with ACME

`acme -f cbm -o build\[sourcefile].prg [sourcefile].asm`

Build all examples (PowerShell)
``` pwsh
.\BuildAll.ps1
```

Other examples in PowerShell

``` pwsh
cd Generic
$ACME_APP = "c:\Users\highb\Documents\C64\ACME\acme.exe"
& $ACME_APP -f cbm -o build\hostinteraction_scroll_text_and_cycle_colors.prg hostinteraction_scroll_text_and_cycle_colors.asm
```

``` pwsh
cd Generic
$ACME_APP = "c:\Users\highb\Documents\C64\ACME\acme.exe"
& $ACME_APP -f cbm -o build\hostinteraction_scroll_text_and_cycle_colors.prg -r build\hostinteraction_scroll_text_and_cycle_colors.report --vicelabels build\hostinteraction_scroll_text_and_cycle_colors.labels hostinteraction_scroll_text_and_cycle_colors.asm
```

``` pwsh
cd C64/Audio
$ACME_APP = "c:\Users\highb\Documents\C64\ACME\acme.exe"
& $ACME_APP -f cbm -o build\irqmusplr.prg -r build\irqmusplr.report --vicelabels build\irqmusplr.labels irqmusplr.asm
```

# Build Apple II .s source files with ca65 (cc65 toolchain)

New Apple II examples use the **cc65 toolchain** (`ca65` assembler + `ld65` linker)
instead of ACME, and the `.s` file extension (so the ACME build loop ignores them).

The shared linker config `Apple2/apple2-b.cfg` emits a DOS 3.3 "B" file: a 4-byte
header (load address + length, little endian) followed by the code — the layout
`BLOAD`/`BRUN` use, and the layout the emulator's "Load & start binary" expects.
Default load address is `$2000` (override with `ld65 --start-addr`).

Assuming `ca65`/`ld65` are in your `PATH`:

``` sh
cd Apple2/Text
ca65 hello_echo.s -o Build/hello_echo.o
ld65 -C ../apple2-b.cfg Build/hello_echo.o -o Build/hello_echo.bin
rm Build/hello_echo.o
```

`BuildAll.ps1` builds these too (it globs `*.s` with ca65/ld65 in addition to the
ACME `*.asm` loop).
