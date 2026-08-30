# Convert C64 Basic text files to C64 .prg file
To convert a text file containing C64 Basic to an actual .prg file that can be loaded into a C64, use the ```petcat```command from the VICE emulator.

Example in PowerShell

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sound
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1TriangleScale.prg" -- "PlaySoundVoice1TriangleScale.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1TriangleScale2.prg" -- "PlaySoundVoice1TriangleScale2.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice2SawtoothScale.prg" -- "PlaySoundVoice2SawtoothScale.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice3SawtoothScale.prg" -- "PlaySoundVoice3SawtoothScale.txt"

& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1PulseLab.prg" -- "PlaySoundVoice1PulseLab.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1NoiseLab.prg" -- "PlaySoundVoice1NoiseLab.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\HelloWorld.prg" -- "HelloWorld.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\SingleColorSprite.prg" -- "SingleColorSprite.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\MultiColorSprite.prg" -- "MultiColorSprite.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\SingleColorSpriteAndHiResGraphics.prg" -- "SingleColorSpriteAndHiResGraphics.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\SingleColorSpriteAndLowResGraphics.prg" -- "SingleColorSpriteAndLowResGraphics.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\ExtendedTextMode.prg" -- "ExtendedTextMode.txt"
```
``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\MultiColorTextMode.prg" -- "MultiColorTextMode.txt"
```


``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\CustomCharset.prg" -- "CustomCharset.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\RelocateScreenRAM.prg" -- "RelocateScreenRAM.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Graphics
& $PETCAT_APP -w2 -o "Build\HiResSinePlot.prg" -- "HiResSinePlot.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Graphics
& $PETCAT_APP -w2 -o "Build\HiResColor.prg" -- "HiResColor.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Graphics
& $PETCAT_APP -w2 -o "Build\LowResMultiColor.prg" -- "LowResMultiColor.txt"
```

``` pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Timer
& $PETCAT_APP -w2 -o "Build\Test_CIA_Timers.prg" -- "Test_CIA_Timers.txt"
```

# Convert VIC-20 Basic text files to VIC-20 .prg file

VIC-20 uses BASIC V2 (same tokens as the C64), but the BASIC program load address is
different. For an unexpanded VIC-20 it is `$1001`. Pass it to `petcat` with `-l 1001`
(no leading `0x` or `$`).

Assuming `petcat` is in your `PATH`:

``` sh
cd VIC20/Text
petcat -w2 -l 1001 -o Build/HelloWorld.prg            -- HelloWorld.txt
petcat -w2 -l 1001 -o Build/BorderBackgroundColors.prg -- BorderBackgroundColors.txt
petcat -w2 -l 1001 -o Build/ReverseVideo.prg          -- ReverseVideo.txt
petcat -w2 -l 1001 -o Build/LowerCaseCharset.prg      -- LowerCaseCharset.txt
petcat -w2 -l 1001 -o Build/MulticolorChars.prg       -- MulticolorChars.txt
petcat -w2 -l 1001 -o Build/ScreenGeometry.prg        -- ScreenGeometry.txt
```

`petcat` tokenizes BASIC V2 source as PETSCII, so the case you type in the `.txt`
file is not a 1:1 match for what appears on a VIC-20 or C64 screen. For readable
text in the default upper/graphics mode, keep quoted source text lowercase. To
demonstrate the VIC-20 lowercase/uppercase character set, switch modes with
`CHR$(14)` and switch back with `CHR$(142)`.

If running an expanded VIC-20 (8K+), use `-l 1201` instead. The
`Build/` subdirectory must exist before running these commands.

# Convert Oric BASIC text files to tokenized tape files

Use the OSDK `Bas2Tap` utility to convert a text listing into the standard Oric
`.tap` format. These instructions assume that the OSDK repository has been cloned to
`~/source/public-repos/osdk`.

Build the native `Bas2Tap` command-line utility first:

``` sh
make -C ~/source/public-repos/osdk/osdk/main/common
make -C ~/source/public-repos/osdk/osdk/main/bas2tap
```

Then build an Oric BASIC sample. BASIC keywords in the source listing must be uppercase:

``` sh
cd Oric/Text
mkdir -p Build
~/source/public-repos/osdk/osdk/main/bas2tap/bas2tap \
  -b2t0 HelloWorld.txt Build/HelloWorld.tap
