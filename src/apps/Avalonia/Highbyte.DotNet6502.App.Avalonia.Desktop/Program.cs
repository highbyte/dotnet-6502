using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
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
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Scripting;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems;

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
    ///   <item>
    ///     <term><c>--remote-port &lt;port&gt;</c></term>
    ///     <description>
    ///       Start the TCP remote control server on the specified port.
    ///       Port must be between 1 and 65535. The server can also be started later from the
    ///       <b>Debug &amp; Remoting</b> tab in the UI.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--remote-bind-address &lt;ip&gt;</c></term>
    ///     <description>
    ///       IP address the remote control server binds to. Defaults to <c>127.0.0.1</c> (loopback only).
    ///       Use <c>0.0.0.0</c> to accept connections from any network interface (note: the protocol is unauthenticated;
    ///       only expose to trusted networks). Only has effect together with <c>--remote-port</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--system &lt;name&gt;</c></term>
    ///     <description>
    ///       Pre-select a system (e.g. <c>C64</c>, <c>Generic</c>).
    ///       Mutually exclusive with <c>--script</c> / <c>--scriptDir</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--systemVariant &lt;name&gt;</c></term>
    ///     <description>
    ///       Pre-select a system variant. Requires <c>--system</c>.
    ///       Mutually exclusive with <c>--script</c> / <c>--scriptDir</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--start</c></term>
    ///     <description>
    ///       Auto-start the emulator after selection.
    ///       Mutually exclusive with <c>--script</c> / <c>--scriptDir</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--waitForSystemReady</c></term>
    ///     <description>Wait until the system reports ready. Requires <c>--start</c>.</description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadPrg &lt;path&gt;</c></term>
    ///     <description>Load a <c>.prg</c> file into memory. Requires <c>--start</c>.</description>
    ///   </item>
    ///   <item>
    ///     <term><c>--runLoadedProgram</c></term>
    ///     <description>Run the loaded program after loading. Requires <c>--start</c> and <c>--loadPrg</c>.</description>
    ///   </item>
    ///   <item>
    ///     <term><c>--script &lt;path&gt;</c></term>
    ///     <description>
    ///       Load and auto-enable a specific Lua script file (absolute or relative to CWD).
    ///       Can be specified multiple times to load several scripts.
    ///       Overrides the ScriptDirectory from configuration; only the specified files are loaded.
    ///       Mutually exclusive with <c>--start</c>, <c>--waitForSystemReady</c>, <c>--loadPrg</c>, and <c>--runLoadedProgram</c>
    ///       — the script is responsible for emulator lifecycle.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--scriptDir &lt;path&gt;</c></term>
    ///     <description>
    ///       Override the Lua script directory from configuration. All .lua files in the directory are loaded and auto-enabled.
    ///       Mutually exclusive with <c>--start</c>, <c>--waitForSystemReady</c>, <c>--loadPrg</c>, and <c>--runLoadedProgram</c>.
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
    /// # Start C64 and run a Lua script (script owns lifecycle)
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --script scripts/example_quit.lua
    ///
    /// # Start C64 and load a .prg file via CLI (no script)
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadPrg game.prg --runLoadedProgram
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
        // Skip if --no-console-window is passed (e.g. when launched by VSCode, which redirects stdout
        // to its own pipe — in that case logs flow through the pipe and a console window would be blank).
        // Note: Console.IsOutputRedirected cannot be used here because Windows Terminal's ConPTY also
        // makes stdout appear as a pipe handle even in interactive sessions.
        bool noConsoleWindow = args.Contains("--no-console-window");
        if (enableConsoleLogging && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !noConsoleWindow)
        {
            AllocConsole();
            Console.Title = "DotNet 6502 Emulator - Log Output";
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
        int debugPort = ParsePortArgument(args, "--debug-port") ?? 6502;
        bool debugWait = args.Contains("--debug-wait");

        // Parse remote control arguments
        int? remotePort = ParsePortArgument(args, "--remote-port");
        string? remoteBindAddress = AutomatedStartupHandler.ParseStringArgument(args, "--remote-bind-address");

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
            return 1; // Exit with error code
        }

        // Note: Don't call WriteBootstrapLog before AllocConsole() is called (Windows). Otherwise no logs will show in console.
        WriteBootstrapLog($"Avalonia program starting.");

        // ----------
        // Get config file
        // ----------
        WriteBootstrapLog($"Creating configuration object.");
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
        // Set up external debug controller (always created on Desktop so the UI toggle is available)
        // ----------
        var debugController = new AvaloniaExternalDebugController(new AvaloniaDebugServerEnvironment(), loggerFactory);

        // ----------
        // Set up remote control controller (started if --remote-port is provided)
        // ----------
        var remoteController = new RemoteControlController(new AvaloniaRemoteControlEnvironment(loggerFactory), loggerFactory);
        if (remotePort.HasValue)
        {
            var effectiveBindAddress = string.IsNullOrWhiteSpace(remoteBindAddress)
                ? IRemoteControlController.DefaultBindAddress
                : remoteBindAddress!.Trim();
            WriteBootstrapLog($"Starting TCP remote control server on {effectiveBindAddress}:{remotePort.Value}.");
            try
            {
                Task.Run(async () => await remoteController.StartAsync(remotePort.Value, effectiveBindAddress)).Wait();
            }
            catch (AggregateException aex) when (aex.InnerException is ArgumentException iae)
            {
                WriteBootstrapLog($"Failed to start remote control server: {iae.Message}", LogLevel.Error);
                return 1;
            }
        }

        if (enableExternalDebug)
        {
            WriteBootstrapLog($"Starting TCP debug adapter server on port {debugPort}.");

            // Start listening immediately — the adapter handles connecting before a system is running.
            Task.Run(async () => await debugController.StartAsync(debugPort)).Wait();

            if (debugWait)
            {
                WriteBootstrapLog("Waiting for debug client to connect (--debug-wait specified)...");

                if (debugController.WaitForClientConnection(timeoutSeconds: 30))
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
        // Initialize Lua scripting engine
        // ----------
        bool automatedStartupMode = autoStart || waitForSystemReady || loadPrgPath != null || runLoadedProgram;
        var scriptingEngine = MoonSharpScriptingConfigurator.Create(configuration, loggerFactory, scriptFilePaths, scriptDirectoryOverride, suppressConfigScripts: automatedStartupMode, hostType: "desktop");

        // Skip the UI's default system selection when a script or --system arg will handle it,
        // to avoid a race where the UI tries to select a system while the script/handler already has.
        bool skipDefaultSystemSelection = systemName != null || scriptFilePaths.Count > 0 || scriptDirectoryOverride != null;

        // ----------
        // Start Avalonia app
        // ----------
        WriteBootstrapLog($"Starting Avalonia app.");
        var app = BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, gamepad, debugController, remoteController, scriptingEngine, skipDefaultSystemSelection);

        // If automated startup is requested, handle it after the app starts
        if (systemName != null)
        {
            _ = Task.Run(async () =>
            {
                var startupLogger = loggerFactory.CreateLogger(nameof(Program));

                // The App object is created lazily by Avalonia during StartWithClassicDesktopLifetime,
                // so we need App.Current to exist before we can await WhenHostAppReadyAsync.
                startupLogger.LogInformation("Waiting for Avalonia App instance...");
                while (Core.App.Current == null)
                    await Task.Delay(10);

                // Await proper readiness — the TCS guarantees all writes made before TrySetResult
                // (including full HostApp construction) are visible to the continuation here.
                startupLogger.LogInformation("Awaiting HostApp initialization...");
                var hostApp = await Core.App.WhenHostAppReadyAsync.ConfigureAwait(false);
                startupLogger.LogInformation("HostApp initialized.");

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
                    uiThreadInvoker: f => Dispatcher.UIThread.InvokeAsync(f));
            });
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
        IGamepad? gamepad = null,
        IExternalDebugController? externalDebugController = null,
        IRemoteControlController? remoteControlController = null,
        IScriptingEngine? scriptingEngine = null,
        bool skipDefaultSystemSelection = false)
        => AppBuilder.Configure(() => new Core.App(
                configuration,
                emulatorConfig,
                logStore,
                logConfig,
                loggerFactory,
                saveCustomConfigString: null,
                saveCustomConfigSection: null,
                gamepad: gamepad,
                externalDebugController: externalDebugController,
                remoteControlController: remoteControlController,
                scriptingEngine: scriptingEngine,
                skipDefaultSystemSelection: skipDefaultSystemSelection))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
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
    /// Parses all occurrences of a named argument from command line arguments.
    /// Usage: --script foo.lua --script bar.lua → ["foo.lua", "bar.lua"]
    /// </summary>
    private static List<string> ParseMultipleStringArgument(string[] args, string argumentName)
    {
        var result = new List<string>();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == argumentName)
                result.Add(args[i + 1]);
        }
        return result;
    }

    /// <summary>
    /// Parses a port number from command line arguments.
    /// Returns null if the argument is not present, enabling the caller
    /// to decide whether the associated feature should be activated or what default to use.
    /// Usage: --debug-port 6502 or --remote-port 6510
    /// </summary>
    private static int? ParsePortArgument(string[] args, string argumentName)
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
}

