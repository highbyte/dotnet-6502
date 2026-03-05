using System.IO;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Handles automated startup of an emulator host from command-line parameters.
/// Host-app-independent: all Avalonia/UI specifics are supplied by the caller
/// via the <paramref name="uiThreadInvoker"/> delegate.
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
    public static bool ValidateArguments(
        string? systemName,
        string? systemVariant,
        bool autoStart,
        bool waitForSystemReady,
        string? loadPrgPath,
        bool runLoadedProgram)
    {
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
    /// <param name="hostApp">The already-initialized debuggable host app.</param>
    /// <param name="uiThreadInvoker">
    /// Optional delegate that runs an async action on the host app's required thread
    /// (e.g. <c>f =&gt; Dispatcher.UIThread.InvokeAsync(f)</c> for Avalonia).
    /// When <see langword="null"/>, the action is called directly on the current thread.
    /// </param>
    public static async Task ExecuteAsync(
        IDebuggableHostApp hostApp,
        string systemName,
        string? systemVariant,
        bool autoStart,
        bool waitForSystemReady,
        string? loadPrgPath,
        bool runLoadedProgram,
        bool enableExternalDebug,
        Action? onStartupComplete,
        ILoggerFactory loggerFactory,
        Func<Func<Task>, Task>? uiThreadInvoker = null)
    {
        var logger = loggerFactory.CreateLogger(nameof(AutomatedStartupHandler));
        Func<Func<Task>, Task> invoke = uiThreadInvoker ?? (f => f());

        try
        {
            // All HostApp operations run via the provided invoker (e.g. UI thread for Avalonia)
            await invoke(async () =>
            {
                // Select the system
                logger.LogInformation($"Selecting system: {systemName}");
                if (!hostApp.AvailableSystemNames.Contains(systemName))
                {
                    logger.LogError($"System '{systemName}' not found. Available systems: {string.Join(", ", hostApp.AvailableSystemNames)}");
                    Environment.Exit(1);
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
                        Environment.Exit(1);
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

                // Start the system if requested
                if (autoStart)
                {
                    // If external debugger is enabled and no PRG to load, block execution
                    // until the debugger connects. This allows debugging from the very
                    // first CPU instruction (e.g., C64 KERNAL boot sequence).
                    if (enableExternalDebug && loadPrgPath == null)
                    {
                        hostApp.WaitForExternalDebugger = true;
                        logger.LogInformation("WaitForExternalDebugger set: CPU will not execute until debugger connects.");
                    }

                    logger.LogInformation("Starting system...");
                    await hostApp.Start();

                    // Wait for system to be ready if requested
                    if (waitForSystemReady)
                    {
                        logger.LogInformation("Waiting for system to be ready...");
                        await WaitForSystemReady(hostApp, logger);
                    }

                    // Load PRG if specified
                    if (loadPrgPath != null)
                    {
                        logger.LogInformation($"Loading PRG file: {loadPrgPath}");
                        var expandedPrgPath = PathHelper.ExpandOSEnvironmentVariables(loadPrgPath);
                        if (!File.Exists(expandedPrgPath))
                        {
                            logger.LogError($"PRG file not found: {expandedPrgPath}");
                            Environment.Exit(1);
                            return;
                        }

                        var prgBytes = await File.ReadAllBytesAsync(expandedPrgPath);
                        if (prgBytes.Length < 2)
                        {
                            logger.LogError($"PRG file too small (must be at least 2 bytes): {expandedPrgPath}");
                            Environment.Exit(1);
                            return;
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
                }
                else
                {
                    // System not started — nothing to do, TCP server is already listening
                }
            });
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
    private static async Task WaitForSystemReady(IDebuggableHostApp hostApp, ILogger logger)
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