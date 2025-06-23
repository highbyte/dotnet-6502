using System.Reflection;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Util.MCPServer;

public static class ToolSetup
{
    public static void ConfigureDotNet6502McpServerTools(this IHostApplicationBuilder builder, IHostApp hostApp, Assembly? additionalToolsAssembly = null)
    {
        // Add MCP server tools from the specified assembly
        builder.Services.AddMcpServer()
            .WithToolsFromAssembly(additionalToolsAssembly);

        // Add the host app as a singleton service
        builder.Services.AddSingleton<IHostApp>((sp) => hostApp);
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Configure all console logs to go to stderr to no interfere with with MCP server STDIO communication
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        var mcpServerBuilder = builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();
        mcpServerBuilder.WithToolsFromAssembly(typeof(C64StateTool).Assembly);
        if (additionalToolsAssembly != null)
        {
            mcpServerBuilder.WithToolsFromAssembly(additionalToolsAssembly);
        }

        builder.Services.AddSingleton<IHostApp>((sp) => hostApp);
    }
}
