# Adding a new emulated system

This guide describes how to add a new emulated computer (a VIC-20, an NES, a home-brew machine —
anything built on the 6502 CPU) to the project. Thanks to the plugin architecture, a new system
plugs in by **adding projects** — no existing app, library, or project file needs to be edited.

The recommended approach is to get a **minimum, no-op system** to *appear and run* first — visible
in the app's system list, selectable, stepping frames while doing nothing — and only then fill in
real emulation. This guide leads with the **Avalonia Desktop** app as the host; the same shape
applies to every host (SilkNet, SadConsole, WASM, Headless).

Throughout, replace `<System>` with your system's name (e.g. `Vic20`) and pick a stable
`SystemName` string (e.g. `"VIC20"`) — that string is the system's identity everywhere (plugins,
configurer, the `--system` CLI argument).

## What you will create

A new system touches three tiers (see the [libraries overview](../libraries/overview.md)):

| Tier | Project | Purpose |
|---|---|---|
| T2 — system core | `Highbyte.DotNet6502.Systems.<System>` | The emulated computer: `ISystem`, `ISystemConfig`, `ISystemConfigurer`. UI-agnostic. |
| T3 — engine plugin | `Highbyte.DotNet6502.Impl.<Tech>.<System>` | `ISystemEnginePlugin` — registers the system for one host technology. One per host. |
| T4 — shell plugin | `App.<Tech>.Shell.<System>` | `ISystemShellPlugin` — per-host UI (menu, config dialog). Can start as a stub. |

System cores and engine plugins live under `src/libraries/`; shell projects live under
`src/apps/`. Add every new project to the solution (`dotnet-6502.sln`).

## How discovery wires it together

A host app holds **no** reference to system-specific projects. Instead:

- The **entry exe** has a convention-glob `ProjectReference` — e.g. Avalonia Desktop globs
  `App.Avalonia.Shell.*\*.csproj`. Your new shell project is picked up automatically.
- The **shell project** references the **engine plugin** (`Impl.<Tech>.<System>`), so the engine
  plugin's DLL flows into the app output.
- At startup `SystemPluginDiscovery` scans for `[assembly: SystemPlugin(...)]` attributes and
  loads the engine plugin (and shell plugin). See
  [`Systems.Plugins`](../libraries/core/dotnet6502-systems-plugins.md).

So the only thing that makes a system *appear* is shipping an engine plugin assembly that a shell
project (matched by the glob) references.

## Step 1 — The system core

Create `src/libraries/Highbyte.DotNet6502.Systems.<System>/` referencing `Highbyte.DotNet6502`
and `Highbyte.DotNet6502.Systems`.

### `ISystem`

Implement [`ISystem`](../libraries/core/dotnet6502-systems.md). For a minimum no-op system, wire a
real `CPU` + `Memory` (so it executes 6502 code) but leave the rendering/input/audio members at
their defaults:

```csharp
public sealed class Vic20 : ISystem
{
    public const string SystemName = "VIC20";
    public string Name => SystemName;

    public CPU CPU { get; } = new();
    public Memory Mem { get; } = new();
    public IScreen Screen { get; } = /* a minimal screen description */;

    // No-op to start: declare no render/audio/input providers.
    public IRenderProvider? RenderProvider => null;
    public List<IRenderProvider> RenderProviders { get; } = new();
    // AudioProvider, AudioProviders, InputConsumer, InputInjector keep their interface defaults.

    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? e = null) { /* run N instructions */ }
    public ExecEvaluatorTriggerResult ExecuteOneInstruction(out InstructionExecResult r, IExecEvaluator? e = null) { ... }

    // SystemInfo, DebugInfo, InstrumentationEnabled, Instrumentations ...
}
```

A system with no render provider produces no picture, no input, no sound — which is exactly the
intended starting point. Add real rendering later (see *Filling it in*).

### `ISystemConfig`

A small config object — implement `ISystemConfig` (validation, `IsDirty`/`ClearDirty`,
render-provider selection). It is fine to start with everything valid and no options.

### `ISystemConfigurer`

`ISystemConfigurer` is what the host calls to build the system. Implement its members directly:

| Member | Minimum behaviour |
|---|---|
| `SystemName` | Return your `SystemName` constant. |
| `GetConfigurationVariants` | Return e.g. `["DEFAULT"]`. |
| `GetNewHostSystemConfig` / `PersistHostSystemConfig` | Create / save the host config (see Step 2). |
| `BuildSystem` | `new Vic20(...)`. |
| `BuildSystemRunner` | `new SystemRunner(system)` — no render/input/audio wiring needed yet. |

!!! tip
    The C64 and Generic systems share `C64SystemConfigurerCore` / `GenericComputerSystemConfigurerCore`
    base classes. Those are *per-system* helpers, not a general base — a brand-new system implements
    `ISystemConfigurer` directly.

## Step 2 — The engine plugin (Avalonia)

Create `src/libraries/Highbyte.DotNet6502.Impl.Avalonia.<System>/` referencing your system core,
`Highbyte.DotNet6502.Systems.Plugins`, and `Highbyte.DotNet6502.Impl.Avalonia`.

Provide a **host config** (`IHostSystemConfig`) — the per-host wrapper around your `ISystemConfig`.
The generic base `HostSystemConfigBase<TSystemConfig>` supplies the boilerplate:

```csharp
public sealed class Vic20HostConfig : HostSystemConfigBase<Vic20SystemConfig>
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.Vic20.Avalonia";
    public override bool AudioSupported => false;
}
```

