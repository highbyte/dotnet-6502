<h1 align="center">Highbyte.DotNet6502.Systems.Commodore64.C64</h1>

# Overview

A partial implementation of a Commodore 64.

Current capabilities
- Run Commodore Basic 2.0 from ROM (user supplied Kernal, Basic, and Chargen ROM files) in text mode.
- Limited VIC2 video chip support 
    - Standard, extended and multi-color character modes
    - Sprites (hi-res & multi-color)
    - IRQ (raster, sprite collision)
    - Background and border color possible to change per raster line
- Limited CIA chip support
    - Keyboard
    - Joystick
    - Timers
    - IRQ
- Limited SID 6581 audio chip support
- WASM and native app UI

# Implementation
Class [```Highbyte.DotNet6502.Systems.Commodore64.C64```](../src/libraries/Highbyte.DotNet6502.Systems/Commodore64/C64.cs)

TODO


# Monitor commands
Additional machine code monitor commands specific to the C64 system.

```
Commands:
  lb     C64 - Load a Commodore Basic 2.0 PRG file from file picker dialog.
  llb    C64 - Load a Commodore Basic 2.0 PRG file from host file system.
  sb     C64 - Save a Commodore Basic 2.0 PRG file to host file system.
```
