using System.Text.Json.Nodes;

namespace Highbyte.DotNet6502.DebugAdapter;

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
            var protocol = new DapProtocol(Console.OpenStandardInput(), Console.OpenStandardOutput(), log);
            var adapter = new DebugAdapterLogic(protocol, log);
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
