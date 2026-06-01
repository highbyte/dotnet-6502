# Limitations

!!! important
    This is mainly a programming exercise that may or may not turn into something more.

## General

- Correct emulation of all aspects of computers such as the Commodore 64 is not likely.
- Not the fastest emulator.
- A real Commodore 64 uses the *6510* CPU, not the 6502 CPU. For the purpose of this emulator the 6502 CPU works fine — they are generally the same (same instruction set).
- Code coverage is currently limited to the core [`Highbyte.DotNet6502`](../libraries/core/dotnet6502.md) library.

## Missing 6502 features

- Coverage of unofficial / undocumented **NMOS** opcodes is partial. Official 6502 opcodes are implemented, and the currently supported unofficial opcodes are exposed through compatibility profiles in [`Highbyte.DotNet6502`](../libraries/core/dotnet6502.md).

## Missing or incomplete C64 features

- Cycle-exact rendering.
- Full 1541 disk drive support — only basic directory listing and file `LOAD` are supported (see [Systems / C64 / Useful tools](../systems/c64/useful-tools.md)).
- Tape drive support.
- Fully chip-accurate SID audio. The default sample-based provider reproduces most tunes
  well (all four waveforms incl. combined, ADSR, hard sync, ring mod, TEST hold, OSC3/ENV3
  readback, and a generic resonant low-pass/band-pass/high-pass filter). The legacy
  command-stream provider is still available as a low-CPU fallback but will sound noticeably
  wrong on most music. See [C64 audio](../systems/c64/libraries.md#audio).
- The VIC-II video emulation does not cover all tricks possible with the C64 VIC chip; advanced apps, games, and demos may not work as expected.
- Different renderer implementations support different feature sets (character-mode-only vs full bitmap+sprites). See [Compatible programs](../systems/c64/compatible-programs.md) for the renderer required by each tested title.

## Per-app limitations

- **Avalonia Desktop** — platform compatibility varies; see [Avalonia Desktop app troubleshooting](../desktop-apps/avalonia-desktop-troubleshooting.md).
- **Avalonia Browser** — Lua TCP client and filesystem APIs are not available (browser sandbox). The Lua key/value store falls back to `localStorage`.
- **SilkNetNative** — requires a GPU with OpenGL drivers. ARM64 (Linux / Windows) is not currently supported. See [SilkNetNative troubleshooting](../desktop-apps/silknet-native-troubleshooting.md).
- **SadConsole** — ARM64 (Linux / Windows) is not currently supported. See [SadConsole troubleshooting](../desktop-apps/sadconsole-troubleshooting.md).
- **Headless** — no rendering or audio (by design); the `screenshot` remote-control command returns an error.
