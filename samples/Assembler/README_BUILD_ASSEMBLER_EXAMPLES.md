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

`BuildAll.ps1` builds these too (it globs `Apple2/**/*.s` with ca65/ld65 in addition
to the ACME `*.asm` loop).

# Build Oric `.s` source files with the OSDK XA assembler

Oric machine-code programs are commonly distributed as `.tap` files. OSDK's `xa`
assembler creates the raw binary and its `header` utility wraps that binary in an
Oric tape header containing the load address, end address, file type, name and
auto-run flag.

These instructions assume that the OSDK repository has been cloned to
`~/source/public-repos/osdk`. Build the native tools first:

``` sh
make -C ~/source/public-repos/osdk/osdk/main/common
make -C ~/source/public-repos/osdk/osdk/main/xa
make -C ~/source/public-repos/osdk/osdk/main/header
```

Build the CB1 VSync raster-bars example at `$0600` and create an auto-running TAP:

``` sh
cd Oric/Raster
mkdir -p Build

~/source/public-repos/osdk/osdk/main/xa/xa \
  -C -W -bt 0x0600 \
  -o Build/vsync_raster_bars.bin \
  -l Build/vsync_raster_bars.labels \
  vsync_raster_bars.s

~/source/public-repos/osdk/osdk/main/header/header \
  -a1 -h1 -b1 -s1 -nRASTERBARS \
  Build/vsync_raster_bars.bin \
  Build/vsync_raster_bars.tap \
  0x0600

rm Build/vsync_raster_bars.bin
```

Build the cable-free Timer 1 raster diagnostic at `$0900`:

``` sh
~/source/public-repos/osdk/osdk/main/xa/xa \
  -C -W -bt 0x0900 \
  -o Build/timer1_raster_bars.bin \
  -l Build/timer1_raster_bars.labels \
  timer1_raster_bars.s

~/source/public-repos/osdk/osdk/main/header/header \
  -a1 -h1 -b1 -s1 -nTIMERBARS \
  Build/timer1_raster_bars.bin \
  Build/timer1_raster_bars.tap \
  0x0900

rm Build/timer1_raster_bars.bin
```

Before loading the sample, enable **CB1 VSync compatibility cable** in the Oric
configuration. Attach `Build/vsync_raster_bars.tap` and enter `CLOAD""`; its tape
header starts the machine-code program automatically. The bar follows a 256-frame
sine-wave path, is painted only while the ULA scans its rows, and is erased again
before the frame ends, so it also exercises the emulator's progressive rasterizer.

`timer1_raster_bars.tap` must be run with **CB1 VSync compatibility cable**
disabled. It uses a free-running VIA Timer 1 period one raster line shorter than
the PAL frame and races cyan/blue paper writes through the text rows. The phase
difference moves the cyan band automatically, making this a direct diagnostic
for progressive scanline rendering without manual calibration.
