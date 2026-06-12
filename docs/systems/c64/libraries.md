# Libraries used by the C64 system

## Core library

The C64 system logic — VIC2, CIA, SID, 1541 — lives in:

- [`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Commodore64)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

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
| **Sample-based** (`C64SidSampleProvider`) | yes | Good but not perfect | Higher | Pure-managed sample-accurate SID emulation. All four waveforms (individual and combined via bitwise AND), full ADSR with the real 16 rate-counter periods, hard sync, ring modulation, TEST-bit hold, OSC3/ENV3 readback, a generic resonant 2-pole state-variable filter (LP / BP / HP), and the `$D418` volume DAC's audible DC term so digi / sample-playback tunes should work. Inner loop takes auto fast paths when the current SID state doesn't actively use the advanced features. Default output rate is 48 kHz, with integer Bresenham downsampling from the SID clock. Missing: chip-variant filter models (6581 R1/R2/R3/R4 vs 8580), chip-measured combined-waveform tables, anti-aliased downsampling, and per-instruction `$D418` cycle-offset (digi works, but writes land at instruction boundaries so high-rate sample tunes are slightly noisier than on real hardware). |
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
