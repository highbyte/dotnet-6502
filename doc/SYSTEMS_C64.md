<h1 align="center">Highbyte.DotNet6502.Systems.Commodore64.C64</h1>

# Overview

A partial implementation of a Commodore 64.

Current capabilities
- Run Commodore Basic 2.0 from ROM (user supplied Kernal, Basic, and Chargen ROM files) in text mode.
- Limited VIC2 video (and timer) chip support (WASM and native)
- Limited SID 6581 audio chip support (WASM and native)

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
