# Libraries used by the C64 system

## Core library

The C64 system logic — VIC2, CIA, SID, 1541 — lives in:

- [`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Commodore64)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

### Device timing

The CPU executes one instruction at a time, but every cycle of it is a bus access counted by
`CPU.BusCycles`. The VIC-II and the two CIAs are brought up to date lazily from that counter: at
every instruction boundary, and at every access to one of their registers, to the cycle of the
access. A read of `$D012` therefore returns the raster line at the cycle the read happens, a CIA
timer read returns the count at that cycle, a raster-compare or timer-control write takes effect
on its own cycle. A raster or CIA timer interrupt is dated to the cycle on which the raster line
began or the timer underflowed, and the CPU applies its sampling rule to that cycle: taken after
the current instruction if it fell at or before the second-to-last cycle, otherwise after the
next one. The SID does the same through its audio provider (see below). The rasterizer applies a
write to the border or background colour registers (`$D020`-`$D024`) from the cycle after the
write lands, so a colour change in the middle of a line splits that line at the write's pixel
position, as on hardware; the VIC-II reports each register write with its frame cycle for this.
The other display registers (mode, scroll, 38/24-column, memory setup) are still sampled once per
raster line, so a mid-line change to one of those becomes visible on the next line. Where a cycle's
pixels land follows the chip: the display window's first pixel is 124 pixels into the raster line
on both PAL and NTSC, taken from the VIC-II's display window at X 24 and where X 0 falls relative to
the line's first cycle. How much border is drawn around that window is a presentation choice, since
real sets showed different amounts. A colour register write is shown a few pixels away from the
cycle boundary it lands on, by an amount measured against VICE that is the same on PAL and NTSC. The
rasterizer does hold a character row's 40 screen codes and colour nibbles the way
the VIC-II does: fetched on the row's first line and shown for its remaining seven, so a screen
write made after that fetch appears from the next row on. When a CPU read is stalled, the VIC-II
and the renderer are brought through the stalled cycles before the CPU continues, so what the
VIC-II fetched during the stall reflects memory before the stalled instruction's write.

The VIC-II also takes the bus from the CPU as on hardware: 40 cycles on every bad line (BA low
from cycle 12, video matrix fetches in cycles 15-54) and two cycles per sprite with DMA on, BA
low three cycles ahead. A CPU read that falls inside such a window waits until the window ends;
writes do not wait. Bad lines follow YSCROLL and the DEN bit as the VIC-II saw it during raster
line $30: clearing DEN before that line switches the display, and its bad lines, off for the whole
frame, clearing it later has no effect until the next frame. Sprite DMA switches on when an
enabled sprite's Y register equals the raster line as the VIC-II compares them and then runs for
the sprite's 21 rows (42 when Y-expanded) whatever the registers do meanwhile, so a Y written to
a line the raster has already passed costs nothing until the raster comes round again.

Which character row a raster line shows, and which of its eight lines, is not arithmetic on the
line number but the chip's own display state: a bad line starts a row (the row counter resets and
the row is fetched), the eighth line of a row advances the row pointer by 40 and drops the chip
into idle state until the next bad line, and in idle state the display area shows the byte at
`$3FFF` (`$39FF` with ECM) in black over the background colour. The vertical border flip-flop is
set when the raster reaches the bottom compare line (251, or 247 with RSEL clear) and reset at the
top one (51 or 55) only if DEN is set; while it is set the whole line, sprites included, is border
colour. That is what makes vertical fine scrolling, a switched-off screen, a border opened by
toggling RSEL around line 251 and row stretching by avoiding bad lines come out as on hardware.
A `$D011` write early enough in a line (before the border check at cycle 16 and the bad line
check at cycle 14) still counts for that line.

## Implementation libraries

C64-specific host code lives in its own **engine-plugin** libraries, one per host technology,
named `Highbyte.DotNet6502.Impl.<Tech>.Commodore64`. Each carries the C64 render targets for that
host technology (where one exists), C64 host config, and an `ISystemEnginePlugin` that registers
the C64 with the host app's DI container. Host apps **discover these plugins at runtime** — see
[`Highbyte.DotNet6502.Systems.Plugins`](../../libraries/core/dotnet6502-systems-plugins.md) — and
hold no direct project reference to them.

| Engine-plugin library | Host technology | Used by app |
| --------------------- | --------------- | ----------- |
| `Highbyte.DotNet6502.Impl.Skia.Commodore64` | SkiaSharp | Blazor WASM, SilkNetNative |
| `Highbyte.DotNet6502.Impl.SilkNet.Commodore64` | OpenGL shaders via Silk.NET | SilkNetNative |
| `Highbyte.DotNet6502.Impl.SadConsole.Commodore64` | SadConsole | SadConsole |
| `Highbyte.DotNet6502.Impl.Avalonia.Commodore64` | Avalonia | Avalonia Desktop, Avalonia Browser |
| `Highbyte.DotNet6502.Impl.AspNet.Commodore64` | Blazor / JS interop | Blazor WASM |
| `Highbyte.DotNet6502.Impl.Headless.Commodore64` | none (headless) | Headless |
| `Highbyte.DotNet6502.Impl.Terminal.Commodore64` | Terminal.Gui (text cells) | Terminal (TUI) |

### Render

C64 render targets live under `Commodore64/Render/` in the engine-plugin libraries above
(`Impl.Skia.Commodore64`, `Impl.SilkNet.Commodore64`, `Impl.SadConsole.Commodore64`). The Avalonia
desktop and browser apps render the C64 via the generic Avalonia bitmap render target in
[`Highbyte.DotNet6502.Impl.Avalonia`](../../libraries/implementation/avalonia.md) — there is no
bespoke C64 renderer, so `Impl.Avalonia.Commodore64` exists only for engine registration and host
config. Likewise the Terminal (TUI) app renders the C64 (character mode only) via the generic
terminal render target in [`Highbyte.DotNet6502.Impl.Terminal`](../../libraries/implementation/terminal.md),
so `Impl.Terminal.Commodore64` exists only for engine registration and host config.

### Input

C64 keyboard handling is **no longer per host**. One reusable `C64InputHandler` (with
`C64HostKeyboard` / `C64InputConfig`) lives in the C64 system core
[`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md) under `Input/`;
each host only supplies a small native-key → `HostKey` translation table inside its own input
context. A few genuinely host-specific bits remain in the engine-plugin libraries (for example
`C64SilkNetGamepad` in `Impl.SilkNet.Commodore64`).

