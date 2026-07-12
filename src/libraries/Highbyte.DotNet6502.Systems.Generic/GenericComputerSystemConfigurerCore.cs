using Highbyte.DotNet6502.Systems.Generic.Config;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Generic;

/// <summary>
/// Host-agnostic <see cref="ISystemConfigurer"/> for the Generic computer. Implements every part
/// of configuring and building a Generic computer that carries no host-technology dependency.
/// </summary>
/// <remarks>
/// Concrete, so a host with no host-technology glue (the Headless host) uses it directly. Hosts
/// that need tech-specific wiring subclass it and override only the relevant <c>virtual</c>
/// members — typically <see cref="BuildSystemRunner"/> (to attach an input handler) and
/// <see cref="LoadExampleProgramBytesAsync"/> (to source example program bytes from an embedded
/// resource or over HTTP). See <c>docs/system-configurer-consolidation.md</c>.
/// </remarks>
public class GenericComputerSystemConfigurerCore : ISystemConfigurer
{
    protected ILoggerFactory LoggerFactory { get; }
    protected IConfiguration Configuration { get; }
    private readonly Func<IHostSystemConfig> _hostConfigFactory;
    private readonly string _configSectionName;

    /// <param name="loggerFactory">Logger factory passed to <see cref="GenericComputerBuilder"/>.</param>
    /// <param name="configuration">Configuration the default <see cref="GetNewHostSystemConfig"/> binds from.</param>
    /// <param name="hostConfigFactory">Creates a fresh, host-specific <see cref="IHostSystemConfig"/>.</param>
    /// <param name="configSectionName">Section the default <see cref="GetNewHostSystemConfig"/> binds the host config from.</param>
    public GenericComputerSystemConfigurerCore(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        Func<IHostSystemConfig> hostConfigFactory,
        string configSectionName)
    {
        LoggerFactory = loggerFactory;
        Configuration = configuration;
        _hostConfigFactory = hostConfigFactory;
        _configSectionName = configSectionName;
    }

    /// <summary>
    /// Ctor for subclasses that fully override config loading/persisting and so need no
    /// <see cref="IConfiguration"/> (e.g. the browser host, which uses local storage).
    /// </summary>
    protected GenericComputerSystemConfigurerCore(
        ILoggerFactory loggerFactory,
        Func<IHostSystemConfig> hostConfigFactory)
        : this(loggerFactory, new ConfigurationBuilder().Build(), hostConfigFactory, configSectionName: "")
    {
    }

    public string SystemName => GenericComputer.SystemName;

    public virtual Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(((GenericComputerSystemConfig)systemConfig).ExamplePrograms.Keys.ToList());

    public IScreen? GetScreenInfo(string configurationVariant, ISystemConfig systemConfig)
    {
        var genericSystemConfig = (GenericComputerSystemConfig)systemConfig;
        var genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericSystemConfig);
        var screen = genericComputerConfig.Memory.Screen;
        const int characterWidth = 8;
        const int characterHeight = 8;

        return new ScreenInfo(
            screen.Cols * characterWidth,
            screen.Rows * characterHeight,
            (screen.Cols + (2 * screen.BorderCols)) * characterWidth,
            (screen.Rows + (2 * screen.BorderRows)) * characterHeight,
            genericComputerConfig.ScreenRefreshFrequencyHz);
    }

    /// <summary>
    /// Creates a fresh host config via the supplied factory and binds it from the matching
    /// <see cref="IConfiguration"/> section. Hosts that store config elsewhere (e.g. browser
    /// local storage) override this.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Host config binding is limited to known application config models that are rooted by the host application.")]
    public virtual Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var hostConfig = _hostConfigFactory();
        Configuration.GetSection(_configSectionName).Bind(hostConfig);
        return Task.FromResult(hostConfig);
    }

    /// <summary>No-op by default. Hosts that persist user config override this.</summary>
    public virtual Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
        => Task.CompletedTask;

    public async Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var genericSystemConfig = (GenericComputerSystemConfig)systemConfig;

        GenericComputerConfig? genericComputerConfig = null;
        if (genericSystemConfig.ExamplePrograms.TryGetValue(configurationVariant, out var exampleProgramPath)
            && !string.IsNullOrEmpty(exampleProgramPath))
        {
            var exampleProgramBytes = await LoadExampleProgramBytesAsync(exampleProgramPath);
            if (exampleProgramBytes != null)
                genericComputerConfig = GenericComputerExampleConfigs.GetExampleConfig(
                    configurationVariant, genericSystemConfig, exampleProgramBytes);
        }

        // No host-supplied program bytes — fall back to the built-in example config.
        genericComputerConfig ??= GenericComputerExampleConfigs.GetExampleConfig(configurationVariant, genericSystemConfig);

        return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig, LoggerFactory);
    }

    /// <summary>
    /// Loads the raw bytes of an example program. Returns <c>null</c> by default — meaning "use the
    /// built-in example config". Hosts that ship example <c>.prg</c> files override this to source
    /// the bytes from an embedded resource (desktop) or over HTTP (browser).
    /// </summary>
    protected virtual Task<byte[]?> LoadExampleProgramBytesAsync(string exampleProgramPath)
        => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// Builds a <see cref="SystemRunner"/> with no input consumer wired — the base behaviour for a
    /// host with no input (the Headless host). Tech hosts override to attach an input handler.
    /// </summary>
    public virtual Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
        => Task.FromResult(new SystemRunner((GenericComputer)system));
}
