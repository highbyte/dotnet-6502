# Overview of desktop apps

Cross-platform desktop applications running the emulator natively. Multiple front-ends exist with overlapping but not identical features — different UI / rendering / input technologies suit different use cases.

| App | UI / rendering | Input | Audio | Use case |
|-----|----------------|-------|-------|----------|
| [Avalonia Desktop](avalonia-desktop.md) | [Avalonia UI](https://avaloniaui.net/) (Skia-based) | Avalonia + SDL (joystick) | NAudio + OpenAL. C64 sample-based (default) and command-stream providers. | Default desktop app; most features (scripting, debugger, remote control). |
| [SadConsole](sadconsole.md) | [SadConsole](https://github.com/Thraka/SadConsole) (terminal/ASCII engine) | SadConsole | NAudio + OpenAL. C64 sample-based (default) and command-stream providers. | Console-style retro UI. |
| [SilkNetNative](silknet-native.md) | [Silk.NET](https://github.com/dotnet/Silk.NET) + ImGui + OpenGL/SkiaSharp | Silk.NET | NAudio + OpenAL. C64 sample-based (default) and command-stream providers. | OpenGL/shader rendering paths; multiple renderer providers including a custom GPU-packet C64 renderer. |
| [Console Monitor](console-monitor.md) | Plain .NET console | Console keyboard | none | Stand-alone 6502 machine code monitor; no system emulation UI. |
| [Headless](headless.md) | none | none | none | Automation, scripting, CI workflows. Driven entirely by CLI args and Lua. |

## Platform support

See per-app troubleshooting pages for platform-specific notes:

- [Avalonia Desktop troubleshooting](avalonia-desktop-troubleshooting.md) — Windows / macOS / Linux all working (Linux ARM64 needs a freetype workaround).
- [SilkNetNative troubleshooting](silknet-native-troubleshooting.md) — requires OpenGL; ARM64 Linux/Windows not currently working.
- [SadConsole troubleshooting](sadconsole-troubleshooting.md) — ARM64 Linux/Windows not currently working.

## Installation

Pre-built binaries are available via Homebrew, Scoop, or manual download. See [Installation](installation.md) for instructions covering Avalonia Desktop and Headless.

For automation and scripting features available across these apps, see [Tools](../tools/overview.md).
