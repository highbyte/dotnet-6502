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

### Render

C64 render targets live under `Commodore64/Render/` in the engine-plugin libraries above
(`Impl.Skia.Commodore64`, `Impl.SilkNet.Commodore64`, `Impl.SadConsole.Commodore64`). The Avalonia
desktop and browser apps render the C64 via the generic Avalonia bitmap render target in
[`Highbyte.DotNet6502.Impl.Avalonia`](../../libraries/implementation/avalonia.md) — there is no
bespoke C64 renderer, so `Impl.Avalonia.Commodore64` exists only for engine registration and host
config.

### Input

C64 keyboard handling is **no longer per host**. One reusable `C64InputHandler` (with
`C64HostKeyboard` / `C64InputConfig`) lives in the C64 system core
[`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md) under `Input/`;
each host only supplies a small native-key → `HostKey` translation table inside its own input
context. A few genuinely host-specific bits remain in the engine-plugin libraries (for example
`C64SilkNetGamepad` in `Impl.SilkNet.Commodore64`).

### Audio

C64 audio is host-agnostic. The C64 system declares an `IAudioProvider`; the desktop NAudio host
target ([`Highbyte.DotNet6502.Impl.NAudio`](../../libraries/implementation/naudio.md)) and the
WebAudio host target ([`Highbyte.DotNet6502.Impl.AspNet`](../../libraries/implementation/aspnet.md))
consume it generically. There is no C64-specific audio library — the former
`Impl.NAudio.Commodore64` was removed when the audio command vocabulary was generalised.

For the cross-system view (which app uses which library, including Generic), see the [Implementation libraries overview](../../libraries/implementation/overview.md).
