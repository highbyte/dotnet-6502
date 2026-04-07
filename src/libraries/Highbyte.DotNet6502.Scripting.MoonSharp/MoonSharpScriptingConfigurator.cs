using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Factory that creates the appropriate <see cref="IScriptingEngine"/> based on configuration.
/// Returns a <see cref="MoonSharpScriptingEngine"/> when scripting is enabled,
/// or a <see cref="NoScriptingEngine"/> when disabled or misconfigured.
/// </summary>
public static class MoonSharpScriptingConfigurator
{
    public static IScriptingEngine Create(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IReadOnlyList<string>? scriptFilePaths = null,
        string? scriptDirectoryOverride = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingConfigurator));

        var config = new ScriptingConfig();
        configuration.GetSection(ScriptingConfig.ConfigSectionName).Bind(config);

        if (!config.Enabled)
        {
            logger.LogInformation("[Scripting] Disabled in configuration. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        // Apply CLI overrides
        if (scriptDirectoryOverride != null)
        {
            logger.LogInformation("[Scripting] ScriptDirectory overridden via CLI: {Dir}", scriptDirectoryOverride);
            config.ScriptDirectory = scriptDirectoryOverride;
        }

        if (scriptFilePaths != null && scriptFilePaths.Count > 0)
        {
            logger.LogInformation("[Scripting] Loading {Count} specific script(s) via CLI: {Files}",
                scriptFilePaths.Count, string.Join(", ", scriptFilePaths));
            config.ScriptLoader = () => scriptFilePaths.Select(path =>
            {
                var fullPath = Path.GetFullPath(path);
                return (Path.GetFileName(fullPath), File.ReadAllText(fullPath));
            });
        }

        // Force auto-enable when CLI overrides are in effect (automation intent)
        if (scriptFilePaths?.Count > 0 || scriptDirectoryOverride != null)
        {
            config.EnableScriptsAtStart = true;
        }

        // ScriptDirectory is only required when no ScriptLoader is set
        if (config.ScriptLoader == null && string.IsNullOrWhiteSpace(config.ScriptDirectory))
        {
            logger.LogWarning("[Scripting] Enabled but ScriptDirectory is not set. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        logger.LogInformation("[Scripting] MoonSharp engine enabled. ScriptDirectory: {Dir}", config.ScriptDirectory);
        var adapter = new MoonSharpScriptingEngineAdapter(loggerFactory);
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
        var adapter = new MoonSharpScriptingEngineAdapter(loggerFactory);
        return new ScriptingEngine(adapter, config, loggerFactory);
    }
}
