using System.Diagnostics;
using Highbyte.DotNet6502.Systems.Timing;
using Highbyte.DotNet6502.App.Headless;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Scripting;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Systems.Recording;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Updates;

// ----------
// Update check CLI flags (--version / --check-update / --update). Handled before any startup work.
// Headless also performs a gated non-blocking startup check after logging is configured.
// ----------
if (ConsoleUpdateCli.WantsHandling(args))
    return await ConsoleUpdateCli.RunAsync(
        args,
        new AppUpdateDescriptor { HomebrewPackage = "dotnet-6502-headless", ScoopPackage = "dotnet-6502-headless" },
        Console.Out);

// ----------
// Parse command line arguments
// ----------
LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);

WriteBootstrapLog("Starting headless emulator.");

var speed = EmulationSpeed.Normal;
if (args.Contains("--speed") && !EmulationSpeed.TryParse(AutomatedStartupHandler.ParseStringArgument(args, "--speed"), out speed))
{
    Console.Error.WriteLine("--speed requires a positive finite multiplier or unthrottled.");
    return 1;
}

int statsInterval = 0;
if (args.Contains("--stats-interval") && (!int.TryParse(AutomatedStartupHandler.ParseStringArgument(args, "--stats-interval"), out statsInterval)
    || statsInterval < 1 || statsInterval > int.MaxValue / 1000))
{
    Console.Error.WriteLine("--stats-interval requires a positive interval in seconds.");
    return 1;
}

// Parse debug adapter arguments
bool enableExternalDebug = args.Contains("--enableExternalDebug");
int debugPort = ParsePortArgument(args, "--debug-port") ?? 6502;
string? debugBindAddress = AutomatedStartupHandler.ParseStringArgument(args, "--debug-bind-address");
bool debugWait = args.Contains("--debug-wait");

// Parse remote control arguments
int? remotePort = ParsePortArgument(args, "--remote-port");
string? remoteBindAddress = AutomatedStartupHandler.ParseStringArgument(args, "--remote-bind-address");
bool allowRemoteQuit = args.Contains("--allow-remote-quit");

// Parse automated startup arguments
string? systemName = AutomatedStartupHandler.ParseStringArgument(args, "--system");
string? systemVariant = AutomatedStartupHandler.ParseStringArgument(args, "--systemVariant");
bool autoStart = args.Contains("--start");
bool waitForSystemReady = args.Contains("--waitForSystemReady");
string? loadPrgPath = AutomatedStartupHandler.ParseStringArgument(args, "--loadPrg");
bool runLoadedProgram = args.Contains("--runLoadedProgram");
string? loadSnapshotPath = AutomatedStartupHandler.ParseStringArgument(args, "--load-snapshot");
bool showStoragePaths = args.Contains("--show-storage-paths");
string? playDemoPath = AutomatedStartupHandler.ParseStringArgument(args, "--play-demo");
string? validateDemoPath = AutomatedStartupHandler.ParseStringArgument(args, "--validate-demo");
string? demoPath = playDemoPath ?? validateDemoPath;
bool validateDemo = validateDemoPath != null;
long demoFrameLimit = 10000000;
var frameLimitText = AutomatedStartupHandler.ParseStringArgument(args, "--demo-max-frames");
if ((frameLimitText != null && (!long.TryParse(frameLimitText, out demoFrameLimit) || demoFrameLimit < 1))
    || (playDemoPath != null && validateDemoPath != null))
{
    Console.Error.WriteLine("Use one of --play-demo / --validate-demo, with a positive --demo-max-frames limit.");
    return 1;
}

// Parse scripting override arguments
var additionalEnvironmentVariables = ParseMultipleStringArgument(args, "--script-allow-env");
List<string> scriptFilePaths = ParseMultipleStringArgument(args, "--script");
string? scriptDirectoryOverride = AutomatedStartupHandler.ParseStringArgument(args, "--scriptDir");

