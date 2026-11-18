# Systems

Library: `Highbyte.DotNet6502.Systems`

UI-agnostic, system-agnostic abstractions and base classes that every emulated system and host app
builds on. It sits one tier above the [`Highbyte.DotNet6502`](dotnet6502.md) CPU library and is
referenced by the Tier-2 system cores, the Tier-3 implementation libraries, and the host apps.

## Emulated-system abstractions

| Type | Role |
|---|---|
| `ISystem` | An emulated computer — exposes `CPU`, `Mem`, screen, `ExecuteOneFrame` / `ExecuteOneInstruction`, and the render/input/audio members below. Implemented by `C64`, `Vic20`, `Apple2`, `Oric`, and `GenericComputer`. |
| `ISystemConfig` | A system's configuration. Carries `IsDirty` / `ClearDirty()`, validation, and render-provider selection. |
| `ISystemConfigurer` | Builds a system and its `SystemRunner` from config — `BuildSystem`, `BuildSystemRunner`, `GetConfigurationVariants`, host-config create/persist. |
| `IHostSystemConfig` / `HostSystemConfigBase<TSystemConfig>` | Per-host wrapper around an `ISystemConfig`. The generic base supplies the shared boilerplate (`SystemConfig`, `AudioSupported`, `IsDirty`, `Clone`, validation). |
| `SystemRunner` | Drives one frame of a system and dispatches its render/input/audio. |
| `SystemList` | Registry of available `ISystemConfigurer`s; host apps populate it from discovered plugins. |

## Snapshots and replay checkpoints

Each `ISnapshotModule` owns a named, versioned part of the save file and reads older payloads.
A recording starts with an ordinary snapshot. During playback, the system's `IRecordingAdapter`
applies resolved input and compares a digest of machine state at frame boundaries. Only a
snapshot can resume the machine from that point.

In the 6502 and C64 components used by replay, `<Type>.State.cs` sits beside the implementation
and groups the methods that need private state for capture, restore, or checkpoint comparison.
The emulator updates live fields in the main class. When both formats need the same bytes, they
call the same writer. For example, the VIC-II saves all line sequencers in a snapshot, while a
frame checkpoint includes the current and following line that can still affect execution.
The shared CPU checkpoint writer lives in the Systems library so other machines can use it.

On the C64, `C64ResolvedInput` owns the byte layout for the keyboard and both joystick ports.
Snapshot restore reinstates held controls and leaves the saved CPU interrupt latch intact.
Applying a recorded input frame also delivers any RESTORE-key edge for that frame.

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
- **Audio** (`Audio/`) — the system declares an `IAudioProvider`; hosts register a matching
  `IAudioTarget`; an `IAudioCoordinator` matches them. Audio output comes in two *styles*:
  the **command stream** style (`IAudioCommandStream` provider + `IAudioCommandTarget`,
  routed through a host synth oscillator graph) and the **PCM sample** style
  (`IAudioSampleProvider` + `IAudioSampleTarget`, with a coordinator-owned SPSC ring buffer
  bridging emulator-time bursts to wall-time DAC drain). A system can declare both styles
  and let the host pick which to register, e.g. the C64 ships with both
  [`C64SidCommandStream`](../../systems/c64/libraries.md#audio) and `C64SidSampleProvider`.

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
- [Systems overview](../../systems/overview.md) — all current system implementations.
