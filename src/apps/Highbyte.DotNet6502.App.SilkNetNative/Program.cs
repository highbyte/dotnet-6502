using Highbyte.DotNet6502.App.SilkNetNative;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
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
    Console.Title = "DotNet6502 Emulator (SilkNetNative) - Log Output";
}

// Note: Don't call Console.WriteLine before AllocConsole() is called (Windows). Otherwise no logs will show in console.
WriteBootstrapLog($"SilkNetNative program starting.");

// Fix for starting in debug mode from VS Code. By default the OS current directory is set to the project folder, not the folder containing the built .exe file...
var currentAppDir = AppDomain.CurrentDomain.BaseDirectory;
Environment.CurrentDirectory = currentAppDir;

// Add unhandled exception handler to catch native crashes
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var exception = args.ExceptionObject as Exception;
    Console.WriteLine($"Unhandled exception caught: {exception?.Message ?? "Unknown error"}");
    Console.WriteLine($"Stack trace: {exception?.StackTrace ?? "No stack trace available"}");
    Console.WriteLine($"IsTerminating: {args.IsTerminating}");
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
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

IConfiguration Configuration = builder.Build();

// ----------
// Create logging
// ----------
WriteBootstrapLog($"Initializing logging.");

DotNet6502InMemLogStore logStore = new();
var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
var loggerFactory = LoggerFactory.Create(builder =>
{
    logConfig.LogLevel = LogLevel.Information;
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
// Systems
// ----------
WriteBootstrapLog($"Creating system list.");
var systemList = new SystemList<SilkNetInputHandlerContext, NAudioAudioHandlerContext>();

var c64Setup = new C64Setup(loggerFactory, Configuration);
systemList.AddSystem(c64Setup);

var genericComputerSetup = new GenericComputerSetup(loggerFactory, Configuration);
systemList.AddSystem(genericComputerSetup);

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
windowOptions.Title = "Highbyte.DotNet6502 emulator + Silk.NET (with ImGui, SkiaSharp, OpenGL, NAudio)";
windowOptions.Size = new Vector2D<int>(windowWidth, windowHeight);
windowOptions.WindowBorder = WindowBorder.Fixed;
windowOptions.API = GraphicsAPI.Default; // = Default = OpenGL 3.3 with forward compatibility
windowOptions.ShouldSwapAutomatically = true;
//windowOptions.TransparentFramebuffer = false;
//windowOptions.PreferredDepthBufferBits = 24;    // Depth buffer bits must be set explicitly on MacOS (tested on M1), otherwise there will be be no depth buffer (for OpenGL 3d).

IWindow window;

try
{
    WriteBootstrapLog($"Creating Silk.NET window...");
    window = Window.Create(windowOptions);
    WriteBootstrapLog($"Silk.NET window created.");
}
catch (Exception ex)
{
    WriteBootstrapLog($"Failed to create Silk.NET window: {ex.Message}", LogLevel.Error);
    WriteBootstrapLog($"Stack trace: {ex.StackTrace}", LogLevel.Error);
    
    // Cleanup on Windows
    if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
        FreeConsole();
    }
    return;
}

var silkNetHostApp = new SilkNetHostApp(systemList, loggerFactory, emulatorConfig, window, logStore, logConfig);

try
{
    WriteBootstrapLog($"Starting Silk.NET host app...");
    silkNetHostApp.Run();
    WriteBootstrapLog($"Silk.NET host app exited normally.");
}
catch (Exception ex)
{
    WriteBootstrapLog($"Application exited with Exception: {ex.Message}", LogLevel.Error);
    WriteBootstrapLog($"Stack trace: {ex.StackTrace}", LogLevel.Error);
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