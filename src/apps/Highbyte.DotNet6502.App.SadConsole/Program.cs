using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.App.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Logging.InMem;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;

// Get config file
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json");
IConfiguration Configuration = builder.Build();

// Create logging
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

//var emulatorConfig = new EmulatorConfig
//{
//    DefaultEmulator = c64Setup.SystemName,
//    UIFont = null,
//    FontScale = 1,
//    Font = "Fonts/C64.font",
//    WindowTitle = "SadConsole with Highbyte.DotNet6502 emulator!",
//    //Monitor = new MonitorConfig
//    //{
//    //    MaxLineLength = 100,
//    //},
//};

var hostSystemConfigs = new Dictionary<string, IHostSystemConfig>
{
    { C64.SystemName, emulatorConfig.C64HostConfig },
    { GenericComputer.SystemName, emulatorConfig.GenericComputerHostConfig}
};

// ----------
// Get systems
// ----------
var systemList = new SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NullAudioHandlerContext>();

var c64HostConfig = new C64HostConfig { };
var c64Setup = new C64Setup(loggerFactory, Configuration, c64HostConfig);
systemList.AddSystem(c64Setup);

var genericComputerHostConfig = new GenericComputerHostConfig { };
var genericComputerSetup = new GenericComputerSetup(loggerFactory, Configuration, genericComputerHostConfig);
systemList.AddSystem(genericComputerSetup);

// ----------
// Start emulator host app
// ----------
emulatorConfig.Validate(systemList);

var silkNetHostApp = new SadConsoleHostApp(systemList, loggerFactory, emulatorConfig, hostSystemConfigs, logStore, logConfig);
silkNetHostApp.Run();