### Audio

C64 audio is host-agnostic. The C64 system declares two interchangeable audio providers; the
host app's audio target chain consumes whichever one is currently selected via the C64 config
UI (`Audio provider` / `Audio target` / `SID emulation` combos).

| Provider | Default | Accuracy | CPU | Notes |
| --- | --- | --- | --- | --- |
| **Sample-based** (`C64SidSampleProvider`) | yes | Good but not perfect | Higher | Pure-managed sample-accurate SID emulation. All four waveforms (individual and combined via bitwise AND), full ADSR with the real 16 rate-counter periods, hard sync, ring modulation, TEST-bit hold, OSC3/ENV3 readback, a generic resonant 2-pole state-variable filter (LP / BP / HP), and the `$D418` volume DAC's audible DC term so digi / sample-playback tunes should work. Inner loop takes auto fast paths when the current SID state doesn't actively use the advanced features. Default output rate is 48 kHz, with integer Bresenham downsampling from the SID clock. Register writes are applied on the exact CPU cycle they happen (the core is caught up to the write's bus cycle first), OSC3/ENV3 reads see the state at the cycle of the read, and the `$D418` volume DAC is averaged over each output sample's window, so high-rate `$D418` sample playback has neither instruction-boundary jitter nor sample-point aliasing. Reads of write-only registers return the chip's data-bus latch (the last byte written to or read from the SID) until it decays after about 7,400 cycles, as on a 6581, so a loader that does `DEC $D418` for its loading noise counts the volume down and buzzes like the real machine. Missing: chip-variant filter models (6581 R1/R2/R3/R4 vs 8580), chip-measured combined-waveform tables, and anti-aliased downsampling. |
| **Command stream** (`C64SidCommandStream`) | no | Not very accurate | Lower | Legacy. Decodes SID register changes into host-agnostic synth commands (volume, voice ADSR + oscillator). A host-side oscillator graph (NAudio or WebAudio) turns them into sound. Cannot reproduce the SID filter, combined waveforms, ring modulation, hard sync, or digi / sample playback. |

The sample-based provider has two **SID emulation modes** (selectable in the config UI):

- `Auto` (default) — full accuracy as listed above.
- `Fast` — drops the advanced features (single waveform per voice, no sync / ring mod / TEST
  hold / OSC3/ENV3 readback / filter). Modest savings (~4% per frame) on sync-using tunes,
  near zero on simple tunes. Many tunes will sound wrong.

Each provider is paired with a host audio target that knows how to play its output style:

| Provider | Compatible target on desktop | Compatible target in browser |
| --- | --- | --- |
| Sample-based | `NAudioSampleTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via `OpenAL`) | `NAudioSampleTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via WebAudio JS interop) — Avalonia Browser only |
| Command stream | `NAudioCommandTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via `OpenAL`) | `NAudioCommandTarget` ([`Impl.NAudio`](../../libraries/implementation/naudio.md), playback via WebAudio JS interop) on Avalonia Browser; `WebAudioCommandTarget` ([`Impl.AspNet`](../../libraries/implementation/aspnet.md), direct WebAudio oscillator nodes) on Blazor WASM |

**Per-app availability:**

- **Avalonia Desktop**, **SadConsole**, **SilkNetNative**: both providers available (sample-based by default).
- **Avalonia Browser**: both providers available (sample-based by default), playback via WebAudio JS interop.
- **Blazor WASM**: command-stream only. The sample-based provider is not yet wired up here — see the design log for the planned work.
- **Terminal (TUI)**: no audio (terminals have no audio output).

There is no C64-specific audio library — the former `Impl.NAudio.Commodore64` was removed
when the audio command vocabulary was generalised, and the new sample path is also
system-agnostic on the target side.

For the cross-system view (which app uses which library, including Generic), see the [Implementation libraries overview](../../libraries/implementation/overview.md).
