<h1 align="center">Compatible C64 programs</h1>

A list of applications that seem to work decently with the [C64 emulator](SYSTEMS_C64.md).

> **Limitations:**<br>
> - The C64 emulator now has **limited support for the 1541 disk drive**. You can attach `.d64` disk images and use the Basic `LOAD` command to load the directory and files. However, only basic directory and file loading is supported. Advanced disk operations, file writing, and copy protection schemes are not supported.
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
| Elite (NTSC)       | https://csdb.dk/release/download.php?id=254875  | `Elite.D64` -> `elite       /tpx` | Basic      | SkiaSharp2b/3, SilkNetOpenGl   | C64 NTSC variant. Bitmap mode.|
| Elite (PAL)        | https://csdb.dk/release/download.php?id=70413  | `.zip` -> `ELITE.D64` -> `elite        [3]` | Basic      | SkiaSharp2b/3, SilkNetOpenGl   | C64 PAL variant. Bitmap mode. Some gfx artifacts. |
| Last Ninja         | https://csdb.dk/release/download.php?id=101848 | `.zip` -> `lncro.d64` -> `last ninja/zcs` | Basic      | SkiaSharp2b/3, SilkNetOpenGl  | Bitmap mode, sprites. |
| Mini Zork          | https://csdb.dk/release/download.php?id=42919  | `Mini-Zork(L+T).d64` -> `mini-zork   /l+t` | Basic      | SkiaSharp*, SilkNetOpenGl, SadConsole  | Character mode (default charset). |
| Rally Speedway     | https://csdb.dk/release/download.php?id=22736  | `jolly_roger_-_rally_speedway.d64` -> `rallyspeedway` | Basic      | SkiaSharp*, SilkNetOpenGl   | Character mode, sprites. |
| Montezuma's Revenge | https://csdb.dk/release/download.php?id=128101 | `.zip` -> `Montezuma's Revenge - 1103.d64` -> `montezuma's rev.` | Basic     | SkiaSharp*, SilkNetOpenGl   | Character mode, sprites. |
| Bubble Bobble      | https://csdb.dk/release/download.php?id=187937 | `Bubble Bobble.d64` -> `bubble bobble`  | Basic      | SkiaSharp*, SilkNetOpenGl   | Character mode, sprites. |


