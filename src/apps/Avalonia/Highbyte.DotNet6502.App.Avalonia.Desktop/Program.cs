using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.SilkNet.SDL.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Logging;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Input;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using Highbyte.DotNet6502.Utils;
using System.Diagnostics;
using System.Globalization;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Scripting;
using Highbyte.DotNet6502.Scripting.MoonSharp;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Configuration;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Plugins;
using Highbyte.DotNet6502.Updates;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

internal sealed partial class Program
{
    private const string BundledExampleScriptsDirectoryName = "example-scripts";

    private static AutomatedRunController? s_automatedRunController;

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
    ///     <term><c>--enableExternalDebug</c></term>
    ///     <description>
    ///       Start the TCP debug adapter server for VSCode debugging. This is the flag that actually
    ///       enables the server; <c>--debug-port</c> / <c>--debug-bind-address</c> / <c>--debug-wait</c>
    ///       only configure it. The server listens on <c>--debug-port</c> (default <c>6502</c>) bound
    ///       to <c>--debug-bind-address</c> (default <c>127.0.0.1</c>).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--debug-port &lt;port&gt;</c></term>
    ///     <description>
    ///       Port the TCP debug adapter server listens on. Defaults to <c>6502</c>. Port must be
    ///       between 1 and 65535. Only has effect together with <c>--enableExternalDebug</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--debug-bind-address &lt;ip&gt;</c></term>
    ///     <description>
    ///       IP address the debug adapter server binds to. Defaults to <c>127.0.0.1</c> (loopback only).
    ///       Use <c>0.0.0.0</c> to accept connections from any network interface (note: the debug adapter is unauthenticated
    ///       and exposes emulator debugging control; only expose to trusted networks). Only has effect together with <c>--enableExternalDebug</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--debug-wait</c></term>
    ///     <description>
    ///       Wait for debug client to connect before starting the application.
    ///       Only effective when used with <c>--enableExternalDebug</c>. Times out after 30 seconds.
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
    ///     <description>
    ///       Load a <c>.prg</c> file into memory. Requires <c>--start</c>. For C64 BASIC-style
    ///       programs, use <c>--waitForSystemReady</c> so the machine has finished booting before
    ///       the PRG is loaded.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadPrgUrl &lt;url&gt;</c></term>
    ///     <description>
    ///       Fetch a <c>.prg</c> file from an absolute <c>http</c>/<c>https</c> URL and load it into
    ///       memory. Same semantics as <c>--loadPrg</c> but the bytes are downloaded instead of read
    ///       from the local filesystem. Requires <c>--start</c>; mutually exclusive with
    ///       <c>--loadPrg</c>, <c>--loadD64</c>, <c>--loadD64Url</c>, <c>--loadCrt</c>, and
    ///       <c>--loadCrtUrl</c>. For C64 BASIC-style programs, use <c>--waitForSystemReady</c>.
    ///       Browser-equivalent of <c>loadPrgUrl</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--runLoadedProgram</c></term>
    ///     <description>
    ///       Run the loaded program after loading. Requires <c>--start</c> and one of <c>--loadPrg</c>,
    ///       <c>--loadPrgUrl</c>, <c>--loadD64</c>, or <c>--loadD64Url</c>. Does not apply to
    ///       <c>--loadCrt</c> / <c>--loadCrtUrl</c>. For C64 BASIC-style programs, pair with <c>--waitForSystemReady</c>.
    ///       In the <c>--loadD64</c> flow, controls whether the disk-info <c>RunCommands</c>
    ///       (e.g. <c>LOAD"*",8,1</c> + <c>RUN</c>) are pasted after the load / mount.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadD64 &lt;path&gt;</c></term>
    ///     <description>
    ///       Load a C64 <c>.d64</c> disk image. Requires <c>--system C64</c>, <c>--start</c>,
    ///       <c>--waitForSystemReady</c>, and exactly one of <c>--d64Program</c> or <c>--diskMount</c>.
    ///       Mutually exclusive with <c>--loadPrg</c>, <c>--loadPrgUrl</c>, <c>--loadD64Url</c>,
    ///       <c>--loadCrt</c>, and <c>--loadCrtUrl</c>. ZIP archives are accepted; by default the
    ///       first <c>.d64</c> entry is used.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadD64Url &lt;url&gt;</c></term>
    ///     <description>
    ///       Fetch a C64 <c>.d64</c> disk image from an absolute <c>http</c>/<c>https</c> URL. Same
    ///       semantics and requirements as <c>--loadD64</c> but the bytes are downloaded instead of
    ///       read from the local filesystem. Mutually exclusive with <c>--loadD64</c>,
    ///       <c>--loadPrg</c>, <c>--loadPrgUrl</c>, <c>--loadCrt</c>, and <c>--loadCrtUrl</c>.
    ///       Browser-equivalent of <c>loadD64Url</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadD64ZipEntry &lt;entry&gt;</c></term>
    ///     <description>
    ///       Select an exact <c>.d64</c> entry when <c>--loadD64</c> / <c>--loadD64Url</c> points at
    ///       a ZIP archive. Use forward slashes for folders.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadCrt &lt;path&gt;</c></term>
    ///     <description>
    ///       Attach a C64 <c>.crt</c> cartridge image at startup. Requires <c>--system C64</c> and
    ///       <c>--start</c>; <c>--waitForSystemReady</c> is not required because cartridges reset /
    ///       boot the machine when attached. Mutually exclusive with PRG, D64, and BASIC startup loads.
    ///       ZIP archives containing exactly one <c>.crt</c> are accepted by default.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadCrtUrl &lt;url&gt;</c></term>
    ///     <description>
    ///       Fetch and attach a C64 <c>.crt</c> cartridge image from an absolute <c>http</c>/<c>https</c>
    ///       URL. Same semantics as <c>--loadCrt</c> but the bytes are downloaded instead of read from
    ///       the local filesystem. Browser-equivalent of <c>loadCrtUrl</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--loadCrtZipEntry &lt;entry&gt;</c></term>
    ///     <description>
    ///       Select an exact <c>.crt</c> entry when <c>--loadCrt</c> / <c>--loadCrtUrl</c> points at
    ///       a ZIP archive. Use forward slashes for folders; this is required for ZIP archives with
    ///       multiple <c>.crt</c> files.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--d64Program &lt;name|*&gt;</c></term>
    ///     <description>
    ///       Extract the named PRG file from the <c>.d64</c> image and load it directly into memory
    ///       (no disk mount). <c>*</c> selects the first directory entry. Mutually exclusive with
    ///       <c>--diskMount</c>; requires <c>--loadD64</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--diskMount</c></term>
    ///     <description>
    ///       Mount the <c>.d64</c> image in drive 8 and prepare to issue <c>LOAD"*",8,1</c> +
    ///       <c>RUN</c> via the keyboard buffer. Mutually exclusive with <c>--d64Program</c>;
    ///       requires <c>--loadD64</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--basicText &lt;text&gt;</c></term>
    ///     <description>
    ///       Inline C64 BASIC source text (plain text) to paste into the running C64 after BASIC is
    ///       ready. Requires <c>--system C64</c>, <c>--start</c>, and <c>--waitForSystemReady</c>.
    ///       Mutually exclusive with <c>--basicFile</c>, <c>--basicUrl</c>, and any PRG / <c>.d64</c>
    ///       load flag. Browser-equivalent of <c>basicText</c> (the browser value is base64url-encoded
    ///       because it travels in a URL; the desktop flag takes plain text).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--basicFile &lt;path&gt;</c></term>
    ///     <description>
    ///       Load C64 BASIC source text from a local file and paste it after BASIC is ready. Same
    ///       requirements and exclusivity as <c>--basicText</c>. The desktop-natural equivalent of the
    ///       browser <c>basicUrl</c> parameter (local file vs URL).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--basicUrl &lt;url&gt;</c></term>
    ///     <description>
    ///       Fetch C64 BASIC source text from an absolute <c>http</c>/<c>https</c> URL and paste it
    ///       after BASIC is ready. Same requirements and exclusivity as <c>--basicText</c>.
    ///       Browser-equivalent of <c>basicUrl</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--runBasic</c></term>
    ///     <description>
    ///       After the BASIC source has been pasted, queue <c>run</c> + Return. Requires one of
    ///       <c>--basicText</c>, <c>--basicFile</c>, or <c>--basicUrl</c>. Mirrors browser <c>runBasic</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--keyboardJoystickEnabled</c></term>
    ///     <description>
    ///       Force-enable the C64 keyboard-emulated joystick before starting. Requires
    ///       <c>--system C64</c>; applies for any C64 start path (plain <c>--start</c>,
    ///       <c>--loadPrg</c>, <c>--loadD64</c>).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--keyboardJoystickNumber &lt;1|2&gt;</c></term>
    ///     <description>
    ///       Which C64 joystick port the keyboard emulates (and also drives via the active gamepad
    ///       port). Implies <c>--keyboardJoystickEnabled</c>. Requires <c>--system C64</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--audioEnabled &lt;true|false&gt;</c></term>
    ///     <description>
    ///       Override the C64 audio-enable config before starting. Omit to keep the existing
    ///       value. Requires <c>--system C64</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--stats-interval &lt;seconds&gt;</c></term>
    ///     <description>
    ///       Log a snapshot of the current instrumentation statistics once every N seconds after automated startup completes.
    ///       Requires <c>--start</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>--exit-after &lt;seconds&gt;</c></term>
    ///     <description>
    ///       Exit the desktop emulator N seconds after automated startup completes. Requires <c>--start</c>.
    ///     </description>
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
    /// # Start C64, wait for BASIC to be ready, then load and run a .prg file via CLI
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadPrg game.prg --runLoadedProgram
    ///
    /// # Start C64, wait for BASIC, run a .prg, log stats every 5 seconds, and exit after 60 seconds
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadPrg game.prg --runLoadedProgram --stats-interval 5 --exit-after 60
    ///
    /// # Start C64 (PAL), mount a .d64 in drive 8, paste LOAD"*",8,1 + RUN, set keyboard-joystick to port 2
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --systemVariant C64PAL --start --waitForSystemReady --loadD64 ~/Downloads/SomeGame.d64 --diskMount --runLoadedProgram --keyboardJoystickEnabled --keyboardJoystickNumber 2
    ///
    /// # Start C64, direct-load the first PRG from a .d64 image (no disk mount) and RUN it
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadD64 ~/Downloads/SomeGame.d64 --d64Program "*" --runLoadedProgram
    ///
    /// # Start C64, fetch a .prg over HTTP and run it
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadPrgUrl https://example.com/game.prg --runLoadedProgram
    ///
    /// # Start C64, fetch a .d64 over HTTP, direct-load the first PRG (no disk mount) and RUN it
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadD64Url https://example.com/game.d64 --d64Program "*" --runLoadedProgram
    ///
    /// # Start C64 and attach a .crt cartridge image
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadCrt ~/Downloads/fc3.crt
    ///
    /// # Start C64, paste BASIC source from a local .bas file and run it
    /// ./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --basicFile hello.bas --runBasic
    /// </code>
    /// <para>
    /// For multi-step workflows not covered by the discrete flags (e.g. mounting a disk and
    /// issuing custom BASIC commands), use <c>--script</c> / <c>--scriptDir</c> with the
    /// <c>c64.load_d64()</c> / <c>c64.print_text()</c> Lua API
    /// (see <c>resources/scripts/shared/example_c64_load_d64.lua</c>).
    /// </para>
    /// </remarks>
    /// <param name="args">Command line arguments.</param>
    [STAThread]
    public static int Main(string[] args)
    {
        // Update-check CLI flags run before any GUI/startup work and exit immediately.
        var updateExitCode = TryHandleUpdateCli(args);
        if (updateExitCode is not null)
            return updateExitCode.Value;

        try
        {
            return RunApp(args);
        }
        catch (Exception ex)
        {
            // A failure before the Avalonia app could start (malformed appsettings.json, a
            // plug-in/DI failure, ...). Show it in a minimal stand-alone error window; if even
            // that fails, RunStartupErrorApp logs it.
            var rootEx = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
            WriteBootstrapLog($"Fatal error during startup: {rootEx}", LogLevel.Critical);
            RunStartupErrorApp("The emulator could not start.\n\n" + rootEx.Message, args);
            return 1;
        }
    }

