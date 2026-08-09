using System.Diagnostics.CodeAnalysis;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2;

/// <summary>
/// Host-agnostic <see cref="ISystemConfigurer"/> for the Apple II.
/// Tech hosts (Avalonia, SilkNet, …) subclass and override <see cref="BuildSystemRunner"/> to
/// wire their input handler — the same shape as <c>Vic20SystemConfigurerCore</c>.
/// </summary>
public class Apple2SystemConfigurerCore : ISystemConfigurer
{
    protected ILoggerFactory LoggerFactory { get; }
    protected IConfiguration Configuration { get; }
    private readonly Func<IHostSystemConfig> _hostConfigFactory;
    private readonly string _configSectionName;

    public Apple2SystemConfigurerCore(
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

    protected Apple2SystemConfigurerCore(ILoggerFactory loggerFactory, Func<IHostSystemConfig> hostConfigFactory)
        : this(loggerFactory, new ConfigurationBuilder().Build(), hostConfigFactory, configSectionName: "")
    {
    }

    public string SystemName => Apple2System.SystemName;

    public virtual IEnumerable<string> GetUserContentDirectories()
        => [Apple2SystemConfig.DefaultROMDirectory];

    /// <summary>
    /// The variant names the emulated machine model, following the C64 precedent
    /// (C64NTSC/C64PAL). Only the Apple II Plus is emulated. The original (non-Autostart,
    /// Integer BASIC) machine is a plausible later variant — it needs a different ROM set,
    /// not different hardware.
    /// </summary>
    public const string VariantApple2Plus = "APPLE2PLUS";

    public virtual Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(new List<string> { VariantApple2Plus });

    public IScreen? GetScreenInfo(string configurationVariant, ISystemConfig systemConfig)
    {
        var apple2Config = BuildApple2ConfigForVariant(configurationVariant);
        return new ScreenInfo(
            Apple2Config.DrawableAreaWidth,
            Apple2Config.DrawableAreaHeight,
            Apple2Config.DrawableAreaWidth,
            Apple2Config.DrawableAreaHeight,
            apple2Config.ScreenRefreshFrequencyHz);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Host config binding is limited to known application config models that are rooted by the host application.")]
    public virtual Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var hostConfig = _hostConfigFactory();
        var section = Configuration.GetSection(_configSectionName);
        section.Bind(hostConfig);

        // IConfiguration.Bind() does not apply the JsonPropertyName aliases used for the
        // persisted Type-valued render settings, so read those keys explicitly.
        ApplyTypeOverridesFromConfig(section.GetSection(nameof(IHostSystemConfig.SystemConfig)), hostConfig.SystemConfig);

        return Task.FromResult(hostConfig);
    }

    private static void ApplyTypeOverridesFromConfig(IConfiguration systemConfigSection, ISystemConfig systemConfig)
    {
        ApplyTypeKey(systemConfigSection, "RenderProviderType", systemConfig.SetRenderProviderType);
        ApplyTypeKey(systemConfigSection, "RenderTargetType", systemConfig.SetRenderTargetType);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Configured type names are validated immediately and constrained to application-defined types.")]
    private static void ApplyTypeKey(IConfiguration section, string key, Action<Type?> setter)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            return;
        var t = Type.GetType(value)
            ?? throw new DotNet6502Exception($"{key} '{value}' could not be resolved.");
        setter(t);
    }

    public virtual Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
        => Task.CompletedTask;

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var apple2SystemConfig = (Apple2SystemConfig)systemConfig;
        var apple2Config = BuildApple2ConfigForVariant(configurationVariant);
        apple2Config.CpuCompatibilityProfile = apple2SystemConfig.CpuCompatibilityProfile;
        apple2Config.MonitorColor = apple2SystemConfig.MonitorColor;
        apple2Config.AudioEnabled = apple2SystemConfig.AudioEnabled;
        apple2Config.LanguageCardEnabled = apple2SystemConfig.LanguageCardEnabled;
        apple2Config.AudioProviderType = apple2SystemConfig.AudioProviderType;

        Dictionary<string, byte[]>? romData = null;
        if (apple2SystemConfig.ROMs.Count > 0)
            romData = ROM.LoadROMS(apple2SystemConfig.EffectiveROMDirectory, apple2SystemConfig.ROMs.ToArray());

        var apple2 = new Apple2System(apple2Config, LoggerFactory, romData);
        apple2.SetCurrentRenderProviderType(apple2SystemConfig.RenderProviderType);
        ISystem system = apple2;
        return Task.FromResult(system);
    }

    private static Apple2Config BuildApple2ConfigForVariant(string configurationVariant)
        => new();

    public virtual Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
        => Task.FromResult(new SystemRunner((Apple2System)system));
}
