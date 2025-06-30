using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.App.SadConsole;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Highbyte.DotNet6502.Util.MCPServer;

// ----------
// Get config file
// ----------
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var devEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT ");
var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";
if (isDevelopment) //only add secrets in development
{
    builder.AddUserSecrets<Program>();
}

IConfiguration Configuration = builder.Build();

// ----------
// Create logging
// ----------
DotNet6502InMemLogStore logStore = new() { WriteDebugMessage = true };
var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
logConfig.LogLevel = LogLevel.Information;  // LogLevel.Debug, LogLevel.Information
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddInMem(logConfig);
    builder.SetMinimumLevel(LogLevel.Trace);
});

// ----------
// Get emulator host config
// ----------
var emulatorConfig = new EmulatorConfig();
Configuration.GetSection(EmulatorConfig.ConfigSectionName).Bind(emulatorConfig);

// ----------
// Get systems
// ----------
var systemList = new SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext>();

var c64Setup = new C64Setup(loggerFactory, Configuration);
systemList.AddSystem(c64Setup);

var genericComputerSetup = new GenericComputerSetup(loggerFactory, Configuration);
systemList.AddSystem(genericComputerSetup);


// ----------
// Init SadConsoleHostApp
// ----------
emulatorConfig.Validate(systemList);
var sadConsoleHostApp = new SadConsoleHostApp(systemList, loggerFactory, emulatorConfig, logStore, logConfig, Configuration);

// ----------
// Start MCP server as a background host if enabled
// ----------
if (emulatorConfig.MCPServerEnabled)
{
    Task.Run(async () =>
    {
        var mcpBuilder = Host.CreateApplicationBuilder();
        mcpBuilder.ConfigureDotNet6502McpServerTools(sadConsoleHostApp,
            additionalToolsAssembly: typeof(Highbyte.DotNet6502.App.SadConsole.MCP.C64SadConsoleTools).Assembly);
        await mcpBuilder.Build().RunAsync();
    });
}

// ----------
// Start SadConsoleHostApp
// ----------
sadConsoleHostApp.Run();
