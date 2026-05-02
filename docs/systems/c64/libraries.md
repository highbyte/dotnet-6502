# Libraries used by the C64 system

## Core library

The C64 system logic — VIC2, CIA, SID, 1541 — lives in:

- [`Highbyte.DotNet6502.Systems.Commodore64`](../../libraries/system-specific/c64.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Commodore64)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

## Implementation libraries

There are no separate C64-only render/input/audio libraries. Instead, each [implementation library](../../libraries/implementation/overview.md) groups its host-platform code (Skia, SilkNet, SadConsole, Avalonia, AspNet, NAudio) under a `Commodore64` sub-namespace.

The tables below pivot the implementation library overview by concern, listing only the libraries that contain C64-specific code.

### Render

| Library | Host technology | Used by app |
| ------- | --------------- | ----------- |
| [`Highbyte.DotNet6502.Impl.Skia`](../../libraries/implementation/skia.md) — `.Commodore64.Render` | SkiaSharp | Blazor WASM, SilkNetNative |
| [`Highbyte.DotNet6502.Impl.SilkNet`](../../libraries/implementation/silknet.md) — `.Commodore64.Render` | OpenGL shaders via Silk.NET | SilkNetNative |
| [`Highbyte.DotNet6502.Impl.SadConsole`](../../libraries/implementation/sadconsole.md) — `.Commodore64.Render` | SadConsole | SadConsole |

The Avalonia desktop and browser apps render via the generic Avalonia path; they do not have a C64-specific renderer.

### Input

| Library | Host technology | Used by app |
| ------- | --------------- | ----------- |
| [`Highbyte.DotNet6502.Impl.Avalonia`](../../libraries/implementation/avalonia.md) — `.Commodore64.Input` | Avalonia | Avalonia Desktop, Avalonia Browser |
| [`Highbyte.DotNet6502.Impl.AspNet`](../../libraries/implementation/aspnet.md) — `.Commodore64.Input` | Blazor / JS interop | Blazor WASM |
| [`Highbyte.DotNet6502.Impl.SilkNet`](../../libraries/implementation/silknet.md) — `.Commodore64.Input` | Silk.NET | SilkNetNative |
| [`Highbyte.DotNet6502.Impl.SadConsole`](../../libraries/implementation/sadconsole.md) — `.Commodore64.Input` | SadConsole | SadConsole |

### Audio

| Library | Host technology | Used by app |
| ------- | --------------- | ----------- |
| [`Highbyte.DotNet6502.Impl.NAudio`](../../libraries/implementation/naudio.md) — `.Commodore64.Audio` | NAudio / OpenAL | Avalonia Desktop, Avalonia Browser, SilkNetNative, SadConsole |
| [`Highbyte.DotNet6502.Impl.AspNet`](../../libraries/implementation/aspnet.md) — `.Commodore64.Audio` | WebAudio API via JS interop | Blazor WASM |

For the cross-system view (which app uses which library, including Generic), see the [Implementation libraries overview](../../libraries/implementation/overview.md).
