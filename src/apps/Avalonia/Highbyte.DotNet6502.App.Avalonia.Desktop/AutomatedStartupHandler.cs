using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Desktop;

/// <summary>
/// Handles automated startup of the emulator from command-line parameters.
/// </summary>
internal static class AutomatedStartupHandler
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
    public static async Task ExecuteAsync(
        string systemName,
        string? systemVariant,
        bool autoStart,
        bool waitForSystemReady,
        string? loadPrgPath,
        bool runLoadedProgram,
        TcpDebugServerManager? debugServerManager,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(AutomatedStartupHandler));

        try
        {
            // Wait for the Avalonia app to be fully initialized
            logger.LogInformation("Waiting for Avalonia app to initialize...");
            while (Core.App.Current?.HostApp == null)
            {
                await Task.Delay(100);
            }

            var hostApp = Core.App.Current.HostApp;
            logger.LogInformation("Avalonia app initialized.");

            // Set flag to skip default system selection in UI initialization
            hostApp.SkipDefaultSystemSelection = true;

            // All HostApp operations must run on the UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
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
                    if (debugServerManager != null && loadPrgPath == null)
                    {
                        hostApp.WaitForExternalDebugger = true;
                        logger.LogInformation("WaitForExternalDebugger set: CPU will not execute until debugger connects.");
                    }

                    logger.LogInformation("Starting system...");
                    await hostApp.Start();

                    // If no PRG to load, signal the debug server immediately after start.
                    // The system exists but WaitForExternalDebugger prevents execution.
                    if (loadPrgPath == null)
                    {
                        logger.LogInformation("No PRG to load, signaling debug server (system created, waiting for debugger).");
                        debugServerManager?.SignalAutomatedStartupComplete(hostApp);
                    }

                    // Wait for system to be ready if requested
                    if (waitForSystemReady)
                    {
                        logger.LogInformation("Waiting for system to be ready...");
                        await WaitForSystemReady(hostApp, systemName, logger);
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

                        // Run the loaded program if requested
                        if (runLoadedProgram)
                        {
                            logger.LogInformation($"Setting PC to 0x{loadAddress:X4} to run loaded program");
                            hostApp.CurrentRunningSystem.CPU.PC = loadAddress;
                        }
                        else if (debugServerManager == null)
                        {
                            // No debugger, just set PC but don't start running
                            logger.LogInformation($"Setting PC to 0x{loadAddress:X4} (system paused)");
                            hostApp.CurrentRunningSystem.CPU.PC = loadAddress;
                        }

                        // Signal debug server after PRG is loaded and ready.
                        // This ensures the debugger doesn't pause the emulator before
                        // the program bytes are in memory.
                        debugServerManager?.SignalAutomatedStartupComplete(hostApp);
                    }
                }
                else
                {
                    // System not started, but signal debug server so it can accept connections
                    debugServerManager?.SignalAutomatedStartupComplete(hostApp);
                }

                logger.LogInformation("Automated startup complete.");
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during automated startup");
        }
    }

    /// <summary>
    /// Waits for the system to be ready.
    /// For C64, waits until Basic prompt appears (simplified: wait 3 seconds).
    /// </summary>
    private static async Task WaitForSystemReady(AvaloniaHostApp hostApp, string systemName, ILogger logger)
    {
        // TODO: Implement proper system-ready detection based on system type
        // For C64, could check for Basic prompt in screen memory
        // For now, use a simple delay
        const int delayMs = 3000;
        logger.LogInformation($"Waiting {delayMs}ms for system to be ready...");
        await Task.Delay(delayMs);
        logger.LogInformation("System should be ready.");
    }
}
