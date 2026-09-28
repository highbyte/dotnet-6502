# Compatible programs

A list of applications that seem to work decently with the [C64 emulator](overview.md).

!!! warning
    You may need a license to be allowed to use C64 apps and games.

!!! note "Limitations"
    - The C64 emulator now has **limited support for the 1541 disk drive**. You can attach `.d64` disk images and use the Basic `LOAD` command to load the directory and files. However, only basic directory and file loading is supported. Advanced disk operations, file writing, and copy protection schemes are not supported.
    - The video emulation is not cycle-exact, and does not cover all tricks possible with the C64 VIC2 video chip. Any advanced app/game/demo may not work as expected.
    - There are currently different video renderer implementations that can be selected in the C64 config UI. All renderers support `Character` mode, most support `sprites`, but only some support `Bitmap` mode (which makes them a bit slower). Bitmap mode may be required by some apps, so the correct renderer must be selected before starting certain apps (see Renderer column in table below).
    - The default sample-based SID audio provider reproduces most C64 music well, but is not
      fully chip-accurate. The legacy command-stream provider is also selectable (low CPU but
      music will sound noticeably wrong). See [C64 audio](libraries.md#audio).

## Games

| Game               | D/L URL                                        | .D64 → PRG file            | .prg type  | Renderer (C64 Config menu)  | Comment   |
|--------------------|------------------------------------------------|-----------------------------|------------|-----------------------------|-----------|
| Digiloi            | <https://csdb.dk/release/download.php?id=213381> | `digiloi.d64` → `digiloi64` | Basic      | Rasterizer, Custom (v1+)  | Character mode. |
| Elite (NTSC)       | <https://csdb.dk/release/download.php?id=254875> | `Elite.D64` → `elite       /tpx` | Basic      | Rasterizer, Custom (v2+), GPU Packet   | C64 NTSC variant. Bitmap mode. |
| Elite (PAL)        | <https://csdb.dk/release/download.php?id=70413> | `.zip` → `ELITE.D64` → `elite        [3]` | Basic      | Rasterizer, Custom (v2+), GPU Packet    | C64 PAL variant. Bitmap mode. Some gfx artifacts. |
| Last Ninja         | <https://csdb.dk/release/download.php?id=101848> | `.zip` → `lncro.d64` → `last ninja/zcs` | Basic      | Rasterizer, Custom (v2+), GPU Packet   | Bitmap mode, sprites. |
| Mini Zork          | <https://csdb.dk/release/download.php?id=42919> | `Mini-Zork(L+T).d64` → `mini-zork   /l+t` | Basic      | Rasterizer, Custom (v1+), GPU Packet, Video Commands   | Character mode (default charset). |
| Rally Speedway     | <https://csdb.dk/release/download.php?id=22736> | `jolly_roger_-_rally_speedway.d64` → `rallyspeedway` | Basic      | Rasterizer, Custom (v1+), GPU Packet  | Character mode, sprites. |
| Montezuma's Revenge | <https://csdb.dk/release/download.php?id=128101> | `.zip` → `Montezuma's Revenge - 1103.d64` → `montezuma's rev.` | Basic     | Rasterizer, Custom (v2+), GPU Packet   | Character mode, sprites. |
| Bubble Bobble      | <https://csdb.dk/release/download.php?id=187937> | `Bubble Bobble.d64` → `bubble bobble`  | Basic      | Rasterizer, Custom (v1+), GPU Packet   | Character mode, sprites. |

For advanced use, see [Useful tools](useful-tools.md) for how to extract PRG files from D64 disk images.

## Demos and intros

| Program | D/L URL | File | Comment |
|---------|---------|------|---------|
| Fairlight Intro (Golden Collection) | <https://csdb.dk/release/download.php?id=221432> | `flt-25.prg` | C64 PAL variant. Character mode with multicolour register changes every second line, per-line background colour bars, a mid-frame charset switch and a hires scroller, all timed by polling the raster register. |
| For Your Sprites Only | <https://csdb.dk/release/download.php?id=245856> | `foryourspritesonly.prg` | C64 PAL variant, per-line sprites switched on. Sprites only: the display is off and the vertical border opened, sprite rows are stretched by rewriting the Y-expand register every line, sprite pointers and colours change per line, raster bars in the border and background. |
| Unfortunate Coincidence | <https://csdb.dk/release/download.php?id=245796> | `unf-coincidence.prg` | C64 PAL variant, per-line sprites switched on. Sprites only: sprite multiplexing with per-line changes of the scroll/mode and memory setup registers. |
| Smooth and Wonders | <https://csdb.dk/release/download.php?id=245318> | `Smooth_And_Wonders.prg` | C64 PAL variant, per-line sprites switched on. Sprites only: full-frame pictures and text on a 384x273 hyperscreen with the display off and the borders opened, drawn with sprites stretched by rewriting the Y-expand register every line and shown as two alternating frames. |
| Krestage 3 | <https://csdb.dk/release/download.php?id=58941> | `KRESTAGE3.D64` → first file | C64 PAL variant, per-line sprites switched on. Two pictures under a scroller, drawn with X-expanded sprites whose expand, multicolour and priority bits are switched while they shift (the sprite split effect), nine sprites on a line and 50-pixel-wide sprites among them; the demo checks the chip for these before it starts. |
| Chars Sucks | <https://csdb.dk/release/download.php?id=244748> | `TRIAD_Charssucks.d64` → first file | C64 PAL variant, per-line sprites switched on. No characters at all: the display is off for the whole frame and the vertical border left open, the logo and the scroller are sprites behind the idle graphics, and the blocks' shading on the two X-expanded sprites is the idle byte of the VIC-II's bank, rewritten twice per line for a few cycles at a time. |
| Robot - Not Human | <https://csdb.dk/release/download.php?id=322645> | `robot - not human.prg` | C64 PAL variant, per-line sprites switched on. A music-synced robot animation with sprites in the side borders and sampled sound. Starts a one-shot CIA timer from a counter of 0 and enables its NMI only afterwards, so it needs the interrupt to be raised when a source is enabled with its flag already set. |
| Party Elk 2 | <https://csdb.dk/release/download.php?id=283419> | `FppScroller.prg` | C64 PAL variant, per-line sprites switched on. A PETSCII logo over an FPP scroller (character rows re-addressed every raster line) that bends through the side borders. Its FPP tables are computed with the undocumented `ARR` opcode, so it needs the `StableUnofficial` CPU profile or higher (the default), and it switches the VIC-II bank in the middle of every scroller line, so the bank change must reach the chip's fetches on the cycle after the write. |

## Online / modem-style software

| Program | Media | SwiftLink requirement | Comment |
| --- | --- | --- | --- |
| Compunet Reborn | `compunet-reborn-live.d64` or `compunet-reborn-live.prg` | SwiftLink at `$DE00`, `NMI`, `HayesModem` mode recommended | Reaches the login prompt reliably on native hosts and is now the primary browser SwiftLink target through the fixed-target WebSocket bridge. |
