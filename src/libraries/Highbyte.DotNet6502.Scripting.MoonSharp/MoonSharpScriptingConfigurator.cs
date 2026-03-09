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

        logger.LogInformation("[Scripting] MoonSharp browser engine enabled (scripts loaded via localStorage callback).");
        var adapter = new MoonSharpScriptingEngineAdapter(loggerFactory);
        return new ScriptingEngine(adapter, config, loggerFactory);
    }
}
