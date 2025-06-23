using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Util.MCPServer;
public static class ToolSetup
{
    public static void ConfigureDotNet6502McpServerTools(this IHostApplicationBuilder builder, IHostApp hostApp)
    {
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Configure all console logs to go to stderr to no interfere with with MCP server STDIO communication
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(C64StateTool).Assembly);

        builder.Services.AddSingleton<IHostApp>((sp) => hostApp);
    }
}
