using Highbyte.DotNet6502.Systems.Vic20.Config;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Vic20;

/// <summary>
/// Host-agnostic <see cref="ISystemConfigurer"/> for the VIC-20.
/// Tech hosts (Avalonia, SilkNet, …) subclass and override <see cref="BuildSystemRunner"/> to
/// wire their input handler. See the GenericComputerSystemConfigurerCore pattern.
/// </summary>
public class Vic20SystemConfigurerCore : ISystemConfigurer
{
    protected ILoggerFactory LoggerFactory { get; }
    protected IConfiguration Configuration { get; }
    private readonly Func<IHostSystemConfig> _hostConfigFactory;
    private readonly string _configSectionName;

    public Vic20SystemConfigurerCore(
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

    protected Vic20SystemConfigurerCore(ILoggerFactory loggerFactory, Func<IHostSystemConfig> hostConfigFactory)
        : this(loggerFactory, new ConfigurationBuilder().Build(), hostConfigFactory, configSectionName: "")
    {
    }

    public string SystemName => Vic20.SystemName;

    public virtual IEnumerable<string> GetUserContentDirectories()
        => [Vic20SystemConfig.DefaultROMDirectory];

    public const string VariantNtsc = "NTSC";
    public const string VariantPal = "PAL";

    public virtual Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(new List<string> { VariantNtsc, VariantPal });

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
        var vic20SystemConfig = (Vic20SystemConfig)systemConfig;
        var vic20Config = BuildVic20ConfigForVariant(configurationVariant);
        vic20Config.CpuCompatibilityProfile = vic20SystemConfig.CpuCompatibilityProfile;

        Dictionary<string, byte[]>? romData = null;
        if (vic20SystemConfig.ROMs.Count > 0)
            romData = ROM.LoadROMS(vic20SystemConfig.EffectiveROMDirectory, vic20SystemConfig.ROMs.ToArray());

        var vic20 = new Vic20(vic20Config, LoggerFactory, romData);
        vic20.SetCurrentRenderProviderType(vic20SystemConfig.RenderProviderType);
        ISystem system = vic20;
        return Task.FromResult(system);
    }

    private static Vic20Config BuildVic20ConfigForVariant(string configurationVariant)
    {
        return configurationVariant switch
        {
            VariantPal => new Vic20Config
            {
                TvModel = TvModel.Pal,
                // VIC-20 PAL (6561): 71 cycles/line × 312 lines = 22152 cycles/frame at 50 Hz.
                CpuCyclesPerFrame = 22152,
            },
            // Default to NTSC. The base Vic20Config already targets NTSC timing.
            _ => new Vic20Config(),
        };
    }

    public virtual Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
        => Task.FromResult(new SystemRunner((Vic20)system));
}
