# Systems.Plugins

Library: `Highbyte.DotNet6502.Systems.Plugins`

A small Tier-1 core library holding the **plugin contracts** and the **discovery** mechanism that
let a host app run an emulated system without holding any compile-time reference to it. It is the
foundation of the project's rule that **a host app is system-agnostic** — no `using` of, and no
`ProjectReference` to, any system-specific (`*.Commodore64` / `*.Generic`) project.

## Why

Before plugins, every app carried system-specific code and `if (system == "C64")`-style
conditionals. Now each system's wiring lives in its own plugin assembly, and apps discover those
assemblies at runtime. Adding a new emulated system, or a new host technology, requires no edit to
any existing app.

## Plugin contracts

### `ISystemEnginePlugin`

Engine-side composition. Ships in `Impl.<Tech>.<System>` libraries (for example
`Impl.Avalonia.Commodore64`, `Impl.SilkNet.Generic`). It registers the system's
`ISystemConfigurer`, host config, and render/audio targets into the host app's DI container.

| Member | Purpose |
|---|---|
| `string SystemName` | The system this plugin provides (e.g. `"C64"`). |
| `string HostTechName` | The host-tech combination targeted (e.g. `"Avalonia.NAudio"`) — used for diagnostics / disambiguation. |
| `void Register(IServiceCollection, IConfiguration)` | Adds the system's services to the host's DI container. |

### `ISystemShellPlugin`

Shell-side composition. Ships in `App.<Tech>.Shell.<System>` libraries. It contributes per-system
UI pieces (ViewModels, Views, menu items, config dialogs) to a host app's shell. The contract is
UI-framework-agnostic — its `Create*Contribution` methods return `object`, and each host app casts
to its own ViewModel/component type.

| Member | Purpose |
|---|---|
| `string SystemName` | The system this shell plugin provides UI for. |
| `void RegisterShellServices(IServiceCollection)` | Registers the plugin's ViewModels / Views / helpers in DI. |
| `object? CreateMenuContribution(IServiceProvider)` | Per-system menu / sidebar ViewModel (or `null`). |
| `object? CreateInfoContribution(IServiceProvider)` | Info-panel ViewModel (or `null`). |
| `object? CreateConfigDialogContribution(IServiceProvider)` | Config-dialog ViewModel (or `null`). |

## Discovery

### `[assembly: SystemPlugin(typeof(X))]`

`SystemPluginAttribute` marks an assembly as carrying a plugin. Discovery reads these attributes
instead of scanning every type. Multiple attributes per assembly are allowed (an engine plugin and
a shell plugin can ship together). The plugin type must have a public parameterless constructor —
the attribute carries a `[DynamicallyAccessedMembers]` annotation so the trimmer / AOT compiler
preserves it.

### `SystemPluginDiscovery.Discover<TPlugin>(...)`

A static scan over assemblies loaded in the current `AppDomain`. It reads each `SystemPlugin`
attribute, instantiates the plugin via `Activator.CreateInstance`, filters by the requested
contract type (`ISystemEnginePlugin` / `ISystemShellPlugin`) and an optional allow-list of system
names, and yields the instances. A misplaced or unloadable plugin is logged and skipped — it never
aborts discovery.

Because .NET lazy-loads referenced assemblies, discovery first ensures candidate plugin assemblies
are loaded, using three strategies:

1. **Build-emitted manifest** — the entry assembly carries
   `[assembly: AssemblyMetadata("Highbyte.DotNet6502.PluginAssembly", "<name>")]` items, one per
   `Highbyte.DotNet6502.*` assembly in its build closure, emitted by the host's
   `EmitPluginAssemblyManifest` MSBuild target. **This is the only strategy that works on browser
   WASM**, where there is no enumerable filesystem.
2. **Transitive reference walk** from the entry assembly.
3. **Filesystem fallback** — probe the app base directory for not-yet-loaded
   `Highbyte.DotNet6502.*` DLLs (the C# compiler prunes `ProjectReference`s whose types are never
   statically used — exactly the case for attribute-only plugin assemblies).

## How a host app uses it

1. The entry exe references the per-system plugin projects only via naming-convention
   `ProjectReference` globs (`App.<Tech>.Shell.*`, `Impl.Headless.*`) so their DLLs land in the
   output — no code reference.
2. At startup the app calls `SystemPluginDiscovery.Discover<ISystemEnginePlugin>()` (and, for GUI
   hosts, `Discover<ISystemShellPlugin>()`), optionally passing the configured enabled-system list.
3. Each discovered engine plugin's `Register(...)` adds its system to DI; shell plugins contribute
   their UI.
4. The app then runs whatever systems were discovered — it never names them in code.

## See also

- [Implementation libraries overview](../implementation/overview.md) — the `Impl.<Tech>.<System>`
  engine-plugin libraries.
- [`Highbyte.DotNet6502.Systems`](dotnet6502-systems.md) — `ISystemConfigurer`, `HostApp`, and the
  abstractions the plugins wire up.
