# NAudio

Library: `Highbyte.DotNet6502.Impl.NAudio`

System-agnostic NAudio-based audio targets for both the **command-stream** and **sample**
audio styles. Carries no system-specific code — host apps register the appropriate target(s)
for any system that declares a matching audio provider. (The former `Impl.NAudio.Commodore64`
library was removed when the audio command vocabulary was generalised.)

## Audio targets

| Target | Style | Pairs with provider style |
| --- | --- | --- |
| `NAudioCommandTarget` | Command stream — synthesizer graph (oscillators + ADSR + master volume) reacts to host-agnostic audio commands. | `IAudioCommandStream` providers, e.g. `C64SidCommandStream`. |
| `NAudioSampleTarget` | PCM sample pull — adapts the coordinator-supplied `AudioSampleReadCallback` to NAudio's `ISampleProvider` contract. | `IAudioSampleProvider` providers, e.g. `C64SidSampleProvider`. |

Both targets share the same NAudio handler context (`NAudioAudioHandlerContext`), which owns
the underlying `IWavePlayer`. The wave player is selected at host-app startup:

- **Desktop** (Avalonia Desktop, SadConsole, SilkNetNative) — `SilkNetOpenALWavePlayer`
  (cross-platform OpenAL).
- **Browser** (Avalonia Browser) — `WebAudioWavePlayer`, a custom NAudio `IWavePlayer` that
  pushes PCM samples to a browser `AudioContext` via `[JSImport]`/`[JSExport]` interop. The
  associated JS module is shipped as an embedded resource (`WebAudioWavePlayerResources`)
  and imported at app startup with `JSHost.ImportAsync(...)`.

## Per-app coverage

| App | `NAudioCommandTarget` | `NAudioSampleTarget` |
| --- | --- | --- |
| Avalonia Desktop | ✓ | ✓ |
| SadConsole | ✓ | ✓ |
| SilkNetNative | ✓ | ✓ |
| Avalonia Browser | ✓ | ✓ |
| Blazor WASM | (uses `WebAudioCommandTarget` from `Impl.AspNet` instead) | — (not yet wired up) |

For the C64-specific provider details and the per-app audio-provider matrix, see
[C64 audio](../../systems/c64/libraries.md#audio).
