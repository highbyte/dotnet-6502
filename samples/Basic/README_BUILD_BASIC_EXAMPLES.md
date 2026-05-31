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