    private static int RunApp(string[] args)
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
        string? debugBindAddress = AutomatedStartupHandler.ParseStringArgument(args, "--debug-bind-address");
        bool debugWait = args.Contains("--debug-wait");

        // Parse remote control arguments
        int? remotePort = ParsePortArgument(args, "--remote-port");
        string? remoteBindAddress = AutomatedStartupHandler.ParseStringArgument(args, "--remote-bind-address");
        bool showStoragePaths = args.Contains("--show-storage-paths");

        // Parse automated startup arguments
        string? systemName = AutomatedStartupHandler.ParseStringArgument(args, "--system");
        string? systemVariant = AutomatedStartupHandler.ParseStringArgument(args, "--systemVariant");
        bool autoStart = args.Contains("--start");
        bool waitForSystemReady = args.Contains("--waitForSystemReady");
        string? loadPrgPath = AutomatedStartupHandler.ParseStringArgument(args, "--loadPrg");
        string? loadPrgUrl = AutomatedStartupHandler.ParseStringArgument(args, "--loadPrgUrl");
        bool runLoadedProgram = args.Contains("--runLoadedProgram");
        // --load-snapshot defines the machine and full state from the snapshot manifest, so it does
        // not combine with --system / --loadPrg / .d64 / .crt; --start resumes the restored machine.
        string? loadSnapshotPath = AutomatedStartupHandler.ParseStringArgument(args, "--load-snapshot");
        var statsInterval = ParseDurationSecondsArgument(args, "--stats-interval");
        if (statsInterval == TimeSpan.MinValue)
            return 1;
        var exitAfter = ParseDurationSecondsArgument(args, "--exit-after");
        if (exitAfter == TimeSpan.MinValue)
            return 1;

