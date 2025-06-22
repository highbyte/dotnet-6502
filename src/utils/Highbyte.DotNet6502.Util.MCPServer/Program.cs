using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Util.MCPServer.Emulator;
using Highbyte.DotNet6502.Util.MCPServer.Emulator.SystemSetup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// ----------
// Register the emulator EmbeddedMCPHostApp as singleton service
// ----------
var configuration = builder.Configuration;

builder.Services.AddSingleton<IHostApp>((sp) =>
{
    // ----------
    // Create emulator logging
    // ----------
    DotNet6502InMemLogStore logStore = new() { WriteDebugMessage = true };
    var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
    var loggerFactory = LoggerFactory.Create(builder =>
    {
        logConfig.LogLevel = LogLevel.Information;  // LogLevel.Debug, LogLevel.Information, 
        builder.AddInMem(logConfig);
        builder.SetMinimumLevel(LogLevel.Trace);
    });

    // ----------
    // Get emulator host config
    // ----------
    var emulatorConfig = new EmulatorConfig();
    configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

    // ----------
    // Get systems
    // ----------
    var systemList = new SystemList<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>();
    var c64Setup = new C64Setup(loggerFactory, configuration);
    systemList.AddSystem(c64Setup);

    // ----------
    // Create & init emulator host app
    // ----------
    var embeddedMCPHostApp = new EmbeddedMCPHostApp(systemList, loggerFactory, emulatorConfig, logStore, logConfig, configuration);
    embeddedMCPHostApp.Init();
    embeddedMCPHostApp.SelectSystem(C64.SystemName).Wait();

    return embeddedMCPHostApp;
});

await builder.Build().RunAsync();
