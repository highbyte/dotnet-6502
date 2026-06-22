using Highbyte.DotNet6502.App.Terminal;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ----------
// DotNet 6502 emulator — interactive terminal (TUI) host.
// Renders the emulated (text-mode) screen as colored text cells in the real terminal via Terminal.Gui.
//
// The entry exe holds no compile-time reference to any specific emulated system. Systems arrive at
// runtime via plug-in discovery: engine plug-ins (Impl.Terminal.<System>) register an
// ISystemConfigurer, and shell plug-ins (App.Terminal.Shell.<System>) optionally contribute a
// system-specific menu control shown in the controls column.
// ----------

// Anchor file/relative resource access to the built app location.
Environment.CurrentDirectory = AppContext.BaseDirectory;

// ----------
// Configuration
// ----------
var appDir = AppContext.BaseDirectory;
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(appDir)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable)
    || devEnvironmentVariable.Equals("development", StringComparison.OrdinalIgnoreCase);
if (isDevelopment)
    configBuilder.AddUserSecrets<Program>(optional: true);

IConfiguration configuration = configBuilder.Build();

// ----------
// Logging — in-memory store shown in the TUI "Logs" pane.
// (No console logging: the terminal is owned by the TUI.)
// ----------
var logStore = new DotNet6502InMemLogStore { WriteDebugMessage = false };
var logConfig = new DotNet6502InMemLoggerConfiguration(logStore) { LogLevel = LogLevel.Information };
using var loggerFactory = LoggerFactory.Create(logBuilder =>
{
    logBuilder.AddInMem(logConfig);
    logBuilder.SetMinimumLevel(LogLevel.Trace);
});

var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    // ----------
    // Host config
    // ----------
    var emulatorConfig = new EmulatorConfig();
    configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

    // ----------
    // Plug-in discovery. Optional "EnabledSystems" allow-list narrows what is discovered.
    // ----------
    var enabledSystems = configuration.GetSection("EnabledSystems").Get<string[]>();
    var pluginLogger = loggerFactory.CreateLogger("PluginDiscovery");

    var enginePlugins = SystemPluginDiscovery
        .Discover<ISystemEnginePlugin>(enabledSystems, pluginLogger)
        .ToList();
    pluginLogger.LogInformation("Discovered {Count} engine plug-in(s): {Names}",
        enginePlugins.Count, string.Join(",", enginePlugins.Select(p => p.SystemName)));

    var shellPlugins = SystemPluginDiscovery
        .Discover<ISystemShellPlugin>(enabledSystems, pluginLogger)
        .ToList();
    pluginLogger.LogInformation("Discovered {Count} shell plug-in(s): {Names}",
        shellPlugins.Count, string.Join(",", shellPlugins.Select(p => p.SystemName)));

    SystemPluginDiscovery.LogPluginDiagnostics(enabledSystems, enginePlugins, shellPlugins, pluginLogger);

    // ----------
    // DI container. Engine plug-ins register the per-system ISystemConfigurer; shell plug-ins
    // register their menu views (which need the host app — provided via a holder, since the host app
    // is constructed after the container is built).
    // ----------
    var services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(loggerFactory);
    services.AddSingleton<IConfiguration>(configuration);

    TuiHostApp? hostAppHolder = null;
    services.AddSingleton<TuiHostApp>(_ => hostAppHolder
        ?? throw new InvalidOperationException("TuiHostApp not yet constructed."));

    foreach (var plugin in enginePlugins)
        plugin.Register(services, configuration);
    foreach (var plugin in shellPlugins)
        plugin.RegisterShellServices(services);

    var serviceProvider = services.BuildServiceProvider(DotNet6502ServiceProviderOptions.Validated);

    // ----------
    // System list, built from the discovered configurers (no system named in code).
    // ----------
    var systemList = new SystemList();
    foreach (var configurer in serviceProvider.GetServices<ISystemConfigurer>())
        systemList.AddSystem(configurer);
    await systemList.RemoveSystemsWithNoConfigurationVariants(bootstrapLogger);

    // Resolve a system's optional terminal contributions (cast from the UI-agnostic plug-in result).
    ITerminalMenuContribution? ResolveMenuContribution(string systemName) =>
        shellPlugins.FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
            ?.CreateMenuContribution(serviceProvider) as ITerminalMenuContribution;

    ITerminalInfoContribution? ResolveInfoContribution(string systemName) =>
        shellPlugins.FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
            ?.CreateInfoContribution(serviceProvider) as ITerminalInfoContribution;

    // ----------
    // Run the TUI host app (or the headless self-test that needs no TTY).
    // ----------
    var hostApp = new TuiHostApp(systemList, loggerFactory, emulatorConfig, logStore,
        ResolveMenuContribution, ResolveInfoContribution);
    hostAppHolder = hostApp;

    if (args.Contains("--selftest"))
    {
        var frames = 200;
        var idx = Array.IndexOf(args, "--frames");
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var f))
            frames = f;

        string? systemName = null;
        var sidx = Array.IndexOf(args, "--system");
        if (sidx >= 0 && sidx + 1 < args.Length)
            systemName = args[sidx + 1];

        var rendered = hostApp.RunSelfTest(frames, systemName);
        Console.WriteLine(rendered);
    }
    else
    {
        hostApp.Run();
    }
}
catch (Exception ex)
{
    var root = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
    bootstrapLogger.LogCritical(root, "The terminal host could not start.");
    Console.Error.WriteLine($"FATAL: the terminal host could not start: {root.Message}");
    Environment.ExitCode = 1;
}

// Make Program accessible for AddUserSecrets<Program>().
public partial class Program;
