using Highbyte.DotNet6502.App.SilkNetNative;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;

internal class Program
{
    private static void Main(string[] args)
    {
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
        var systemList = new SystemList<SilkNetRenderContextContainer, SilkNetInputHandlerContext, NAudioAudioHandlerContext>();

        var c64Setup = new C64Setup(loggerFactory);
        systemList.AddSystem(c64Setup);

        var genericComputerSetup = new GenericComputerSetup(loggerFactory);
        systemList.AddSystem(genericComputerSetup);

        // TODO: Read options from appsettings.json
        var emulatorConfig = new EmulatorConfig
        {
            DefaultEmulator = c64Setup.SystemName,
            //DefaultEmulator  = genericComputerSetup.SystemName,
            DefaultDrawScale = 3.0f,
            Monitor = new MonitorConfig
            {
                MaxLineLength = 100,
                //DefaultDirectory = "../../../../../../samples/Assembler/C64/Build"

                //DefaultDirectory = "../../../../../../samples/Assembler/Generic/Build"
                //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
                //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/samples/Assembler/Generic/Build"
            },
        };
        emulatorConfig.Validate(systemList);

        // ----------
        // Silk.NET Window
        // ----------

        var windowWidth = SilkNetHostApp.DEFAULT_WIDTH;
        var windowHeight = SilkNetHostApp.DEFAULT_HEIGHT;

        var windowOptions = WindowOptions.Default;
        // Update frequency, in hertz. 
        windowOptions.UpdatesPerSecond = SilkNetHostApp.DEFAULT_RENDER_HZ;
        // Render frequency, in hertz.
        windowOptions.FramesPerSecond = 60.0f;  // TODO: With Vsync=false the FramesPerSecond settings does not seem to matter. Measured in OnRender method it'll be same as UpdatesPerSecond setting.

        windowOptions.VSync = false;  // TODO: With Vsync=true Silk.NET seem to use incorrect UpdatePerSecond. The actual FPS its called is 10 lower than it should be (measured in the OnUpdate method)
        windowOptions.WindowState = WindowState.Normal;
        windowOptions.Title = "DotNet 6502 emulator hosted in native Silk.NET window using SkiaSharp, OpenGL, and NAudio";
        windowOptions.Size = new Vector2D<int>(windowWidth, windowHeight);
        windowOptions.WindowBorder = WindowBorder.Fixed;
        windowOptions.API = GraphicsAPI.Default; // = Default = OpenGL 3.3 with forward compatibility
        windowOptions.ShouldSwapAutomatically = true;
        //windowOptions.TransparentFramebuffer = false;
        //windowOptions.PreferredDepthBufferBits = 24;    // Depth buffer bits must be set explicitly on MacOS (tested on M1), otherwise there will be be no depth buffer (for OpenGL 3d).

        var window = Window.Create(windowOptions);

        var silkNetHostApp = new SilkNetHostApp(systemList, loggerFactory, emulatorConfig, window, logStore, logConfig);
        silkNetHostApp.Run();
    }
}
