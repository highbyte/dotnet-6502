using Highbyte.DotNet6502.App.SilkNetNative.Core;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

// ----------
// Parse command line arguments
// ----------
bool enableConsoleLogging = args.Contains("--console-log") || args.Contains("-c");

// The whole startup runs inside one try/catch so that *any* failure — a malformed
// appsettings.json, a plug-in/DI error, window creation, ... — is shown in a minimal quit-only
// error UI (SilkNetHostApp.RunStartupErrorOnly), or written to the console if even that UI
// cannot be created. loggerFactory is declared out here so the catch can use it if startup got
// far enough to create it.
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
        Console.Title = "DotNet 6502 Emulator (SilkNetNative) - Log Output";
    }

    // Note: Don't call Console.WriteLine before AllocConsole() is called (Windows). Otherwise no logs will show in console.
    WriteBootstrapLog($"SilkNetNative program starting.");

    // Fix for starting in debug mode from VS Code. By default the OS current directory is set to the project folder, not the folder containing the built .exe file...
    var currentAppDir = AppDomain.CurrentDomain.BaseDirectory;
    Environment.CurrentDirectory = currentAppDir;

    // Add unhandled exception handler to catch native crashes
    AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
    {
        var exception = eventArgs.ExceptionObject as Exception;
        Console.WriteLine($"Unhandled exception caught: {exception?.Message ?? "Unknown error"}");
        Console.WriteLine($"Stack trace: {exception?.StackTrace ?? "No stack trace available"}");
        Console.WriteLine($"IsTerminating: {eventArgs.IsTerminating}");
        if (exception?.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {exception.InnerException.Message}");
            Console.WriteLine($"Inner stack trace: {exception.InnerException.StackTrace}");
        }
    };

    // ----------
    // Get config file
    // ----------
    WriteBootstrapLog($"Creating configuration object.");
    var appDir = AppContext.BaseDirectory;
    var configBuilder = new ConfigurationBuilder()
        .SetBasePath(appDir)
        .AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", optional: true);

    IConfiguration Configuration = configBuilder.Build();

    // ----------
    // Create logging
    // ----------
    WriteBootstrapLog($"Initializing logging.");

    DotNet6502InMemLogStore logStore = new();
    var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
    loggerFactory = LoggerFactory.Create(logBuilder =>
    {
        logConfig.LogLevel = LogLevel.Information;
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
    // Plug-in discovery + DI bootstrap.
    // Shell plug-ins (e.g. App.SilkNetNative.Shell.Commodore64) register their per-system
    // ISystemConfigurer and IImGuiMenuContributor implementations into DI. The menu later
    // resolves contributors lazily by SystemName.
    // ----------
    WriteBootstrapLog($"Creating system list.");
    var enabledSystems = Configuration.GetSection("EnabledSystems").Get<string[]>();
    var pluginLogger = loggerFactory.CreateLogger("PluginDiscovery");
    var shellPlugins = SystemPluginDiscovery
        .Discover<ISystemShellPlugin>(enabledSystems, pluginLogger)
        .ToList();
    pluginLogger.LogInformation("Discovered {Count} shell plug-in(s): {Names}",
        shellPlugins.Count, string.Join(",", shellPlugins.Select(p => p.SystemName)));

    // Engine plug-ins (in the Impl.SilkNet.<System> libraries) register the per-system
    // ISystemConfigurer and optionally contribute render targets (ISilkNetRenderTargetPlugin).
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
    // SilkNetHostApp + ISilkNetMenuHost are registered as deferred factories: the host is
    // constructed below and assigned to `hostAppHolder`; the menu is created inside the host's
    // OnLoad callback and exposed via SilkNetHostApp.Menu. Resolution happens lazily during
    // the first per-frame draw — by then both are non-null.
    SilkNetHostApp? hostAppHolder = null;
    services.AddSingleton<SilkNetHostApp>(_ => hostAppHolder
        ?? throw new InvalidOperationException("SilkNetHostApp not yet constructed."));
    services.AddSingleton<ISilkNetMenuHost>(_ => hostAppHolder?.Menu
        ?? throw new InvalidOperationException("SilkNetImGuiMenu not yet constructed (OnLoad has not run)."));

    // Engine plug-ins register the per-system ISystemConfigurer (+ supporting services).
    foreach (var plugin in enginePlugins)
        plugin.Register(services, Configuration);

    // Shell plug-ins register per-system UI (menu contributors etc.).
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

    // Any fatal startup error (no systems, invalid DefaultEmulator, ...) is caught inside the host
    // app's OnLoad and shown as a quit-only error dialog — see SilkNetHostApp.OnLoad.

    // ----------
    // Create Silk.NET Window and run SilkNetHostApp
    // ----------
    WriteBootstrapLog($"Configuring Silk.NET window.");
    var windowWidth = SilkNetHostApp.DEFAULT_WIDTH;
    var windowHeight = SilkNetHostApp.DEFAULT_HEIGHT;

    var windowOptions = WindowOptions.Default;
    // Update frequency, in hertz.
    windowOptions.UpdatesPerSecond = SilkNetHostApp.DEFAULT_RENDER_HZ;
    // Render frequency, in hertz.
    windowOptions.FramesPerSecond = 60.0f;  // TODO: With Vsync=false the FramesPerSecond settings does not seem to matter. Measured in OnRender method it'll be same as UpdatesPerSecond setting.

    windowOptions.VSync = false;  // TODO: With Vsync=true Silk.NET seem to use incorrect UpdatePerSecond. The actual FPS its called is 10 lower than it should be (measured in the OnUpdate method)
    windowOptions.WindowState = WindowState.Normal;
    windowOptions.Title = "DotNet 6502 Emulator + Silk.NET (with ImGui, SkiaSharp, OpenGL, NAudio)";
    windowOptions.Size = new Vector2D<int>(windowWidth, windowHeight);
    windowOptions.WindowBorder = WindowBorder.Fixed;
    windowOptions.API = GraphicsAPI.Default; // = Default = OpenGL 3.3 with forward compatibility
    windowOptions.ShouldSwapAutomatically = true;
    //windowOptions.TransparentFramebuffer = false;
    //windowOptions.PreferredDepthBufferBits = 24;    // Depth buffer bits must be set explicitly on MacOS (tested on M1), otherwise there will be be no depth buffer (for OpenGL 3d).

    WriteBootstrapLog($"Creating Silk.NET window...");
    var window = Window.Create(windowOptions);
    WriteBootstrapLog($"Silk.NET window created.");

    // Build the resolveMenuContributor factory now that DI is ready and plug-ins are registered.
    // Closes over shellPlugins + serviceProvider. The plug-in's CreateMenuContribution produces
    // the per-system IImGuiMenuContributor — null for systems without a menu plug-in.
    Func<string, IImGuiMenuContributor?> resolveMenuContributor = systemName =>
        shellPlugins.FirstOrDefault(p => p.SystemName.Equals(systemName, StringComparison.OrdinalIgnoreCase))
            ?.CreateMenuContribution(serviceProvider) as IImGuiMenuContributor;

    // Render-target-capable engine plug-ins — the host invokes these from its render-config callback
    // so system-specific render targets stay out of SilkNetHostApp.
    var renderTargetPlugins = enginePlugins.OfType<ISilkNetRenderTargetPlugin>().ToList();

    var silkNetHostApp = new SilkNetHostApp(systemList, loggerFactory, emulatorConfig, window, logStore, logConfig, resolveMenuContributor, renderTargetPlugins);
    hostAppHolder = silkNetHostApp;

    WriteBootstrapLog($"Starting Silk.NET host app...");
    silkNetHostApp.Run();
    WriteBootstrapLog($"Silk.NET host app exited normally.");
}
catch (Exception startupEx)
{
    // Any failure during startup — show it in a minimal quit-only error UI. WriteBootstrapLog
    // writes to the console and always works; the in-memory logger may not exist yet.
    var rootEx = startupEx is AggregateException agg ? agg.InnerException ?? agg : startupEx;
    WriteBootstrapLog($"Fatal error during startup: {rootEx}", LogLevel.Critical);
    SilkNetHostApp.RunStartupErrorOnly(
        "The emulator could not start.\n\n" + rootEx.Message,
        loggerFactory?.CreateLogger("Program"));
}

// ----------
// App exited
// ----------
WriteBootstrapLog($"SilkNetNative app exited.");
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

    Console.Write($"{timestamp} ");
    var originalColor = Console.ForegroundColor;
    Console.ForegroundColor = levelColor;
    Console.Write(levelString);
    Console.ForegroundColor = originalColor;
    Console.WriteLine($": Program[0] {message}");
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