        // Parse scripting override arguments
        List<string> scriptFilePaths = ParseMultipleStringArgument(args, "--script");
        string? scriptDirectoryOverride = AutomatedStartupHandler.ParseStringArgument(args, "--scriptDir");

        // Parse C64 .d64 startup arguments (handled by C64AvaloniaStartupParticipant via ExtraParameters)
        string? loadD64Path = AutomatedStartupHandler.ParseStringArgument(args, "--loadD64");
        string? loadD64Url = AutomatedStartupHandler.ParseStringArgument(args, "--loadD64Url");
        string? loadD64ZipEntry = AutomatedStartupHandler.ParseStringArgument(args, "--loadD64ZipEntry");
        string? d64Program = AutomatedStartupHandler.ParseStringArgument(args, "--d64Program");
        string? loadCrtPath = AutomatedStartupHandler.ParseStringArgument(args, "--loadCrt");
        string? loadCrtUrl = AutomatedStartupHandler.ParseStringArgument(args, "--loadCrtUrl");
        string? loadCrtZipEntry = AutomatedStartupHandler.ParseStringArgument(args, "--loadCrtZipEntry");
        bool diskMount = args.Contains("--diskMount");
        string? keyboardJoystickNumberRaw = AutomatedStartupHandler.ParseStringArgument(args, "--keyboardJoystickNumber");
        bool keyboardJoystickEnabledFlag = args.Contains("--keyboardJoystickEnabled");
        string? audioEnabledRaw = AutomatedStartupHandler.ParseStringArgument(args, "--audioEnabled");

        // Parse C64 BASIC-paste startup arguments (also handled by C64AvaloniaStartupParticipant).
        // Three mutually exclusive sources for the same BASIC text: inline (--basicText), local file
        // (--basicFile), or HTTP (--basicUrl). The participant reads a base64url 'basicText' extra or
        // a 'basicUrl' extra, so --basicText / --basicFile are encoded into the 'basicText' extra below.
        string? basicTextArg = AutomatedStartupHandler.ParseStringArgument(args, "--basicText");
        string? basicFileArg = AutomatedStartupHandler.ParseStringArgument(args, "--basicFile");
        string? basicUrl = AutomatedStartupHandler.ParseStringArgument(args, "--basicUrl");
        bool runBasic = args.Contains("--runBasic");

        // Validate automated startup arguments.
        // The handler validator predates --loadD64 and rejects --runLoadedProgram unless --loadPrg
        // is set. When --loadD64 is supplied, suppress that one check by feeding the validator a
        // sentinel path; the real request carries the original (null) loadPrgPath unchanged.
        bool hasScripts = scriptFilePaths.Count > 0 || scriptDirectoryOverride != null;

        // --loadPrg and --loadPrgUrl are two sources for the same PRG load; only one may be given.
        if (loadPrgPath != null && loadPrgUrl != null)
        {
            Console.Error.WriteLine("Error: --loadPrg and --loadPrgUrl are mutually exclusive.");
            return 1;
        }
        // Same for the two .d64 sources.
        if (loadD64Path != null && loadD64Url != null)
        {
            Console.Error.WriteLine("Error: --loadD64 and --loadD64Url are mutually exclusive.");
            return 1;
        }
        if (loadCrtPath != null && loadCrtUrl != null)
        {
            Console.Error.WriteLine("Error: --loadCrt and --loadCrtUrl are mutually exclusive.");
            return 1;
        }
        // Validate any supplied load URL is an absolute http/https URL (desktop fetches over HTTP).
        if (!ValidateAbsoluteHttpUrl(loadPrgUrl, "--loadPrgUrl")
            || !ValidateAbsoluteHttpUrl(loadD64Url, "--loadD64Url")
            || !ValidateAbsoluteHttpUrl(loadCrtUrl, "--loadCrtUrl"))
        {
            return 1;
        }

        // The shared validator predates the URL variants and the .d64 flow: it only knows
        // --loadPrg. Feed it the effective PRG source (local path or URL), or a sentinel when a
        // .d64 (path or URL) is the load source, so its "--start required" / "--runLoadedProgram
        // requires a load" rules apply uniformly.
        var effectiveLoadPrg = loadPrgPath ?? loadPrgUrl;
        var validatorLoadPrgPath = effectiveLoadPrg ?? ((loadD64Path != null || loadD64Url != null) ? "<loadD64>" : null);
        if (!AutomatedStartupHandler.ValidateArguments(systemName, systemVariant, autoStart, waitForSystemReady, validatorLoadPrgPath, runLoadedProgram, hasScripts, loadSnapshotPath))
        {
            return 1; // Exit with error code
        }
        if ((statsInterval.HasValue || exitAfter.HasValue) && !autoStart)
        {
            Console.Error.WriteLine("Error: --stats-interval and --exit-after require --start to be specified.");
            return 1;
        }

        // Validate .d64 startup arguments locally (handler stays system-agnostic). The validator is
        // source-agnostic: it takes the effective .d64 load (local path or URL) and the effective
        // PRG load (local path or URL) so the same rules apply regardless of where the bytes come from.
        var effectiveLoadD64 = loadD64Path ?? loadD64Url;
        var effectiveLoadCrt = loadCrtPath ?? loadCrtUrl;
        if (!ValidateD64Arguments(
                effectiveLoadD64, loadD64ZipEntry, d64Program, diskMount,
                keyboardJoystickEnabledFlag, keyboardJoystickNumberRaw, audioEnabledRaw,
                systemName, autoStart, waitForSystemReady, effectiveLoadPrg, effectiveLoadCrt,
                out int parsedKeyboardJoystickNumber, out bool? parsedAudioEnabled))
        {
            return 1;
        }

        if (!ValidateCrtArguments(
                effectiveLoadCrt,
                loadCrtZipEntry,
                systemName, autoStart,
                effectiveLoadPrg, effectiveLoadD64, runLoadedProgram))
        {
            return 1;
        }

