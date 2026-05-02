# Overview of libraries

The codebase is organized into three tiers of libraries. Each tier has a clear responsibility and depends only on tiers below it.

```
┌──────────────────────────────────────────────────────┐
│  Implementation libraries                            │  Highbyte.DotNet6502.Impl.*
│  (Render / Input / Audio for specific UI tech)       │
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  System-specific core libraries                      │  Highbyte.DotNet6502.Systems.Commodore64
│  (computer logic — CPU, video chip, I/O, etc.)       │  Highbyte.DotNet6502.Systems.Generic
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  Core libraries                                      │  Highbyte.DotNet6502
│  (CPU, abstractions, monitor, scripting,             │  Highbyte.DotNet6502.Systems
│  debug adapter, remoting, AI)                        │  Highbyte.DotNet6502.Monitor
└──────────────────────────────────────────────────────┘  Highbyte.DotNet6502.DebugAdapter
                                                          Highbyte.DotNet6502.Remoting
                                                          Highbyte.DotNet6502.Scripting.MoonSharp
                                                          Highbyte.DotNet6502.AI
```

## Tier 1 — [Core libraries](core/overview.md)

UI-agnostic, system-agnostic. Run anywhere .NET runs. The 6502 CPU emulator itself, the abstractions for emulated systems, and supporting libraries (monitor, scripting, debug adapter, remoting, AI).

| Library | Purpose |
|---------|---------|
| [`Highbyte.DotNet6502`](core/dotnet6502.md) | 6502 CPU emulation. Stand-alone — embed in any .NET app. |
| [`Highbyte.DotNet6502.Systems`](core/dotnet6502-systems.md) | Interfaces and base classes for emulated systems. |
| [`Highbyte.DotNet6502.Monitor`](core/dotnet6502-monitor.md) | Built-in machine code monitor base. |
| [`Highbyte.DotNet6502.DebugAdapter`](core/dotnet6502-debugadapter.md) | DAP server for VS Code debugger integration. |
| [`Highbyte.DotNet6502.Remoting`](core/dotnet6502-remoting.md) | TCP remote control protocol. |
| [`Highbyte.DotNet6502.Scripting.MoonSharp`](core/dotnet6502-scripting-moonsharp.md) | Lua scripting engine adapter. |
| [`Highbyte.DotNet6502.AI`](core/dotnet6502-ai.md) | AI integrations (e.g. C64 Basic code completion). |

## Tier 2 — [System-specific core libraries](system-specific/c64.md)

The "computer" layer — implements `ISystem` for a specific machine using the core libraries. Still UI-agnostic.

- [Commodore 64](../systems/c64/overview.md) — `Highbyte.DotNet6502.Systems.Commodore64`
- [Generic](../systems/generic/overview.md) — `Highbyte.DotNet6502.Systems.Generic`

## Tier 3 — [Implementation libraries](implementation/overview.md)

Bind a system to a UI/render/input/audio technology. Each app picks one or more implementation libraries to plug into the system.

| Library | Provides | Used by |
|---------|----------|---------|
| [`Impl.Skia`](implementation/skia.md) | Render | Blazor WASM, SilkNetNative |
| [`Impl.Avalonia`](implementation/avalonia.md) | Render, Input | Avalonia Desktop, Avalonia Browser |
| [`Impl.AspNet`](implementation/aspnet.md) | Input, WebAudio | Blazor WASM |
| [`Impl.Browser`](implementation/browser.md) | Gamepad input (JS interop) | Avalonia Browser |
| [`Impl.SilkNet`](implementation/silknet.md) | Render (OpenGL), Input | SilkNetNative |
| [`Impl.SilkNet.SDL`](implementation/silknet-sdl.md) | Joystick input | Avalonia Desktop |
| [`Impl.SadConsole`](implementation/sadconsole.md) | Render, Input | SadConsole |
| [`Impl.NAudio`](implementation/naudio.md) | Audio (NAudio + OpenAL) | All native apps |

For the cross-reference of which app uses which libraries, see [Implementation libraries / Overview](implementation/overview.md).
