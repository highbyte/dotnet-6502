<h1 align="center">Compatible C64 programs</h1>

# How extract .prg file
If the download is a .zip (or other compressions) file, start with unzip:ing it to a folder.

If the unzip:ed contents is a .prg file, then it is possible to be loaded directly into the emulator. No more extra steps needed.

If the unzip:ed contents is a .D64 file (which is a C64 disk image file), a .prg file needs to be extracted from the .D64 file. For this purpose the `c1541` command line utility provided by [VICE](https://vice-emu.sourceforge.io/) emulator can be used.
- Install VICE (if not already installed)
- List contents of .D64 image (example)
  `[VICE install path]\bin\c1541.exe -attach "lncro.d64" -list`
- Extract .prg file from .D64 image (example). The last argument is what you want to name the extracted .prg file.
  `[VICE install path]\bin\c1541.exe  -attach "lncro.d64" -read "last ninja/zcs" "last ninja-zcs.prg"`
- In this example, The Last Ninja was extracted from the .D64 image `lncro.d64` to the file `last ninja-zcs.prg`


# How load .prg file
## .prg type Basic
- File must be loaded as a Basic program, from the menu `Load Basic PRG file`, or via the monitor.
- Start by typing `RUN` (and press Enter).
- _The Basic program is most likely a short stub program that starts a machine language program by invoking SYS._

## .prg type Binary 
- File must be loaded as a Binary program (from the menu or monitor).
- It's loaded into a memory address that is specified in the first two bytes of the .prg file (a C64 standard).
- Menu: `Load & start binary PRG file`
  - It's loaded and started automatically.
- Monitor: 
  - See Monitor docs. Relevant commands are `ll [file]` and `g [address]`
