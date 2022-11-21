using Highbyte.DotNet6502.App.SkiaNative;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Generic;

// Fix for starting in debug mode from VS Code. By default the OS current directory is set to the project folder, not the folder containing the built .exe file...
var currentAppDir = AppDomain.CurrentDomain.BaseDirectory;
Environment.CurrentDirectory = currentAppDir;

// TODO: Read options from appsettings.json
var emulatorConfig = new EmulatorConfig
{
    Emulator = "C64",
    //Emulator = "Generic",
    DrawScale = 3.0f,
    Monitor = new MonitorConfig
    {
        //DefaultDirectory = "../../../../../.cache/Examples/Assembler/C64"

        //DefaultDirectory = "../../../../../.cache/Examples/Assembler/Generic"
        //DefaultDirectory = "%USERPROFILE%/source/repos/dotnet-6502/.cache/Examples/Assembler/Generic"
        //DefaultDirectory = "%HOME%/source/repos/dotnet-6502/.cache/Examples/Assembler/Generic"
    }
};
emulatorConfig.Validate();

var c64Config = new C64Config
{
    C64Model = "C64NTSC",   // C64NTSC, C64PAL
    Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
    // C64Model = "C64PAL",   // C64NTSC, C64PAL
    // Vic2Model = "PAL",     // NTSC, NTSC_old, PAL
    //ROMDirectory = "%USERPROFILE%/Documents/C64/VICE/C64",
    ROMDirectory = "%HOME%/Downloads/C64",
    ROMs = new List<ROM>
    {
        new ROM
        {
            Name = C64Config.BASIC_ROM_NAME,
            File = "basic.901226-01.bin",
            Data = null,
            Checksum = "79015323128650c742a3694c9429aa91f355905e",
        },
        new ROM
        {
            Name = C64Config.CHARGEN_ROM_NAME,
            File = "characters.901225-01.bin",
            Data = null,
            Checksum = "adc7c31e18c7c7413d54802ef2f4193da14711aa",
        },
        new ROM
        {
            Name = C64Config.KERNAL_ROM_NAME,
            File = "kernal.901227-03.bin",
            Data = null,
            Checksum = "1d503e56df85a62fee696e7618dc5b4e781df1bb",
        }
    }
};
c64Config.Validate();

var genericComputerConfig = new GenericComputerConfig
{
    //ProgramBinaryFile = "../../../../../.cache/Examples/Assembler/Generic/hostinteraction_scroll_text_and_cycle_colors.prg",
    ProgramBinaryFile = "%HOME%/source/repos/dotnet-6502/.cache/Examples/Assembler/Generic/hostinteraction_scroll_text_and_cycle_colors.prg",
    CPUCyclesPerFrame = 8000,
    Memory = new EmulatorMemoryConfig
    {
        Screen = new EmulatorScreenConfig
        {
            Cols = 40,
            Rows = 25,
            BorderCols = 3,
            BorderRows = 3,
            UseAscIICharacters = true,
            DefaultBgColor = 0x00,     // 0x00 = Black (C64 scheme)
            DefaultFgColor = 0x01,     // 0x0f = Light grey, 0x0e = Light Blue, 0x01 = White  (C64 scheme)
            DefaultBorderColor = 0x0b, // 0x0b = Dark grey (C64 scheme)
        },
        Input = new EmulatorInputConfig
        {
            KeyPressedAddress = 0xd030,
            KeyDownAddress = 0xd031,
            KeyReleasedAddress = 0xd031,
        }
    }
};
genericComputerConfig.Validate();

// ----------
// Systems
// ----------
var systemList = new SystemList();
systemList.BuildSystemLookups(c64Config, genericComputerConfig);

var system = systemList.Systems[emulatorConfig.Emulator];

// ----------
// Silk.NET Window
// ----------
var screen = (IScreen)system;

int windowWidth = (int)(screen.VisibleWidth * emulatorConfig.DrawScale);
int windowHeight = (int)(screen.VisibleHeight * emulatorConfig.DrawScale);

var windowOptions = WindowOptions.Default;
// Update frequency, in hertz. 
windowOptions.UpdatesPerSecond = screen.RefreshFrequencyHz;
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
var silkNetWindow = new SilkNetWindow(emulatorConfig.Monitor, window, system, GetSystemRunner, emulatorConfig.DrawScale);
silkNetWindow.Run();

// Functions for building SystemRunner based on Skia rendering.
// Will be used as from SilkNetWindow in OnLoad (when OpenGL context has been created.)
SystemRunner GetSystemRunner(ISystem system, SkiaRenderContext skiaRenderContext, SilkNetInputHandlerContext silkNetInputHandlerContext)
{
    if (system is C64 c64)
    {
        var renderer = (IRenderer<C64, SkiaRenderContext>)systemList.Renderers[c64];
        renderer.Init(system, skiaRenderContext);

        var inputHandler = (IInputHandler<C64, SilkNetInputHandlerContext>)systemList.InputHandlers[c64];
        inputHandler.Init(system, silkNetInputHandlerContext);

        var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, SilkNetInputHandlerContext>(c64);
        var systemRunner = systemRunnerBuilder
            .WithRenderer(renderer)
            .WithInputHandler(inputHandler)
            .Build();
        return systemRunner;
    }

    if (system is GenericComputer genericComputer)
    {
        var renderer = (IRenderer<GenericComputer, SkiaRenderContext>)systemList.Renderers[genericComputer];
        renderer.Init(system, skiaRenderContext);

        var inputHandler = (IInputHandler<GenericComputer, SilkNetInputHandlerContext>)systemList.InputHandlers[genericComputer];
        inputHandler.Init(system, silkNetInputHandlerContext);

        var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, SkiaRenderContext, SilkNetInputHandlerContext>(genericComputer);
        var systemRunner = systemRunnerBuilder
            .WithRenderer(renderer)
            .WithInputHandler(inputHandler)
            .Build();
        return systemRunner;
    }

    throw new NotImplementedException($"System not handled: {system.Name}");
}
