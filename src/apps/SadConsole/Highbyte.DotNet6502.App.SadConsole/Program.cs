using Highbyte.DotNet6502.App.SadConsole.Core;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

// ----------
// Parse command line arguments
// ----------
bool enableConsoleLogging = args.Contains("--console-log") || args.Contains("-c");

// The whole startup runs inside one try/catch so that *any* failure — from a malformed
// appsettings.json to a plug-in/DI error to a window-creation failure — is shown in a minimal
// quit-only error UI (SadConsoleHostApp.RunStartupErrorOnly), or written to the console if even
// that UI cannot be created. loggerFactory is declared out here so the catch can use it if
// startup got far enough to create it.
ILoggerFactory? loggerFactory = null;
try
{
    LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);

    // On Windows, WinExe applications don't have a console attached.
    // Create a new console window for logging if enabled.
    // Note: This creates a separate console window rather than attaching to the parent terminal,
    // which avoids cursor/prompt synchronization issues with PowerShell/cmd.
    if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        AllocConsole();
        System.Console.Title = "DotNet 6502 Emulator (SadConsole) - Log Output";
    }

    // Note: Don't call Console.WriteLine before AllocConsole() is called (Windows). Otherwise no logs will show in console.
    WriteBootstrapLog($"SadConsole program starting.");

    // Keep file pickers and relative resource access anchored to the built app location.
    Environment.CurrentDirectory = AppContext.BaseDirectory;

    // ----------
    // Get config file
    // ----------
    WriteBootstrapLog($"Creating configuration object.");
    var appDir = AppContext.BaseDirectory;
    var configBuilder = new ConfigurationBuilder()
        .SetBasePath(appDir)
        .AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", optional: true);

    var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable)
        || devEnvironmentVariable.Equals("development", StringComparison.OrdinalIgnoreCase);
    if (isDevelopment) //only add secrets in development
    {
        configBuilder.AddUserSecrets<Program>(optional: true);
    }

    configBuilder.AddJsonFile(AppStoragePaths.GetUserSettingsFilePath("SadConsole"), optional: true, reloadOnChange: true);

    IConfiguration Configuration = configBuilder.Build();

    // ----------
    // Create logging
    // ----------
    WriteBootstrapLog($"Initializing logging.");

    DotNet6502InMemLogStore logStore = new() { WriteDebugMessage = true };
    var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
    loggerFactory = LoggerFactory.Create(logBuilder =>
    {
        logConfig.LogLevel = LogLevel.Information;  // LogLevel.Debug, LogLevel.Information,
        logBuilder.AddInMem(logConfig);
        logBuilder.SetMinimumLevel(LogLevel.Trace);

        // Add console logging if enabled via command line
        if (enableConsoleLogging)
        {
            logBuilder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            logBuilder.AddFilter(null, consoleLogLevel);  // Apply log level filter

            WriteBootstrapLog($"Console logging enabled (level: {consoleLogLevel})");
        }
    });

    // ----------
    // Get emulator host config
    // ----------
    WriteBootstrapLog($"Reading emulator config.");
    var emulatorConfig = new EmulatorConfig();
    Configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

    // ----------
    // Get systems
    // ----------
    WriteBootstrapLog($"Creating system list.");
    var enabledSystems = Configuration.GetSection("EnabledSystems").Get<string[]>();
    var pluginLogger = loggerFactory.CreateLogger("PluginDiscovery");
    var shellPlugins = SystemPluginDiscovery
        .Discover<ISystemShellPlugin>(enabledSystems, pluginLogger)
        .ToList();
    pluginLogger.LogInformation("Discovered {Count} shell plug-in(s): {Names}",
        shellPlugins.Count, string.Join(",", shellPlugins.Select(p => p.SystemName)));

    // Engine plug-ins (in the Impl.SadConsole.<System> libraries) register the per-system
    // ISystemConfigurer. The SystemList is built from those below.
    var enginePlugins = SystemPluginDiscovery
        .Discover<ISystemEnginePlugin>(enabledSystems, pluginLogger)
        .ToList();
    pluginLogger.LogInformation("Discovered {Count} engine plug-in(s): {Names}",
        enginePlugins.Count, string.Join(",", enginePlugins.Select(p => p.SystemName)));

    // Diagnose enabled-but-missing systems and engine/shell plug-in mismatches.
    SystemPluginDiscovery.LogPluginDiagnostics(enabledSystems, enginePlugins, shellPlugins, pluginLogger);

    var services = new ServiceCollection();
    services.AddSingleton(loggerFactory);
    services.AddSingleton<IConfiguration>(Configuration);

    SadConsoleHostApp? hostAppHolder = null;
    services.AddSingleton<SadConsoleHostApp>(_ => hostAppHolder
        ?? throw new InvalidOperationException("SadConsoleHostApp not yet constructed."));

    // Engine plug-ins register the per-system ISystemConfigurer (+ supporting services).
    foreach (var plugin in enginePlugins)
        plugin.Register(services, Configuration);

    // Shell plug-ins register per-system UI (menu/info consoles etc.).
    foreach (var plugin in shellPlugins)
        plugin.RegisterShellServices(services);

    var serviceProvider = services.BuildServiceProvider();

    var systemList = new SystemList();
    foreach (var configurer in serviceProvider
        .GetServices<ISystemConfigurer>())
    {
        systemList.AddSystem(configurer);
    }

    // Drop any system that declares no configuration variants — it cannot be built or run, and
    // would crash the variant picker in the menu. Treated as unavailable, like a missing plug-in.
    await systemList.RemoveSystemsWithNoConfigurationVariants(pluginLogger);
    systemList.EnsureUserContentDirectories(pluginLogger);

    // ----------
    // Start SadConsoleHostApp
    // ----------
    // Any fatal startup error (no systems, invalid DefaultEmulator, ...) is caught inside the host
    // app and shown as a quit-only error screen — see SadConsoleHostApp.CreateMainSadConsoleScreen.
    Func<string, ISadConsoleMenuContribution?> resolveMenuContribution = systemName =>
        shellPlugins.FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
            ?.CreateMenuContribution(serviceProvider) as ISadConsoleMenuContribution;

    Func<string, ISadConsoleInfoContribution?> resolveInfoContribution = systemName =>
        shellPlugins.FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
            ?.CreateInfoContribution(serviceProvider) as ISadConsoleInfoContribution;

    // Engine plug-ins may also contribute a system-specific SadConsole glyph/colour transform
    // (ISadConsoleRenderCustomizationPlugin), so system-specific render code stays out of SadConsoleHostApp.
    var renderCustomizationPlugins = enginePlugins.OfType<ISadConsoleRenderCustomizationPlugin>().ToList();

    WriteBootstrapLog($"Starting SadConsole app.");
    var sadConsoleHostApp = new SadConsoleHostApp(
        systemList,
        loggerFactory,
        emulatorConfig,
        logStore,
        logConfig,
        resolveMenuContribution,
        resolveInfoContribution,
        renderCustomizationPlugins);
    hostAppHolder = sadConsoleHostApp;
    sadConsoleHostApp.Run();
}
catch (Exception startupEx)
{
    // Any failure during startup — show it in a minimal quit-only error UI. WriteBootstrapLog
    // writes to the console and always works; the in-memory logger may not exist yet.
    var rootEx = startupEx is AggregateException agg ? agg.InnerException ?? agg : startupEx;
    WriteBootstrapLog($"Fatal error during startup: {rootEx}", LogLevel.Critical);
    SadConsoleHostApp.RunStartupErrorOnly(
        "The emulator could not start.\n\n" + rootEx.Message,
        loggerFactory?.CreateLogger("Program"));
}

