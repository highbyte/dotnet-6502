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

A new system touches three tiers — a **system core**, an **engine plugin**, and a **shell
plugin** — per the model described in [architecture.md](./../architecture.md). System cores and
engine plugins live under `src/libraries/`; shell projects live under `src/apps/`. Add every
new project to the solution (`dotnet-6502.slnx`).

## Step 1 — The system core

Create `src/libraries/Highbyte.DotNet6502.Systems.<System>/` referencing `Highbyte.DotNet6502`
and `Highbyte.DotNet6502.Systems`.

### `ISystem`

Implement [dotnet6502-systems.md](./../libraries/core/dotnet6502-systems.md). For a minimum no-op system, wire a
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

!!! note "Audio-less systems must still implement `AudioEnabled`"
    `ISystemConfig` has a `bool AudioEnabled { get; set; }` member. Even if your system
    produces no audio, you must declare it — a plain auto-property that returns `false` is
    sufficient. Omitting it results in a compile error that is not immediately obvious from
    the interface name alone.

```csharp
    public bool AudioEnabled { get; set; } = false;
```

### `ISystemConfigurer`

`ISystemConfigurer` is what the host calls to build the system. Implement its members directly:

| Member | Minimum behaviour |
| --- | --- |
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

!!! tip "Add a config section to `appsettings.json` even when there are no settings yet"
    The Avalonia host binds the config section by name at startup. Add an entry matching
    `ConfigSectionName` to `appsettings.json` — an empty `SystemConfig` object is fine. The
    host is silent if the section is missing, so omitting it is easy to miss.

```json
    "Highbyte.DotNet6502.Vic20.Avalonia": {
      "SystemConfig": {}
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

!!! warning "Namespace collision when the system name matches the project suffix"
    If your system name (e.g. `Vic20`) is the same as the last segment of the engine plugin's
    namespace (`Highbyte.DotNet6502.Impl.Avalonia.Vic20`), the compiler cannot resolve a bare
    reference to the system class inside that plugin project — it sees `Vic20` as the nested
    namespace, not the type. Fix it with a using alias at the top of the affected file:

```csharp
    using Vic20System = Highbyte.DotNet6502.Systems.Vic20.Vic20;
```

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

!!! note "Shell project needs `<ImplicitUsings>enable</ImplicitUsings>`"
    The shell `.csproj` should include `<ImplicitUsings>enable</ImplicitUsings>` in its
    `PropertyGroup`. Without it, types like `IServiceProvider` (which appears in the
    `ISystemShellPlugin` method signatures) are not in scope, producing confusing compile
    errors about missing types rather than missing usings.

## Step 4 — Build and verify

Add the three projects to `dotnet-6502.slnx`, build, and run the Avalonia Desktop app. Confirm the
new system appears in the system selector and can be started/stopped. Nothing is drawn — that is
expected for a no-op system.

## Filling it in

Once the no-op system appears, add real behaviour incrementally — each layer is independent:

1. **Emulation** — real memory map, I/O chips, timing in the system core.
2. **Rendering** — have the system expose an `IRenderProvider`; add a render target. Reuse a host's
   generic render target where possible (the C64 and Generic systems both render through the
   generic Avalonia bitmap target).

    !!! tip "Derive border constants from the system's known pixel budget"
        The border dimensions in `IScreen` (`VisibleLeftRightBorderWidth`,
        `VisibleTopBottomBorderHeight`) control the aspect ratio of the rendered window. Start
        from the system's actual visible pixel area (e.g. NTSC VIC-20: 256×200 px) and subtract
        the text area (`cols × charWidth` × `rows × charHeight`) to get the border in each
        direction. Using placeholder values (e.g. 2 cols / 2 rows) produces a portrait window
        even for systems whose hardware display is landscape.
3. **Input** — implement `IInputConsumer` on the system; map host keys via `HostKey` /
   `IHostInputState`. See the [keyboard.md](./c64/keyboard.md) for the pattern.
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
       [dotnet6502-systems-plugins.md](./../libraries/core/dotnet6502-systems-plugins.md).
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

## Documenting startup parameters

If your system adds its own automated-startup parameters — CLI flags for the Avalonia Desktop /
Headless apps, or URL query parameters for the Avalonia Browser app — document them as a separate
**snippet fragment per system**, so the app pages stay a thin assembly of *general* parameters
followed by one group per system. There is no glob include in MkDocs, so each fragment is wired
into the app page with an explicit `--8<--` line.

The fragments live in `includes/startup-params/` and follow a `{frontend}-{scope}.md` naming
convention (see that folder's `README.md` for the full rules):

| File pattern | Purpose |
| --- | --- |
| `cli-<system>.md` | Your system's CLI flags (shared by the Avalonia Desktop and Headless apps). |
| `browser-<system>.md` | Your system's URL query parameters (Avalonia Browser app). |

`cli-general.md` / `browser-general.md` hold the system-agnostic parameters and are **not**
touched when adding a system. The existing C64 fragments (`cli-c64.md`, `browser-c64.md`) are the
templates to copy.

To add docs for e.g. a VIC-20:

1. Create `includes/startup-params/cli-vic20.md` starting with a
   `### VIC-20 parameters *(system-specific)*` group heading and `####` sub-sections, each with a
   `Parameter | Description | Depends on | Example` table. Copy the structure from `cli-c64.md`.
2. Create `includes/startup-params/browser-vic20.md` the same way (copy `browser-c64.md`), using
   the query-parameter syntax (`name=value`) and noting the desktop equivalents per group.
3. Add one include line to each app page, after the existing system fragments:
   - `docs/host-apps/avalonia/desktop.md` and `docs/host-apps/headless/overview.md`:
     `--8<-- "startup-params/cli-vic20.md"`
   - `docs/host-apps/avalonia/browser.md`: `--8<-- "startup-params/browser-vic20.md"`

Keep group headings at `###` and sub-sections at `####` so the page TOC stays two levels deep.
Run `mkdocs build --strict` (see [BUILD_DOCS.md](https://github.com/highbyte/dotnet-6502/blob/master/BUILD_DOCS.md))
to confirm the snippets resolve; the CI **Docs check** workflow runs the same strict build.

## See also

- [dotnet6502-systems.md](./../libraries/core/dotnet6502-systems.md) — the abstractions you implement.
- [dotnet6502-systems-plugins.md](./../libraries/core/dotnet6502-systems-plugins.md) — plugin contracts + discovery.
- [overview.md](./../libraries/implementation/overview.md) — `Impl.<Tech>` vs `Impl.<Tech>.<System>`.
- [libraries.md](./c64/libraries.md) / [libraries.md](./generic/libraries.md) — worked examples to copy from.
