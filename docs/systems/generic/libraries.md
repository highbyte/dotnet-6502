# Libraries used by the Generic system

## Core library

The Generic system logic — configurable screen size, IO memory locations, and a host-agnostic command-stream renderer — lives in:

- [`Highbyte.DotNet6502.Systems.Generic`](../../libraries/system-specific/generic.md)
  · [source](https://github.com/highbyte/dotnet-6502/tree/master/src/libraries/Highbyte.DotNet6502.Systems.Generic)

This library has no UI, rendering, or I/O dependencies. It exposes abstractions that the implementation libraries below plug into.

## Implementation libraries

Like the C64 system, Generic does not have dedicated render/input/audio libraries — the host-specific code lives as `Generic` sub-namespaces inside the broader [implementation libraries](../../libraries/implementation/overview.md). However, the Generic system uses far less host-specific code than the C64: only **Input** has per-host sub-namespaces. Render is driven by the system's own `GenericVideoCommandStream`, and the system has no audio.

### Render

The Generic system does not have a per-host renderer. It produces a stream of host-agnostic video commands via [`GenericVideoCommandStream`](https://github.com/highbyte/dotnet-6502/blob/master/src/libraries/Highbyte.DotNet6502.Systems.Generic/Render/GenericVideoCommandStream.cs); each implementation library renders that stream using its host technology (SkiaSharp, Silk.NET, SadConsole, Avalonia). No `Generic.Render` sub-namespaces exist in the implementation libraries.

### Input

| Library | Host technology | Used by app |
| ------- | --------------- | ----------- |
| [`Highbyte.DotNet6502.Impl.Avalonia`](../../libraries/implementation/avalonia.md) — `.Generic.Input` | Avalonia | Avalonia Desktop, Avalonia Browser |
| [`Highbyte.DotNet6502.Impl.AspNet`](../../libraries/implementation/aspnet.md) — `.Generic.Input` | Blazor / JS interop | Blazor WASM |
| [`Highbyte.DotNet6502.Impl.SilkNet`](../../libraries/implementation/silknet.md) — `.Generic.Input` | Silk.NET | SilkNetNative |
| [`Highbyte.DotNet6502.Impl.SadConsole`](../../libraries/implementation/sadconsole.md) — `.Generic.Input` | SadConsole | SadConsole |

### Audio

The Generic system does not produce audio. No `Generic.Audio` sub-namespaces exist in the implementation libraries.

For the cross-system view (which app uses which library, including C64), see the [Implementation libraries overview](../../libraries/implementation/overview.md).
