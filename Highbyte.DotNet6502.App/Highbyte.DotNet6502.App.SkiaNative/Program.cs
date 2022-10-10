using Silk.NET.Maths;
using Silk.NET.Windowing;
using Highbyte.DotNet6502.App.SkiaNative;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Monitor;

// Fix for starting in debug mode from VS Code. By default the OS current directory is set to the project folder, not the folder containing the built .exe file...
var currentAppDir = AppDomain.CurrentDomain.BaseDirectory;
Environment.CurrentDirectory = currentAppDir;

// ----------
// Systems
// ----------
Dictionary<string, (ISystem System, Func<SkiaRenderContext, SilkNetInputHandlerContext, SystemRunner> SystemRunnerBuilder)> SystemsList = new()
{
    {"Commodore 64", (C64.BuildC64(), GetC64SystemRunner)}
};

// TODO: Read options from appsettings.json
var options = new EmulatorOptions
{
    SystemName = "Commodore 64",
    Monitor = new MonitorOptions
    {
        //DefaultDirectory = "../../../../../.cache/Examples/SadConsoleTest/AssemblerSource"
        DefaultDirectory = @"C:\Users\highb\source\repos\dotnet-6502\.cache\Examples\SadConsoleTest\AssemblerSource"
    }
};

float scale = 3.0f;

var system = SystemsList[options.SystemName].System;
var screen = (IScreen)system;

// ----------
// Silk.NET Window
// ----------
int windowWidth = (int)(screen.VisibleWidth * scale);
int windowHeight = (int)(screen.VisibleHeight * scale);

var windowOptions = WindowOptions.Default;
// Update frequency, in hertz. 
windowOptions.UpdatesPerSecond = 60.0f;
// Render frequency, in hertz.
windowOptions.FramesPerSecond = 60.0f;
//windowOptions.VSync = true;
windowOptions.WindowState = WindowState.Normal;
windowOptions.Title = "DotNet 6502 emulator hosted in native app using SkiaSharp drawing, with OpenGL context provided by Silk.NET.";
windowOptions.Size = new Vector2D<int>(windowWidth, windowHeight);
windowOptions.API = GraphicsAPI.Default; // = Default = OpenGL 3.3 with forward compatibility
windowOptions.ShouldSwapAutomatically = true;
//windowOptions.TransparentFramebuffer = false;
//windowOptions.PreferredDepthBufferBits = 24;    // Depth buffer bits must be set explicitly on MacOS (tested on M1), otherwise there will be be no depth buffer (for OpenGL 3d).

IWindow window = Window.Create(windowOptions);


//SilkNetInput<C64> silkNetInput = null;
//var silkNetInput = new SilkNetInput<C64, SkiaRenderContext>();

var silkNetWindow = new SilkNetWindow<C64>(options.Monitor, window, GetC64SystemRunner, scale);
silkNetWindow.Run();

// Functions for building SystemRunner based on Skia rendering.
// Will be used as from SilkNetWindow in OnLoad (when OpenGL context has been created.)
SystemRunner GetC64SystemRunner(SkiaRenderContext skiaRenderContext, SilkNetInputHandlerContext silkNetInputHandlerContext)
{
    var c64 = C64.BuildC64();

    var renderer = new C64SkiaRenderer();
    renderer.Init(c64, skiaRenderContext);

    var inputHandler = new C64SilkNetInputHandler();
    inputHandler.Init(c64, silkNetInputHandlerContext);

    var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, SilkNetInputHandlerContext>(c64);
    var systemRunner = systemRunnerBuilder
        .WithRenderer(renderer)
        .WithInputHandler(inputHandler)
        .Build();
    return systemRunner;
}