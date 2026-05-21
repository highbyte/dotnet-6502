# Libraries used by the Generic system

## Core library

The Generic system logic — configurable screen size, IO memory locations, and a host-agnostic command-stream renderer — lives in:

- [`Highbyte.DotNet6502.Systems.Generic`](../../libraries/system-specific/generic.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Generic)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

## Implementation libraries

Generic-specific host code lives in its own **engine-plugin** libraries, one per host technology,
named `Highbyte.DotNet6502.Impl.<Tech>.Generic`. Each carries the Generic host config, an
`ISystemEnginePlugin` that registers the Generic computer with the host app's DI container, and —
unlike the C64 — its per-host input handler. Host apps **discover these plugins at runtime** — see
[`Highbyte.DotNet6502.Systems.Plugins`](../../libraries/core/dotnet6502-systems-plugins.md) — and
hold no direct project reference to them.

| Engine-plugin library | Host technology | Used by app |
| --------------------- | --------------- | ----------- |
| `Highbyte.DotNet6502.Impl.Avalonia.Generic` | Avalonia | Avalonia Desktop, Avalonia Browser |
| `Highbyte.DotNet6502.Impl.AspNet.Generic` | Blazor / JS interop | Blazor WASM |
| `Highbyte.DotNet6502.Impl.SilkNet.Generic` | Silk.NET | SilkNetNative |
| `Highbyte.DotNet6502.Impl.SadConsole.Generic` | SadConsole | SadConsole |
| `Highbyte.DotNet6502.Impl.Headless.Generic` | none (headless) | Headless |

The Generic system needs less host-specific code than the C64: it has **no per-host renderer** and
**no audio**.

### Render

The Generic system does not have a per-host renderer. It produces a stream of host-agnostic video commands via [`GenericVideoCommandStream`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems.Generic/Render/GenericVideoCommandStream.cs); each host renders that stream using its host technology (SkiaSharp, Silk.NET, SadConsole, Avalonia) via that host's generic render target. The engine-plugin libraries contain no Generic render code.

### Input

Unlike the C64, Generic input handlers *are* host-specific — one per host, under `Generic/Input/`
in each engine-plugin library:

| Engine-plugin library | Host technology | Used by app |
| --------------------- | --------------- | ----------- |
| `Highbyte.DotNet6502.Impl.Avalonia.Generic` | Avalonia | Avalonia Desktop, Avalonia Browser |
| `Highbyte.DotNet6502.Impl.AspNet.Generic` | Blazor / JS interop | Blazor WASM |
| `Highbyte.DotNet6502.Impl.SilkNet.Generic` | Silk.NET | SilkNetNative |
| `Highbyte.DotNet6502.Impl.SadConsole.Generic` | SadConsole | SadConsole |

### Audio

The Generic system does not produce audio. No Generic audio code exists in the engine-plugin libraries.

For the cross-system view (which app uses which library, including C64), see the [Implementation libraries overview](../../libraries/implementation/overview.md).
