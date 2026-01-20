<h1 align="center">Highbyte.DotNet6502.App.WASM</h1>

# Overview
Web app written with [Blazor Web Assembly](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor).

<img align="top" src="Screenshots/BlazorWASM_C64_Basic.png" width="25%" height="25%" title="Blazor WebAssembly app, C64 Basic" /><img align="top" src="Screenshots/BlazorWASM_C64_LastNinja.png" width="25%" height="25%" title="Blazor WebAssembly app, C64 Last Ninja" /> <img align="top" src="Screenshots/BlazorWASM_C64_Monitor.png" width="36%" height="25%" title="Blazor WebAssembly app, C64 monitor" />

Technologies
  - UI: `Blazor` UI controls.
  - Rendering: `Highbyte.DotNet6502.Impl.Skia`. Using [`SkiaSharp.Views.Blazor`](https://www.nuget.org/packages/SkiaSharp.Views.Blazor) library to provide a Canvas for drawing on with [`SkiaSharp`](https://www.nuget.org/packages/SkiaSharp) library.
  - Input: `Highbyte.DotNet6502.Impl.AspNet`.
  - Audio: `Highbyte.DotNet6502.Impl.AspNet`. Custom `WebAudio JS interop` for synthesizer and playback.

Live version: <a href="https://highbyte.se/dotnet-6502/app" target="_blank">https://highbyte.se/dotnet-6502/app</a>

# Features

## System: C64 
- Via C64 config UI you have to upload binaries for the ROMs that a C64 uses (Kernal, Basic, Chargen). Or use a convenient auto-download functionality (with a license notice).

- Renderer provider `Rasterizer` -> target `Skia 2-layer canvas`
  - Character mode (normal and multi-color).
  - Bitmap mode (normal and bitmap mode).
  - Sprites (normal and multi-color).
  - Rendering of raster lines for border and background colors.

- Renderer provider `Custom` -> target `Skia legacy v1`
  - Character mode (normal and multi-color).
  - Pre-rendered images for each character.
  - Sprites (normal and multi-color).
  - Rendering of raster lines for border and background colors.

- Renderer provider `Custom` -> target `Skia legacy v2`
  - Character mode (normal and multi-color).
  - Bitmap mode (normal and bitmap mode).
  - Sprites (normal and multi-color).
  - Rendering of raster lines for border and background colors.

- Renderer provider `Video commands` -> target `Skia commands`
  - Character mode (normal).

- Input using `AspNet`

- Audio via `WebAudio` synthesizer using .NET -> JavaScript interop.

## System: Generic computer 
The example 6502 machine code that is loaded and run by default for the _Generic_ computer is this a assembled version of [this 6502 assembly code](../samples/Assembler/Generic/hostinteraction_scroll_text_and_cycle_colors.asm)

## UI

### Menu
Start and stop of selected system.

Configuration options of selected system.

### Monitor
A Blazor WASM implementation of the [machine code monitor](MONITOR.md) is available by pressing F12.

### Stats
A toggleable stats window by pressing F11.

# How to run locally for development
For development system requirements, see details [here](DEVELOP.md#Requirements)

## Visual Studio 2026/2022 (Windows)

Open solution `dotnet-6502.sln`.
Set project `Highbyte.DotNet6502.App.WASM` as startup, and start with F5.

> [!IMPORTANT]  
> Running a Debug build of the Blazor WASM app is very slow. To get acceptable performance a published release build with AOT is required. It can make local debugging tricky sometimes.

## From command line (Windows, Linux, Mac)
### Run Debug build
```shell
cd ./src/apps/Highbyte.DotNet6502.App.WASM
dotnet run
```
Open browser at http://localhost:5000.

### Run optimized Publish build

To serve the published build the below example uses the DotNet global tool "serve", install with `dotnet tool install --global dotnet-serve`.

```powershell 
cd ./src/apps/Highbyte.DotNet6502.App.WASM
if(Test-Path ./bin/Publish/) { del ./bin/Publish/ -r -force }
dotnet publish -c Release -o ./bin/Publish/
dotnet serve -o:/ --directory ./bin/Publish/wwwroot/
```
A browser is automatically opened.
