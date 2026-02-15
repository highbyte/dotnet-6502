using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Impl.SilkNet.SDL.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Logging;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Input;
using System.Runtime.InteropServices;
using System.Threading;
using Highbyte.DotNet6502.Utils;
using System.Diagnostics;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

internal sealed partial class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Command Line Parameters:</b>
    /// </para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Parameter</term>
    ///     <description>Description</description>
    ///   </listheader>
    ///   <item>
    ///     <term><c>--console-log</c> or <c>-c</c></term>
    ///     <description>
    ///       Enable console logging. On Windows, opens a separate console window.
    ///       On macOS/Linux, logs appear inline in the terminal.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--log-level &lt;level&gt;</c> or <c>-l &lt;level&gt;</c></term>
    ///     <description>
    ///       Set the minimum log level for console output. Default is <c>Information</c>.
    ///       Valid values: <c>Trace</c>, <c>Debug</c>, <c>Information</c>, <c>Warning</c>, <c>Error</c>, <c>Critical</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--debug-port &lt;port&gt;</c></term>
    ///     <description>
    ///       Enable TCP debug adapter server on the specified port for VSCode debugging.
    ///       Port must be between 1 and 65535.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--debug-wait</c></term>
    ///     <description>
    ///       Wait for debug client to connect before starting the application.
    ///       Only effective when used with <c>--debug-port</c>. Times out after 30 seconds.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// <b>Examples:</b>
    /// </para>
    /// <code>
    /// # Enable console logging with default level (Information)
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --console-log
    /// 
    /// # Enable console logging with Debug level
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop -c -l Debug
    /// 
    /// # Enable console logging with Warning level only
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --console-log --log-level Warning
    /// </code>
    /// </remarks>
    /// <param name="args">Command line arguments.</param>
    [STAThread]
    public static int Main(string[] args)
    {
        // ----------
        // Parse command line arguments
        // ----------
        // Setup logging
        bool enableConsoleLogging = args.Contains("--console-log") || args.Contains("-c");
        LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);
        // Set bootstrap console logging flag (for Console.WriteLine before ILogger is available)
        AppLogger.ConsoleLoggingEnabled = enableConsoleLogging;
        // On Windows, WinExe applications don't have a console attached.
        // Create a new console window for logging if enabled.
        // Note: This creates a separate console window rather than attaching to the parent terminal,
        // which avoids cursor/prompt synchronization issues with PowerShell/cmd.
        if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AllocConsole();
            Console.Title = "DotNet6502 Emulator - Log Output";
        }

        WriteBootstrapLog("Starting.");

        // Check if we should wait for a debugger to attach based on environment variable
        var waitForDebuggerEnv = Environment.GetEnvironmentVariable("DOTNET6502_WAIT_FOR_DEBUGGER");
        WriteBootstrapLog($"DOTNET6502_WAIT_FOR_DEBUGGER={waitForDebuggerEnv}");
        if (!string.IsNullOrEmpty(waitForDebuggerEnv) && waitForDebuggerEnv.ToLower() == "true")
        {
            WriteBootstrapLog($"Waiting for debugger to attach... (set DOTNET6502_WAIT_FOR_DEBUGGER=false to disable)");

            while (!System.Diagnostics.Debugger.IsAttached) 
            {
                Thread.Sleep(100);
            }

            WriteBootstrapLog("Debugger attached, break into debugger immediately.");
            Debugger.Break();
        }

        // Parse debug adapter arguments
        bool enableExternalDebug = args.Contains("--enableExternalDebug");
        int debugPort = ParseDebugPort(args, defaultPort: 6502);
        bool debugWait = args.Contains("--debug-wait");

        // Parse automated startup arguments
        string? systemName = AutomatedStartupHandler.ParseStringArgument(args, "--system");
        string? systemVariant = AutomatedStartupHandler.ParseStringArgument(args, "--systemVariant");
        bool autoStart = args.Contains("--start");
        bool waitForSystemReady = args.Contains("--waitForSystemReady");
        string? loadPrgPath = AutomatedStartupHandler.ParseStringArgument(args, "--loadPrg");
        bool runLoadedProgram = args.Contains("--runLoadedProgram");

        // Validate automated startup arguments
        if (!AutomatedStartupHandler.ValidateArguments(systemName, systemVariant, autoStart, waitForSystemReady, loadPrgPath, runLoadedProgram))
        {
            return 1; // Exit with error code
        }

        // Note: Don't call WriteBootstrapLog before AllocConsole() is called (Windows). Otherwise no logs will show in console.
        WriteBootstrapLog($"Avalonia program starting.");

        // ----------
        // Get config file
        // ----------
        WriteBootstrapLog($"Creating configuration object.");
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
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
        WriteBootstrapLog($"Initializing logging.");

        DotNet6502InMemLogStore logStore = new(insertAtStart: false) { WriteDebugMessage = true };
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

        // Create an ILogger for bridging Avalonia logs
        var avaloniaILogger = loggerFactory.CreateLogger("Avalonia");
        var avaloniaLoggerBridge = new AvaloniaLoggerBridge(avaloniaILogger, LogLevel.Warning);

        // ----------
        // Get emulator host config
        // ----------
        WriteBootstrapLog($"Reading emulator config.");
        var emulatorConfig = new EmulatorConfig();
        configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

        // ----------
        // Create SDL2 gamepad for controller input
        // ----------
        WriteBootstrapLog($"Creating Gamepad implementation (SDL2).");
        var gamepad = new Sdl2Gamepad(loggerFactory);

        // ----------
        // Start debug adapter server if requested
        // ----------
        TcpDebugServerManager? debugServerManager = null;
        if (enableExternalDebug)
        {
            WriteBootstrapLog($"Starting TCP debug adapter server on port {debugPort}.");

            // Create debug log file
            var debugLogFilePath = Path.Combine(Path.GetTempPath(), $"dotnet6502-debugadapter-avalonia-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var debugLogWriter = new StreamWriter(debugLogFilePath, append: true) { AutoFlush = true };
            debugLogWriter.WriteLine($"Debug adapter server started at {DateTime.Now}");

            debugServerManager = new TcpDebugServerManager(debugLogWriter);

            //// If loading a PRG for debugging (not running it), set the pending PC before debugger connects
            //if (loadPrgPath != null && !runLoadedProgram)
            //{
            //    var expandedPrgPath = PathHelper.ExpandOSEnvironmentVariables(loadPrgPath);
            //    if (File.Exists(expandedPrgPath))
            //    {
            //        var prgBytes = File.ReadAllBytes(expandedPrgPath);
            //        if (prgBytes.Length >= 2)
            //        {
            //            // Read load address (first two bytes, little-endian)
            //            ushort loadAddress = (ushort)(prgBytes[0] | (prgBytes[1] << 8));
            //            debugLogWriter.WriteLine($"Setting pending PC to 0x{loadAddress:X4} from PRG file (before debugger connects)");
            //            debugServerManager.SetPendingProgramCounter(loadAddress);
            //        }
            //    }
            //}

            // Start listening immediately — the adapter handles connecting before a system is running.
            _ = Task.Run(async () => await debugServerManager.StartAsync(debugPort));

            if (debugWait)
            {
                WriteBootstrapLog("Waiting for debug client to connect (--debug-wait specified)...");

                if (debugServerManager.WaitForClientConnection(timeoutSeconds: 30))
                {
                    WriteBootstrapLog("Debug client connected, continuing startup.");
                }
                else
                {
                    WriteBootstrapLog("Debug client connection timeout, continuing startup anyway.");
                }
            }
        }

        // ----------
        // Start Avalonia app
        // ----------
        WriteBootstrapLog($"Starting Avalonia app.");
        var app = BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, gamepad);

        // If automated startup is requested, handle it after the app starts
        if (systemName != null)
        {
            _ = Task.Run(async () => await AutomatedStartupHandler.ExecuteAsync(
                systemName,
                systemVariant,
                autoStart,
                waitForSystemReady,
                loadPrgPath,
                runLoadedProgram,
                enableExternalDebug,
                onStartupComplete: () => debugServerManager?.SignalProgramReady(),
                loggerFactory));
        }

        app.StartWithClassicDesktopLifetime(args);

        // ----------
        // App exited
        // ----------
        WriteBootstrapLog($"Avalonia app exited.");
        // Detach from parent console on Windows to restore the command prompt
        if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FreeConsole();
        }

        return 0; // Success
    }

    private static void WriteBootstrapLog(string message, LogLevel logLevel = LogLevel.Information)
    {
        AppLogger.WriteBootstrapLog(message, logLevel, nameof(Program));
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        AvaloniaLoggerBridge avaloniaLoggerBridge,
        IGamepad? gamepad = null)
        => AppBuilder.Configure(() => new Core.App(
                configuration,
                emulatorConfig,
                logStore,
                logConfig,
                loggerFactory,
                saveCustomConfigString: null,
                saveCustomConfigSection: null,
                gamepad: gamepad))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI()
            .AfterSetup(_ =>
            {
                // Set up the Avalonia logger bridge to route logs via Avalonia Logger through ILogger
                global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
            });
}

internal sealed partial class Program
{
    // Windows API to create a new console window for the process
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    // Windows API to detach from console before exiting
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    /// <summary>
    /// Parses the log level from command line arguments.
    /// Usage: --log-level Debug or -l Warning
    /// Valid values: Trace, Debug, Information, Warning, Error, Critical
    /// </summary>
    private static LogLevel ParseLogLevel(string[] args, LogLevel defaultLevel)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--log-level" or "-l")
            {
                if (Enum.TryParse<LogLevel>(args[i + 1], ignoreCase: true, out var level))
                {
                    return level;
                }
            }
        }
        return defaultLevel;
    }

    /// <summary>
    /// Parses the debug port from command line arguments.
    /// Usage: --debug-port 6502
    /// Returns defaultPort if not specified or invalid.
    /// </summary>
    private static int ParseDebugPort(string[] args, int defaultPort)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--debug-port")
            {
                if (int.TryParse(args[i + 1], out var port) && port > 0 && port <= 65535)
                {
                    return port;
                }
            }
        }
        return defaultPort;
    }
}

