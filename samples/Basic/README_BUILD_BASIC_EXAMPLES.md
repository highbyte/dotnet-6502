# Convert C64 Basic text files to C64 .prg file
To convert a text file containing C64 Basic to an actual .prg file that can be loaded into a C64, use the ´´´petcat´´´command from the VICE emulator.

Example in PowerShell

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sound
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1TriangleScale.prg" -- "PlaySoundVoice1TriangleScale.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1TriangleScale2.prg" -- "PlaySoundVoice1TriangleScale2.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice2SawtoothScale.prg" -- "PlaySoundVoice2SawtoothScale.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice3SawtoothScale.prg" -- "PlaySoundVoice3SawtoothScale.txt"

& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1PulseLab.prg" -- "PlaySoundVoice1PulseLab.txt"
& $PETCAT_APP -w2 -o "Build\PlaySoundVoice1NoiseLab.prg" -- "PlaySoundVoice1NoiseLab.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\HelloWorld.prg" -- "HelloWorld.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\SingleColorSprite.prg" -- "SingleColorSprite.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\MultiColorSprite.prg" -- "MultiColorSprite.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Sprites
& $PETCAT_APP -w2 -o "Build\SingleColorSpriteAndHiResGraphics.prg" -- "SingleColorSpriteAndHiResGraphics.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\ExtendedTextMode.prg" -- "ExtendedTextMode.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\CustomCharset.prg" -- "CustomCharset.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Text
& $PETCAT_APP -w2 -o "Build\RelocateScreenRAM.prg" -- "RelocateScreenRAM.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Graphics
& $PETCAT_APP -w2 -o "Build\HiResSinePlot.prg" -- "HiResSinePlot.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Graphics
& $PETCAT_APP -w2 -o "Build\HiResColor.prg" -- "HiResColor.txt"
´´´

´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
cd C64/Graphics
& $PETCAT_APP -w2 -o "Build\LowResMultiColor.prg" -- "LowResMultiColor.txt"
´´´
