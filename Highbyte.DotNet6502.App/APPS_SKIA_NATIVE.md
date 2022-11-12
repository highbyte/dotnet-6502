<h1 align="center">Highbyte.DotNet6502.App.SkiaNative</h1>

# Overview
<img src="../Examples/Screenshots/SkiaNative_C64.png" width="50%" height="50%" title="SkiaSharp rendering in native Silk.NET host window">

# Features

Native cross-platform app written in .NET with a Window from Silk.NET, using a SkiaSharp renderer (from ```Highbyte.DotNet6502.Impl.Skia```) and a Silk.NET input handler (from ```Highbyte.DotNet6502.Impl.SilkNet```).

Currently the system to emulate is configured in the appsettings.json file.

# System: C64 
A directory containing the C64 ROM files (Kernal, Basic, Chargen) is supplied by the user, and set in the appsettings.json file.

# System: Generic computer 
TODO

# Monitor
A toggleable monitor UI in Silk.NET + [ImGui](https://www.nuget.org/packages/Silk.NET.OpenGL.Extensions.ImGui/) invoked by pressing F12.
