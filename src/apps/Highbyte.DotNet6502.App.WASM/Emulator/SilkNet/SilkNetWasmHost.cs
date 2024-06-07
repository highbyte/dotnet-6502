#if SILKNETWASM

using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Systems;
using Silk.NET.Core.Loader;
using Silk.NET.Core.Native;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Emulator.SilkNet;


public class SilkNetWasmHost : WasmHostBase
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly IJSRuntime _jsRuntime;

    [System.Runtime.InteropServices.DllImport("SDL", EntryPoint = "SDL_GetPlatform", CallingConvention = (System.Runtime.InteropServices.CallingConvention)2)]
    static extern unsafe byte* I_SDL_GetPlatform();
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    static unsafe byte* S_SDL_GetPlatform() => I_SDL_GetPlatform();
    private static IView s_window;
    private int _currentCanvasWidth;
    private int _currentCanvasHeight;

    public SilkNetWasmHost(
            IJSRuntime jsRuntime,
            SystemList<WASMRenderContextContainer, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList,
            Action<string> updateStats,
            Action<string> updateDebug,
            Func<bool, Task> setMonitorState,
            EmulatorConfig emulatorConfig,
            Func<Task> toggleDebugStatsState,
            ILoggerFactory loggerFactory,
            GamepadList gamepadList,
            float initialMasterVolume = 50.0f
        ) : base(
            jsRuntime,
            systemList,
            updateStats,
            updateDebug,
            setMonitorState,
            emulatorConfig,
            toggleDebugStatsState,
            loggerFactory,
            gamepadList,
            initialMasterVolume)
    {
        _logger = loggerFactory.CreateLogger(typeof(SilkNetWasmHost).Name);
        _jsRuntime = jsRuntime;
        _loggerFactory = loggerFactory;
    }

    protected async override void OnInit()
    {
        // Init and start Silk.NET render loop
        var canvasId = "canvas";
        await _jsRuntime.InvokeVoidAsync("setCanvas", typeof(SilkNetWasmHost).Assembly.GetName().Name, $"{typeof(SilkNetWasmHost).FullName}.CanvasDropped", canvasId);
        unsafe
        {
            Console.WriteLine(SilkMarshal.PtrToString((nint)I_SDL_GetPlatform()));
            delegate* unmanaged[Cdecl]<byte*> gp = &S_SDL_GetPlatform;
            if (gp == null)
            {
                throw new("what");
            }
        }
        SearchPathContainer.Platform = UnderlyingPlatform.Browser;

        s_window = GetSilkNetWindow();

        Console.WriteLine("Before setting window event handlers");
        s_window.Load += OnSilkNetLoad;
        s_window.Render += OnSilkNetRender;
        s_window.Update += OnSilkNetUpdate;
        s_window.Resize += OnSilkNetResize;
        s_window.FramebufferResize += OnSilkNetFramebufferResize;
        s_window.Closing += OnSilkNetClosing;

        Console.WriteLine("Before window run");
        s_window.Run();
        Console.WriteLine("After window run");

    }

    [JSInvokable("Highbyte.DotNet6502.App.WASM.Emulator.SilkNet.SilkNetWasmHost.CanvasDropped")]
    public static void CanvasDropped() =>
        Silk.NET.Windowing.Window.CanvasDropped(s_window);

    private IView GetSilkNetWindow()
    {
        Console.WriteLine("Start GetSilkNetWindow");
#if SILKNETWASM //NOTE: not having this WASM check changes nothing, but it's here for clarity
        // Register SDL windowing and Input manually
        // Silk.NET doesn't have all the reflection facilities to find them automatically on WASM
        Console.WriteLine("Before Window registration");
        SdlWindowing.RegisterPlatform();
        Console.WriteLine("Before Input registration");
        SdlInput.RegisterPlatform();
#endif

        var opts = ViewOptions.Default;
        opts.FramesPerSecond = 90;
        opts.UpdatesPerSecond = 90;
        opts.API = GraphicsAPI.Default;

#if WASM
        //On WASM, we should be using OpenGLES 3.0
        opts.API = new GraphicsAPI(ContextAPI.OpenGLES, new APIVersion(3, 0));
#endif

        opts.VSync = false;
        Console.WriteLine("Before window creation");
        Console.WriteLine(Silk.NET.Windowing.Window.Platforms.Count);

        IView window;
        if (Silk.NET.Windowing.Window.IsViewOnly)
        {
            Console.WriteLine("View only");
            window = Silk.NET.Windowing.Window.GetView(opts);
        }
        else
        {
            Console.WriteLine("Not view only");
            window = Silk.NET.Windowing.Window.Create(new(opts));
        }
        Console.WriteLine($"After window creation");
        return window;
    }

    protected void OnSilkNetLoad()
    {
        RenderContextContainer = new WASMRenderContextContainer(
            null,
            new SilkNetOpenGlRenderContext(s_window, (float)_emulatorConfig.CurrentDrawScale));
        InputHandlerContext = new AspNetInputHandlerContext(_loggerFactory, _gamepadList);
        AudioHandlerContext = new WASMAudioHandlerContext(_audioContext, _jsRuntime, _initialMasterVolume);

        _systemList.InitContext(() => RenderContextContainer, () => InputHandlerContext, () => AudioHandlerContext);
    }

    /// <summary>
    /// Runs on every Render Frame event.
    /// 
    /// Use this method to render the world.
    /// 
    /// This method is called at a RenderFrequency set in the GameWindowSettings object.
    /// </summary>
    /// <param name="args"></param>
    protected void OnSilkNetRender(double deltaTime)
    {
        Render();
    }

    /// <summary>
    /// Runs on every Update Frame event.
    /// 
    /// Use this method to run logic.
    /// 
    /// </summary>
    /// <param name=""></param>
    protected void OnSilkNetUpdate(double deltaTime)
    {
        if (EmulatorState != EmulatorState.Running)
            return;
        EmulatorRunOneFrame();
    }

    protected void OnSilkNetClosing()
    {
        Cleanup();
    }

    private void OnSilkNetResize(Vector2D<int> size)
    {
        _logger.LogInformation($"OnSilkNetResize: {size}");
    }

    private void OnSilkNetFramebufferResize(Vector2D<int> size)
    {
        _logger.LogInformation($"OnSilkNetFramebufferResize: {size}");

        //RenderContextContainer.SilkNetOpenGlRenderContext.Gl.Viewport(size);

        //// Temporary hack for Silk.NET viewport size. Parameter size seems to be 1,1 when canvas is hidden...
        //Vector2D<int> sizeWorkaround;
        //if (size.X <= 1 && size.Y <= 1)
        //{
        //    if (SystemRunner != null)
        //    {
        //        var screen = SystemRunner.System.Screen;
        //        var scale = 2.0f; // TODO: Get scale from UI.
        //        sizeWorkaround = new Vector2D<int>((int)(screen.VisibleWidth * scale), (int)(screen.VisibleHeight * scale));
        //    }
        //    else
        //    {
        //        sizeWorkaround = new Vector2D<int>(836, 470);
        //        //sizeWorkaround = new Vector2D<int>(_currentCanvasWidth, _currentCanvasHeight);
        //    }

        //}
        //else
        //{
        //    sizeWorkaround = size;
        //}

        //RenderContextContainer.SilkNetOpenGlRenderContext.Gl.Viewport(sizeWorkaround);
    }

    /// <summary>
    /// Canvas size change event triggered by the GUI.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public override void OnAfterUpdateCanvasSize(int width, int height)
    {
        _logger.LogInformation($"OnAfterUpdateCanvasSize: {width}  {height}");

        // Hack: remember last time canvas size was changed to use in OnSilkNetFramebufferResize
        _currentCanvasWidth = width;
        _currentCanvasHeight = height;

        //Vector2D<int> size = new(width, height);
        //RenderContextContainer.SilkNetOpenGlRenderContext.Gl.Viewport(size);
    }


    protected override void OnBeforeRender()
    {
    }

    protected override void OnAfterPause()
    {
    }

    protected override async Task OnAfterStop()
    {
        // Workaround for Silk.NET WASM canvas that doesn't seem to work if it was invisible first.
        // Also, make sure to resize canvas to 0,0 when stopped (done in general code at Index.razor.cs?)
        RenderContextContainer.SilkNetOpenGlRenderContext.Gl.Clear((uint)ClearBufferMask.ColorBufferBit);
        await _jsRuntime!.InvokeVoidAsync("triggerResize"); // hack to make sure canvas is refreshed
    }

    protected override async Task OnAfterStart()
    {
        // Workaround for Silk.NET WASM canvas that doesn't seem to work if it was invisible first.
        // Make sure to resize back canvas to original size when started (done in general code at Index.razor.cs?)
        await _jsRuntime!.InvokeVoidAsync("triggerResize"); // hack to make sure canvas is refreshed
    }

    protected override void OnAfterCleanup()
    {

        //// Clear canvas
        //_renderContext.SkiaRenderContext.GetCanvas().Clear();

        //// Clean up Skia resources
        //_renderContext.SkiaRenderContext?.Cleanup();

        //// Clean up input handler resources
        //InputHandlerContext?.Cleanup();

        //// Stop any playing audio
        //_systemRunner.AudioHandler.StopPlaying();
        //// Clean up audio resources
        ////AudioHandlerContext?.Cleanup();
    }
}

#endif