// Validate automated startup arguments
bool hasScripts = scriptFilePaths.Count > 0 || scriptDirectoryOverride != null;
if (demoPath != null && (systemName != null || systemVariant != null || autoStart || waitForSystemReady
    || loadPrgPath != null || runLoadedProgram || loadSnapshotPath != null || hasScripts || enableExternalDebug))
{
    Console.Error.WriteLine("Demo startup selects its own machine and initial state; do not combine it with other startup, script, or debugger arguments.");
    return 1;
}
if (!AutomatedStartupHandler.ValidateArguments(systemName, systemVariant, autoStart, waitForSystemReady, loadPrgPath, runLoadedProgram, hasScripts, loadSnapshotPath))
{
    return 1;
}

// ----------
// Get config file
// ----------
WriteBootstrapLog("Creating configuration object.");
var appDir = AppContext.BaseDirectory;
var builder = new ConfigurationBuilder()
    .SetBasePath(appDir)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";
if (isDevelopment)
{
    builder.AddUserSecrets<Program>();
}

builder.AddJsonFile(AppStoragePaths.GetUserSettingsFilePath("Headless"), optional: true, reloadOnChange: true);

IConfiguration configuration = builder.Build();

// ----------
// Create logging
// ----------
WriteBootstrapLog("Initializing logging.");
var loggerFactory = LoggerFactory.Create(logBuilder =>
{
    logBuilder.SetMinimumLevel(LogLevel.Trace);
    logBuilder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
    logBuilder.AddFilter(null, consoleLogLevel);
});

var logger = loggerFactory.CreateLogger(nameof(Program));

// Non-blocking startup update check (Headless logs to console). Gated by the UpdateCheckEnabled
// setting and the standard CI / DOTNET6502_NO_UPDATE_CHECK suppressors, so scripted/CI runs make no
// network call. Uses the config built above.
_ = ConsoleUpdateCli.CheckAndLogOnStartupAsync(
    new AppUpdateDescriptor { HomebrewPackage = "dotnet-6502-headless", ScoopPackage = "dotnet-6502-headless" },
    loggerFactory.CreateLogger("UpdateCheck"),
    configuration.GetValue("UpdateCheckEnabled", true));

// ----------
// Get emulator host config
// ----------
logger.LogInformation("Reading emulator config.");
var emulatorConfig = new HeadlessEmulatorConfig();
configuration.GetSection(HeadlessEmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

// ----------
// Set up cancellation for graceful shutdown
// ----------
var appCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    appCts.Cancel();
};

// ----------
// Set up external debug controller
// ----------
var debugEnvironment = new HeadlessDebugServerEnvironment(appCts);
var debugController = new HeadlessExternalDebugController(debugEnvironment, loggerFactory);

if (enableExternalDebug && !showStoragePaths)
{
    var effectiveDebugBindAddress = string.IsNullOrWhiteSpace(debugBindAddress)
        ? IExternalDebugController.DefaultBindAddress
        : debugBindAddress.Trim();
    logger.LogInformation("Starting TCP debug adapter server on {BindAddress}:{DebugPort}.", effectiveDebugBindAddress, debugPort);
    try
    {
        await debugController.StartAsync(debugPort, effectiveDebugBindAddress);
    }
    catch (ArgumentException ex)
    {
        logger.LogError("Failed to start debug adapter server: {Message}", ex.Message);
        return 1;
    }

    if (debugWait)
    {
        logger.LogInformation("Waiting for debug client to connect (--debug-wait specified)...");
        if (debugController.WaitForClientConnection(timeoutSeconds: 30))
        {
            logger.LogInformation("Debug client connected, continuing startup.");
        }
        else
        {
            logger.LogWarning("Debug client connection timeout, continuing startup anyway.");
        }
    }
}

// ----------
// Set up remote control controller
// ----------
var remoteEnvironment = new HeadlessRemoteControlEnvironment(loggerFactory, allowQuit: allowRemoteQuit);
var remoteController = new RemoteControlController(remoteEnvironment, loggerFactory);
if (remotePort.HasValue && !showStoragePaths)
{
    var effectiveBindAddress = string.IsNullOrWhiteSpace(remoteBindAddress)
        ? IRemoteControlController.DefaultBindAddress
        : remoteBindAddress.Trim();
    logger.LogInformation("Starting TCP remote control server on {BindAddress}:{RemotePort}.", effectiveBindAddress, remotePort.Value);
    try
    {
        await remoteController.StartAsync(remotePort.Value, effectiveBindAddress);
    }
    catch (ArgumentException ex)
    {
        logger.LogError("Failed to start remote control server: {Message}", ex.Message);
        return 1;
    }
}

