# SadConsole desktop app

## General prerequisites


## Compatibility Matrix

| OS / Architecture | x64 | arm64 |
|-------------------|-----|-------|
| **Windows**       | ✅ Works | 🟡 Not tested |
| **macOS**         | ➖ N/A | ✅ Works |
| **Linux**         | ⚠️ Works* | ❌ Not working |

*May require additional packages (see below)

## Notes
### Windows x64
Tested on Windows 11 (x64). No extra configuration.

### Windows arm64
Not tested.

### Mac arm64
Tested on MacBook Air M1, MacOS 26. No extra configuration.

### Linux x64
Should work.

#### Linux via WSLg under Windows
Tested on Ubuntu 22.04.5 (x64). No extra configuration.

### Linux arm64
Tested on Ubuntu 25.10. Not working.

Exception below. Not investigated, but maybe a Microsoft.Xna.Framework issue. Missing arm64 native libraries?

```
Exception: The type initializer for 'Keyboard' threw an exception.
Stack trace:    at Microsoft.Xna.Framework.Input.Keyboard.PlatformGetState()
   at Microsoft.Xna.Framework.Input.Keyboard.GetState()
   at SadConsole.Host.Keyboard.Refresh()
   at SadConsole.Host.Keyboard..ctor()
   at SadConsole.Game..ctor()
   at SadConsole.Game.Create(Builder configuration)
   at Highbyte.DotNet6502.App.SadConsole.SadConsoleHostApp.Run() in /home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SadConsole/SadConsoleHostApp.cs:line 163
   at Program.<Main>$(String[] args) in /home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SadConsole/Program.cs:line 63
```