using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.App.SadConsole;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Systems.Logging.InMem;

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
//    FontSize = Sizes.One,
//    Font = "Fonts/C64.font",
//    WindowTitle = "SadConsole with Highbyte.DotNet6502 emulator!",
//    //Monitor = new MonitorConfig
//    //{
//    //    MaxLineLength = 100,
//    //},
//};

// ----------
// Get systems
// ----------
var systemList = new SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext>();

var c64Setup = new C64Setup(loggerFactory, Configuration);
systemList.AddSystem(c64Setup);

var genericComputerSetup = new GenericComputerSetup(loggerFactory, Configuration);
systemList.AddSystem(genericComputerSetup);

// ----------
// Start emulator host app
// ----------
emulatorConfig.Validate(systemList);

var silkNetHostApp = new SadConsoleHostApp(systemList, loggerFactory, emulatorConfig, logStore, logConfig);
silkNetHostApp.Run();