        // Validate the C64 BASIC-paste flags and resolve the inline / file sources into a base64url
        // 'basicText' value (the participant always base64url-decodes that extra). On error: exit 1.
        if (!ValidateBasicArguments(
                basicTextArg, basicFileArg, basicUrl, runBasic,
                systemName, autoStart, waitForSystemReady,
                effectiveLoadPrg, effectiveLoadD64, effectiveLoadCrt,
                out string? basicTextEncoded))
        {
            return 1;
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

        var userSettingsFilePath = AppStoragePaths.GetUserSettingsFilePath("Avalonia.Desktop");
        builder.AddJsonFile(userSettingsFilePath, optional: true, reloadOnChange: true);

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

        bool automatedStartupMode = autoStart || waitForSystemReady || effectiveLoadPrg != null || effectiveLoadD64 != null || effectiveLoadCrt != null || runLoadedProgram || basicTextEncoded != null || basicUrl != null;
        var scriptingConfig = MoonSharpScriptingConfigurator.CreateEffectiveConfig(configuration, loggerFactory, scriptFilePaths, scriptDirectoryOverride, suppressConfigScripts: automatedStartupMode);

        if (showStoragePaths)
        {
            return ShowStoragePathsAndExit(configuration, loggerFactory, userSettingsFilePath, scriptingConfig);
        }

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
                // Keep Avalonia startup on the original STA thread; awaiting here can resume Main on an MTA thread.
#pragma warning disable VSTHRD002
                remoteController.StartAsync(remotePort.Value, effectiveBindAddress).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }
            catch (ArgumentException ex)
            {
                WriteBootstrapLog($"Failed to start remote control server: {ex.Message}", LogLevel.Error);
                return 1;
            }
        }

        if (enableExternalDebug)
        {
            var effectiveDebugBindAddress = string.IsNullOrWhiteSpace(debugBindAddress)
                ? IExternalDebugController.DefaultBindAddress
                : debugBindAddress!.Trim();
            WriteBootstrapLog($"Starting TCP debug adapter server on {effectiveDebugBindAddress}:{debugPort}.");

            // Start listening immediately — the adapter handles connecting before a system is running.
            try
            {
#pragma warning disable VSTHRD002
                debugController.StartAsync(debugPort, effectiveDebugBindAddress).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }
            catch (ArgumentException ex)
            {
                WriteBootstrapLog($"Failed to start debug adapter server: {ex.Message}", LogLevel.Error);
                return 1;
            }

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
        var scriptingEngine = MoonSharpScriptingConfigurator.Create(configuration, loggerFactory, scriptFilePaths, scriptDirectoryOverride, suppressConfigScripts: automatedStartupMode, hostType: "desktop");

        // HTTP-backed load sources for the URL variants (--loadPrgUrl / --loadD64Url / --loadCrtUrl). Desktop
        // downloads the bytes itself; the Browser host does the equivalent. Both stay null in the
        // local-file path so the existing filesystem load path is unchanged.
        Func<Task<byte[]>>? loadPrgBytesProvider = null;
        if (loadPrgUrl != null)
        {
            var prgUrl = loadPrgUrl;
            loadPrgBytesProvider = async () =>
            {
                using var http = new HttpClient();
                WriteBootstrapLog($"Fetching PRG from '{prgUrl}'...");
                var bytes = await http.GetByteArrayAsync(prgUrl);
                WriteBootstrapLog($"Fetched {bytes.Length} bytes from '{prgUrl}'.");
                return bytes;
            };
        }

        // When a .d64/.crt URL or a --basicUrl is supplied, give the C64 startup participant
        // resource fetchers so it can download the image / BASIC text from the appropriate point
        // in the startup lifecycle (mirrors the Browser host).
        AutomatedStartupContext? automatedStartupContext = null;
        if (loadD64Url != null || loadCrtUrl != null || basicUrl != null)
        {
            automatedStartupContext = new AutomatedStartupContext
            {
                FetchBinaryResource = async url =>
                {
                    using var http = new HttpClient();
                    WriteBootstrapLog($"Fetching binary resource from '{url}'...");
                    var bytes = await http.GetByteArrayAsync(url);
                    WriteBootstrapLog($"Fetched {bytes.Length} bytes from '{url}'.");
                    return bytes;
                },
                FetchTextResource = async url =>
                {
                    using var http = new HttpClient();
                    var bytes = await http.GetByteArrayAsync(url);
                    var text = System.Text.Encoding.UTF8.GetString(bytes);
                    return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
                },
            };
        }

        // Build an automated-startup runner that MainViewModel.InitializeAsync invokes from
        // MainView.OnViewLoaded. When non-null, it suppresses the UI's default system selection.
        //  - --system <name> (and friends): runner invokes AutomatedStartupHandler.
        //  - --script / --scriptDir:        runner is a no-op (the script owns the lifecycle,
        //                                    we just need to prevent default system selection).
        //  - mutually exclusive (validated above): at most one of these applies.
        Func<IHostApp, Task>? automatedStartupRunner = null;
        if (loadSnapshotPath != null)
        {
            // Snapshot startup: the snapshot's manifest determines the machine, so the handler's
            // snapshot-load branch selects + rebuilds + restores it (paused), then resumes if
            // --start. None of the C64-specific PRG/.d64/.crt/participant setup applies.
            s_automatedRunController = (statsInterval.HasValue || exitAfter.HasValue)
                ? new AutomatedRunController(loggerFactory, statsInterval, exitAfter)
                : null;

            automatedStartupRunner = async _ =>
            {
                var startupLogger = loggerFactory.CreateLogger(nameof(Program));
                while (Core.App.Current == null)
                    await Task.Delay(10);
#pragma warning disable VSTHRD003
                var hostApp = await Core.App.WhenHostAppReadyAsync.ConfigureAwait(false);
#pragma warning restore VSTHRD003
                var debuggableHostApp = hostApp as IDebuggableHostApp;

                var startupRequest = new AutomatedStartupRequest(
                    systemName ?? "", null, autoStart, waitForSystemReady,
                    null, false, enableExternalDebug)
                {
                    LoadSnapshotPath = loadSnapshotPath,
                };
                await AutomatedStartupHandler.ExecuteAsync(
                    hostApp,
                    startupRequest,
                    onStartupComplete: () => debugController.SignalProgramReady(),
                    loggerFactory: loggerFactory,
                    prepareForExternalDebuggerStart: debuggableHostApp != null
                        ? () => debuggableHostApp.WaitForExternalDebugger = true
                        : null);

                if (hostApp is HostApp instrumentedHostApp)
                    Dispatcher.UIThread.Post(() => s_automatedRunController?.Start(instrumentedHostApp));
            };
        }
        else if (systemName != null)
        {
            s_automatedRunController = (statsInterval.HasValue || exitAfter.HasValue)
                ? new AutomatedRunController(loggerFactory, statsInterval, exitAfter)
                : null;

            automatedStartupRunner = async _ =>
            {
                var startupLogger = loggerFactory.CreateLogger(nameof(Program));

                // The App object is created lazily by Avalonia during StartWithClassicDesktopLifetime,
                // so we need App.Current to exist before we can await WhenHostAppReadyAsync.
                startupLogger.LogInformation("Waiting for Avalonia App instance...");
                while (Core.App.Current == null)
                    await Task.Delay(10);

                // Await proper readiness — the TCS guarantees all writes made before TrySetResult
                // (including full HostApp construction) are visible to the continuation here.
                // VSTHRD003: suppressed — WhenHostAppReadyAsync is a TCS completion signal, not work
                // started in another context; ConfigureAwait(false) already handles context capture.
                startupLogger.LogInformation("Awaiting HostApp initialization...");
#pragma warning disable VSTHRD003
                var hostApp = await Core.App.WhenHostAppReadyAsync.ConfigureAwait(false);
#pragma warning restore VSTHRD003
                startupLogger.LogInformation("HostApp initialized.");

                // hostApp is IHostApp here; cast to IDebuggableHostApp for the WaitForExternalDebugger setter.
                var debuggableHostApp = hostApp as IDebuggableHostApp;

                // Resolve the optional per-system automated-startup participant, keyed by system
                // name (mirrors the Browser host). No system-specific code here — the participant
                // decides per host whether it does anything. See docs/automated-startup-abstraction.md.
                var startupParticipant = Core.App.Current?.GetServiceProvider()
                    ?.GetKeyedService<IAutomatedStartupParticipant>(systemName);

                var startupExtras = new Dictionary<string, string>(
                    BuildC64AutomationExtras(
                        loadD64Path,
                        loadD64Url,
                        loadD64ZipEntry,
                        loadCrtPath,
                        loadCrtUrl,
                        loadCrtZipEntry,
                        d64Program,
                        diskMount,
                        keyboardJoystickEnabledFlag,
                        keyboardJoystickNumberRaw != null ? (int?)parsedKeyboardJoystickNumber : null,
                        parsedAudioEnabled),
                    StringComparer.OrdinalIgnoreCase);
                // Add the C64 BASIC-paste extras (mutually exclusive with the .d64 / PRG load flows,
                // validated above). 'basicText' carries the base64url the participant decodes; 'basicUrl'
                // is fetched via AutomatedStartupContext.FetchTextResource.
                if (basicTextEncoded != null)
                    startupExtras["basicText"] = basicTextEncoded;
                if (basicUrl != null)
                    startupExtras["basicUrl"] = basicUrl;
                if (runBasic)
                    startupExtras["runBasic"] = "true";
                // Pass the effective PRG source (local path or URL) as LoadPrgPath: the handler uses
                // loadPrgBytesProvider for the bytes when it is set (URL case), but a non-null
                // LoadPrgPath is what tells the C64 participant a PRG load is pending so it applies
                // the BASIC-settle wait — matching the local-file behaviour.
                var startupRequest = new AutomatedStartupRequest(
                    systemName, systemVariant, autoStart, waitForSystemReady,
                    effectiveLoadPrg, runLoadedProgram, enableExternalDebug)
                {
                    ExtraParameters = startupExtras,
                };
                await AutomatedStartupHandler.ExecuteAsync(
                    hostApp,
                    startupRequest,
                    onStartupComplete: () => debugController.SignalProgramReady(),
                    loggerFactory: loggerFactory,
                    prepareForExternalDebuggerStart: debuggableHostApp != null
                        ? () => debuggableHostApp.WaitForExternalDebugger = true
                        : null,
                    loadPrgBytesProvider: loadPrgBytesProvider,
                    startupParticipant: startupParticipant,
                    startupContext: automatedStartupContext);

                if (hostApp is HostApp instrumentedHostApp)
                    Dispatcher.UIThread.Post(() => s_automatedRunController?.Start(instrumentedHostApp));
            };
        }
        else if (scriptFilePaths.Count > 0 || scriptDirectoryOverride != null)
        {
            // Lua scripts handle the lifecycle themselves; the runner just exists to suppress
            // the UI's default system selection so the script's SelectSystem isn't raced.
            automatedStartupRunner = _ => Task.CompletedTask;
        }

        // ----------
        // Start Avalonia app
        // ----------
        WriteBootstrapLog($"Starting Avalonia app.");
        Func<Task>? loadExampleScripts = scriptingEngine is { IsEnabled: true, CanManageScripts: false } && !string.IsNullOrWhiteSpace(scriptingEngine.ScriptDirectory)
            ? () => CopyBundledExampleScriptsAsync(scriptingEngine.ScriptDirectory, loggerFactory)
            : null;
        var app = BuildAvaloniaApp(configuration, emulatorConfig, logStore, logConfig, loggerFactory, avaloniaLoggerBridge, gamepad, debugController, remoteController, scriptingEngine, automatedStartupRunner, loadExampleScripts, userSettingsFilePath, scriptingConfig);

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

    /// <summary>
    /// Shows a fatal startup error in a minimal stand-alone Avalonia window — used when the normal
    /// app could not be started at all. If even this minimal UI fails, the error is only logged.
    /// </summary>
    private static void RunStartupErrorApp(string message, string[] args)
    {
        try
        {
            AppBuilder.Configure(() => new StartupErrorApp(message))
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteBootstrapLog($"Could not display the startup error UI: {ex}", LogLevel.Critical);
        }
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
        Func<IHostApp, Task>? automatedStartupRunner = null,
        Func<Task>? loadExamples = null,
        string? userSettingsFilePath = null,
        ScriptingConfig? scriptingConfig = null)
        => AppBuilder.Configure(() => new Core.App(
                configuration,
                emulatorConfig,
                logStore,
                logConfig,
                loggerFactory,
                saveCustomConfigString: PersistStringToUserSettingsAsync,
                saveCustomConfigSection: PersistConfigSectionToUserSettingsAsync,
                gamepad: gamepad,
                externalDebugController: externalDebugController,
                remoteControlController: remoteControlController,
                scriptingEngine: scriptingEngine,
                loadExamples: loadExamples,
                automatedStartupRunner: automatedStartupRunner,
                userSettingsFilePath: userSettingsFilePath,
                scriptingConfig: scriptingConfig,
                appUpdateService: new DesktopAppUpdateService(loggerFactory, emulatorConfig)))
            .UsePlatformDetect()
            .LogToTrace()
            .AfterSetup(_ =>
            {
                // Set up the Avalonia logger bridge to route logs via Avalonia Logger through ILogger
                global::Avalonia.Logging.Logger.Sink = avaloniaLoggerBridge;
            });

    private static Task PersistStringToUserSettingsAsync(string configSectionName, string configSectionJson, string? optionalFileName = null)
    {
        var userSettingsFilePath = string.IsNullOrWhiteSpace(optionalFileName)
            ? AppStoragePaths.GetUserSettingsFilePath("Avalonia.Desktop")
            : Path.Combine(AppStoragePaths.GetUserSettingsDirectory("Avalonia.Desktop"), optionalFileName);
        return AppSettingsUserFile.MergeSectionAsync(userSettingsFilePath, configSectionName, configSectionJson);
    }

    private static Task PersistConfigSectionToUserSettingsAsync(string configSectionName, IConfigurationSection configSection, string? optionalFileName = null)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(ToDictionary(configSection));
        return PersistStringToUserSettingsAsync(configSectionName, json, optionalFileName);
    }

    private static Task CopyBundledExampleScriptsAsync(string scriptDirectory, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(Program));
        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, BundledExampleScriptsDirectoryName);

        if (!Directory.Exists(sourceDirectory))
        {
            logger.LogWarning("Bundled example scripts directory does not exist: {Directory}", sourceDirectory);
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(scriptDirectory);

        var copiedCount = 0;
        var skippedCount = 0;
        foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*.lua", SearchOption.TopDirectoryOnly))
        {
            var destinationFile = Path.Combine(scriptDirectory, Path.GetFileName(sourceFile));
            if (File.Exists(destinationFile))
            {
                skippedCount++;
                continue;
            }

            File.Copy(sourceFile, destinationFile);
            copiedCount++;
        }

        logger.LogInformation(
            "Copied {CopiedCount} bundled example script(s) to {Directory}; skipped {SkippedCount} existing file(s).",
            copiedCount,
            scriptDirectory,
            skippedCount);
        return Task.CompletedTask;
    }

    private static Dictionary<string, object?> ToDictionary(IConfigurationSection section)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in section.GetChildren())
        {
            var children = child.GetChildren().ToList();
            result[child.Key] = children.Count == 0
                ? child.Value
                : ToDictionary(child);
        }

        return result;
    }

    private static int ShowStoragePathsAndExit(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        string userSettingsFilePath,
        ScriptingConfig scriptingConfig)
    {
        var logger = loggerFactory.CreateLogger(nameof(Program));
        var enabledSystems = configuration.GetSection("EnabledSystems").Get<string[]>();
        var enginePlugins = SystemPluginDiscovery
            .Discover<ISystemEnginePlugin>(enabledSystems, logger)
            .ToList();

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new CustomConfigPersistence(PersistStringToUserSettingsAsync));
        foreach (var plugin in enginePlugins)
            plugin.Register(services, configuration);

        var serviceProvider = services.BuildServiceProvider();
        var systemList = new SystemList();
        foreach (var configurer in serviceProvider.GetServices<ISystemConfigurer>())
            systemList.AddSystem(configurer);

