using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Highbyte.DotNet6502.App.SkiaNative;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using SkiaSharp;
using System.Collections.Generic;

// Fix for starting in debug mode from VS Code. By default the OS current directory is set to the project folder, not the folder containing the built .exe file...
var currentAppDir = AppDomain.CurrentDomain.BaseDirectory;
Environment.CurrentDirectory = currentAppDir;

// Systems
Dictionary<string, (ISystem System, Func<SKCanvas, SystemRunner> SystemRunnerBuilder)> SystemsList = new ()
{
    {"C64", (C64.BuildC64(), GetC64SystemRunner)}    
};

string selectedSystemId = "C64";
float scale = 3.0f;

var system = SystemsList[selectedSystemId].System;
var screen = (IScreen)system;


// ----------
// Silk.NET Window
// ----------
// int width = 1280;
// int height = 720;
// int width = 1130;
// int height = 770;
int windowWidth = (int)(screen.VisibleWidth * scale);
int windowHeight = (int)(screen.VisibleHeight * scale);

var windowOptions = WindowOptions.Default;
// Update frequency, in hertz. 
windowOptions.UpdatesPerSecond = 0.0f;
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


var silkNetWindow = new SilkNetWindow<C64>(window, GetC64SystemRunner, scale);
silkNetWindow.Run();


// Functions for building SystemRunner based on Skia rendering.
// Will be used as from SilkNetWindow in OnLoad (when OpenGL context has been created.)
SystemRunner GetC64SystemRunner(SKCanvas skCanvas)
{
    return  C64SkiaSystemRunnerBuilder.BuildSystemRunner(skCanvas);
}