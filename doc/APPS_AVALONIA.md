<h1 align="center">Highbyte.DotNet6502.App.Avalonia.Browser and Desktop</h1>

# Overview
<img align="top" src="Screenshots/Avalonia.Browser_C64_Basic.png" width="25%" height="25%" title="Avalonia Browser WebAssembly app, C64 Basic" /><img align="top" src="Screenshots/Avalonia.Browser_C64_LastNinja.png" width="25%" height="25%" title="Avalonia Browser WebAssembly app, C64 Last Ninja" /> <img align="top" src="Screenshots/Avalonia.Browser_C64_Monitor.png" width="38%" height="38%" title="Avalonia Browser WebAssembly app, C64 monitor" />

A deployed version can be found here [https://highbyte.se/dotnet-6502/app2](https://highbyte.se/dotnet-6502/app2)

# Features
A cross platform app (desktop and web) written with Avalonia UI, using built-in Avalonia controls for both UI and emulator screen.

# System: C64 
- Via C64 config UI you have to upload binaries for the ROMs that a C64 uses (Kernal, Basic, Chargen). Or use a convenient auto-download functionality (with a license notice).

- Renderer provider `Rasterizer` -> target `Avalonia 2-layer bitmap`
  - Character mode (normal and multi-color).
  - Bitmap mode (normal and bitmap mode).
  - Sprites (normal and multi-color).
  - Rendering of raster lines for border and background colors.

- Renderer provider `Video commands` -> target `Skia commands`
  - Character mode (normal).

- Input using `Avalonia`

- Audio not yet supported.

# System: Generic computer 
The example 6502 machine code that is loaded and run by default for the _Generic_ computer is this a assembled version of [this 6502 assembly code](../samples/Assembler/Generic/hostinteraction_scroll_text_and_cycle_colors.asm)


# UI

## Menu
Start and stop of selected system.

Configuration options of selected system.

## Monitor
A Avalonia implementation of the [machine code monitor](MONITOR.md) is available by pressing F12.

## Stats
A toggleable stats window by pressing F11.

# How to run locally

For system requirements, see details [here](DEVELOP.md#Requirements)

## Visual Studio 2022 or 2025 (Windows)

Open solution `dotnet-6502.sln`.
Set project `Highbyte.DotNet6502.App.Avalonia.Desktop` or `Highbyte.DotNet6502.App.Avalonia.Browser` as startup, and start with F5.

> [!IMPORTANT]  
> Running a Debug build of the Avalonia `Browser` app is very slow. To get acceptable performance a published release build with AOT is required. The `Desktop` app has ok performance in Debug mode, so using the Desktop app when developing and testing locally is recommended.

## Browser app from command line (Windows, Linux, Mac)
### Run Debug build (very slow)
```shell
cd ./src/apps/Avalonia/Highbyte.DotNet6502.App.Browser
dotnet run
```
Open browser at http://localhost:5000.

### Run optimized Publish build (AOT)
Requires 
- DotNet workload "wasm-tools" , install with `dotnet workload install wasm-tools`
- DotNet global tool "serve", install with `dotnet tool install --global dotnet-serve`

```powershell 
cd ./src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser
if(Test-Path $publishDir) { del ./bin/Publish/ -r -force }
dotnet publish -c Release -o ./bin/Publish/
dotnet serve -o:$path --directory ./bin/Publish/wwwroot/
```
A browser is automatically opened.
