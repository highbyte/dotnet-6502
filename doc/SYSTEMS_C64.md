<h1 align="center">Highbyte.DotNet6502.Systems.Commodore64.C64</h1>

# Overview

A partial implementation of a Commodore 64.

Current capabilities
- Run Commodore Basic 2.0 from ROM (user supplied Kernal, Basic, and Chargen ROM files).
- Limited VIC2 video chip support 
    - Standard, extended and multi-color character modes
    - Standard and multi-color bitmap mode _(newer SkiaRenderer 2/2b in native & WASM, and OpenGL renderer in native only)
    - Sprites (hi-res & multi-color)
    - IRQ (raster, sprite collision)
    - Background and border color possible to per raster line
    - Fine scrolling per raster line (new newer SkiaRenderer 2b in native & WASM only)
- Limited CIA chip support
    - Keyboard
    - Joystick
    - Timers
    - IRQ
- Limited SID 6581 audio chip support
- WASM and native app UI

# C64 programs that works and how to run them

See [`SYSTEMS_C64_COMPATIBLE_PRG.md`](SYSTEMS_C64_COMPATIBLE_PRG.md)

# Implementations

- System logic [`Highbyte.DotNet6502.Systems.Commodore64`](../src/libraries/Highbyte.DotNet6502.Systems.Commodore64)

- Rendering
  - [`Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video`](../src/libraries/Highbyte.DotNet6502.Impl.SilkNet/Commodore64/Video/)
  - [`Highbyte.DotNet6502.Impl.Skia.Commodore64.Video`](../src/libraries/Highbyte.DotNet6502.Impl.Skia/Commodore64/Video/)
  - [`Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video`](../src/libraries/Highbyte.DotNet6502.Impl.SadConsole/Commodore64/Video/)

- Input
  - [`Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input`](../src/libraries/Highbyte.DotNet6502.Impl.SilkNet/Commodore64/Input/)
  - [`Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input`](../src/libraries/Highbyte.DotNet6502.Impl.AspNet/Commodore64/Input/)
  - [`Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input`](../src/libraries/Highbyte.DotNet6502.Impl.SadConsole/Commodore64/Input/)  

- Audio
  - [`Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio`](../src/libraries/Highbyte.DotNet6502.Impl.NAudio/Commodore64/Audio/)
  - [`Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio`](../src/libraries/Highbyte.DotNet6502.Impl.AspNet/Commodore64/Audio/)

TODO


# Monitor commands
Additional machine code monitor commands specific to the C64 system.

```
Commands:
  lb     C64 - Load a Commodore Basic 2.0 PRG file from file picker dialog.
  llb    C64 - Load a Commodore Basic 2.0 PRG file from host file system.
  sb     C64 - Save a Commodore Basic 2.0 PRG file to host file system.
```
