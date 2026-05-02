# Overview of the C64 system

A partial implementation of a Commodore 64.

Core library: [`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md).

## Current capabilities

- Run Commodore Basic 2.0 from ROM (user-supplied Kernal, Basic, and Chargen ROM files).
- Limited VIC2 video chip support (Render provider: Rasterizer)
    - Standard, extended and multi-color character modes
    - Standard and multi-color bitmap mode
    - Sprites (hi-res & multi-color)
    - IRQ (raster, sprite collision)
    - Background and border color possible to set per raster line
    - Fine scrolling per raster line
- Minimal VIC2 video chip support (Render provider: VideoCommands)
    - Standard character mode (normal case only), no custom character set.
- Limited CIA chip support
    - Keyboard
    - Joystick
    - Timers
    - IRQ
- Limited SID 6581 audio chip support.
- **Limited 1541 disk drive support**
    - Attach `.d64` disk images.
    - Load directory and files to the C64 using the Basic `LOAD` command.
- Blazor Browser (WASM), Avalonia Browser (WASM) and Desktop, SilkNetNative Desktop, and SadConsole Desktop UI:s.

## 1541 Disk Drive Support

The C64 emulator now includes limited support for the Commodore 1541 disk drive. You can:

- Attach `.d64` disk image files to the emulator.
- Use the C64's Basic `LOAD` command to load the disk directory and files from the attached disk image.
- Example: `LOAD "$",8` to list the directory, or `LOAD "FILENAME",8` to load a file.

*Note: Only basic directory and file loading is supported. Advanced disk operations, file writing, and copy protection schemes are not supported.*

## Implementations

- System logic [`Highbyte.DotNet6502.Systems.Commodore64`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Commodore64)

- Rendering
    - [`Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.SilkNet/Commodore64/Video/)
    - [`Highbyte.DotNet6502.Impl.Skia.Commodore64.Video`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.Skia/Commodore64/Video/)
    - [`Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.SadConsole/Commodore64/Video/)

- Input
    - [`Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Input`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.SilkNet/Commodore64/Input/)
    - [`Highbyte.DotNet6502.Impl.AspNet.Commodore64.Input`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.AspNet/Commodore64/Input/)
    - [`Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Input`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.SadConsole/Commodore64/Input/)

- Audio
    - [`Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.NAudio/Commodore64/Audio/)
    - [`Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio`](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Impl.AspNet/Commodore64/Audio/)

## Monitor commands

Additional machine code monitor commands specific to the C64 system:

```
Commands:
  lb     C64 - Load a Commodore Basic 2.0 PRG file from file picker dialog.
  llb    C64 - Load a Commodore Basic 2.0 PRG file from host file system.
  sb     C64 - Save a Commodore Basic 2.0 PRG file to host file system.
```

For general monitor commands, see [Monitor library](../../libraries/core/dotnet6502-monitor.md).