// ----------
// Initialize Lua scripting engine
// ----------
bool automatedStartupMode = autoStart || waitForSystemReady || loadPrgPath != null || runLoadedProgram || loadSnapshotPath != null || demoPath != null;
var scriptingConfig = MoonSharpScriptingConfigurator.CreateEffectiveConfig(configuration, loggerFactory, scriptFilePaths, scriptDirectoryOverride, suppressConfigScripts: automatedStartupMode);
scriptingConfig.AllowedEnvironmentVariables.AddRange(additionalEnvironmentVariables);
var scriptingEngine = MoonSharpScriptingConfigurator.CreateForNativeHost(scriptingConfig, loggerFactory, "headless", suppressScripts: automatedStartupMode && !hasScripts);

// ----------
// Create system list and host app
// ----------
logger.LogInformation("Creating headless host app.");

// The per-system Headless engine plug-ins (C64, Generic, and Oric) register each
// ISystemConfigurer. The SystemList is built from those registrations.
var enabledSystems = configuration.GetSection("EnabledSystems").Get<string[]>();
var pluginLogger = loggerFactory.CreateLogger("PluginDiscovery");
var enginePlugins = SystemPluginDiscovery
    .Discover<ISystemEnginePlugin>(enabledSystems, pluginLogger)
    .ToList();
pluginLogger.LogInformation("Discovered {Count} engine plug-in(s): {Names}",
    enginePlugins.Count, string.Join(",", enginePlugins.Select(p => p.SystemName)));

// Headless has no shell layer — pass null for shellPlugins.
SystemPluginDiscovery.LogPluginDiagnostics(enabledSystems, enginePlugins, shellPlugins: null, pluginLogger);

var services = new ServiceCollection();
services.AddSingleton(loggerFactory);
services.AddSingleton<IConfiguration>(configuration);
foreach (var plugin in enginePlugins)
    plugin.Register(services, configuration);
var serviceProvider = services.BuildServiceProvider();

var systemList = new SystemList();
foreach (var configurer in serviceProvider
    .GetServices<ISystemConfigurer>())
{
    systemList.AddSystem(configurer);
}

// Drop any system that declares no configuration variants — it cannot be built or run.
await systemList.RemoveSystemsWithNoConfigurationVariants(pluginLogger);
systemList.EnsureUserContentDirectories(pluginLogger);

if (showStoragePaths)
{
    var paths = await StoragePathsInfoFactory.CreateAsync(
        "Headless",
        AppStoragePaths.GetUserSettingsFilePath("Headless"),
        systemList,
        scriptingConfig);
    Console.Write(StoragePathsInfoFactory.FormatForConsole(paths));
    return 0;
}

// No usable system: a headless run cannot do anything without one. There is no UI to show an
// error dialog in, so log a clear message and exit with a non-zero code.
if (systemList.Systems.Count == 0)
{
    logger.LogCritical(
        "No emulator systems are available. Check the 'EnabledSystems' setting in appsettings.json " +
        "and that the system plug-in assemblies are deployed. Exiting.");
    return 1;
}

var hostApp = new HeadlessHostApp(systemList, loggerFactory, appCts, speed);
hostApp.SetStoragePathsContext(AppStoragePaths.GetUserSettingsFilePath("Headless"), scriptingConfig);

// Wire the debug environment to the host app
debugEnvironment.HostApp = hostApp;

// Wire the remote environment to the host app
remoteEnvironment.HostApp = hostApp;

// Set scripting engine
await hostApp.RunOnEmulationThreadAsync(async () =>
{
    hostApp.SetScriptingEngine(scriptingEngine ?? new NoScriptingEngine());
    await hostApp.DrainStartupScriptActionsAsync();
});

logger.LogInformation("Headless host app initialized.");

// ----------
// Automated startup
// ----------
if (systemName != null || loadSnapshotPath != null)
{
    var startupRequest = new AutomatedStartupRequest(
        systemName ?? "", systemVariant, autoStart, waitForSystemReady,
        loadPrgPath, runLoadedProgram, enableExternalDebug)
    {
        LoadSnapshotPath = loadSnapshotPath,
    };
    await hostApp.RunOnEmulationThreadAsync(() => AutomatedStartupHandler.ExecuteAsync(
        hostApp,
        startupRequest,
        onStartupComplete: () => debugController.SignalProgramReady(),
        loggerFactory: loggerFactory,
        prepareForExternalDebuggerStart: () => hostApp.WaitForExternalDebugger = true));
}

