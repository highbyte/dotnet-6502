using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.App.SadConsole;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

// ----------
// Parse command line arguments
// ----------
bool enableConsoleLogging = args.Contains("--console-log") || args.Contains("-c");
LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);

// On Windows, WinExe applications don't have a console attached.
// Create a new console window for logging if enabled.
// Note: This creates a separate console window rather than attaching to the parent terminal,
// which avoids cursor/prompt synchronization issues with PowerShell/cmd.
if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    AllocConsole();
    System.Console.Title = "DotNet6502 Emulator (SadConsole) - Log Output";
}

// Note: Don't call Console.WriteLine before AllocConsole() is called (Windows). Otherwise no logs will show in console.
WriteBootstrapLog($"SadConsole program starting.");

// ----------
// Get config file
// ----------
WriteBootstrapLog($"Creating configuration object.");
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT ");
var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";
if (isDevelopment) //only add secrets in development
{
    builder.AddUserSecrets<Program>();
}

IConfiguration Configuration = builder.Build();

// ----------
// Create logging
// ----------
WriteBootstrapLog($"Initializing logging.");

DotNet6502InMemLogStore logStore = new() { WriteDebugMessage = true };
var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
var loggerFactory = LoggerFactory.Create(builder =>
{
    logConfig.LogLevel = LogLevel.Information;  // LogLevel.Debug, LogLevel.Information, 
    builder.AddInMem(logConfig);
    builder.SetMinimumLevel(LogLevel.Trace);

    // Add console logging if enabled via command line
    if (enableConsoleLogging)
    {
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
        builder.AddFilter(null, consoleLogLevel);  // Apply log level filter

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
var systemList = new SystemList<SadConsoleInputHandlerContext, NAudioAudioHandlerContext>();

var c64Setup = new C64Setup(loggerFactory, Configuration);
systemList.AddSystem(c64Setup);

var genericComputerSetup = new GenericComputerSetup(loggerFactory, Configuration);
systemList.AddSystem(genericComputerSetup);

// ----------
// Start SadConsoleHostApp
// ----------
emulatorConfig.Validate(systemList);

WriteBootstrapLog($"Starting SadConsole app.");
var sadConsoleHostApp = new SadConsoleHostApp(systemList, loggerFactory, emulatorConfig, logStore, logConfig, Configuration);
sadConsoleHostApp.Run();

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
