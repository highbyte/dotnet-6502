# Systems

Library: `Highbyte.DotNet6502.Systems`

UI-agnostic, system-agnostic abstractions and base classes that every emulated system and host app
builds on. It sits one tier above the [`Highbyte.DotNet6502`](dotnet6502.md) CPU library and is
referenced by the Tier-2 system cores, the Tier-3 implementation libraries, and the host apps.

## Emulated-system abstractions

| Type | Role |
|---|---|
| `ISystem` | An emulated computer — exposes `CPU`, `Mem`, screen, `ExecuteOneFrame` / `ExecuteOneInstruction`, and the render/input/audio members below. Implemented by `C64`, `GenericComputer`. |
| `ISystemConfig` | A system's configuration. Carries `IsDirty` / `ClearDirty()`, validation, and render-provider selection. |
| `ISystemConfigurer` | Builds a system and its `SystemRunner` from config — `BuildSystem`, `BuildSystemRunner`, `GetConfigurationVariants`, host-config create/persist. |
| `IHostSystemConfig` / `HostSystemConfigBase<TSystemConfig>` | Per-host wrapper around an `ISystemConfig`. The generic base supplies the shared boilerplate (`SystemConfig`, `AudioSupported`, `IsDirty`, `Clone`, validation). |
| `SystemRunner` | Drives one frame of a system and dispatches its render/input/audio. |
| `SystemList` | Registry of available `ISystemConfigurer`s; host apps populate it from discovered plugins. |

## Host app

`HostApp` is the base class for every front-end (`AvaloniaHostApp`, `SilkNetHostApp`,
`HeadlessHostApp`, …). It owns system selection, the start/stop lifecycle, the input/audio context
wiring, scripting integration, and the automated-startup flow. A host app subclasses `HostApp` and
otherwise stays system-agnostic — the systems it can run come from runtime plugin discovery (see
[`Systems.Plugins`](dotnet6502-systems-plugins.md)).

## Render / input / audio pipelines

All three subsystems share one shape: **the system declares a *provider*, the host registers a
*target*, a coordinator matches them.** This keeps the system core free of host-technology types.

- **Render** — `ISystem.RenderProvider` / `RenderProviders`; hosts register render targets.
- **Input** (`Input/`) — `IInputConsumer` (with `Init(IHostInputState)`) is an `ISystem` member.
  `HostKey` is the host-agnostic key abstraction; each host's input context implements
  `IHostInputState` and maps native keys to `HostKey`.
- **Audio** (`Audio/`) — the system declares an `IAudioProvider`; hosts register an
  `IAudioCommandTarget`; an `IAudioCoordinator` matches them. Audio output is one of several
  *styles* declared by the system (today: a synth-command stream).

## Automated startup

`IAutomatedStartupParticipant` + `AutomatedStartupRequest`, with a pre-start gate in
`AutomatedStartupHandler`, let a system contribute its own command-line / URL-driven startup steps
(e.g. the C64 ROM-download and BASIC-paste actions) without the host app knowing about that
system.

## Also in this library

- Scripting hook interfaces (implemented by [`Scripting.MoonSharp`](dotnet6502-scripting-moonsharp.md)).
- Debug / instrumentation interfaces and frame-timing helpers.

## See also

- [`Highbyte.DotNet6502.Systems.Plugins`](dotnet6502-systems-plugins.md) — plugin contracts + discovery.
- [Systems overview](../../systems/overview.md) — the C64 and Generic implementations.
