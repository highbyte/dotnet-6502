using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Plugins;

/// <summary>
/// Discovers plugin instances from assemblies loaded in the current AppDomain.
/// Reads <see cref="SystemPluginAttribute"/> on each assembly, instantiates the
/// referenced types, and filters by the plugin contract type plus an optional
/// allow-list of system names.
/// </summary>
/// <remarks>
/// Static-reference discovery works on every target (Desktop, WASM, iOS, Android).
/// Loading plugin DLLs from disk is a separate concern handled by a different
/// extension; this class is for assemblies already loaded.
/// </remarks>
public static class SystemPluginDiscovery
{
    /// <summary>
    /// Assembly-metadata key under which an entry assembly lists the plug-in assemblies it ships,
    /// so discovery can <see cref="Assembly.Load(AssemblyName)"/> them by name. Emitted at build
    /// time by the host's <c>EmitPluginAssemblyManifest</c> MSBuild target — one
    /// <c>[assembly: AssemblyMetadata("Highbyte.DotNet6502.PluginAssembly", "&lt;name&gt;")]</c>
    /// per <c>Highbyte.DotNet6502.*</c> assembly in the build closure. This is the discovery path
    /// for browser WASM, where the filesystem scan cannot work. See
    /// <see cref="EnsureReferencedAssembliesLoaded"/>.
    /// </summary>
    public const string PluginAssemblyMetadataKey = "Highbyte.DotNet6502.PluginAssembly";

