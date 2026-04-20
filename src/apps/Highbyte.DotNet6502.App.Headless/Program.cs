using Highbyte.DotNet6502.App.Headless;
using Highbyte.DotNet6502.App.Headless.SystemSetup;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Scripting;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// ----------
// Parse command line arguments
// ----------
LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);

WriteBootstrapLog("Starting headless emulator.");

// Parse debug adapter arguments
bool enableExternalDebug = args.Contains("--enableExternalDebug");
int debugPort = ParseDebugPort(args, defaultPort: 6502);
bool debugWait = args.Contains("--debug-wait");

// Parse remote control arguments
int? remotePort = ParseOptionalPort(args, "--remote-port");
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
    logger.LogInformation("Starting TCP debug adapter server on port {DebugPort}.", debugPort);
    await debugController.StartAsync(debugPort);

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
    logger.LogInformation("Starting TCP remote control server on port {RemotePort}.", remotePort.Value);
    await remoteController.StartAsync(remotePort.Value);
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
var systemList = new SystemList<NullInputHandlerContext, NullAudioHandlerContext>();

var c64Setup = new C64Setup(loggerFactory, configuration);
systemList.AddSystem(c64Setup);

var genericComputerSetup = new GenericComputerSetup(loggerFactory, configuration);
systemList.AddSystem(genericComputerSetup);

var hostApp = new HeadlessHostApp(systemList, loggerFactory, appCts);

// Wire the debug environment to the host app
debugEnvironment.HostApp = hostApp;

// Wire the remote environment to the host app
remoteEnvironment.HostApp = hostApp;

// Set scripting engine
hostApp.SetScriptingEngine(scriptingEngine ?? new NoScriptingEngine());

logger.LogInformation("Headless host app initialized.");

// ----------
// Automated startup
// ----------
if (systemName != null)
{
    await AutomatedStartupHandler.ExecuteAsync(
        hostApp,
        systemName,
        systemVariant,
        autoStart,
        waitForSystemReady,
        loadPrgPath,
        runLoadedProgram,
        enableExternalDebug,
        onStartupComplete: () => debugController.SignalProgramReady(),
        loggerFactory: loggerFactory,
        uiThreadInvoker: null); // No UI thread in headless mode
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

static int ParseDebugPort(string[] args, int defaultPort)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--debug-port")
        {
            if (int.TryParse(args[i + 1], out var port) && port > 0 && port <= 65535)
                return port;
        }
    }
    return defaultPort;
}

static int? ParseOptionalPort(string[] args, string argumentName)
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
