<h1 align="center">Highbyte.DotNet6502.App.SadConsole</h1>

# Overview
<img align="top" src="Screenshots/SadConsole_C64_Basic.png" width="25%" height="25%" title="SadConsole rendering in native SadConsole host window" /> <img align="top" src="Screenshots/SadConsole_C64_Monitor.png" width="38%" height="38%" title="SadConsole rendering in native SadConsole host window" />

# Features
Native cross-platform app based on the [`SadConsole`](https://github.com/Thraka/SadConsole) terminal/ascii/console/game engine.

# System: C64 
- A directory containing the C64 ROM files (Kernal, Basic, Chargen) is supplied by the user. Defaults are set in the appsettings.json file, and possible to change in the UI.
- Only video mode that works in C64 character mode (not multicolor) with built-in characters set from ROM is supported. 
- Generation of sound via NAudio with custom OpenAL (Silk.NET) provider (for cross platform compatibility).

# System: Generic computer 
TODO

# Monitor
Press button or toggle with F12.
TODO

# Stats
Press button or toggle with F11.
TODO