// ----------
// Block until shutdown
// ----------
int demoExitCode = 0;
if (demoPath != null)
{
    try
    {
        await using var demoFile = File.OpenRead(Path.GetFullPath(demoPath));
        await hostApp.RunOnEmulationThreadAsync(() => hostApp.LoadDemoAsync(demoFile));
        if (hostApp.DemoSession!.TotalFrames > demoFrameLimit)
            throw new InvalidDataException($"Demo exceeds the {demoFrameLimit} frame execution limit; choose --demo-max-frames explicitly.");
        if (validateDemo)
        {
            while (hostApp.DemoSession.Mode == RecordingMode.Playback && !appCts.IsCancellationRequested)
                await hostApp.RunOnEmulationThreadAsync(() => hostApp.StepEmulatorFramesAsync(100));
            demoExitCode = appCts.IsCancellationRequested ? 2
                : hostApp.DemoSession.Outcome == PlaybackOutcome.Completed ? 0 : 3;
            logger.LogInformation("Demo validation {Outcome} at frame {Frame}/{Total}. {Error}",
                hostApp.DemoSession.Outcome, hostApp.DemoSession.Frame, hostApp.DemoSession.TotalFrames,
                hostApp.DemoSession.Error);
        }
        else await hostApp.RunOnEmulationThreadAsync(() => hostApp.Start());
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Demo is incompatible or could not be loaded.");
        demoExitCode = 4;
    }
}
try
{
    if (!validateDemo && demoExitCode == 0)
    {
        logger.LogInformation("Headless emulator running. Press Ctrl+C to exit.");
        if (statsInterval == 0)
            await Task.Delay(Timeout.Infinite, appCts.Token);
        else
        {
            var previousTime = Stopwatch.GetTimestamp();
            long previousFrames = hostApp.CompletedGuestFrames;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(statsInterval));
            while (await timer.WaitForNextTickAsync(appCts.Token))
            {
                var now = Stopwatch.GetTimestamp();
                long frames = hostApp.CompletedGuestFrames;
                var rate = (frames - previousFrames) / Stopwatch.GetElapsedTime(previousTime, now).TotalSeconds;
                logger.LogInformation("{Rate:F1} guest frames/sec ({Frames} completed)", rate, frames);
                previousTime = now;
                previousFrames = frames;
            }
        }
    }
}
catch (OperationCanceledException)
{
    // Expected on Ctrl+C or TerminateApplication()
}

// ----------
// Cleanup
// ----------
logger.LogInformation("Shutting down.");
// Lua can cancel a pending statistics tick from the emulation thread. Join that thread
// from the owner after the queued Close finishes.
await hostApp.RunOnEmulationThreadAsync(() => { hostApp.Close(); return Task.CompletedTask; }).ConfigureAwait(false);
if (enableExternalDebug)
    await debugController.StopAsync();
if (remotePort.HasValue)
    await remoteController.StopAsync();

logger.LogInformation("Headless emulator exited.");
hostApp.DisposeEmulationThread();
return demoExitCode;

// ----------
// Helper methods
// ----------

static void WriteBootstrapLog(string message)
{
    // Match the SimpleConsole format used by ILogger
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} info: Program[0] {message}");
}

static LogLevel ParseLogLevel(string[] args, LogLevel defaultLevel)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--log-level" or "-l")
        {
            if (Enum.TryParse<LogLevel>(args[i + 1], ignoreCase: true, out var level))
                return level;
        }
    }
    return defaultLevel;
}

static List<string> ParseMultipleStringArgument(string[] args, string argumentName)
{
    var result = new List<string>();
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == argumentName)
            result.Add(args[i + 1]);
    }
    return result;
}

static int? ParsePortArgument(string[] args, string argumentName)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == argumentName)
        {
            if (int.TryParse(args[i + 1], out var port) && port > 0 && port <= 65535)
                return port;
        }
    }
    return null;
}

// Partial class needed for UserSecrets
public partial class Program { }
