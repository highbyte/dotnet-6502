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
        // DI: Register the emulator host app
        builder.Services.AddSingleton<IHostApp>((sp) => hostApp);

        // DI: Register MCP server dependencies
        builder.Services.AddSingleton<StateManager>();
        builder.Services.AddSingleton<BreakpointManager>();

        // Add console logging
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            // Configure all console logs to go to stderr to no interfere with with MCP server STDIO communication
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Register MCP server and tools
        var mcpServerBuilder = builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();

        // Register MCP tools
        mcpServerBuilder.WithToolsFromAssembly(typeof(C64StateTool).Assembly);
        // Add additional MCP server tools from the specified assembly
        if (additionalToolsAssembly != null)
            mcpServerBuilder.WithToolsFromAssembly(additionalToolsAssembly);
    }
}
