using System.Diagnostics.CodeAnalysis;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric;

/// <summary>Host-independent construction and configuration for the Oric Atmos.</summary>
public class OricSystemConfigurerCore : ISystemConfigurer
{
    public const string VariantAtmos48K = "ATMOS48K";

    protected ILoggerFactory LoggerFactory { get; }
    protected IConfiguration Configuration { get; }
    private readonly Func<IHostSystemConfig> _hostConfigFactory;
    private readonly string _configSectionName;

    public OricSystemConfigurerCore(
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

    protected OricSystemConfigurerCore(ILoggerFactory loggerFactory, Func<IHostSystemConfig> hostConfigFactory)
        : this(loggerFactory, new ConfigurationBuilder().Build(), hostConfigFactory, string.Empty) { }

    public string SystemName => OricMachine.SystemName;

    public virtual IEnumerable<string> GetUserContentDirectories()
        => [OricSystemConfig.DefaultROMDirectory];

    public virtual Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(new List<string> { VariantAtmos48K });

    public IScreen? GetScreenInfo(string configurationVariant, ISystemConfig systemConfig)
    {
        ValidateVariant(configurationVariant);
        return new ScreenInfo(
            OricConfig.VisibleWidth,
            OricConfig.VisibleHeight,
            OricConfig.VisibleWidth,
            OricConfig.VisibleHeight,
            OricConfig.ScreenRefreshFrequencyHz);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only known host config models are bound and rooted by the host.")]
    public virtual Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var hostConfig = _hostConfigFactory();
        var section = Configuration.GetSection(_configSectionName);
        section.Bind(hostConfig);
        ApplyTypeOverrides(section.GetSection(nameof(IHostSystemConfig.SystemConfig)), hostConfig.SystemConfig);
        return Task.FromResult(hostConfig);
    }

    public virtual Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig) => Task.CompletedTask;

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        ValidateVariant(configurationVariant);
        var oricSystemConfig = (OricSystemConfig)systemConfig;
        var config = new OricConfig
        {
            CpuCompatibilityProfile = oricSystemConfig.CpuCompatibilityProfile,
            AudioEnabled = oricSystemConfig.AudioEnabled,
            AudioProviderType = oricSystemConfig.AudioProviderType,
            VSyncHackEnabled = oricSystemConfig.VSyncHackEnabled,
            JoystickInterface = oricSystemConfig.JoystickInterface,
            KeyboardJoystickEnabled = oricSystemConfig.KeyboardJoystickEnabled,
            KeyboardJoystick = oricSystemConfig.KeyboardJoystick,
        };

        Dictionary<string, byte[]>? romData = null;
        if (oricSystemConfig.ROMs.Count > 0)
            romData = ROM.LoadROMS(oricSystemConfig.EffectiveROMDirectory, oricSystemConfig.ROMs.ToArray());

        var oric = new OricMachine(config, LoggerFactory, romData);
        oric.SetCurrentRenderProviderType(oricSystemConfig.RenderProviderType);
        return Task.FromResult<ISystem>(oric);
    }

    public virtual Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
        => Task.FromResult(new SystemRunner((OricMachine)system));

    private static void ValidateVariant(string configurationVariant)
    {
        if (!string.Equals(configurationVariant, VariantAtmos48K, StringComparison.Ordinal))
            throw new DotNet6502Exception($"Unsupported Oric configuration variant: {configurationVariant}");
    }

    private static void ApplyTypeOverrides(IConfiguration section, ISystemConfig config)
    {
        ApplyTypeKey(section, "RenderProviderType", config.SetRenderProviderType);
        ApplyTypeKey(section, "RenderTargetType", config.SetRenderTargetType);
        ApplyTypeKey(section, "AudioProviderType", config.SetAudioProviderType);
        ApplyTypeKey(section, "AudioTargetType", config.SetAudioTargetType);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Configured type names are constrained to application-defined types by the setters.")]
    private static void ApplyTypeKey(IConfiguration section, string key, Action<Type?> setter)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            return;
        setter(Type.GetType(value) ?? throw new DotNet6502Exception($"{key} '{value}' could not be resolved."));
    }
}
