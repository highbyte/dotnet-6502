using System.IO;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Handles automated startup of an emulator host from command-line / URL parameters.
/// Host-app-independent: callers are responsible for invoking <c>ExecuteAsync</c> on the
/// thread the host requires (e.g. UI thread for Avalonia). Optional <c>lifecycleInvoker</c>
/// lets the caller defer the post-selection start lifecycle to a different dispatcher
/// priority (used by Browser/WASM to let the framework finish initial rendering first).
/// External-debugger wiring is supplied via <c>prepareForExternalDebuggerStart</c>.
/// </summary>
public static class AutomatedStartupHandler
{
    /// <summary>
    /// Parses a string argument from command line arguments.
    /// Usage: --argName value
    /// </summary>
    public static string? ParseStringArgument(string[] args, string argumentName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == argumentName)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// Validates automated startup arguments.
    /// </summary>
    /// <param name="hasScripts">
    /// True when <c>--script</c> or <c>--scriptDir</c> was supplied.
    /// Scripts own the full emulator lifecycle, so they are mutually exclusive with
    /// <c>--start</c>, <c>--waitForSystemReady</c>, <c>--loadPrg</c>, and <c>--runLoadedProgram</c>.
    /// </param>
    public static bool ValidateArguments(
        string? systemName,
        string? systemVariant,
        bool autoStart,
        bool waitForSystemReady,
        string? loadPrgPath,
        bool runLoadedProgram,
        bool hasScripts = false)
    {
        // --script / --scriptDir and lifecycle/setup flags are mutually exclusive
        if (hasScripts && (systemName != null || systemVariant != null || autoStart || waitForSystemReady || loadPrgPath != null || runLoadedProgram))
        {
            Console.Error.WriteLine("Error: --script and --scriptDir are mutually exclusive with --system, --systemVariant, --start, --waitForSystemReady, --loadPrg, and --runLoadedProgram. The Lua script is responsible for emulator setup and lifecycle when scripts are used.");
            return false;
        }

        // If no system specified, no other automated args should be present
        if (systemName == null)
        {
            if (systemVariant != null || autoStart || waitForSystemReady || loadPrgPath != null || runLoadedProgram)
            {
                Console.Error.WriteLine("Error: --systemVariant, --start, --waitForSystemReady, --loadPrg, and --runLoadedProgram require --system to be specified.");
                return false;
            }
            return true;
        }

        // --waitForSystemReady requires --start
        if (waitForSystemReady && !autoStart)
        {
            Console.Error.WriteLine("Error: --waitForSystemReady requires --start to be specified.");
            return false;
        }

        // --loadPrg requires --start
        if (loadPrgPath != null && !autoStart)
        {
            Console.Error.WriteLine("Error: --loadPrg requires --start to be specified.");
            return false;
        }

        // --runLoadedProgram requires --start
        if (runLoadedProgram && !autoStart)
        {
            Console.Error.WriteLine("Error: --runLoadedProgram requires --start to be specified.");
            return false;
        }

        // --runLoadedProgram requires --loadPrg
        if (runLoadedProgram && loadPrgPath == null)
        {
            Console.Error.WriteLine("Error: --runLoadedProgram requires --loadPrg to be specified.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Handles automated system startup, load, and run operations.
    /// </summary>
    /// <param name="hostApp">The already-initialized host app.</param>
    /// <param name="request">
    /// The system-agnostic automated-startup parameters (system, variant, start / wait flags, PRG
    /// path, run-loaded, external-debug). When <see cref="AutomatedStartupRequest.EnableExternalDebug"/>
    /// is true the caller has an external debugger active, which affects two branches: (1) before
    /// <c>Start()</c> with no PRG to load, <paramref name="prepareForExternalDebuggerStart"/> is
    /// invoked so the host can block CPU execution until the debugger attaches; and (2) after a PRG
    /// is loaded but not run, the PC is left untouched so the debugger can position it.
    /// </param>
    /// <param name="prepareForExternalDebuggerStart">
    /// Optional callback invoked before <c>hostApp.Start()</c> when
    /// <c>request.EnableExternalDebug</c> is true and no PRG is to be loaded.
    /// Typical implementation: <c>() =&gt; debuggableHostApp.WaitForExternalDebugger = true</c>.
    /// </param>
    /// <param name="onFatalError">
    /// Optional callback invoked when a fatal error is encountered (e.g. unknown system / variant,
    /// missing PRG file). When <see langword="null"/>, the process is terminated with
    /// <c>Environment.Exit(1)</c> (previous default behaviour, preserved for desktop / headless).
    /// Browser hosts should pass a non-null callback (typically a no-op or a UI notification)
    /// to avoid terminating the WASM runtime.
    /// </param>
    /// <param name="lifecycleInvoker">
    /// Optional delegate that wraps the post-selection startup lifecycle (system <c>Start()</c>,
    /// optional <c>waitForSystemReady</c>, optional PRG load, and the <paramref name="onStartupComplete"/>
    /// signal). When <see langword="null"/>, the lifecycle runs inline and is awaited synchronously
    /// — the right behaviour for desktop / headless hosts.
    /// <para>
    /// The Browser host needs to defer the lifecycle so the framework can finish its initial
    /// view rendering before the emulator starts hammering the UI thread. A typical browser
    /// implementation queues the lifecycle on the UI dispatcher at <c>Background</c> priority
    /// (fire-and-forget) and returns <see cref="Task.CompletedTask"/> immediately, e.g.:
    /// <code>
    /// lifecycleInvoker: lifecycle =&gt;
    /// {
    ///     Dispatcher.UIThread.Post(() =&gt; { _ = lifecycle(); }, DispatcherPriority.Background);
    ///     return Task.CompletedTask;
    /// }
    /// </code>
    /// </para>
    /// </param>
    /// <param name="loadPrgBytesProvider">
    /// Optional async provider of PRG bytes. When non-null, takes precedence over
    /// <paramref name="loadPrgPath"/> — the bytes are obtained from this delegate instead of
    /// the local filesystem. Used by the Browser host to fetch a PRG via HTTP. The resulting
    /// byte array must include the 2-byte little-endian load address header (standard PRG format).
    /// </param>
    /// <param name="startupParticipant">
    /// Optional per-system participant resolved by the host (keyed by system name). When non-null,
    /// its <see cref="IAutomatedStartupParticipant.EnsureReadyForStartAsync"/> is invoked after the
    /// system + variant are selected and before <c>Start()</c> (returning <see langword="false"/>
    /// aborts automated startup), and its <see cref="IAutomatedStartupParticipant.OnSystemReadyAsync"/>
    /// is invoked after the system has started. See <c>docs/automated-startup-abstraction.md</c>.
    /// </param>
    /// <param name="startupContext">
    /// Optional host-supplied capabilities forwarded to
    /// <see cref="IAutomatedStartupParticipant.OnSystemReadyAsync"/>. When <see langword="null"/>
    /// an empty context is passed. See <c>docs/automated-startup-seam2.md</c>.
    /// </param>
    /// <remarks>
    /// The caller is responsible for invoking this method on the thread the host app requires
    /// (e.g. Avalonia's UI thread). All host-app interactions before the lifecycle run inline
    /// on that thread.
    /// </remarks>
    public static async Task ExecuteAsync(
        IHostApp hostApp,
        AutomatedStartupRequest request,
        Action? onStartupComplete,
        ILoggerFactory loggerFactory,
        Action? prepareForExternalDebuggerStart = null,
        Action? onFatalError = null,
        Func<Func<Task>, Task>? lifecycleInvoker = null,
        Func<Task<byte[]>>? loadPrgBytesProvider = null,
        IAutomatedStartupParticipant? startupParticipant = null,
        AutomatedStartupContext? startupContext = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(AutomatedStartupHandler));
        Func<Func<Task>, Task> invokeLifecycle = lifecycleInvoker ?? (f => f());
        Action fatalError = onFatalError ?? (() => Environment.Exit(1));

        var (systemName, systemVariant, autoStart, waitForSystemReady,
             loadPrgPath, runLoadedProgram, enableExternalDebug) = request;

        try
        {
            // Select the system
            logger.LogInformation($"Selecting system: {systemName}");
            if (!hostApp.AvailableSystemNames.Contains(systemName))
            {
                logger.LogError($"System '{systemName}' not found. Available systems: {string.Join(", ", hostApp.AvailableSystemNames)}");
                fatalError();
                return;
            }

            await hostApp.SelectSystem(systemName);

            // Select the system variant if specified
            if (systemVariant != null)
            {
                logger.LogInformation($"Selecting system variant: {systemVariant}");
                if (!hostApp.AllSelectedSystemConfigurationVariants.Contains(systemVariant))
                {
                    logger.LogError($"System variant '{systemVariant}' not found for system '{systemName}'. Available variants: {string.Join(", ", hostApp.AllSelectedSystemConfigurationVariants)}");
                    fatalError();
                    return;
                }
                await hostApp.SelectSystemConfigurationVariant(systemVariant);
            }
            else
            {
                // Use first variant
                if (hostApp.AllSelectedSystemConfigurationVariants.Count > 0)
                {
                    var firstVariant = hostApp.AllSelectedSystemConfigurationVariants[0];
                    logger.LogInformation($"Using first available system variant: {firstVariant}");
                    await hostApp.SelectSystemConfigurationVariant(firstVariant);
                }
            }

            // Pre-start gate: when the system will actually be started, give its optional
            // participant a chance to make the configuration valid (e.g. download missing ROMs
            // interactively) before Start(). A false result is a deliberate, graceful abort — not
            // a fatal error — so it calls onFatalError directly (host falls back to its normal UI)
            // rather than fatalError(), whose default terminates the process.
            if (autoStart && startupParticipant is not null)
            {
                if (!await startupParticipant.EnsureReadyForStartAsync(hostApp, request))
                {
                    logger.LogInformation($"Automated startup aborted by participant for system '{systemName}'.");
                    onFatalError?.Invoke();
                    return;
                }
            }

            // Start the system if requested. The lifecycle (Start + post-Start work) is
            // dispatched via invokeLifecycle so hosts that need to defer the lifecycle to a
            // lower dispatcher priority (e.g. Browser/WASM) can do so without coupling the
            // handler to any UI framework. Default invoker is direct-await.
            if (autoStart)
            {
                await invokeLifecycle(async () =>
                {
                    // If external debugger is enabled and no PRG to load, block execution
                    // until the debugger connects. This allows debugging from the very
                    // first CPU instruction (e.g., C64 KERNAL boot sequence).
                    if (enableExternalDebug && loadPrgPath == null)
                    {
                        if (prepareForExternalDebuggerStart != null)
                        {
                            prepareForExternalDebuggerStart();
                            logger.LogInformation("WaitForExternalDebugger set: CPU will not execute until debugger connects.");
                        }
                        else
                        {
                            logger.LogWarning("enableExternalDebug=true but no prepareForExternalDebuggerStart callback provided; CPU will not block on debugger.");
                        }
                    }

                    logger.LogInformation("Starting system...");
                    await hostApp.Start();

                    // Wait for system to be ready if requested
                    if (waitForSystemReady)
                    {
                        logger.LogInformation("Waiting for system to be ready...");
                        await WaitForSystemReadyAsync(hostApp, logger);
                    }

                    // Load PRG if specified — either via async provider (Browser fetches over HTTP)
                    // or local filesystem path (Desktop / Headless). Provider wins if both are set.
                    if (loadPrgBytesProvider != null || loadPrgPath != null)
                    {
                        byte[] prgBytes;
                        if (loadPrgBytesProvider != null)
                        {
                            logger.LogInformation("Loading PRG bytes via provider");
                            try
                            {
                                prgBytes = await loadPrgBytesProvider();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "PRG bytes provider threw while fetching");
                                fatalError();
                                return;
                            }
                            if (prgBytes.Length < 2)
                            {
                                logger.LogError($"PRG content too small (must be at least 2 bytes): got {prgBytes.Length}");
                                fatalError();
                                return;
                            }
                        }
                        else
                        {
                            logger.LogInformation($"Loading PRG file: {loadPrgPath}");
                            var expandedPrgPath = PathHelper.ExpandOSEnvironmentVariables(loadPrgPath!);
                            if (!File.Exists(expandedPrgPath))
                            {
                                logger.LogError($"PRG file not found: {expandedPrgPath}");
                                fatalError();
                                return;
                            }

                            prgBytes = await File.ReadAllBytesAsync(expandedPrgPath);
                            if (prgBytes.Length < 2)
                            {
                                logger.LogError($"PRG file too small (must be at least 2 bytes): {expandedPrgPath}");
                                fatalError();
                                return;
                            }
                        }

                        // Read load address (first two bytes, little-endian)
                        ushort loadAddress = (ushort)(prgBytes[0] | (prgBytes[1] << 8));
                        logger.LogInformation($"PRG load address: 0x{loadAddress:X4}");

                        // Load into memory
                        var mem = hostApp.CurrentRunningSystem!.Mem;
                        for (int i = 2; i < prgBytes.Length; i++)
                        {
                            mem[(ushort)(loadAddress + (ushort)(i - 2))] = prgBytes[i];
                        }

                        logger.LogInformation($"Loaded {prgBytes.Length - 2} bytes at 0x{loadAddress:X4}");

                        logger.LogInformation("Automated startup complete.");

                        // Signal debug adapters BEFORE setting CPU.PC.
                        // When stopOnEntry is pending, NotifyProgramReady() sets IsStopped=true
                        // synchronously so the run loop pauses BEFORE the PC is redirected to
                        // the program start — preventing any program instructions from executing.
                        onStartupComplete?.Invoke();

                        // Run the loaded program if requested
                        if (runLoadedProgram)
                        {
                            logger.LogInformation($"Setting PC to 0x{loadAddress:X4} to run loaded program");
                            hostApp.CurrentRunningSystem.CPU.PC = loadAddress;
                        }
                        else if (!enableExternalDebug)
                        {
                            // No debugger — set PC to load address so execution starts at the program
                            logger.LogInformation($"Setting PC to 0x{loadAddress:X4} (no external debugger)");
                            hostApp.CurrentRunningSystem.CPU.PC = loadAddress;
                        }
                    }
                    else
                    {
                        // No PRG to load (KERNAL boot debugging or plain emulator start).
                        // Signal adapters now — KERNAL is ready and no PC redirect is needed.
                        logger.LogInformation("Automated startup complete.");
                        onStartupComplete?.Invoke();
                    }

                    // Post-ready hook: after the system is started (and, if requested, reported
                    // ready) let the participant run a system-specific action — e.g. the C64
                    // participant pastes BASIC source. See docs/automated-startup-seam2.md.
                    if (startupParticipant is not null)
                        await startupParticipant.OnSystemReadyAsync(
                            hostApp, request, startupContext ?? new AutomatedStartupContext());
                });
            }
            else
            {
                // System not started — nothing to do, TCP server is already listening
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during automated startup");
        }
    }

    /// <summary>
    /// Waits for the system to be ready.
    /// If the running system implements <see cref="ISystemState"/>, polls
    /// <see cref="ISystemState.IsSystemReady"/> until it returns true or the
    /// timeout expires. Otherwise falls back to a fixed delay.
    /// </summary>
    private static async Task WaitForSystemReadyAsync(IHostApp hostApp, ILogger logger)
    {
        if (hostApp.CurrentRunningSystem is ISystemState systemState)
        {
            const int maxWaitMs = 30_000;
            const int pollIntervalMs = 100;
            var elapsed = 0;
            logger.LogInformation("Waiting for system to be ready (polling ISystemState.IsSystemReady)...");
            while (!systemState.IsSystemReady())
            {
                if (elapsed >= maxWaitMs)
                {
                    logger.LogWarning("Timed out after {MaxWaitMs}ms waiting for system to be ready.", maxWaitMs);
                    return;
                }
                await Task.Delay(pollIntervalMs);
                elapsed += pollIntervalMs;
            }
            logger.LogInformation("System is ready.");
        }
        else
        {
            const int delayMs = 3000;
            logger.LogInformation("System does not implement ISystemState. Waiting 3000ms as fallback...");
            await Task.Delay(delayMs);
            logger.LogInformation("System should be ready.");
        }
    }
}
