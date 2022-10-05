using Highbyte.DotNet6502.Systems;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;

namespace Highbyte.DotNet6502.App.SkiaNative;
public class SilkNetWindow<TSystem> where TSystem: ISystem
{
    private static IWindow s_window;
    private readonly Func<GRContext, SKCanvas, SystemRunner> _getSystemRunner;
    private readonly float _canvasScale;

    // SkipSharp context/surface/canvas
    private SKRenderContext _skRenderContext;

    // Emulator    
    private SystemRunner _systemRunner;

    public SilkNetWindow(
        IWindow slikNetWindow,
        Func<GRContext, SKCanvas, SystemRunner> getSystemRunner,
        float scale = 1.0f
        ) 
    {
        s_window = slikNetWindow;
        _getSystemRunner = getSystemRunner;
        _canvasScale = scale;
    }

    public void Run()
    {
        s_window.Load += OnLoad;
        s_window.Closing += OnClosing;
        s_window.Update += OnUpdate;
        s_window.Render += OnRender;
        s_window.Resize += OnResize;

        s_window.Run();
    }

    protected void OnLoad()
    {
        // Init SkipSharp resources (must be done in OnLoad, otherwise no OpenGL context will exist create by SilkNet.)
        _skRenderContext = new SKRenderContext(s_window.Size.X, s_window.Size.Y, _canvasScale);

        _systemRunner = _getSystemRunner(_skRenderContext.Context, _skRenderContext.Canvas);

        //_silkNetInput.Init(s_window);
    }

    protected void OnClosing()
    {
        // Cleanup SilkNet resources
        //_silkNetInput.Cleanup();
        s_window?.Dispose();

        // Cleanup Skia resources
        _skRenderContext.CleanUp();
    }

    /// <summary>
    /// Runs on every Update Frame event.
    /// 
    /// Use this method to run logic.
    /// 
    /// </summary>
    /// <param name=""></param>
    protected void OnUpdate(double deltaTime)
    {
        // if(_silkNetInput.Exit)
        // {
        //     s_window.Close();
        //     return;
        // }

        // Handle input
        //_silkNetInput.HandleInput(deltaTime);
        _systemRunner.ProcessInput();

        // Update world
        // RunLogic();
        _systemRunner.RunEmulatorOneFrame();

        // Reset input state
        // _silkNetInput.FrameDone(deltaTime);
    }


    /// <summary>
    /// Runs on every Render Frame event.
    /// 
    /// Use this method to render the world.
    /// 
    /// This method is called at a RenderFrequency set in the GameWindowSettings object.
    /// </summary>
    /// <param name="args"></param>
    protected void OnRender(double deltaTime)
    {
        // Render our world, drawing on the Skia canvas
        //TestDraw(_skRenderContext.Canvas);
        _systemRunner.Draw();

        // Flush the Skia Context
        _skRenderContext.Context?.Flush();

        // SilkNet windows are what's known as "double-buffered". In essence, the window manages two buffers.
        // One is rendered to while the other is currently displayed by the window.
        // This avoids screen tearing, a visual artifact that can happen if the buffer is modified while being displayed.
        // After drawing, call this function to swap the buffers. If you don't, it won't display what you've rendered.

        // NOTE: s_window.SwapBuffers() seem to have some problem. Window is darker, and some flickering.
        //       Use windowOptions.ShouldSwapAutomatically = true  instead
        //s_window.SwapBuffers();
    }

    private void OnResize(Vector2D<int> vec2)
    {
        //_worldRenderer.Resize(vec2.X, vec2.Y);
    }    

    // private void InitSkiaSharpContext()
    // {
    //     // Create the SkiaSharp context
    //     var glInterface = GRGlInterface.Create();
    //     var grContextOptions = new GRContextOptions{};
    //     _context = GRContext.CreateGl(glInterface, grContextOptions);

    //     // Create main Skia surface from OpenGL context
    //     var glFramebufferInfo = new GRGlFramebufferInfo(
    //             fboId: 0,
    //             format: SKColorType.Rgba8888.ToGlSizedFormat());

    //     _renderTarget = new GRBackendRenderTarget(_windowOptions.Size.X, _windowOptions.Size.Y, sampleCount: 0, stencilBits: 0, glInfo: glFramebufferInfo);

    //     // Create the SkiaSharp render target surface
    //     _renderSurface = SKSurface.Create(_context, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    // }

    // private void TestDraw(SKCanvas skCanvas)
    // {
    //     skCanvas.Clear(SKColors.CornflowerBlue);

    //     using (var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Gold })
    //     {
    //         skCanvas.DrawRect(50, 100, 100, 200, paint);
    //     }
    // }
}