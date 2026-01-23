using StreamJsonRpc;

namespace Highbyte.DotNet6502.DebugAdapter;

class Program
{
    static async Task Main(string[] args)
    {
        // Debug adapter protocol communication happens over stdin/stdout
        var adapter = new DebugAdapter6502();
        
        var jsonRpc = JsonRpc.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput(), adapter);
        adapter.SetRpc(jsonRpc);
        
        // Wait for the RPC connection to terminate
        await jsonRpc.Completion;
    }
}