```

`-b2t0` creates a program that must be started with `RUN` after loading. Use `-b2t1`
instead when the program should run automatically after loading.

To build every `.txt` sample in the current category directory:

``` sh
mkdir -p Build
for source in *.txt; do
  ~/source/public-repos/osdk/osdk/main/bas2tap/bas2tap \
    -b2t0 "$source" "Build/${source%.txt}.tap"
done
```

The resulting `.tap` file contains an Oric tape header followed by tokenized BASIC that
loads at `$0501`. Keep the editable `.txt` source beside the generated file, following the
same `Text/Build/` layout used by the other systems.

# Convert Apple II Basic text files to tokenized Applesoft files

VICE's `petcat` only understands Commodore BASIC dialects — it cannot tokenize
Applesoft. Use **CiderPress II** (`cp2`, a cross-platform .NET CLI) instead. Its
`import` command tokenizes Applesoft text into a disk image, and `extract` writes
the bare tokenized bytes back out (no header; the program always loads at `$0801`).

Assuming `cp2` is in your `PATH` (adjust `$CP2` otherwise):

``` sh
cd Apple2/Text
cp2 cdi work.do 140k dos
cp2 import work.do bas HelloWorld.txt
cp2 extract work.do HELLOWORLD
mv HELLOWORLD Build/HelloWorld.bas
rm work.do
```

``` sh
cd Apple2/Sound
cp2 cdi work.do 140k dos
cp2 import work.do bas PlayNotes.txt
cp2 extract work.do PLAYNOTES
mv PLAYNOTES Build/PlayNotes.bas
rm work.do
```

Notes:

- The intermediate DOS 3.3 disk image is required by `cp2`; it is deleted afterwards.
- `import bas` derives the DOS file name from the source file name (uppercased,
  truncated) — `HelloWorld.txt` becomes `HELLOWORLD`.
- The resulting `.bas` file is bare tokenized Applesoft: exactly the bytes that live
  at `$0801` on a real machine. The emulator's "Load Basic" and the machine code
  monitor's `lb`/`llb` commands expect this format.
- Applesoft is uppercase-only on the Apple II Plus; keep the source text uppercase.

## Why `.txt` for source and `.bas` for the tokenized file

Note that here `.bas` means a **tokenized** program, not BASIC source text — the opposite
of the common PC convention where `.bas` is a source listing. The source is the `.txt`
file, as for the C64 and VIC-20 examples.

The reason is that `.bas` names the Apple II *file type*, not the file's authoring format:
ProDOS file type `$FC` is called **BAS** and means a tokenized Applesoft program, exactly
as Commodore's **PRG** means a tokenized/loadable program. So `HelloWorld.txt` →
`HelloWorld.bas` mirrors the C64's `HelloWorld.txt` → `HelloWorld.prg`.

There is no cross-platform standard for tokenized Applesoft on a modern filesystem, because
the Apple II carries the type as filesystem metadata rather than in the name. For reference,
CiderPress II itself uses neither convention directly:

- `cp2 extract --preserve=naps` writes `HELLOWORLD#fc0801` — the NAPS scheme, encoding the
  ProDOS type (`fc`) and load address (`0801`) in the name. Same family as the `#06xxxx`
  naming seen for extracted ProDOS binaries.
- `cp2 export <img> bas <file>` de-tokenizes back to a listing and writes `.txt`.

`.bas` was chosen over the NAPS form because it works in GUI file-picker filters and as an
embedded resource name, while still naming the correct Apple II file type.
