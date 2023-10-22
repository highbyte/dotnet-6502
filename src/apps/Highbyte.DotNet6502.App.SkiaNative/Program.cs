using AutoMapper;
using Highbyte.DotNet6502.App.SkiaNative;
using Highbyte.DotNet6502.App.SkiaNative.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Logging.InMem;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Generic;
using Microsoft.Extensions.Logging;

// Fix for starting in debug mode from VS Code. By default the OS current directory is set to the project folder, not the folder containing the built .exe file...
var currentAppDir = AppDomain.CurrentDomain.BaseDirectory;
Environment.CurrentDirectory = currentAppDir;

DotNet6502InMemLogStore logStore = new();
var logConfig = new DotNet6502InMemLoggerConfiguration(logStore);
var loggerFactory = LoggerFactory.Create(builder =>
{
    logConfig.LogLevel = LogLevel.Information;
    builder.AddInMem(logConfig);
    builder.SetMinimumLevel(LogLevel.Trace);
});

// ----------
// Systems
// ----------
var systemList = new SystemList<SkiaRenderContext, SilkNetInputHandlerContext, NAudioAudioHandlerContext>();

var c64HostConfig = new C64HostConfig();
var c64Setup = new C64Setup(loggerFactory, c64HostConfig);
await systemList.AddSystem(c64Setup);

var genericComputerSetup = new GenericComputerSetup(loggerFactory);
await systemList.AddSystem(genericComputerSetup);

// TODO: Read options from appsettings.json
var emulatorConfig = new EmulatorConfig
{
    DefaultEmulator = c64Setup.SystemName,
    //DefaultEmulator  = genericComputerSetup.SystemName,
    DefaultDrawScale = 3.0f,
    Monitor = new MonitorConfig
    {
        //DefaultDirectory = "../../../../../../samples/Assembler/C64/Build"

        //DefaultDirectory = "../../../../../../samples/Assembler/Generic/Build"
        //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
        //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
    },
    HostSystemConfigs = new Dictionary<string, IHostSystemConfig>
    {
        { C64.SystemName, c64HostConfig }
        //{ GenericComputer.SystemName, new GenericComputerHostConfig() }
    }
};
emulatorConfig.Validate(systemList);

// TODO: Make Automapper configuration more generic, incorporate in classes that need it?
var mapperConfiguration = new MapperConfiguration(
    cfg =>
    {
        cfg.CreateMap<C64HostConfig, C64HostConfig>();
    }
);
var mapper = mapperConfiguration.CreateMapper();

// ----------
// Silk.NET Window
// ----------

int windowWidth = SilkNetWindow.DEFAULT_WIDTH;
int windowHeight = SilkNetWindow.DEFAULT_HEIGHT;

var windowOptions = WindowOptions.Default;
// Update frequency, in hertz. 
windowOptions.UpdatesPerSecond = SilkNetWindow.DEFAULT_RENDER_HZ;
// Render frequency, in hertz.
windowOptions.FramesPerSecond = 60.0f;  // TODO: With Vsync=false the FramesPerSecond settings does not seem to matter. Measured in OnRender method it'll be same as UpdatesPerSecond setting.

windowOptions.VSync = false;  // TODO: With Vsync=true Silk.NET seem to use incorrect UpdatePerSecond. The actual FPS its called is 10 lower than it should be (measured in the OnUpdate method)
windowOptions.WindowState = WindowState.Normal;
windowOptions.Title = "DotNet 6502 emulator hosted in native app using SkiaSharp drawing, with OpenGL context provided by Silk.NET.";
windowOptions.Size = new Vector2D<int>(windowWidth, windowHeight);
windowOptions.WindowBorder = WindowBorder.Fixed;
windowOptions.API = GraphicsAPI.Default; // = Default = OpenGL 3.3 with forward compatibility
windowOptions.ShouldSwapAutomatically = true;
//windowOptions.TransparentFramebuffer = false;
//windowOptions.PreferredDepthBufferBits = 24;    // Depth buffer bits must be set explicitly on MacOS (tested on M1), otherwise there will be be no depth buffer (for OpenGL 3d).

IWindow window = Window.Create(windowOptions);
var silkNetWindow = new SilkNetWindow(emulatorConfig, window, systemList, logStore, logConfig, loggerFactory, mapper);
silkNetWindow.Run();
