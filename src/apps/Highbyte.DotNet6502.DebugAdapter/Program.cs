
using Highbyte.DotNet6502.Systems.Generic;

namespace Highbyte.DotNet6502.DebugAdapter.ConsoleApp;

class Program
{
    private static bool _shouldExit = false;

    static async Task Main(string[] args)
    {
        var logFile = Path.Combine(Path.GetTempPath(), "dotnet6502-debugadapter.log");
        using var log = new StreamWriter(logFile, append: true);
        log.AutoFlush = true;

        log.WriteLine($"[{DateTime.Now:HH:mm:ss}] Debug adapter starting...");

        try
        {
            // Create STDIO transport
            var transport = new StdioTransport(Console.OpenStandardInput(), Console.OpenStandardOutput(), log);
            transport.Disconnected += (sender, e) => _shouldExit = true;

            // Use a generic computer as the system being debugged. It will have empty memory at start, and no ROM or program loaded.
            var system = new GenericComputer();

            var protocol = new DapProtocol(transport, log);
            var adapter = new DebugAdapterLogic(protocol, log, system);
            adapter.OnExit += () => _shouldExit = true;

            log.WriteLine("[Main] Protocol initialized, entering message loop...");

            while (!_shouldExit)
            {
                var message = await protocol.ReadMessageAsync();
                if (message == null)
                {
                    log.WriteLine("[Main] Received null message, exiting");
                    break;
                }

                await adapter.HandleMessageAsync(message);
            }

            log.WriteLine("[Main] Exiting normally");
        }
        catch (Exception ex)
        {
            log.WriteLine($"[Main] EXCEPTION: {ex}");
        }
    }
}
