# Overview of the Apple II system

A minimal implementation of an Apple II Plus.

Core library: `Highbyte.DotNet6502.Systems.Apple2`.

The system's single configuration variant is named `APPLE2PLUS` after the emulated machine
model, following the C64 precedent (`C64NTSC`/`C64PAL`). Future family models that share the
hardware core (the original Integer BASIC Apple II, a PAL Europlus) would be added as further
variants; only a fundamentally different machine (e.g. the 65C816-based IIgs) would warrant a
separate system.

The Apple II is the simplest machine in this project. It has no timer chip and no interrupt
source at all — the Autostart Monitor and Applesoft BASIC poll the keyboard latch directly — so
there is no CIA/VIA equivalent to emulate and no keyboard matrix to scan.

## Current capabilities

- Boot to the Applesoft BASIC `]` prompt from a user-supplied Apple II Plus ROM.
- 48 KB flat RAM at `$0000`–`$BFFF`, no bank switching.
- 12 KB system ROM (Applesoft BASIC + Autostart Monitor) at `$D000`–`$FFFF`.
    - Accepts both a trimmed 12 KB image and the 20 KB `$B000`–`$FFFF` layout that older
      emulator distributions use (the loadable part is the last 12 KB).
- Soft switches at `$C000`–`$C0FF`, decoded on bits 7-4 so each switch covers 16 addresses:
    - `$C000` keyboard data (ASCII in bits 6-0) + strobe (bit 7).
    - `$C010` clears the strobe.
    - `$C050`–`$C057` display-mode switches tracked as state (text/graphics, mixed, page 1/2,
      lo-res/hi-res).
    - `$C030` speaker toggle counted but silent.
- Empty peripheral slots at `$C100`–`$CFFF` read as `$FF`, which makes the Autostart ROM's disk
  scan fail cleanly so it falls through to BASIC.
- 40 &times; 24 text display in the hardware's 7&times;8 character cell (280&times;192 pixels),
  page 1 (`$0400`–`$07FF`) or page 2 (`$0800`–`$0BFF`).
    - Interleaved row addressing: `address = base + (row % 8) * $80 + (row / 8) * $28`.
    - The 8 "screen hole" bytes per 128-byte block are firmware scratch space and are never
      displayed.
    - Normal (`$80`–`$FF`), inverse (`$00`–`$3F`) and flashing (`$40`–`$7F`) video; flashing
      alternates at roughly 2 Hz.
    - Uppercase-only 64-glyph character set, matching the Apple II / II Plus character generator.
- Monochrome display with a selectable phosphor colour (green, white, amber).
- Pixel-exact render path (`Apple2Rasterizer`, the default): draws every cell from the real 5&times;7
  dot patterns in the character generator ROM, in two compositing layers.
- Lightweight glyph command-stream render path (`Apple2VideoCommandStream`) for hosts that draw
  characters rather than pixels. It renders on an 8-pixel grid using a host font, so on a host
  with fixed 8&times;8 cells it overflows the 280-pixel display slightly — prefer the rasterizer
  for a faithful picture.
- Host-agnostic input handling — host key to ASCII with Shift and Control, plus typematic
  auto-repeat.
- CTRL-RESET (warm reset through the reset vector, like the RESET key on the real keyboard)
  on **Ctrl + F12**, the same combo the Virtual ][ emulator uses.
- Avalonia Desktop and Avalonia Browser (WASM) UI, with a configuration dialog for ROM files,
  ROM download, monitor colour, render provider and CPU compatibility profile.

## Not yet implemented

- Graphics modes. The lo-res (GR) and hi-res (HGR) soft switches are tracked but the display
  always renders the text page.
- Audio. `$C030` speaker toggles are counted but not turned into sound.
- Disk II — the controller is software-driven and is a large, separate effort.
- Peripheral slots, cassette, paddles and game-port input, light pen.
- The language card / 16 KB RAM expansion.
- The original, non-Autostart Apple II with Integer BASIC. That is a different ROM set rather
  than different hardware, so it is a plausible later configuration variant.

## ROMs

See [ROMs](roms.md).
