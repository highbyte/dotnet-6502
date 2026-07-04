using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Factory that creates the appropriate <see cref="IScriptingEngine"/> based on configuration.
/// Returns a <see cref="MoonSharpScriptingEngine"/> when scripting is enabled,
/// or a <see cref="NoScriptingEngine"/> when disabled or misconfigured.
/// </summary>
public static class MoonSharpScriptingConfigurator
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Configuration binding targets the known ScriptingConfig model used directly by the application.")]
    public static IScriptingEngine Create(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IReadOnlyList<string>? scriptFilePaths = null,
        string? scriptDirectoryOverride = null,
        bool suppressConfigScripts = false,
        string hostType = "unknown")
    {
        var logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingConfigurator));

        var config = new ScriptingConfig();
        configuration.GetSection(ScriptingConfig.ConfigSectionName).Bind(config);

        if (!config.Enabled)
        {
            logger.LogInformation("[Scripting] Disabled in configuration. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        var hasScriptFilePaths = scriptFilePaths is { Count: > 0 };

        // Automated startup mode (--start etc) owns the lifecycle, so suppress scripts from config.
        // Blank ScriptDirectory normally means "use the default", so return a disabled engine
        // unless an explicit CLI script source is present.
        if (suppressConfigScripts && scriptDirectoryOverride == null && !hasScriptFilePaths)
        {
            logger.LogInformation("[Scripting] Suppressing scripts from configuration (automated startup mode). Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        // Apply CLI overrides
        if (scriptDirectoryOverride != null)
        {
            logger.LogInformation("[Scripting] ScriptDirectory overridden via CLI: {Dir}", scriptDirectoryOverride);
            config.ScriptDirectory = scriptDirectoryOverride;
        }

        if (hasScriptFilePaths)
        {
            logger.LogInformation("[Scripting] Loading {Count} specific script(s) via CLI: {Files}",
                scriptFilePaths!.Count, string.Join(", ", scriptFilePaths));
            config.ScriptLoader = () => scriptFilePaths.Select(path =>
            {
                var fullPath = Path.GetFullPath(path);
                return (Path.GetFileName(fullPath), File.ReadAllText(fullPath));
            });
        }

        // Force auto-enable when CLI overrides are in effect (automation intent)
        if (hasScriptFilePaths || scriptDirectoryOverride != null)
        {
            config.EnableScriptsAtStart = true;
        }

        var resolvedScriptDirectory = config.ResolvedScriptDirectory();

        // ScriptDirectory is only required when no ScriptLoader is set and no default is available.
        if (config.ScriptLoader == null && string.IsNullOrWhiteSpace(resolvedScriptDirectory))
        {
            logger.LogWarning("[Scripting] Enabled but ScriptDirectory is not set. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        logger.LogInformation("[Scripting] MoonSharp engine enabled. ScriptDirectory: {Dir}", resolvedScriptDirectory);
        var adapter = new MoonSharpScriptingEngineAdapter(loggerFactory, hostType);
        return new ScriptingEngine(adapter, config, loggerFactory);
    }

    /// <summary>
    /// Creates the appropriate <see cref="IScriptingEngine"/> for browser/WASM environments.
    /// Returns <see cref="NoScriptingEngine"/> if scripting is disabled in configuration.
    /// </summary>
    /// <param name="config">
    /// Pre-bound <see cref="ScriptingConfig"/>. Binding must be done by the caller (e.g. in Program.cs)
    /// where the AOT ConfigurationBindingGenerator can see the call site and generate trim-safe code.
    /// </param>
    public static IScriptingEngine CreateForBrowser(
        ScriptingConfig config,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingConfigurator));

        if (!config.Enabled)
        {
            logger.LogInformation("[Scripting] Disabled in configuration. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        // Filesystem access is not available in browser/WASM environments.
        // Force these off regardless of what the configuration says.
        if (config.AllowFileIO)
        {
            logger.LogWarning("[Scripting] AllowFileIO is not supported in browser environments and will be ignored.");
            config.AllowFileIO = false;
        }

        // Raw TCP sockets are not available in browser/WASM environments.
        if (config.AllowTcpClient)
        {
            logger.LogWarning("[Scripting] AllowTcpClient is not supported in browser environments and will be ignored.");
            config.AllowTcpClient = false;
        }

        logger.LogInformation("[Scripting] MoonSharp browser engine enabled (scripts loaded via localStorage callback).");
        var adapter = new MoonSharpScriptingEngineAdapter(loggerFactory, "browser");
        return new ScriptingEngine(adapter, config, loggerFactory);
    }
}
