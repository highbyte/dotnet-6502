# Limitations

!!! important
    This is mainly a programming exercise that may or may not turn into something more.

## General

- Correct emulation of all aspects of computers such as the Commodore 64 is not likely.
- Not the fastest emulator.
- The SonarCloud coverage metric currently measures only the core [`Highbyte.DotNet6502`](../libraries/core/dotnet6502.md) library; other libraries have tests but are not included in that metric.

## 6502 accuracy limits

- Official 6502 opcodes and all undocumented **NMOS** opcode bytes are implemented. Compatibility profiles control which undocumented opcodes are enabled; the C64's default profile excludes `LAS` and `JAM`. Some chip-dependent behaviours remain approximate, including `ANE` / `LXA` results and the bus activity of a jammed CPU. See [C64 accuracy and limitations](../systems/c64/accuracy.md#cpu-6510).

## Missing or incomplete C64 features

- Fully chip-accurate VIC-II rendering: the C64 emulation is cycle-exact and the default renderer draws graphics pixel by pixel, but sprite collision interrupt timing and some sprite gap cases still differ. The optional legacy pixel generator has additional mid-line limitations. See [C64 accuracy and limitations](../systems/c64/accuracy.md#vic-ii).
- Full 1541 disk drive support — only basic directory listing and file `LOAD` are supported (see [C64 disk drive support](../systems/c64/overview.md#1541-disk-drive-support)).
- Tape drive support.
- Fully chip-accurate SID audio. The default sample-based provider reproduces most tunes
  well (all four waveforms incl. combined, ADSR, hard sync, ring mod, TEST hold, OSC3/ENV3
  readback, and a generic resonant low-pass/band-pass/high-pass filter). The legacy
  command-stream provider is still available as a low-CPU fallback but will sound noticeably
  wrong on most music. See [C64 audio](../systems/c64/libraries.md#audio).
- Advanced apps, games, and demos may still show VIC-II differences. Render providers also support different feature sets: the rasterizer handles character modes, bitmap modes, and sprites, while simpler providers may not. See [Compatible programs](../systems/c64/compatible-programs.md) for the renderers used by tested titles.

## Per-app limitations

- **Avalonia Desktop** — platform compatibility varies; see [Avalonia Desktop app troubleshooting](../host-apps/avalonia/troubleshooting.md).
- **Avalonia Browser** — Lua TCP client and filesystem APIs are not available (browser sandbox). The Lua key/value store falls back to `localStorage`.
- **SilkNetNative** — requires a GPU with OpenGL drivers. ARM64 (Linux / Windows) is not currently supported. See [SilkNetNative troubleshooting](../host-apps/silknet-native/troubleshooting.md).
- **SadConsole** — ARM64 (Linux / Windows) is not currently supported. See [SadConsole troubleshooting](../host-apps/sadconsole/troubleshooting.md).
- **Headless** — no interactive display or audio (by design). The `screenshot` remote-control command works when the current system provides frame layers; otherwise it returns "Screenshot not available".
