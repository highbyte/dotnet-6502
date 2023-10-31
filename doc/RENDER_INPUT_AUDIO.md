<h1 align="center">Highbyte.DotNet6502.Impl.* libraries</h1>

# Overview
Rendering, input handling, and audio libraries

| App                                  | Techniques                                  | Implementation libraries                      | C64     | Generic |
| ------------------------------------ | ------------------------------------------- | --------------------------------------------- | :---:   | :---:   |
| `Highbyte.DotNet6502.App.WASM`   | Render: `SkiaSharp.Blazor.View`,`OpenGL`    | Render: `Highbyte.DotNet6502.Impl.Skia`       | x       | x       |
|                                      | Input:  `Blazor`,`ASP.NET`,`JavaScript`     | Input:  `Highbyte.DotNet6502.Impl.AspNet`     | x       | x       |
|                                      | Audio:  `WebAudio API`, `Blazor JS interop` | Audio:  `Highbyte.DotNet6502.Impl.AspNet`     | x       |         |
|                                      |                                             |                                               |         |         |
| `Highbyte.DotNet6502.App.SilkNetNative` | Render: `Silk.NET`,`OpenGL`,`SkiaSharp`     | Render: `Highbyte.DotNet6502.Impl.Skia`, `Highbyte.DotNet6502.Impl.OpenGl`       | x       | x       |
|                                      | Input:  `Silk.NET`                          | Input:  `Highbyte.DotNet6502.Impl.SilkNet`    | x       | x       |
|                                      | Audio:  -                                   | Audio:  -                                     |         |         |
|                                      |                                             |                                               |         |         |
| `Highbyte.DotNet6502.App.SadConsole` | Render: `SadConsole`                        | Render: `Highbyte.DotNet6502.Impl.SadConsole` | x       | x       |
|                                      | Input:  `SadConsole`                        | Input:  `Highbyte.DotNet6502.Impl.SadConsole` | x       | x       |
|                                      | Audio:  -                                   | Audio:  -                                     |         |         |

# Library: Highbyte.DotNet6502.Impl.Skia

[```Highbyte.DotNet6502.Impl.Skia```](#HighbyteDotNet6502ImplSkia)
- Library with renderers implemented with the [```SkiaSharp```](https://github.com/mono/SkiaSharp) 2D drawing library. Can be used from both native and WASM applications.

## Renderer
### C64
### Generic

# Library: Highbyte.DotNet6502.Impl.AspNet

[```Highbyte.DotNet6502.Impl.AspNet```](#HighbyteDotNet6502ImplAspNet)
- InputHandlers implemented with [```ASP.NET Blazor```](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor). Can be used from web applications.
- AudioHandlers implemented with [```ASP.NET Blazor```](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) JS Interop to WebAudio. Can be used from web applications.

## InputHandler
### C64
### Generic

## AudioHandler
### C64
Experimental (non-complete) emulation of C64 SID audio chip.

# Library: Highbyte.DotNet6502.Impl.SilkNet

[```Highbyte.DotNet6502.Impl.SilkNet```](#HighbyteDotNet6502ImplSilkNet)
- InputHandlers implemented with the [```Silk.NET```](https://github.com/dotnet/Silk.NET) windowing library. Can be used from native applications.

## InputHandler
### C64
### Generic

# Library: Highbyte.DotNet6502.Impl.SadConsole

[```Highbyte.DotNet6502.Impl.SadConsole```](#HighbyteDotNet6502ImplSadConsole)
- Renderers and InputHandlers implemented with the [```SadConsole```](https://github.com/Thraka/SadConsole) 2D console drawing library. Can be used from native applications.

## Renderer
### C64
### Generic

## InputHandler
### C64
### Generic