    /// <summary>
    /// Discover plugin instances of type <typeparamref name="TPlugin"/> in the current AppDomain.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin contract — typically <see cref="ISystemShellPlugin"/>
    /// or a closed <c>ISystemEnginePlugin&lt;TIn,TAu&gt;</c>.</typeparam>
    /// <param name="enabledSystemNames">If supplied, only plugins whose
    /// <c>SystemName</c> property matches one of these (case-insensitive) are returned.
    /// Pass <c>null</c> to return all discovered plugins.</param>
    /// <param name="logger">Optional logger for skipped/failed plugins.</param>
    public static IEnumerable<TPlugin> Discover<TPlugin>(
        IEnumerable<string>? enabledSystemNames = null,
        ILogger? logger = null)
        where TPlugin : class
    {
        logger ??= NullLogger.Instance;

        HashSet<string>? allowList = enabledSystemNames is null
            ? null
            : new HashSet<string>(enabledSystemNames, StringComparer.OrdinalIgnoreCase);

        var systemNameProp = typeof(TPlugin).GetProperty("SystemName");

        // .NET lazy-loads referenced assemblies, so a plug-in DLL that's only referenced
        // via ProjectReference may not appear in AppDomain.GetAssemblies() at this point.
        // Walk the transitive reference graph and ensure each is loaded before scanning.
        EnsureReferencedAssembliesLoaded(logger);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            SystemPluginAttribute[] attrs;
            try
            {
                attrs = assembly.GetCustomAttributes<SystemPluginAttribute>().ToArray();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not read SystemPluginAttribute from {Assembly}", assembly.FullName);
                continue;
            }

            foreach (var attr in attrs)
            {
                bool assignable;
                try
                {
                    assignable = typeof(TPlugin).IsAssignableFrom(attr.PluginType);
                }
                catch (Exception ex)
                {
                    // Evaluating IsAssignableFrom forces the plugin type's interface graph to load.
                    // That can throw (TypeLoadException / FileNotFoundException) if the plugin's
                    // dependencies — e.g. the input/audio context assemblies of a different host —
                    // are not present on disk. Don't let one misplaced plugin abort discovery.
                    logger.LogDebug(ex,
                        "Skipping plugin {Plugin} from {Assembly} — could not resolve its type graph " +
                        "(dependency assemblies missing?)",
                        attr.PluginType.FullName, assembly.FullName);
                    continue;
                }

                if (!assignable)
                {
                    // The plugin does not implement the requested contract — e.g. an engine plugin
                    // closed over a different <TInput,TAudio> pair, or a shell plugin when engine
                    // plugins were requested. Expected when plugins for several hosts share a folder.
                    logger.LogDebug(
                        "Skipping plugin {Plugin} from {Assembly} — does not implement {Contract}",
                        attr.PluginType.FullName, assembly.FullName, typeof(TPlugin));
                    continue;
                }

                TPlugin? instance;
                try
                {
                    instance = (TPlugin?)Activator.CreateInstance(attr.PluginType);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to instantiate plugin {Plugin} from {Assembly}",
                        attr.PluginType.FullName, assembly.FullName);
                    continue;
                }
                if (instance is null)
                    continue;

                if (allowList is not null && systemNameProp is not null)
                {
                    var name = systemNameProp.GetValue(instance) as string;
                    if (name is null || !allowList.Contains(name))
                    {
                        logger.LogDebug("Skipping plugin {Plugin} (SystemName={Name}) — not in enabled list",
                            attr.PluginType.FullName, name);
                        continue;
                    }
                }

                yield return instance;
            }
        }
    }

    /// <summary>
    /// Logs diagnostics comparing the configured <c>EnabledSystems</c> list with the plugins that
    /// discovery actually produced. Helps explain a system that silently fails to appear:
    /// <list type="bullet">
    /// <item>A <see cref="LogLevel.Warning"/> for each enabled system with no engine plugin — it
    /// will not appear in the system list.</item>
    /// <item>A <see cref="LogLevel.Warning"/> for each system that has a shell/UI plugin but no
    /// engine plugin — the shell services are registered but the system never appears.</item>
    /// <item>A <see cref="LogLevel.Information"/> for each system that has an engine plugin but no
    /// shell plugin — it runs without a per-system menu / config UI (legal, e.g. Headless).</item>
    /// </list>
    /// Missing per-system menu/info/config <i>contributions</i> are not flagged: an
    /// <see cref="ISystemShellPlugin"/> may legally return <c>null</c> from any of its
    /// <c>CreateXxxContribution</c> methods.
    /// </summary>
    /// <param name="enabledSystemNames">The configured <c>EnabledSystems</c> allow-list, or
    /// <c>null</c> when all systems are enabled (the enabled-but-missing check is then skipped).</param>
    /// <param name="enginePlugins">Engine plugins discovered for this host.</param>
    /// <param name="shellPlugins">Shell/UI plugins discovered for this host, or <c>null</c> for
    /// hosts that have no shell layer (e.g. Headless).</param>
    /// <param name="logger">Logger that receives the diagnostics.</param>
    public static void LogPluginDiagnostics(
        IEnumerable<string>? enabledSystemNames,
        IReadOnlyCollection<ISystemEnginePlugin> enginePlugins,
        IReadOnlyCollection<ISystemShellPlugin>? shellPlugins,
        ILogger logger)
    {
        logger ??= NullLogger.Instance;

        var engineNames = new HashSet<string>(
            enginePlugins.Select(p => p.SystemName), StringComparer.OrdinalIgnoreCase);
        var shellNames = new HashSet<string>(
            shellPlugins?.Select(p => p.SystemName) ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var enabledList = enabledSystemNames?
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // Enabled in config, but no engine plugin discovered → the system will not appear.
        if (enabledList is not null)
        {
            foreach (var name in enabledList)
            {
                if (engineNames.Contains(name))
                    continue;
                if (shellNames.Contains(name))
                    logger.LogWarning(
                        "System '{System}' is enabled but only a shell/UI plugin was found — no engine " +
                        "plugin. It will not appear in the system list. Check that the Impl.<Tech>.{System} " +
                        "project is deployed (for browser builds, that it is listed under TrimmerRootAssembly).",
                        name, name);
                else
                    logger.LogWarning(
                        "System '{System}' is enabled but no plugin was found for it — it will not appear " +
                        "in the system list. Check that the system's projects are deployed (for browser " +
                        "builds, that they are listed under TrimmerRootAssembly).",
                        name);
            }
        }

        // Shell plugin present without an engine plugin → not caught above when EnabledSystems is null.
        foreach (var name in shellNames)
        {
            if (engineNames.Contains(name))
                continue;
            if (enabledList is not null &&
                enabledList.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                continue; // already reported by the enabled-list loop above
            logger.LogWarning(
                "System '{System}' has a shell/UI plugin but no engine plugin — it will not appear in the " +
                "system list. The shell plugin's services are registered but unused.",
                name);
        }

        // Engine plugin present without a shell plugin → runs, but has no per-system UI.
        if (shellPlugins is not null)
        {
            foreach (var name in engineNames)
            {
                if (shellNames.Contains(name))
                    continue;
                logger.LogInformation(
                    "System '{System}' has an engine plugin but no shell/UI plugin — it will run without a " +
                    "per-system menu or config dialog.",
                    name);
            }
        }
    }

    private static void EnsureReferencedAssembliesLoaded(ILogger logger)
    {
        var loaded = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name ?? ""),
            StringComparer.Ordinal);

        var entry = Assembly.GetEntryAssembly();

        // (1) Build-emitted manifest. The entry assembly may carry
        // [assembly: AssemblyMetadata("Highbyte.DotNet6502.PluginAssembly", "<name>")] items — one
        // per Highbyte.DotNet6502.* assembly in its build closure — emitted by the host's
        // EmitPluginAssemblyManifest MSBuild target. This is the only strategy that works on
        // browser WASM: there is no enumerable filesystem there (so (3) finds nothing), and a
        // system-agnostic host holds no static reference to the per-system plug-in assemblies for
        // the reference walk (2) to follow.
        if (entry is not null)
        {
            foreach (var meta in entry.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (!string.Equals(meta.Key, PluginAssemblyMetadataKey, StringComparison.Ordinal))
                    continue;
                var name = meta.Value;
                if (string.IsNullOrEmpty(name) || !loaded.Add(name))
                    continue;
                try
                {
                    Assembly.Load(new AssemblyName(name));
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not load manifest plug-in assembly {Name}", name);
                }
            }
        }

        // (2) Transitive reference walk from the entry assembly. Picks up any plug-in DLL whose
        // assembly metadata is still referenced from the executable (i.e. the C# compiler did not
        // optimise the reference away).
        if (entry is not null)
        {
            var queue = new Queue<Assembly>();
            queue.Enqueue(entry);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var refName in current.GetReferencedAssemblies())
                {
                    if (refName.Name is null || !loaded.Add(refName.Name))
                        continue;
                    try
                    {
                        queue.Enqueue(Assembly.Load(refName));
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Could not load referenced assembly {Ref}", refName.FullName);
                    }
                }
            }
        }

        // (3) Filesystem fallback: the C# compiler prunes ProjectReferences when no type from the
        // referenced assembly is statically used. Plug-in assemblies — discovered purely via
        // [SystemPlugin] attributes — fit that case. Probe the app's base directory for any DLL
        // we haven't loaded yet whose name suggests it might carry plug-ins.
        // Heuristic: only probe DLLs that start with a known plug-in-host prefix to avoid loading
        // every native/third-party DLL into the AppDomain.
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            return;

        foreach (var path in Directory.EnumerateFiles(baseDir, "*.dll"))
        {
            var nameFromFile = Path.GetFileNameWithoutExtension(path);
            if (!nameFromFile.StartsWith("Highbyte.DotNet6502.", StringComparison.Ordinal))
                continue;
            if (loaded.Contains(nameFromFile))
                continue;
            try
            {
                var asm = Assembly.LoadFrom(path);
                var loadedName = asm.GetName().Name;
                if (loadedName is not null)
                    loaded.Add(loadedName);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not LoadFrom {Path}", path);
            }
        }
    }
}
