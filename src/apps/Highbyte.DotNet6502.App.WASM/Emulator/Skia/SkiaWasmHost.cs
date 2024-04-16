using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Emulator.Skia;

public class SkiaWasmHost : WasmHostBase
{
    private bool _initialized;

    private PeriodicAsyncTimer? _updateTimer;

    private SKCanvas _skCanvas = default!;
    private GRContext _grContext = default!;

    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    public SkiaWasmHost(
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
        _logger = loggerFactory.CreateLogger(typeof(SkiaWasmHost).Name);
        _loggerFactory = loggerFactory;

    }

    private SKCanvas GetCanvas()
    {
        return _skCanvas;
    }

    private GRContext GetGRContext()
    {
        return _grContext;
    }

    public void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        if (EmulatorState != EmulatorState.Running)
            return;

        if (!(e.Surface.Context is GRContext grContext && grContext != null))
            return;

        if (!_initialized)
        {
            _grContext = grContext;
            _skCanvas = e.Surface.Canvas;
            _initialized = true;
        }

        Render();
    }

    protected override void OnInit()
    {
        RenderContextContainer = new WASMRenderContextContainer(
            new SkiaRenderContext(GetCanvas, GetGRContext),
            null);
        InputHandlerContext = new AspNetInputHandlerContext(_loggerFactory, _gamepadList);
        AudioHandlerContext = new WASMAudioHandlerContext(_audioContext, _jsRuntime, _initialMasterVolume);
        _systemList.InitContext(() => RenderContextContainer, () => InputHandlerContext, () => AudioHandlerContext);
    }

    protected override void OnBeforeRender()
    {
        _skCanvas.Scale((float)EmulatorConfig.CurrentDrawScale);
    }

    protected override void OnAfterPause()
    {
        _updateTimer?.Stop();
    }

    protected override Task OnAfterStop()
    {
        _initialized = false;
        return Task.CompletedTask;
    }

    protected override Task OnAfterStart()
    {
        if (_updateTimer != null)
        {
        }
        else
        {
            var screen = SystemRunner.System.Screen;
            // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
            double updateIntervalMS = 1 / screen.RefreshFrequencyHz * 1000;
            _updateTimer = new PeriodicAsyncTimer();
            _updateTimer.IntervalMilliseconds = updateIntervalMS;
            _updateTimer.Elapsed += UpdateTimerElapsed;
        }
        _updateTimer!.Start();

        return Task.CompletedTask;
    }

    protected override void OnAfterCleanup()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer = null;
        }

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

    private void UpdateTimerElapsed(object? sender, EventArgs e) => EmulatorRunOneFrame();

}
