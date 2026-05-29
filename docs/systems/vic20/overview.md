# Overview of the VIC-20 system

A partial implementation of a Commodore VIC-20.

Core library: [`Highbyte.DotNet6502.Systems.Vic20`](../../libraries/system-specific/vic20.md).

## Current capabilities

- Run VIC-20 Basic from ROM (user-supplied Kernal, Basic, and Chargen ROM files).
- VIC-I character-mode display
    - 22 columns &times; 23 rows, NTSC (60 Hz).
    - Border rendering (5 character columns each side, 1 row top/bottom).
    - Background and border color via VIC-I register `$900F`.
    - 16 background colors (`$900F` high nibble), 8 foreground/border colors (low nibble).
- Two VIA 6522 chips
    - **VIA1** — keyboard matrix scanning, system timer (CA1 raster IRQ from VIC-I).
    - **VIA2** — user port, additional timer.
- VIC-I raster interrupt via VIA1 CA1, used by KERNAL for keyboard scan and cursor blink.
- Host-agnostic command-stream render path (`Vic20VideoCommandStream`).
- PETSCII screen code rendering with glyph-to-Unicode conversion.
- Host-agnostic input handling (VIA-based keyboard matrix).
- Avalonia Desktop and Avalonia Browser (WASM) UI.

## Not yet implemented

- Audio (no SID or other audio chip emulation — the VIC-20 has no SID; the VIC-I has limited built-in sound via `$900A`–`$900E` which is not yet emulated).
- Disk drive support (no VIC-1541 or equivalent).
- Joystick / game port input.
- PAL variant (NTSC only at present).
- RAM expansion (unexpanded memory map only).
- VIC-I bitmap/graphics modes beyond character mode.
- Light pen support.

## Implementation libraries

For the libraries used to render and accept input, see [Libraries](libraries.md).

## Monitor commands

Additional machine code monitor commands specific to the VIC-20 system:

```
Commands:
  lv     VIC-20 - Load a VIC-20 PRG file from file picker dialog.
  llv    VIC-20 - Load a VIC-20 PRG file from host file system.
  sv     VIC-20 - Save a VIC-20 PRG file to host file system.
```

For general monitor commands, see [Monitor library](../../libraries/core/dotnet6502-monitor.md).
