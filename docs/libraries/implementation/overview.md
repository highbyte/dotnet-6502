# Overview

Implementation libraries provide rendering, input handling, and audio for specific UI technologies, so a *system* (e.g. C64) can render to screen, handle input, and produce sound on a host platform.

## Two kinds of implementation library

Each host technology is split in two:

- **`Impl.<Tech>`** — host-technology glue that is **system-agnostic**: render targets, audio
  targets, and input context types that work for any system (e.g. `Impl.Avalonia`, `Impl.NAudio`,
  `Impl.SilkNet`). Host apps reference these directly.
- **`Impl.<Tech>.<System>`** — **per-system engine-plugin** libraries (e.g.
  `Impl.Avalonia.Commodore64`, `Impl.SilkNet.Generic`, `Impl.Headless.Commodore64`). Each ships an
  `ISystemEnginePlugin` that registers that system for that host, plus any system-specific render
  code. Host apps do **not** reference these — they are discovered at runtime via
  [`Highbyte.DotNet6502.Systems.Plugins`](../core/dotnet6502-systems-plugins.md).

The per-system breakdown of which `Impl.<Tech>.<System>` libraries exist, and where their
render/input/audio code lives, is documented under
[Systems / C64 / Libraries](../../systems/c64/libraries.md),
[Systems / VIC-20 / Libraries](../../systems/vic20/libraries.md),
[Systems / Oric / Libraries](../../systems/oric/libraries.md), and the corresponding system pages.

!!! note "Terminology"
    Earlier docs spoke of "InputHandlers" and "AudioHandlers". The render, input, and audio
    subsystems now share one shape: the system declares a *provider*, the host registers a
    *target*, a coordinator matches them. Input uses `IInputConsumer` + `HostKey`; audio uses
    `IAudioProvider` + `IAudioCommandTarget`. See [`Highbyte.DotNet6502.Systems`](../core/dotnet6502-systems.md).

## Render/Audio/Input technologies used

The table below lists the system-agnostic `Impl.<Tech>` library each app uses per concern.

| App                                        | Techniques                                  | Implementation libraries                      | C64 | VIC-20 | Apple II | Oric | Generic |
| ------------------------------------------ | ------------------------------------------- | --------------------------------------------- | :-: | :----: | :------: | :--: | :-----: |
| `Highbyte.DotNet6502.App.WASM`             | Render: `SkiaSharp.Blazor.View`, `OpenGL`   | Render: [`Highbyte.DotNet6502.Impl.Skia`](skia.md) | x | | | | x |
|                                            | Input: `Blazor`, `ASP.NET`, `JS interop`    | Input: [`Highbyte.DotNet6502.Impl.AspNet`](aspnet.md) | x | | | | x |
|                                            | Audio: `WebAudio API`, `JS interop`         | Audio: [`Highbyte.DotNet6502.Impl.AspNet`](aspnet.md) | x | | | | |
| `Highbyte.DotNet6502.App.Avalonia.Desktop` | Render: `Avalonia`                          | Render: [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md) | x | x | x | x | x |
|                                            | Input: `Avalonia`, `SDL`                    | Input: [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md), [`Highbyte.DotNet6502.Impl.SilkNet.SDL`](silknet-sdl.md) | x | x | x | x | x |
|                                            | Audio: `NAudio`, `OpenAL`                   | Audio: [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x | | x | x | |
| `Highbyte.DotNet6502.App.Avalonia.Browser` | Render: `Avalonia`                          | Render: [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md) | x | x | x | x | x |
|                                            | Input: `Avalonia`, `JS interop`             | Input: [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md), [`Highbyte.DotNet6502.Impl.Browser`](browser.md) | x | x | x | x | x |
|                                            | Audio: `NAudio`, `JS interop`               | Audio: [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x | | x | x | |
| `Highbyte.DotNet6502.App.SilkNetNative`    | Render: `Silk.NET`, `OpenGL`, `SkiaSharp`   | Render: [`Highbyte.DotNet6502.Impl.Skia`](skia.md) | x | | | | x |
|                                            |                                             | Render (OpenGL/shaders): [`Highbyte.DotNet6502.Impl.SilkNet`](silknet.md) | x | | | | |
|                                            | Input: `Silk.NET`                           | Input: [`Highbyte.DotNet6502.Impl.SilkNet`](silknet.md) | x | | | | x |
|                                            | Audio: `NAudio`, `OpenAL`                   | Audio: [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x | | | | |
| `Highbyte.DotNet6502.App.SadConsole`       | Render: `SadConsole`                        | Render: [`Highbyte.DotNet6502.Impl.SadConsole`](sadconsole.md) | x | | | | x |
|                                            | Input: `SadConsole`                         | Input: [`Highbyte.DotNet6502.Impl.SadConsole`](sadconsole.md) | x | | | | x |
|                                            | Audio: `NAudio`, `OpenAL`                   | Audio: [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x | | | | |
| `Highbyte.DotNet6502.App.Terminal`         | Render: `Terminal.Gui` (text cells)         | Render: [`Highbyte.DotNet6502.Impl.Terminal`](terminal.md) | x | x | x | x | |
|                                            | Input: `Terminal.Gui`                       | Input: [`Highbyte.DotNet6502.Impl.Terminal`](terminal.md) | x | x | x | x | |
|                                            | Audio: none                                 | Audio: none (terminals have no audio output) | | | | | |

The Headless app has C64, Generic, and Oric engine plugins, but deliberately registers no render,
audio, or user-input target, so it is not represented in this I/O technology matrix.
