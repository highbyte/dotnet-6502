# Overview of libraries

The codebase is organized into three tiers of libraries. Each tier has a clear responsibility and depends only on tiers below it.

```
┌──────────────────────────────────────────────────────┐
│  Tier 3 — Implementation libraries                   │  Highbyte.DotNet6502.Impl.*
│  Render / Input / Audio for specific UI tech         │
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  Tier 2 — System-specific core libraries             │  Highbyte.DotNet6502.Systems.Commodore64
│  Computer logic — CPU, video chip, I/O, etc.         │  Highbyte.DotNet6502.Systems.Generic
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  Tier 1 — Core libraries                             │  Highbyte.DotNet6502
│  CPU, abstractions, monitor, scripting,              │  Highbyte.DotNet6502.Systems
│  debug adapter, remoting, AI                         │  Highbyte.DotNet6502.Monitor
└──────────────────────────────────────────────────────┘  Highbyte.DotNet6502.DebugAdapter
                                                          Highbyte.DotNet6502.Remoting
                                                          Highbyte.DotNet6502.Scripting.MoonSharp
                                                          Highbyte.DotNet6502.AI
```

## [Tier 1 — Core libraries](core/overview.md)

UI-agnostic, system-agnostic. The 6502 CPU emulator itself, the abstractions for emulated systems, and supporting libraries (monitor, scripting, debug adapter, remoting, AI). Run anywhere .NET runs.

→ [Per-library reference and dependency layering](core/overview.md)

## [Tier 2 — System-specific core libraries](system-specific/c64.md)

The "computer" layer — implements `ISystem` for a specific machine using the core libraries. Still UI-agnostic.

- [Commodore 64](../systems/c64/overview.md) — `Highbyte.DotNet6502.Systems.Commodore64`
- [Generic](../systems/generic/overview.md) — `Highbyte.DotNet6502.Systems.Generic`

## [Tier 3 — Implementation libraries](implementation/overview.md)

Bind a system to a UI / render / input / audio technology. Each app picks one or more implementation libraries to plug into the system.

→ [App-by-library matrix](implementation/overview.md) showing which implementation libraries each desktop and web app consumes.
