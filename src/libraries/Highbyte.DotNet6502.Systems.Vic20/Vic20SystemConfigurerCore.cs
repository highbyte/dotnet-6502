using Highbyte.DotNet6502.Systems.Vic20.Config;
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

    public virtual Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(new List<string> { "Default" });

    public virtual Task<IHostSystemConfig> GetNewHostSystemConfig()
    {
        var hostConfig = _hostConfigFactory();
        Configuration.GetSection(_configSectionName).Bind(hostConfig);
        return Task.FromResult(hostConfig);
    }

    public virtual Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
        => Task.CompletedTask;

    public Task<ISystem> BuildSystem(string configurationVariant, ISystemConfig systemConfig)
    {
        var vic20Config = new Vic20Config();
        ISystem vic20 = new Vic20(vic20Config, LoggerFactory);
        return Task.FromResult(vic20);
    }

    public virtual Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
        => Task.FromResult(new SystemRunner((Vic20)system));
}
