# Architecture

The dotnet-6502 codebase is organized into **four tiers** with a strict dependency rule.
A host app can run any emulated system without holding a compile-time reference to it —
systems are discovered as **plugins** at runtime.

This page is the single overview of that model. Per-library reference lives under
[Libraries](libraries/overview.md); the full plugin contract and discovery flow live in
[`Systems.Plugins`](libraries/core/dotnet6502-systems-plugins.md).

## The four tiers

```
┌──────────────────────────────────────────────────────┐
│  Tier 4 — Apps + shell plugins (not libraries)       │  App.<Tech> · App.<Tech>.Core
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
│  Computer logic — CPU wiring, video chip, I/O        │  Highbyte.DotNet6502.Systems.Generic
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

| Tier | What it is | Project name patterns | May reference |
|---|---|---|---|
| 1 — Core | 6502 CPU, system abstractions, plugin contracts, tooling. UI- and system-agnostic. | `Highbyte.DotNet6502`, `Highbyte.DotNet6502.Systems`, `Highbyte.DotNet6502.Systems.Plugins`, `Highbyte.DotNet6502.Monitor`, `…DebugAdapter`, `…Remoting`, `…Scripting.MoonSharp`, `…AI` | — |
| 2 — System core | An emulated computer's logic (CPU wiring, video chip, I/O). UI-agnostic. | `Highbyte.DotNet6502.Systems.<System>` | Tier 1 |
| 3 — Implementation | Render / input / audio bound to a host technology. Split into system-agnostic glue and per-system engine plugins. | `Highbyte.DotNet6502.Impl.<Tech>`, `Highbyte.DotNet6502.Impl.<Tech>.<System>` | Tiers 1, 2 |
| 4 — Apps + shells | Entry exes and per-system UI shell plugins. | `Highbyte.DotNet6502.App.<Tech>`, `Highbyte.DotNet6502.App.<Tech>.Shell.<System>` | Tiers 1, 3 |

## The dependency rule

Each tier may reference only tiers below it. On top of that, three rules govern how
**system-specific** projects are referenced:

1. **A host app holds no compile-time reference to a system.** Entry exes have no
   `ProjectReference` to, and no `using` of, any `…Systems.<System>` or
   `…Impl.<Tech>.<System>` project. Adding or removing a system never edits an existing app.
2. **Shell plugins reference exactly one engine plugin.** Each
   `App.<Tech>.Shell.<System>` project references the matching `Impl.<Tech>.<System>` so
   the engine assembly flows into the app's output.
3. **Entry exes discover shells via a glob.** Each entry exe has a convention
   `ProjectReference` glob (e.g. `App.Avalonia.Shell.*\*.csproj`) so new shell projects
   are picked up automatically without editing the exe.

The combined effect: **adding a new emulated system, or a new host technology, is
project addition only** — no existing app, library, or csproj is edited.

## Plugin contracts

Two interfaces, both defined in
[`Highbyte.DotNet6502.Systems.Plugins`](libraries/core/dotnet6502-systems-plugins.md), are
what each plugin assembly carries:

- **`ISystemEnginePlugin`** — engine-side composition. Ships in `Impl.<Tech>.<System>`.
  Registers the system's `ISystemConfigurer` and host config into the host app's DI
  container.
- **`ISystemShellPlugin`** — shell-side composition. Ships in
  `App.<Tech>.Shell.<System>`. Contributes per-system UI pieces (menu, info panel,
  config dialog) to the host app's shell. Its `Create*Contribution` methods return
  `object`, so the contract is UI-framework-agnostic — each host app casts to its own
  ViewModel / component type.

A plugin assembly marks itself with `[assembly: SystemPlugin(typeof(<PluginType>))]`.
Discovery instantiates the type via `Activator.CreateInstance` and the host app wires
it in.

## Plugin discovery

At startup, a host app calls `SystemPluginDiscovery.Discover<ISystemEnginePlugin>()`
(and `Discover<ISystemShellPlugin>()` for GUI hosts). Discovery reads every
`[assembly: SystemPlugin]` in the current `AppDomain` and instantiates the plugins.

Because .NET lazy-loads referenced assemblies, discovery first ensures candidate
plugin assemblies are loaded, using three strategies in order:

1. **Build-emitted manifest** —
   `[assembly: AssemblyMetadata("Highbyte.DotNet6502.PluginAssembly", "<name>")]`
   items emitted by the `EmitPluginAssemblyManifest` MSBuild target in each entry exe.
   **This is the only strategy that works on browser WASM and on single-file desktop
   publishes**, where the relevant DLLs cannot be enumerated from disk.
2. **Transitive reference walk** from the entry assembly.
3. **Filesystem fallback** — probe the app base directory for `Highbyte.DotNet6502.*`
   DLLs not yet loaded (the C# compiler prunes `ProjectReference`s whose types are
   never statically used — exactly the case for attribute-only plugin assemblies).

For the per-method contract and the full discovery flow, see
[`Systems.Plugins`](libraries/core/dotnet6502-systems-plugins.md).

## Glossary

- **System core** — a Tier-2 `Highbyte.DotNet6502.Systems.<System>` project
  implementing `ISystem`, `ISystemConfig`, `ISystemConfigurer`. UI-agnostic.
- **Engine plugin** — a Tier-3 `Highbyte.DotNet6502.Impl.<Tech>.<System>` project
  carrying an `ISystemEnginePlugin`. Registers one system for one host technology.
- **Shell plugin** — a Tier-4 `Highbyte.DotNet6502.App.<Tech>.Shell.<System>` project
  carrying an `ISystemShellPlugin`. Per-host UI pieces (menu, info panel, config
  dialog) for one system.
- **Host technology** ("host tech") — a UI / rendering / input / audio stack:
  *Avalonia* (Desktop and Browser), *SilkNet* (Native), *SadConsole*, *Blazor WASM*,
  *Terminal* (TUI), *Headless*. Each host tech has an `Impl.<Tech>` glue library and an
  `App.<Tech>` entry exe.
- **Entry exe** — the `App.<Tech>` (or `App.<Tech>.Core`) project that boots the host
  app. Holds the convention `ProjectReference` glob that picks up shell plugins, and
  the `EmitPluginAssemblyManifest` MSBuild target that emits the discovery manifest.
- **Composition root** — the place in the entry exe where the DI container is
  configured and plugin discovery runs.

## See also

- [Libraries overview](libraries/overview.md) — per-library reference, organized by tier.
- [Implementation libraries overview](libraries/implementation/overview.md) — app-by-library matrix.
- [`Highbyte.DotNet6502.Systems.Plugins`](libraries/core/dotnet6502-systems-plugins.md) —
  plugin contracts and discovery in full detail.
- [Adding a new emulated system](systems/adding-a-system.md) — step-by-step walkthrough
  for adding a new computer (VIC-20, NES, …) on top of this model.
