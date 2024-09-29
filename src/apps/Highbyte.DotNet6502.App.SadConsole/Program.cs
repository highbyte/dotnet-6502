using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.App.SadConsole;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
// Start SadConsoleHostApp
// ----------
emulatorConfig.Validate(systemList);

var silkNetHostApp = new SadConsoleHostApp(systemList, loggerFactory, emulatorConfig, logStore, logConfig, Configuration);
silkNetHostApp.Run();
