# Overview of libraries

This page is the library index. For the **four-tier model**, the dependency rule, and how
host apps stay system-agnostic via plugin discovery, see
[Architecture](../architecture.md).

## [Tier 1 — Core libraries](core/overview.md)

UI-agnostic, system-agnostic. The 6502 CPU emulator itself, the abstractions for emulated
systems, the plugin contracts + discovery, and supporting libraries (monitor, scripting,
debug adapter, remoting, AI). Run anywhere .NET runs.

→ [Per-library reference and dependency layering](core/overview.md)

## [Tier 2 — System-specific core libraries](system-specific/c64.md)

The "computer" layer — implements `ISystem` for a specific machine using the core
libraries. Still UI-agnostic.

- [Commodore 64](../systems/c64/overview.md) — `Highbyte.DotNet6502.Systems.Commodore64`
- [Generic](../systems/generic/overview.md) — `Highbyte.DotNet6502.Systems.Generic`

## [Tier 3 — Implementation libraries](implementation/overview.md)

Bind a system to a UI / render / input / audio technology. Each tier-3 technology is
split in two:

- **`Impl.<Tech>`** — host-technology glue that is *system-agnostic* (e.g. `Impl.Avalonia`,
  `Impl.NAudio`).
- **`Impl.<Tech>.<System>`** — *per-system* **engine-plugin** libraries (e.g.
  `Impl.Avalonia.Commodore64`, `Impl.SilkNet.Generic`). Each ships an
  `ISystemEnginePlugin` registering that system for that host.

→ [App-by-library matrix](implementation/overview.md) showing which implementation
libraries each desktop and web app consumes.
