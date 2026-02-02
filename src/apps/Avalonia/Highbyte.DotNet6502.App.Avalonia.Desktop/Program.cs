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
using Highbyte.DotNet6502.DebugAdapter;
using System.Threading;

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
    public static void Main(string[] args)
    {
        // ----------
        // Parse command line arguments
        // ----------
        bool enableConsoleLogging = args.Contains("--console-log") || args.Contains("-c");
        LogLevel consoleLogLevel = ParseLogLevel(args, defaultLevel: LogLevel.Information);
        
        // Parse debug adapter arguments
        int debugPort = ParseDebugPort(args);
        bool debugWait = args.Contains("--debug-wait");

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
        TcpDebugAdapterServer? debugServer = null;
        bool debugClientConnected = false;
        if (debugPort > 0)
        {
            WriteBootstrapLog($"Starting TCP debug adapter server on port {debugPort}.");
            
            // Create debug log file
            var debugLogFilePath = Path.Combine(Path.GetTempPath(), $"dotnet6502-debugadapter-avalonia-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var debugLogWriter = new StreamWriter(debugLogFilePath, append: true) { AutoFlush = true };
            debugLogWriter.WriteLine($"Debug adapter server started at {DateTime.Now}");
            
            debugServer = new TcpDebugAdapterServer(debugLogWriter);
            debugServer.ClientConnected += (sender, e) =>
            {
                debugClientConnected = true;
                WriteBootstrapLog("Debug client connected.");
                debugLogWriter.WriteLine($"Debug client connected at {DateTime.Now}");
                
                var protocol = new DapProtocol(e.Transport, debugLogWriter);
                var adapter = new DebugAdapterLogic(protocol, debugLogWriter);
                
                // Attach to emulator when it's running
                // Note: This uses a polling approach because the system may not be started yet
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait for app instance and emulator to be in a running state
                        debugLogWriter.WriteLine("Waiting for emulator to be in running state...");
                        while (Core.App.Current?.HostApp?.CurrentRunningSystem == null)
                        {
                            await Task.Delay(100);
                        }
                        
                        debugLogWriter.WriteLine("Emulator is running, attaching debug adapter...");
                        var system = Core.App.Current.HostApp.CurrentRunningSystem;
                        adapter.AttachToEmulator(system.CPU, system.Mem);
                        
                        // Install breakpoint evaluator
                        var breakpointEvaluator = adapter.GetBreakpointEvaluator();
                        Core.App.Current.HostApp.CurrentSystemRunner!.SetCustomExecEvaluator(breakpointEvaluator);
                        
                        // Set debug adapter reference so AvaloniaHostApp can check IsStopped property
                        Core.App.Current.HostApp.SetDebugAdapter(adapter);
                        
                        // Set flag to disable built-in monitor when external debugger is attached
                        Core.App.Current.HostApp.IsExternalDebuggerAttached = true;
                        
                        debugLogWriter.WriteLine("Breakpoint evaluator installed and external debugger flag set");
                    }
                    catch (Exception ex)
                    {
                        debugLogWriter.WriteLine($"Failed to attach to emulator: {ex}");
                    }
                });
                
                // Start message loop for this client
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            var message = await protocol.ReadMessageAsync();
                            if (message == null)
                            {
                                debugLogWriter.WriteLine("Received null message, debug client disconnected");
                                break;
                            }
                            await adapter.HandleMessageAsync(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        debugLogWriter.WriteLine($"Debug adapter error: {ex}");
                    }
                    finally
                    {
                        debugLogWriter.WriteLine($"Debug adapter stopped at {DateTime.Now}");
                        
                        // Reset debugger state to unfreeze emulator
                        adapter.Reset();
                        if (Core.App.Current?.HostApp != null)
                        {
                            // Remove breakpoint evaluator to prevent exceptions when program runs again
                            Core.App.Current.HostApp.CurrentSystemRunner?.SetCustomExecEvaluator(null);
                            Core.App.Current.HostApp.IsExternalDebuggerAttached = false;
                            Core.App.Current.HostApp.SetDebugAdapter(null!);
                            debugLogWriter.WriteLine("Emulator state reset, breakpoint evaluator removed, resuming normal execution");
                        }
                        
                        debugLogWriter.Close();
                    }
                });
            };
            
            // Start listening for connections
            _ = Task.Run(async () => await debugServer.StartAsync(debugPort));
            
            if (debugWait)
            {
                WriteBootstrapLog("Waiting for debug client to connect (--debug-wait specified)...");
                // Wait for client to connect before starting app
                var waitStart = DateTime.Now;
                while (!debugClientConnected && (DateTime.Now - waitStart).TotalSeconds < 30)
                {
                    Thread.Sleep(100);
                }
                if (debugClientConnected)
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
        BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, gamepad)
            .StartWithClassicDesktopLifetime(args);

        // ----------
        // App exited
        // ----------
        WriteBootstrapLog($"Avalonia app exited.");
        // Detach from parent console on Windows to restore the command prompt
        if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FreeConsole();
        }
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
    /// Usage: --debug-port 4711
    /// Returns 0 if not specified or invalid.
    /// </summary>
    private static int ParseDebugPort(string[] args)
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
        return 0;
    }
}
