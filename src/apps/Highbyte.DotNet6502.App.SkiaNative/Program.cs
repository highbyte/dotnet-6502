using Highbyte.DotNet6502.App.SkiaNative;
using Highbyte.DotNet6502.App.SkiaNative.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
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

var c64Setup = new C64Setup(loggerFactory);
await systemList.AddSystem(C64.SystemName, c64Setup.BuildSystem, c64Setup.BuildSystemRunner, c64Setup.GetNewConfig, c64Setup.PersistConfig);

var genericComputerSetup = new GenericComputerSetup(loggerFactory);
await systemList.AddSystem(GenericComputer.SystemName, genericComputerSetup.BuildSystem, genericComputerSetup.BuildSystemRunner, genericComputerSetup.GetNewConfig, genericComputerSetup.PersistConfig);

// TODO: Read options from appsettings.json
var emulatorConfig = new EmulatorConfig
{
    DefaultEmulator = "C64",
    //DefaultEmulator  = "Generic",
    DefaultDrawScale = 3.0f,
    Monitor = new MonitorConfig
    {
        //DefaultDirectory = "../../../../../../samples/Assembler/C64/Build"

        //DefaultDirectory = "../../../../../../samples/Assembler/Generic/Build"
        //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
        //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
    }
};
emulatorConfig.Validate(systemList);

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
var silkNetWindow = new SilkNetWindow(emulatorConfig.Monitor, window, systemList, emulatorConfig.DefaultDrawScale, emulatorConfig.DefaultEmulator, logStore, logConfig, loggerFactory);
silkNetWindow.Run();
