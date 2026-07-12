using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Transport;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using System.Diagnostics.CodeAnalysis;
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

    public virtual IEnumerable<string> GetUserContentDirectories()
        => [C64SystemConfig.DefaultROMDirectory];

    public Task<List<string>> GetConfigurationVariants(ISystemConfig systemConfig)
        => Task.FromResult(C64ModelInventory.C64Models.Keys.ToList());

    public IScreen? GetScreenInfo(string configurationVariant, ISystemConfig systemConfig)
    {
        if (!C64ModelInventory.C64Models.TryGetValue(configurationVariant, out var c64Model))
            return null;

        return new Vic2Screen(c64Model.Vic2Models.First(), c64Model.CPUFrequencyHz);
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
        var section = Configuration.GetSection(_configSectionName);
        section.Bind(hostConfig);

        // IConfiguration.Bind() can't fill Type-valued properties (they have private setters and
        // Type is not a leaf config value). Read the friendly JSON keys explicitly and apply via
        // the strongly-typed setters so appsettings like
        //     "SystemConfig": { "AudioProviderType": "Foo.Bar, Assembly" }
        // actually take effect rather than silently leaving the constructor default in place.
        ApplyTypeOverridesFromConfig(section.GetSection(nameof(IHostSystemConfig.SystemConfig)), hostConfig.SystemConfig);

        return Task.FromResult(hostConfig);
    }

    private static void ApplyTypeOverridesFromConfig(IConfiguration systemConfigSection, ISystemConfig systemConfig)
    {
        ApplyTypeKey(systemConfigSection, "RenderProviderType", systemConfig.SetRenderProviderType);
        ApplyTypeKey(systemConfigSection, "RenderTargetType", systemConfig.SetRenderTargetType);
        ApplyTypeKey(systemConfigSection, "AudioProviderType", systemConfig.SetAudioProviderType);
        ApplyTypeKey(systemConfigSection, "AudioTargetType", systemConfig.SetAudioTargetType);
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
            SwiftLink = c64SystemConfig.SwiftLink.Clone(),
            ROMs = c64SystemConfig.ROMs,
            ROMDirectory = c64SystemConfig.EffectiveROMDirectory,
            RenderProviderType = c64SystemConfig.RenderProviderType ?? DefaultRenderProviderType,
            AudioProviderType = c64SystemConfig.AudioProviderType,
            SidEmulationMode = c64SystemConfig.SidEmulationMode,
            CpuCompatibilityProfile = c64SystemConfig.CpuCompatibilityProfile,
            Vic2RasterizerPerLineSprites = c64SystemConfig.Vic2RasterizerPerLineSprites,
        };

        var c64 = C64.BuildC64(c64Config, LoggerFactory);
        return Task.FromResult<ISystem>(c64);
    }

    /// <summary>
    /// Builds a <see cref="SystemRunner"/> with no input consumer wired — the base behaviour for a
    /// host with no input (the Headless host). Tech hosts override to attach a C64 input handler.
    /// </summary>
    public virtual async Task<SystemRunner> BuildSystemRunner(ISystem system, IHostSystemConfig hostSystemConfig)
    {
        var c64 = (C64)system;
        var swiftLink = c64.CartridgeSlot.GetCartridge<SwiftLinkDevice>();
        if (SupportsSwiftLinkTcpTransport && swiftLink != null && hostSystemConfig is IC64SwiftLinkTcpHostConfig swiftLinkHostConfig)
        {
            swiftLink.ReceivePacingCycles =
                swiftLinkHostConfig.SwiftLinkTransportMode == C64SwiftLinkTransportMode.HayesModem
                && swiftLink.ReceiveMode == C64SwiftLinkReceiveMode.Compatible
                    ? GetReceivePacingCyclesPerCharacter(c64)
                    : 0;

            ISwiftLinkTransport transport = swiftLinkHostConfig.SwiftLinkTransportMode switch
            {
                C64SwiftLinkTransportMode.HayesModem => new HayesModemTransport(
                    (host, port) => new TcpTransport(
                        host,
                        port,
                        LoggerFactory.CreateLogger(nameof(TcpTransport))),
                    LoggerFactory.CreateLogger(nameof(HayesModemTransport))),
                _ => new TcpTransport(
                    swiftLinkHostConfig.SwiftLinkTcpHost,
                    swiftLinkHostConfig.SwiftLinkTcpPort,
                    LoggerFactory.CreateLogger(nameof(TcpTransport)))
            };
            swiftLink.Transport = transport;
            if (swiftLinkHostConfig.SwiftLinkConnectOnBoot)
                await transport.ConnectAsync();
        }

        return new SystemRunner(c64);
    }

    // Paces received SwiftLink bytes for modem-style software (e.g. Compunet) in Compatible mode.
    // Compunet programs the ACIA control register to 19200 baud (low nibble 0xF); a real CMD
    // SwiftLink doubles that to 38400 via its 3.6864 MHz crystal (twice the standard 6551's
    // 1.8432 MHz). The Compunet server paces its send rate to how fast the client drains the ACIA,
    // so faster delivery directly speeds up screen downloads.
    //
    // We pace at the programmed 19200 (not the doubled 38400): at 38400 the per-byte receive NMI
    // fires roughly every ~270 CPU cycles, which starves the client's foreground handshake in this
    // emulator and makes it drop back to BASIC during connect. 19200 is the highest rate that stays
    // stable here while bringing throughput close to VICE / real SwiftLink — about 6× the old flat
    // 1200-baud pacing, which ran downloads at roughly half VICE's speed.
    private static ulong GetReceivePacingCyclesPerCharacter(C64 c64)
    {
        const double BitsPerCharacter = 10.0;      // 8N1 framing
        const double EffectiveBaudRate = 19200.0;  // Compunet's programmed ACIA rate; highest stable receive pace in this emulator
        return (ulong)Math.Ceiling(c64.CpuFrequencyHz * (BitsPerCharacter / EffectiveBaudRate));
    }

    /// <summary>
    /// Fallback render-provider type used when <see cref="C64SystemConfig.RenderProviderType"/> is
    /// not set. Null by default (the host decides); the browser host overrides it.
    /// </summary>
    protected virtual Type? DefaultRenderProviderType => null;

    /// <summary>
    /// Browser hosts cannot use raw TCP, so they override this to defer SwiftLink transport
    /// hookup until a WebSocket-backed transport exists.
    /// </summary>
    protected virtual bool SupportsSwiftLinkTcpTransport => true;
}
