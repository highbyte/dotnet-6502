# Overview

Core libraries are UI-agnostic and system-agnostic. They run anywhere .NET runs and form the foundation of every emulator app in the project.

| Library | Purpose | NuGet | Depends on |
|---------|---------|-------|------------|
| [`Highbyte.DotNet6502`](dotnet6502.md) | 6502 CPU emulator. Stand-alone — embed in any .NET app. | [`Highbyte.DotNet6502`](https://www.nuget.org/packages/Highbyte.DotNet6502/) | — |
| [`Highbyte.DotNet6502.Systems`](dotnet6502-systems.md) | Interfaces and base classes for emulated systems (`ISystem`, host-app base, scripting hooks). | — | CPU |
| [`Highbyte.DotNet6502.Monitor`](dotnet6502-monitor.md) | Built-in machine code monitor base — disassemble, breakpoints, memory inspection. UI-tech-agnostic. | — | CPU |
| [`Highbyte.DotNet6502.DebugAdapter`](dotnet6502-debugadapter.md) | DAP (Debug Adapter Protocol) server over STDIO and TCP. Powers VS Code source-level debugging. | — | CPU, Systems |
| [`Highbyte.DotNet6502.Remoting`](dotnet6502-remoting.md) | TCP remote control protocol — newline-delimited JSON. | — | CPU, Systems |
| [`Highbyte.DotNet6502.Scripting.MoonSharp`](dotnet6502-scripting-moonsharp.md) | Lua scripting engine (MoonSharp adapter) implementing the scripting interfaces from `Systems`. | — | Systems |
| [`Highbyte.DotNet6502.AI`](dotnet6502-ai.md) | AI integrations (e.g. C64 BASIC code completion via OpenAI / Ollama / custom endpoint). | — | Systems |

## Layering

The core libraries form a small dependency chain. The diagram below shows the intent — every library is consumed *upwards* by system-specific libraries (Tier 2) and implementation libraries (Tier 3) without core itself depending on either.

```
                    ┌────────────────────────────────────┐
   AI               │ Highbyte.DotNet6502.AI             │
                    └──────────────┬─────────────────────┘
                                   │
   Scripting        ┌──────────────▼─────────────────────┐
                    │ Highbyte.DotNet6502.Scripting.     │
                    │   MoonSharp                        │
                    └──────────────┬─────────────────────┘
                                   │
   Tooling          ┌──────────────▼─────────────────────┐
                    │ Highbyte.DotNet6502.DebugAdapter   │
                    │ Highbyte.DotNet6502.Remoting       │
                    │ Highbyte.DotNet6502.Monitor        │
                    └──────────────┬─────────────────────┘
                                   │
   Abstractions     ┌──────────────▼─────────────────────┐
                    │ Highbyte.DotNet6502.Systems        │
                    │ (ISystem, HostApp, scripting       │
                    │  interfaces, debug interfaces)     │
                    └──────────────┬─────────────────────┘
                                   │
   CPU              ┌──────────────▼─────────────────────┐
                    │ Highbyte.DotNet6502                │
                    │ (CPU, memory, addressing modes)    │
                    └────────────────────────────────────┘
```

## What's in *not* in core

These belong elsewhere even though they sound related:

- **System emulation** (C64, Generic computer) — see [System-specific core libraries](../system-specific/c64.md).
- **UI / rendering / input / audio code** — see [Implementation libraries](../implementation/overview.md).
- **CLI argument parsing** for the host apps — that lives in each app project, not in a library.

## Where to start

- Embedding the CPU in your own .NET app → [`Highbyte.DotNet6502`](dotnet6502.md) has runnable example code.
- Building a new host app for an existing system → start with [`Highbyte.DotNet6502.Systems`](dotnet6502-systems.md) (`HostApp` base class) and pick implementation libraries from [Tier 3](../implementation/overview.md).
- Adding a new emulated system → implement `ISystem` from [`Highbyte.DotNet6502.Systems`](dotnet6502-systems.md), then add a system-specific library (Tier 2) following the [C64](../system-specific/c64.md) or [Generic](../system-specific/generic.md) pattern.
