# Overview of libraries

The codebase is organized into tiers. Each tier has a clear responsibility and depends only on tiers below it.

```
┌──────────────────────────────────────────────────────┐
│  Apps + shell plugins (not libraries)                │  App.<Tech> · App.<Tech>.Core
│  Composition root + per-system UI shells             │  App.<Tech>.Shell.<System>
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  Tier 3 — Implementation libraries                   │  Highbyte.DotNet6502.Impl.<Tech>
│  Render / Input / Audio for specific UI tech         │  Highbyte.DotNet6502.Impl.<Tech>.<System>
│  · Impl.<Tech>         — host-tech glue (agnostic)   │
│  · Impl.<Tech>.<System> — per-system engine plugins  │
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  Tier 2 — System-specific core libraries             │  Highbyte.DotNet6502.Systems.Commodore64
│  Computer logic — CPU, video chip, I/O, etc.         │  Highbyte.DotNet6502.Systems.Generic
└────────────────────┬─────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────┐
│  Tier 1 — Core libraries                             │  Highbyte.DotNet6502
│  CPU, abstractions, plugin contracts, monitor,       │  Highbyte.DotNet6502.Systems
│  scripting, debug adapter, remoting, AI              │  Highbyte.DotNet6502.Systems.Plugins
└──────────────────────────────────────────────────────┘  Highbyte.DotNet6502.Monitor
                                                          Highbyte.DotNet6502.DebugAdapter
                                                          Highbyte.DotNet6502.Remoting
                                                          Highbyte.DotNet6502.Scripting.MoonSharp
                                                          Highbyte.DotNet6502.AI
```

## [Tier 1 — Core libraries](core/overview.md)

UI-agnostic, system-agnostic. The 6502 CPU emulator itself, the abstractions for emulated systems, the plugin contracts + discovery, and supporting libraries (monitor, scripting, debug adapter, remoting, AI). Run anywhere .NET runs.

→ [Per-library reference and dependency layering](core/overview.md)

## [Tier 2 — System-specific core libraries](system-specific/c64.md)

The "computer" layer — implements `ISystem` for a specific machine using the core libraries. Still UI-agnostic.

- [Commodore 64](../systems/c64/overview.md) — `Highbyte.DotNet6502.Systems.Commodore64`
- [Generic](../systems/generic/overview.md) — `Highbyte.DotNet6502.Systems.Generic`

## [Tier 3 — Implementation libraries](implementation/overview.md)

Bind a system to a UI / render / input / audio technology. Each tier-3 technology is split in two:

- **`Impl.<Tech>`** — host-technology glue that is *system-agnostic* (e.g. `Impl.Avalonia`,
  `Impl.NAudio`).
- **`Impl.<Tech>.<System>`** — *per-system* **engine-plugin** libraries (e.g.
  `Impl.Avalonia.Commodore64`, `Impl.SilkNet.Generic`). Each ships an `ISystemEnginePlugin`
  registering that system for that host.

→ [App-by-library matrix](implementation/overview.md) showing which implementation libraries each desktop and web app consumes.

## Plugin discovery — how apps stay system-agnostic

A host app holds **no** project reference to any system-specific (`*.Commodore64` / `*.Generic`)
project. Instead it discovers the engine plugins (and per-system UI *shell plugins*,
`App.<Tech>.Shell.<System>`) at runtime, via
[`Highbyte.DotNet6502.Systems.Plugins`](core/dotnet6502-systems-plugins.md). Adding a new emulated
system, or a new host technology, requires no edit to existing apps.
