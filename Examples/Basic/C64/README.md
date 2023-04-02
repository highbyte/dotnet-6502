# Convert C64 Basic text files to C64 .prg file
To convert a text file containing C64 Basic to an actual .prg file that can be loaded into a C64, use the ´´´petcat´´´command from the VICE emulator.

Example in PowerShell
´´´ pwsh
$PETCAT_APP = "C:\Users\highb\Documents\C64\VICE\bin\petcat.exe"
& $PETCAT_APP -w2 -o "PlaySound.prg" -- "PlaySound.txt"
& $PETCAT_APP -w2 -o "PlaySound2.prg" -- "PlaySound2.txt"
´´´
