<h1 align="center">Highbyte.DotNet6502.Impl.* libraries</h1>

# Overview
Rendering, input handling, and audio libraries

| App                                  | Techniques                                  | Implementation libraries                      | C64     | Generic |
| ------------------------------------ | ------------------------------------------- | --------------------------------------------- | :---:   | :---:   |
| `Highbyte.DotNet6502.App.WASM`       | Render: `SkiaSharp.Blazor.View`,`OpenGL`    | Render: `Highbyte.DotNet6502.Impl.Skia`       | x       | x       |
|                                      | Input:  `Blazor`,`ASP.NET`,`JavaScript`     | Input:  `Highbyte.DotNet6502.Impl.AspNet`     | x       | x       |
|                                      | Audio:  `WebAudio API`, `Blazor JS interop` | Audio:  `Highbyte.DotNet6502.Impl.AspNet`     | x       |         |
|                                      |                                             |                                               |         |         |
| `Highbyte.DotNet6502.App.Avalonia.Desktop` and `Highbyte.DotNet6502.App.Avalonia.Browser`      | Render: `Avalonia`              | Render: `Highbyte.DotNet6502.Impl.Avalonia`   | x       | x       |
| | Input:  `Blazor`,`ASP.NET`,`JavaScript`     | Input:  `Highbyte.DotNet6502.Impl.Avalonia`     | x       | x       |
|                                      | Audio:  -                                   | Audio:  -                                     | x       |         |
|                                      |                                             |                                               |         |         |
| `Highbyte.DotNet6502.App.SilkNetNative` | Render: `Silk.NET`,`OpenGL`,`SkiaSharp`  | Render: `Highbyte.DotNet6502.Impl.Skia`       | x       | x       |
|                                      |                                             | Render (OpenGL/shaders): `Highbyte.DotNet6502.Impl.SilkNet`    | x       |        |
|                                      | Input:  `Silk.NET`                          | Input:  `Highbyte.DotNet6502.Impl.SilkNet`    | x       | x       |
|                                      | Audio:  `NAudio`                            | Audio:  `Highbyte.DotNet6502.Impl.NAudio`     | x       |         |
|                                      |                                             |                                               |         |         |
| `Highbyte.DotNet6502.App.SadConsole` | Render: `SadConsole`                        | Render: `Highbyte.DotNet6502.Impl.SadConsole` | x       | x       |
|                                      | Input:  `SadConsole`                        | Input:  `Highbyte.DotNet6502.Impl.SadConsole` | x       | x       |
|                                      | Audio:  `NAudio`                            | Audio:  `Highbyte.DotNet6502.Impl.NAudio`     | x       |         |

# Library: Highbyte.DotNet6502.Impl.Skia

[`Highbyte.DotNet6502.Impl.Skia`](#HighbyteDotNet6502ImplSkia)
- Library with renderers implemented with the [`SkiaSharp`](https://github.com/mono/SkiaSharp) 2D drawing library. 
- For C64, multiple variants of a SkiaSharp renderer exists, with different tradeoffs (speed vs completeness).
- It's possible to use special Skia SKSL fragment shaders with SkiaSharp, which is used by some renderer variants.
- Can be used from both native and WASM applications.
- Note: other alternative renderers using only OpenGL + shaders can be found in [`Highbyte.DotNet6502.Impl.SilkNet`](#HighbyteDotNet6502ImplSilkNet) currently used by the Silk.NET native app, see below.

## Renderer targets
### Common render targets
TODO
### C64-specific render targets
TODO
### Generic-specific render targets
TODO

# Library: Highbyte.DotNet6502.Impl.Avalonia
## Renderer targets
### Common render targets
TODO
### C64-specific render targets
TODO
### Generic-specific render targets
TODO


## Input

### C64
TODO
### Generic
TODO

# Library: Highbyte.DotNet6502.Impl.AspNet

[`Highbyte.DotNet6502.Impl.AspNet`](#HighbyteDotNet6502ImplAspNet)
- InputHandlers implemented with [`ASP.NET Blazor`](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor). Can be used from web applications.
- AudioHandlers implemented with [`ASP.NET Blazor`](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) JS Interop to WebAudio. Can be used from web applications.

## InputHandler
### C64
TODO
### Generic
TODO

## AudioHandler
### C64
Experimental (non-complete) emulation of C64 SID audio chip.

# Library: Highbyte.DotNet6502.Impl.SilkNet

[`Highbyte.DotNet6502.Impl.SilkNet`](#HighbyteDotNet6502ImplSilkNet)
- InputHandlers implemented with the [`Silk.NET`](https://github.com/dotnet/Silk.NET) windowing library. Can be used from native Silk.NET application.
- Renderers implemented with Silk.NET OpenGL bindings, together with custom shaders. Can be used from native Silk.NET application.

## Renderer targets
### Common render targets
TODO
### C64-specific render targets
TODO
### Generic-specific render targets
TODO


## InputHandler
### C64
TODO
### Generic
TODO

# Library: Highbyte.DotNet6502.Impl.NAudio

[`Highbyte.DotNet6502.Impl.Naudio`](#HighbyteDotNet6502ImplNAudio)
- AudioHandlers implemented with  [`NAudio`](https://github.com/naudio/NAudio) audio library using a custom `Silk.NET.OpenAL` provider for cross platform support. Can be used from all native applications.

## AudioHandler
### C64
TODO

# Library: Highbyte.DotNet6502.Impl.SadConsole

[`Highbyte.DotNet6502.Impl.SadConsole`](#HighbyteDotNet6502ImplSadConsole)
- Renderers and InputHandlers implemented with the [`SadConsole`](https://github.com/Thraka/SadConsole) 2D console drawing library. Can be used from native SadConsole application.

## Renderer targets
### Common render targets
TODO
### C64-specific render targets
TODO
### Generic-specific render targets
TODO


## InputHandler
### C64
TODO
### Generic
TODO
