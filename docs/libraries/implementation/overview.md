# Overview

Implementation libraries provide rendering, input handling, and audio for specific UI technologies. Each app combines a *system* (e.g. C64) with one or more implementation libraries to render to screen, handle input, and produce sound on its host platform.

## Render/Audio/Input technologies used

| App                                        | Techniques                                  | Implementation libraries                      | C64     | Generic |
| ------------------------------------------ | ------------------------------------------- | --------------------------------------------- | :---:   | :---:   |
| `Highbyte.DotNet6502.App.WASM`             | Render: `SkiaSharp.Blazor.View`, `OpenGL`   | Render: [`Highbyte.DotNet6502.Impl.Skia`](skia.md) | x   | x   |
|                                            | Input:  `Blazor`, `ASP.NET`, `JS interop`   | Input:  [`Highbyte.DotNet6502.Impl.AspNet`](aspnet.md) | x | x |
|                                            | Audio:  `WebAudio API`, `JS interop`        | Audio:  [`Highbyte.DotNet6502.Impl.AspNet`](aspnet.md) | x |   |
| `Highbyte.DotNet6502.App.Avalonia.Desktop` | Render: `Avalonia`                          | Render: [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md) | x | x |
|                                            | Input:  `Avalonia`, `SDL`                   | Input:  [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md), [`Highbyte.DotNet6502.Impl.SilkNet.SDL`](silknet-sdl.md) | x | x |
|                                            | Audio:  `NAudio`, `OpenAL`                  | Audio:  [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x |   |
| `Highbyte.DotNet6502.App.Avalonia.Browser` | Render: `Avalonia`                          | Render: [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md) | x | x |
|                                            | Input:  `Avalonia`, `JS interop`            | Input:  [`Highbyte.DotNet6502.Impl.Avalonia`](avalonia.md), [`Highbyte.DotNet6502.Impl.Browser`](browser.md) | x | x |
|                                            | Audio:  `NAudio`, `JS interop`              | Audio:  [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x |   |
| `Highbyte.DotNet6502.App.SilkNetNative`    | Render: `Silk.NET`, `OpenGL`, `SkiaSharp`   | Render: [`Highbyte.DotNet6502.Impl.Skia`](skia.md) | x | x |
|                                            |                                             | Render (OpenGL/shaders): [`Highbyte.DotNet6502.Impl.SilkNet`](silknet.md) | x |   |
|                                            | Input:  `Silk.NET`                          | Input:  [`Highbyte.DotNet6502.Impl.SilkNet`](silknet.md) | x | x |
|                                            | Audio:  `NAudio`, `OpenAL`                  | Audio:  [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x |   |
| `Highbyte.DotNet6502.App.SadConsole`       | Render: `SadConsole`                        | Render: [`Highbyte.DotNet6502.Impl.SadConsole`](sadconsole.md) | x | x |
|                                            | Input:  `SadConsole`                        | Input:  [`Highbyte.DotNet6502.Impl.SadConsole`](sadconsole.md) | x | x |
|                                            | Audio:  `NAudio`, `OpenAL`                  | Audio:  [`Highbyte.DotNet6502.Impl.NAudio`](naudio.md) | x |   |