// ----------
// App exited
// ----------
WriteBootstrapLog($"SadConsole app exited.");
// Detach from parent console on Windows to restore the command prompt
if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    FreeConsole();
}

// ----------
// Helper methods
// ----------
static void WriteBootstrapLog(string message, LogLevel logLevel = LogLevel.Information)
{
    var timestamp = DateTime.Now.ToString("HH:mm:ss");
    var (levelString, levelColor) = logLevel switch
    {
        LogLevel.Trace => ("trce", ConsoleColor.Gray),
        LogLevel.Debug => ("dbug", ConsoleColor.Gray),
        LogLevel.Information => ("info", ConsoleColor.Green),
        LogLevel.Warning => ("warn", ConsoleColor.Yellow),
        LogLevel.Error => ("fail", ConsoleColor.Red),
        LogLevel.Critical => ("crit", ConsoleColor.Red),
        _ => ("info", ConsoleColor.Green)
    };

    System.Console.Write($"{timestamp} ");
    var originalColor = System.Console.ForegroundColor;
    System.Console.ForegroundColor = levelColor;
    System.Console.Write(levelString);
    System.Console.ForegroundColor = originalColor;
    System.Console.WriteLine($": Program[0] {message}");
}

static LogLevel ParseLogLevel(string[] args, LogLevel defaultLevel)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if ((args[i] == "--log-level" || args[i] == "-l") && i + 1 < args.Length)
        {
            if (Enum.TryParse<LogLevel>(args[i + 1], ignoreCase: true, out var level))
            {
                return level;
            }
        }
    }
    return defaultLevel;
}

// Windows API to create a new console window for the process
[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool AllocConsole();

// Windows API to detach from console before exiting
[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool FreeConsole();
