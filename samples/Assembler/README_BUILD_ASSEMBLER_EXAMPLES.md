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
