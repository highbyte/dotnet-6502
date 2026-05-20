using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64;

/// <summary>
/// Host-agnostic C64 <see cref="ISystemConfigurer"/>. Implements every part of configuring and
/// building a C64 that carries no host-technology dependency — system name, configuration
/// variants, <see cref="BuildSystem"/>, and the appsettings-bound <see cref="GetNewHostSystemConfig"/>.
/// </summary>
/// <remarks>
/// Concrete, so a host with no host-technology glue (the Headless host) uses it directly. Hosts
/// that need tech-specific wiring subclass it and override only the relevant <c>virtual</c>
/// members — typically <see cref="BuildSystemRunner"/> (to attach an input handler) and, for the
/// browser host, <see cref="GetNewHostSystemConfig"/>/<see cref="PersistHostSystemConfig"/>.
/// The host config is supplied as an opaque <see cref="IHostSystemConfig"/> factory plus its
/// configuration section name, so the core stays independent of any host-config base class.
/// See <c>docs/system-configurer-consolidation.md</c>.
/// </remarks>
public class C64SystemConfigurerCore : ISystemConfigurer
{
    protected ILoggerFactory LoggerFactory { get; }
    protected IConfiguration Configuration { get; }
    private readonly Func<IHostSystemConfig> _hostConfigFactory;
    private readonly string _configSectionName;

    /// <param name="loggerFactory">Logger factory passed to <see cref="C64.BuildC64"/>.</param>
    /// <param name="configuration">Configuration the default <see cref="GetNewHostSystemConfig"/> binds from.</param>
    /// <param name="hostConfigFactory">Creates a fresh, host-specific <see cref="IHostSystemConfig"/>.</param>
    /// <param name="configSectionName">Section the default <see cref="GetNewHostSystemConfig"/> binds the host config from.</param>
    public C64SystemConfigurerCore(
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
    protected C64SystemConfigurerCore(
        ILoggerFactory loggerFactory,
        Func<IHostSystemConfig> hostConfigFactory)
        : this(loggerFactory, new ConfigurationBuilder().Build(), hostConfigFactory, configSectionName: "")
    {
    }

    public string SystemName => C64.SystemName;

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(C64ModelInventory.C64Models.Keys.ToList());

    /// <summary>
    /// Creates a fresh host config via the supplied factory and binds it from the matching
    /// <see cref="IConfiguration"/> section. Hosts that store config elsewhere (e.g. browser
    /// local storage) override this.
    /// </summary>
    public virtual Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var hostConfig = _hostConfigFactory();
        Configuration.GetSection(_configSectionName).Bind(hostConfig);
        return Task.FromResult(hostConfig);
    }

    /// <summary>No-op by default. Hosts that persist user config override this.</summary>
    public virtual Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
        => Task.CompletedTask;

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var c64SystemConfig = (C64SystemConfig)systemConfig;
        var c64Config = new C64Config
        {
            C64Model = configurationVariant,
            Vic2Model = C64ModelInventory.C64Models[configurationVariant].Vic2Models.First().Name,
            AudioEnabled = c64SystemConfig.AudioEnabled,
            KeyboardJoystickEnabled = c64SystemConfig.KeyboardJoystickEnabled,
            KeyboardJoystick = c64SystemConfig.KeyboardJoystick,
            ROMs = c64SystemConfig.ROMs,
            ROMDirectory = c64SystemConfig.ROMDirectory,
            RenderProviderType = c64SystemConfig.RenderProviderType ?? DefaultRenderProviderType,
        };

        var c64 = C64.BuildC64(c64Config, LoggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    /// <summary>
    /// Builds a <see cref="SystemRunner"/> with no input consumer wired — the base behaviour for a
    /// host with no input (the Headless host). Tech hosts override to attach a C64 input handler.
    /// </summary>
    public virtual Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
        => Task.FromResult(new SystemRunner((C64)system));

    /// <summary>
    /// Fallback render-provider type used when <see cref="C64SystemConfig.RenderProviderType"/> is
    /// not set. Null by default (the host decides); the browser host overrides it.
    /// </summary>
    protected virtual Type? DefaultRenderProviderType => null;
}
