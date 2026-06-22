using Highbyte.DotNet6502.App.Headless;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Scripting;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ----------
// Parse command line arguments
// ----------
LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);

WriteBootstrapLog("Starting headless emulator.");

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

// Parse scripting override arguments
List<string> scriptFilePaths = ParseMultipleStringArgument(args, "--script");
string? scriptDirectoryOverride = AutomatedStartupHandler.ParseStringArgument(args, "--scriptDir");

// Validate automated startup arguments
bool hasScripts = scriptFilePaths.Count > 0 || scriptDirectoryOverride != null;
if (!AutomatedStartupHandler.ValidateArguments(systemName, systemVariant, autoStart, waitForSystemReady, loadPrgPath, runLoadedProgram, hasScripts))
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

if (enableExternalDebug)
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
if (remotePort.HasValue)
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
bool automatedStartupMode = autoStart || waitForSystemReady || loadPrgPath != null || runLoadedProgram;
var scriptingEngine = MoonSharpScriptingConfigurator.Create(configuration, loggerFactory, scriptFilePaths, scriptDirectoryOverride, suppressConfigScripts: automatedStartupMode, hostType: "headless");

// ----------
// Create system list and host app
// ----------
logger.LogInformation("Creating headless host app.");

// Engine plug-ins (C64HeadlessEnginePlugin / GenericHeadlessEnginePlugin, in the
// App.Headless.Shell.* projects) register the per-system ISystemConfigurer. The SystemList is
// built from those.
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
var serviceProvider = services.BuildServiceProvider(DotNet6502ServiceProviderOptions.Validated);

var systemList = new SystemList();
foreach (var configurer in serviceProvider
    .GetServices<ISystemConfigurer>())
{
    systemList.AddSystem(configurer);
}

// Drop any system that declares no configuration variants — it cannot be built or run.
await systemList.RemoveSystemsWithNoConfigurationVariants(pluginLogger);

// No usable system: a headless run cannot do anything without one. There is no UI to show an
// error dialog in, so log a clear message and exit with a non-zero code.
if (systemList.Systems.Count == 0)
{
    logger.LogCritical(
        "No emulator systems are available. Check the 'EnabledSystems' setting in appsettings.json " +
        "and that the system plug-in assemblies are deployed. Exiting.");
    return 1;
}

var hostApp = new HeadlessHostApp(systemList, loggerFactory, appCts);

// Wire the debug environment to the host app
debugEnvironment.HostApp = hostApp;

// Wire the remote environment to the host app
remoteEnvironment.HostApp = hostApp;

// Set scripting engine
hostApp.SetScriptingEngine(scriptingEngine ?? new NoScriptingEngine());
await hostApp.DrainStartupScriptActionsAsync();

logger.LogInformation("Headless host app initialized.");

// ----------
// Automated startup
// ----------
if (systemName != null)
{
    var startupRequest = new AutomatedStartupRequest(
        systemName, systemVariant, autoStart, waitForSystemReady,
        loadPrgPath, runLoadedProgram, enableExternalDebug);
    await AutomatedStartupHandler.ExecuteAsync(
        hostApp,
        startupRequest,
        onStartupComplete: () => debugController.SignalProgramReady(),
        loggerFactory: loggerFactory,
        prepareForExternalDebuggerStart: () => hostApp.WaitForExternalDebugger = true);
}

// ----------
// Block until shutdown
// ----------
logger.LogInformation("Headless emulator running. Press Ctrl+C to exit.");
try
{
    await Task.Delay(Timeout.Infinite, appCts.Token);
}
catch (OperationCanceledException)
{
    // Expected on Ctrl+C or TerminateApplication()
}

// ----------
// Cleanup
// ----------
logger.LogInformation("Shutting down.");
hostApp.Close();
if (enableExternalDebug)
    await debugController.StopAsync();
if (remotePort.HasValue)
    await remoteController.StopAsync();

logger.LogInformation("Headless emulator exited.");
return 0;

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