#pragma warning disable VSTHRD002
        systemList.RemoveSystemsWithNoConfigurationVariants(logger).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        systemList.EnsureUserContentDirectories(logger);

#pragma warning disable VSTHRD002
        var paths = StoragePathsInfoFactory.CreateAsync(
            "Avalonia.Desktop",
            userSettingsFilePath,
            systemList,
            scriptingConfig).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        Console.Write(StoragePathsInfoFactory.FormatForConsole(paths));
        return 0;
    }
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

    // Windows API to attach the (WinExe, console-less) process to the parent terminal's console,
    // so stdout from the --version / --check-update / --update flags reaches the invoking shell.
    private const uint AttachParentProcess = 0xFFFFFFFF; // ATTACH_PARENT_PROCESS
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint dwProcessId);

    /// <summary>
    /// Handles the update-check CLI flags (<c>--version</c> / <c>--check-update</c> / <c>--update</c>)
    /// before any GUI startup, returning the process exit code — or null if none was passed. The same
    /// shared <see cref="ConsoleUpdateCli"/> the console hosts use. No automatic startup notice: the GUI
    /// is normally launched without an attached terminal. This app ships as a Homebrew cask on macOS but
    /// a formula on Linux (same package name), hence <c>HomebrewIsCask = OperatingSystem.IsMacOS()</c>.
    /// </summary>
    private static int? TryHandleUpdateCli(string[] args)
    {
        if (!ConsoleUpdateCli.WantsHandling(args))
            return null;

        // WinExe has no console attached; attach to the parent (best effort) before writing output.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            AttachConsole(AttachParentProcess);

        // Blocking is safe: this is the process entry point, before any UI/synchronization context exists.
#pragma warning disable VSTHRD002
        return ConsoleUpdateCli.RunAsync(args, DesktopAppUpdateService.CreateDescriptor(), Console.Out).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

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

    /// <summary>
    /// Validates that a supplied load URL (<c>--loadPrgUrl</c> / <c>--loadD64Url</c> / <c>--loadCrtUrl</c>) is an absolute
    /// <c>http</c>/<c>https</c> URL. Null (flag absent) passes. Prints to stderr and returns false on
    /// a malformed or non-HTTP URL so the caller exits 1.
    /// </summary>
    private static bool ValidateAbsoluteHttpUrl(string? url, string argumentName)
    {
        if (url == null)
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Console.Error.WriteLine($"Error: {argumentName} must be an absolute http/https URL.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates the .d64 startup CLI flags (Desktop). Returns false on hard errors (prints to
    /// stderr and the caller exits 1). C64-only knobs supplied without <c>--loadD64</c> are
    /// downgraded to a warning so users can keep them while iterating on a partial command line.
    /// </summary>
    private static bool ValidateD64Arguments(
        string? loadD64Path,
        string? loadD64ZipEntry,
        string? d64Program,
        bool diskMount,
        bool keyboardJoystickEnabled,
        string? keyboardJoystickNumberRaw,
        string? audioEnabledRaw,
        string? systemName,
        bool autoStart,
        bool waitForSystemReady,
        string? loadPrgPath,
        string? loadCrtPath,
        out int keyboardJoystickNumber,
        out bool? audioEnabled)
    {
        keyboardJoystickNumber = 2;
        audioEnabled = null;

        if (keyboardJoystickNumberRaw != null)
        {
            if (!int.TryParse(keyboardJoystickNumberRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                || (parsed != 1 && parsed != 2))
            {
                Console.Error.WriteLine("Error: --keyboardJoystickNumber must be 1 or 2.");
                return false;
            }
            keyboardJoystickNumber = parsed;
        }

        if (audioEnabledRaw != null)
        {
            if (bool.TryParse(audioEnabledRaw, out var parsedAudio))
            {
                audioEnabled = parsedAudio;
            }
            else
            {
                Console.Error.WriteLine("Error: --audioEnabled must be 'true' or 'false'.");
                return false;
            }
        }

        // --d64Program / --diskMount only make sense with --loadD64.
        if (loadD64Path == null && (d64Program != null || diskMount || loadD64ZipEntry != null))
        {
            Console.Error.WriteLine("Warning: --d64Program / --diskMount / --loadD64ZipEntry have no effect without --loadD64/--loadD64Url; ignoring.");
        }

        // --keyboardJoystick* / --audioEnabled are general C64 runtime knobs. They only need
        // --system C64 to apply (they take effect when the C64 starts, regardless of how it was
        // started — plain --start, --loadPrg / --loadPrgUrl, --loadD64 / --loadD64Url, or
        // --loadCrt / --loadCrtUrl).
        var hasRuntimeConfigKnobs = keyboardJoystickEnabled || keyboardJoystickNumberRaw != null || audioEnabledRaw != null;
        if (hasRuntimeConfigKnobs
            && !string.Equals(systemName, "C64", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Warning: --keyboardJoystick* / --audioEnabled require --system C64; ignoring.");
        }

        if (loadD64Path == null)
            return true;

        // --loadD64 is C64-only. Match string-literal style used elsewhere in this file for
        // system selection — Desktop doesn't reference the C64 library directly.
        if (!string.Equals(systemName, "C64", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Error: --loadD64/--loadD64Url requires --system C64.");
            return false;
        }
        if (!autoStart || !waitForSystemReady)
        {
            Console.Error.WriteLine("Error: --loadD64/--loadD64Url requires --start and --waitForSystemReady.");
            return false;
        }
        if (loadPrgPath != null)
        {
            Console.Error.WriteLine("Error: --loadD64/--loadD64Url is mutually exclusive with --loadPrg/--loadPrgUrl.");
            return false;
        }
        if (loadCrtPath != null)
        {
            Console.Error.WriteLine("Error: --loadD64/--loadD64Url is mutually exclusive with --loadCrt/--loadCrtUrl.");
            return false;
        }
        if (d64Program == null && !diskMount)
        {
            Console.Error.WriteLine("Error: --loadD64/--loadD64Url requires exactly one of --d64Program or --diskMount.");
            return false;
        }
        if (d64Program != null && diskMount)
        {
            Console.Error.WriteLine("Error: --d64Program and --diskMount are mutually exclusive.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates the C64 cartridge startup CLI flags (<c>--loadCrt</c> / <c>--loadCrtUrl</c>).
    /// Cartridge attach is a reset/boot-time action, so <c>--waitForSystemReady</c> is allowed but
    /// not required.
    /// </summary>
    private static bool ValidateCrtArguments(
        string? loadCrtPath,
        string? loadCrtZipEntry,
        string? systemName,
        bool autoStart,
        string? effectiveLoadPrg,
        string? effectiveLoadD64,
        bool runLoadedProgram)
    {
        if (loadCrtPath == null)
        {
            if (loadCrtZipEntry != null)
                Console.Error.WriteLine("Warning: --loadCrtZipEntry has no effect without --loadCrt/--loadCrtUrl; ignoring.");
            return true;
        }

        if (!string.Equals(systemName, "C64", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Error: --loadCrt/--loadCrtUrl requires --system C64.");
            return false;
        }
        if (!autoStart)
        {
            Console.Error.WriteLine("Error: --loadCrt/--loadCrtUrl requires --start.");
            return false;
        }
        if (effectiveLoadPrg != null || effectiveLoadD64 != null)
        {
            Console.Error.WriteLine("Error: --loadCrt/--loadCrtUrl is mutually exclusive with --loadPrg/--loadPrgUrl and --loadD64/--loadD64Url.");
            return false;
        }
        if (runLoadedProgram)
        {
            Console.Error.WriteLine("Error: --runLoadedProgram does not apply to --loadCrt/--loadCrtUrl.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates the C64 BASIC-paste CLI flags (<c>--basicText</c> / <c>--basicFile</c> /
    /// <c>--basicUrl</c> / <c>--runBasic</c>) and resolves the inline / file sources into the
    /// base64url value the C64 participant decodes from its <c>basicText</c> extra. Returns false on
    /// a hard error (prints to stderr and the caller exits 1). <paramref name="basicTextEncoded"/>
    /// is set only for the inline / file sources; <c>--basicUrl</c> is forwarded verbatim and fetched
    /// later by the participant.
    /// </summary>
    private static bool ValidateBasicArguments(
        string? basicTextArg,
        string? basicFileArg,
        string? basicUrl,
        bool runBasic,
        string? systemName,
        bool autoStart,
        bool waitForSystemReady,
        string? effectiveLoadPrg,
        string? effectiveLoadD64,
        string? effectiveLoadCrt,
        out string? basicTextEncoded)
    {
        basicTextEncoded = null;

        var sourceCount = (basicTextArg != null ? 1 : 0) + (basicFileArg != null ? 1 : 0) + (basicUrl != null ? 1 : 0);
        var hasBasic = sourceCount > 0;

        if (runBasic && !hasBasic)
        {
            Console.Error.WriteLine("Error: --runBasic requires one of --basicText, --basicFile, or --basicUrl.");
            return false;
        }
        if (!hasBasic)
            return true;

        if (sourceCount > 1)
        {
            Console.Error.WriteLine("Error: --basicText, --basicFile, and --basicUrl are mutually exclusive.");
            return false;
        }
        // BASIC paste uses the C64 keyboard buffer after BASIC reports ready.
        if (!string.Equals(systemName, "C64", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Error: --basicText/--basicFile/--basicUrl require --system C64.");
            return false;
        }
        if (!autoStart || !waitForSystemReady)
        {
            Console.Error.WriteLine("Error: --basicText/--basicFile/--basicUrl require --start and --waitForSystemReady.");
            return false;
        }
        if (effectiveLoadPrg != null || effectiveLoadD64 != null || effectiveLoadCrt != null)
        {
            Console.Error.WriteLine("Error: --basicText/--basicFile/--basicUrl are mutually exclusive with --loadPrg/--loadPrgUrl, --loadD64/--loadD64Url, and --loadCrt/--loadCrtUrl.");
            return false;
        }
        if (!ValidateAbsoluteHttpUrl(basicUrl, "--basicUrl"))
            return false;

        // Resolve the inline text or local file into the base64url 'basicText' extra. The participant
        // always base64url-decodes that extra, so encode here (desktop takes plain text — unlike the
        // browser query parameter, which must be base64url because it travels in a URL).
        string? rawText = null;
        if (basicTextArg != null)
        {
            rawText = basicTextArg;
        }
        else if (basicFileArg != null)
        {
            var expanded = PathHelper.ExpandOSEnvironmentVariables(basicFileArg);
            if (!File.Exists(expanded))
            {
                Console.Error.WriteLine($"Error: --basicFile not found: {expanded}");
                return false;
            }
            rawText = File.ReadAllText(expanded);
        }

        if (rawText != null)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                Console.Error.WriteLine("Error: --basicText/--basicFile content is empty.");
                return false;
            }
            basicTextEncoded = ToBase64Url(rawText);
        }

        return true;
    }

    /// <summary>
    /// Encodes a UTF-8 string as base64url (RFC 4648 §5: '+'→'-', '/'→'_', padding stripped). The
    /// C64 participant accepts unpadded base64url.
    /// </summary>
    private static string ToBase64Url(string text)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>
    /// Build the <see cref="AutomatedStartupRequest.ExtraParameters"/> dictionary the C64 Avalonia
    /// startup participant reads. <c>.d64</c> keys
    /// (<c>loadD64Path</c> or <c>loadD64Url</c>, optional <c>loadD64ZipEntry</c>, plus
    /// <c>d64Program</c>/<c>diskMount</c>) are only
    /// emitted when <c>--loadD64</c> or <c>--loadD64Url</c> is supplied. <c>.crt</c> keys
    /// (<c>loadCrtPath</c> or <c>loadCrtUrl</c>, optional <c>loadCrtZipEntry</c>) are only emitted
    /// when <c>--loadCrt</c> or <c>--loadCrtUrl</c> is supplied. The C64 runtime knobs
    /// (<c>keyboardJoystickEnabled</c>/<c>keyboardJoystickNumber</c>/<c>audioEnabled</c>) are
    /// emitted whenever the user supplied them, since they apply for any C64 start path.
    /// Empty / null entries are skipped so the participant sees only what the user actually supplied.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildC64AutomationExtras(
        string? loadD64Path,
        string? loadD64Url,
        string? loadD64ZipEntry,
        string? loadCrtPath,
        string? loadCrtUrl,
        string? loadCrtZipEntry,
        string? d64Program,
        bool diskMount,
        bool keyboardJoystickEnabled,
        int? keyboardJoystickNumber,
        bool? audioEnabled)
    {
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (loadD64Path != null || loadD64Url != null)
        {
            // The participant resolves the bytes from whichever key is present: 'loadD64Path' reads
            // the local file, 'loadD64Url' downloads via AutomatedStartupContext.FetchBinaryResource.
            if (loadD64Path != null)
                extras["loadD64Path"] = loadD64Path;
            if (loadD64Url != null)
                extras["loadD64Url"] = loadD64Url;
            if (loadD64ZipEntry != null)
                extras["loadD64ZipEntry"] = loadD64ZipEntry;
            if (d64Program != null)
                extras["d64Program"] = d64Program;
            if (diskMount)
                extras["diskMount"] = "true";
        }

        if (loadCrtPath != null || loadCrtUrl != null)
        {
            // The participant resolves the bytes from whichever key is present: 'loadCrtPath' reads
            // the local file, 'loadCrtUrl' downloads via AutomatedStartupContext.FetchBinaryResource.
            if (loadCrtPath != null)
                extras["loadCrtPath"] = loadCrtPath;
            if (loadCrtUrl != null)
                extras["loadCrtUrl"] = loadCrtUrl;
            if (loadCrtZipEntry != null)
                extras["loadCrtZipEntry"] = loadCrtZipEntry;
        }

        if (keyboardJoystickEnabled)
            extras["keyboardJoystickEnabled"] = "true";
        if (keyboardJoystickNumber.HasValue)
            extras["keyboardJoystickNumber"] = keyboardJoystickNumber.Value.ToString(CultureInfo.InvariantCulture);
        if (audioEnabled.HasValue)
            extras["audioEnabled"] = audioEnabled.Value ? "true" : "false";

        return extras;
    }

    private static TimeSpan? ParseDurationSecondsArgument(string[] args, string argumentName)
    {
        var rawValue = AutomatedStartupHandler.ParseStringArgument(args, argumentName);
        if (rawValue == null)
            return null;

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
            return TimeSpan.FromSeconds(seconds);

        Console.Error.WriteLine($"Error: {argumentName} requires a positive number of seconds.");
        return TimeSpan.MinValue;
    }

    private sealed class AutomatedRunController
    {
        private readonly ILogger _logger;
        private readonly TimeSpan? _statsInterval;
        private readonly TimeSpan? _exitAfter;
        private DispatcherTimer? _statsTimer;
        private DispatcherTimer? _exitTimer;
        private HostApp? _hostApp;

        public AutomatedRunController(ILoggerFactory loggerFactory, TimeSpan? statsInterval, TimeSpan? exitAfter)
        {
            _logger = loggerFactory.CreateLogger("AutomatedRunController");
            _statsInterval = statsInterval;
            _exitAfter = exitAfter;
        }

        public void Start(HostApp hostApp)
        {
            _hostApp = hostApp;

            if (_statsInterval.HasValue)
            {
                _logger.LogInformation("Starting periodic instrumentation snapshots every {Seconds:0.##} seconds.", _statsInterval.Value.TotalSeconds);
                _statsTimer = new DispatcherTimer
                {
                    Interval = _statsInterval.Value
                };
                _statsTimer.Tick += (_, _) => LogSnapshot("periodic");
                _statsTimer.Start();
                LogSnapshot("startup");
            }

            if (_exitAfter.HasValue)
            {
                _logger.LogInformation("The emulator will exit automatically after {Seconds:0.##} seconds.", _exitAfter.Value.TotalSeconds);
                _exitTimer = new DispatcherTimer
                {
                    Interval = _exitAfter.Value
                };
                _exitTimer.Tick += OnExitTimerTick;
                _exitTimer.Start();
            }
        }

        private void OnExitTimerTick(object? sender, EventArgs e)
        {
            _exitTimer?.Stop();
            _statsTimer?.Stop();
            if (_statsTimer != null)
                LogSnapshot("final");

            _logger.LogInformation("Exit-after timer elapsed. Quitting application.");
            _hostApp?.QuitApplication();
        }

        private void LogSnapshot(string reason)
        {
            if (_hostApp == null)
                return;

            var visibleStats = _hostApp.GetStats()
                .Where(s => s.stat.ShouldShow())
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .Select(s => $"{s.name}={s.stat.GetDescription()}")
                .ToList();

            if (visibleStats.Count == 0)
            {
                _logger.LogInformation("Instrumentation snapshot ({Reason}): no visible stats yet.", reason);
                return;
            }

            _logger.LogInformation(
                "Instrumentation snapshot ({Reason}){NewLine}{Snapshot}",
                reason,
                Environment.NewLine,
                string.Join(Environment.NewLine, visibleStats));
        }
    }
}
