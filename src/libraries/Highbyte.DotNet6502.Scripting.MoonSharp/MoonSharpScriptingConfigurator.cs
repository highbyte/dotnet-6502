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
    public static IScriptingEngine Create(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingConfigurator));

        var config = new ScriptingConfig();
        configuration.GetSection(ScriptingConfig.ConfigSectionName).Bind(config);

        if (!config.Enabled)
        {
            logger.LogInformation("[Scripting] Disabled in configuration. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        if (string.IsNullOrWhiteSpace(config.ScriptDirectory))
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
    /// Scripts are supplied via <paramref name="scriptLoader"/> instead of being read from the filesystem.
    /// Returns <see cref="NoScriptingEngine"/> if scripting is disabled in configuration.
    /// </summary>
    public static IScriptingEngine CreateForBrowser(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        Func<IEnumerable<(string fileName, string content)>> scriptLoader)
    {
        var logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingConfigurator));

        var config = new ScriptingConfig();
        configuration.GetSection(ScriptingConfig.ConfigSectionName).Bind(config);

        if (!config.Enabled)
        {
            logger.LogInformation("[Scripting] Disabled in configuration. Using NoScriptingEngine.");
            return new NoScriptingEngine();
        }

        // File I/O and HTTP are not available in the browser sandbox.
        config.AllowFileIO = false;
        config.AllowHttpRequests = false;
        config.ScriptLoader = scriptLoader;

        logger.LogInformation("[Scripting] MoonSharp browser engine enabled (scripts loaded via localStorage callback).");
        var adapter = new MoonSharpScriptingEngineAdapter(loggerFactory);
        return new ScriptingEngine(adapter, config, loggerFactory);
    }
}
