<h1 align="center">Compatible C64 programs</h1>

A list of applications that seem to work decently with the C64 emulator.

> **Limitations:**<br>
> - The C64 emulator currently lacks support for the C64 disk and tape drives. Therefore programs must be loaded from the emulator menu (or monitor) as **.prg** files from the host OS file system. Also, any loaded .prg file that tries to access the C64 disk or tape drive most likely will not work (hang).
>
> - The video emulation is not cycle exact, and does not cover all tricks possible with the C64 VIC2 video chip. Any advanced app/game/demo may not work as expected.
>
> - There are currently different video renderer implementations that can be selected in the C64 config UI. All renderers supports `Character` mode, most support `sprites`, but only some support `Bitmap` mode (which makes them a bit slower). Bitmap mode may be required by some apps, so the correct renderer must be selected before starting certain apps (see Renderer column in table below).
>
> - The audio emulation is currently not very accurate, so expect especially music to not sound correct.


# Games

| Game               | D/L URL                                        | .D64 -> PRG file            | .prg type  | Renderer (C64 Config menu)  | Comment   |
|--------------------|------------------------------------------------|-----------------------------|------------|-----------------------------|-----------|
| Digiloi            | https://csdb.dk/release/download.php?id=213381 | `digiloi.d64` -> `digiloi64` | Basic      | SkiaSharp*, SilkNetOpenGl   | Character mode. |
| Last Ninja         | https://csdb.dk/release/download.php?id=101848 | `lncro.d64` -> `last ninja/zcs` | Basic      | SkiaSharp2b, SilkNetOpenGl  | Bitmap mode, sprites. |
| Mini Zork          | https://csdb.dk/release/download.php?id=42919  | `Mini-Zork(L+T).d64` -> `mini-zork   /l+t` | Basic      | SkiaSharp*, SilkNetOpenGl, SadConsole  | Character mode (default charset). |
| Rally Speedway     | https://csdb.dk/release/download.php?id=22736  | `jolly_roger_-_rally_speedway.d64` -> `rallyspeedway` | Basic      | SkiaSharp*, SilkNetOpenGl   | Character mode, sprites. |
| Montezuma's Revenge| https://csdb.dk/release/download.php?id=128101 | `Montezuma's Revenge - 1103.d64` -> `montezuma's rev.` | Basic     | SkiaSharp*, SilkNetOpenGl   | Character mode, sprites. |
| Bubble Bobble      | https://csdb.dk/release/download.php?id=187937 | `Bubble Bobble.d64` -> `bubble bobble`  | Basic      | SkiaSharp*, SilkNetOpenGl   | Character mode, sprites. |

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
