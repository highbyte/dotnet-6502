using System.ComponentModel;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Util.MCPServer;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Highbyte.DotNet6502.App.SadConsole.MCP;

[McpServerToolType]
public static class C64SadConsoleTools
{
    [McpServerTool, Description("Get C64 emulator log messages")]
    public static async Task<CallToolResult> GetLogMessages(IHostApp hostApp, int numberOfLogMessages)
    {
        try
        {
            List<string> logs = null!;
            await hostApp.ExternalControlInvokeOnUIThread(async () =>
            {
                C64ToolHelper.AssertC64EmulatorIsRunningOrPaused(hostApp);
                var logStore = ((SadConsoleHostApp)hostApp).LogStore;
                logs = logStore.GetLogMessages().Take(numberOfLogMessages).ToList();
            });

            return C64ToolHelper.BuildCallToolDataResult(logs);

        }
        catch (Exception ex)
        {
            return C64ToolHelper.BuildCallToolErrorResult(ex);
        }
    }
}