Then the engine plugin itself — mark the assembly and implement `ISystemEnginePlugin`:

```csharp
[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.Impl.Avalonia.Vic20.Vic20AvaloniaEnginePlugin))]

public sealed class Vic20AvaloniaEnginePlugin : ISystemEnginePlugin
{
    public string SystemName => Vic20.SystemName;
    public string HostTechName => "Avalonia.NAudio";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISystemConfigurer>(sp =>
            new Vic20Setup(sp.GetRequiredService<ILoggerFactory>(), configuration));
    }
}
```

That is enough for the system to **appear in the app and run** (as a no-op).

## Step 3 — The shell project (Avalonia)

Create `src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Shell.<System>/`. Its key job at this
stage is simply to **reference the engine plugin** (`Impl.Avalonia.<System>`) so the glob in the
entry exe deploys both.

To start, ship a stub `ISystemShellPlugin` that contributes no UI:

```csharp
[assembly: SystemPlugin(typeof(Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Vic20AvaloniaShellPlugin))]

public sealed class Vic20AvaloniaShellPlugin : ISystemShellPlugin
{
    public string SystemName => Vic20.SystemName;
    public void RegisterShellServices(IServiceCollection services) { }
    public object? CreateMenuContribution(IServiceProvider sp) => null;
    public object? CreateInfoContribution(IServiceProvider sp) => null;
    public object? CreateConfigDialogContribution(IServiceProvider sp) => null;
}
```

The system now shows up in the Avalonia Desktop app's system list, is selectable, and steps frames
— with no per-system menu or config dialog yet.

## Step 4 — Build and verify

Add the three projects to `dotnet-6502.sln`, build, and run the Avalonia Desktop app. Confirm the
new system appears in the system selector and can be started/stopped. Nothing is drawn — that is
expected for a no-op system.

## Filling it in

Once the no-op system appears, add real behaviour incrementally — each layer is independent:

1. **Emulation** — real memory map, I/O chips, timing in the system core.
2. **Rendering** — have the system expose an `IRenderProvider`; add a render target. Reuse a host's
   generic render target where possible (the C64 and Generic systems both render through the
   generic Avalonia bitmap target).
3. **Input** — implement `IInputConsumer` on the system; map host keys via `HostKey` /
   `IHostInputState`. See the [C64 keyboard mapping](c64/keyboard.md) for the pattern.
4. **Audio** — expose an `IAudioProvider`; the system-agnostic host audio targets
   (`Impl.NAudio`, `Impl.AspNet`) consume it with no per-system audio library.
5. **Per-host UI** — flesh out the shell plugin: a menu ViewModel, an info panel, a config dialog.
6. **Other hosts** — repeat Step 2 + Step 3 for SilkNet, SadConsole, and the Blazor WASM browser
   app. Headless uses an `Impl.Headless.<System>` engine plugin and the `Impl.Headless.*` glob
   instead of a shell project. The **Avalonia Browser** app reuses the Avalonia engine plugin and
   shell project you already created in Steps 2–3 (it globs `App.Avalonia.Shell.*` like the
   desktop app) — but, like all browser hosts, it needs the extra csproj wiring described below.

!!! warning "Browser hosts — Avalonia Browser and Blazor WASM"
    Both browser hosts need extra care, for two reasons:

    1. **No filesystem scan.** A browser host cannot enumerate plugin DLLs on disk, so it relies on
       a build-emitted plugin manifest — see
       [`Systems.Plugins`](../libraries/core/dotnet6502-systems-plugins.md).
    2. **Trimming / AOT.** Browser publishes run the IL trimmer, which removes assemblies that are
       only referenced indirectly via `[SystemPlugin]` attributes. Each browser app's `.csproj`
       therefore keeps a `TrimmerRootAssembly` block that pins the per-system projects **by name**.
       Unlike the shell `ProjectReference` glob, this block **cannot be globbed** — you must extend
       it when adding a system.

    Add your system's three projects to the `TrimmerRootAssembly` block in **both** browser
    csproj files:

    - `src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser/...csproj` —
      `App.Avalonia.Shell.<System>`, `Impl.Avalonia.<System>`, `Systems.<System>`.
    - `src/apps/BlazorWASM/Highbyte.DotNet6502.App.WASM/...csproj` —
      `App.WASM.Shell.<System>`, `Impl.AspNet.<System>`, `Systems.<System>`.

    For example, in the Avalonia Browser app's csproj:

    ```xml
    <ItemGroup>
      <!-- existing systems ... -->
      <TrimmerRootAssembly Include="Highbyte.DotNet6502.App.Avalonia.Shell.Vic20" />
      <TrimmerRootAssembly Include="Highbyte.DotNet6502.Impl.Avalonia.Vic20" />
      <TrimmerRootAssembly Include="Highbyte.DotNet6502.Systems.Vic20" />
    </ItemGroup>
    ```

    Omitting these entries makes the system work in a Debug run but silently disappear from a
    published (trimmed/AOT) browser build.

## See also

- [`Highbyte.DotNet6502.Systems`](../libraries/core/dotnet6502-systems.md) — the abstractions you implement.
- [`Highbyte.DotNet6502.Systems.Plugins`](../libraries/core/dotnet6502-systems-plugins.md) — plugin contracts + discovery.
- [Implementation libraries overview](../libraries/implementation/overview.md) — `Impl.<Tech>` vs `Impl.<Tech>.<System>`.
- [C64 libraries](c64/libraries.md) / [Generic libraries](generic/libraries.md) — worked examples to copy from.
