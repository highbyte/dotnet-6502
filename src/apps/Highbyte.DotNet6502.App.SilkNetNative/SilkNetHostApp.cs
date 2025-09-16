using System.Numerics;
using System.Reflection;
using Highbyte.DotNet6502.App.SilkNetNative.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Render;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v1;
using Highbyte.DotNet6502.Impl.Skia.Commodore64.Render.Legacy.v2;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetHostApp : HostApp<SilkNetInputHandlerContext, NAudioAudioHandlerContext>
{
    // --------------------
    // Injected variables
    // --------------------
    private readonly ILogger _logger;
    private readonly IWindow _window;
    private readonly EmulatorConfig _emulatorConfig;
    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly bool _defaultAudioEnabled;
    private float _defaultAudioVolumePercent;
    private readonly ILoggerFactory _loggerFactory;

    // --------------------
    // Other variables / constants
    // --------------------
    private SkiaGlCanvasProvider _skiaGlCanvasProvider;

    private SilkNetInputHandlerContext _inputHandlerContext = default!;
    private NAudioAudioHandlerContext _audioHandlerContext = default!;

    public float CanvasScale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set { _emulatorConfig.CurrentDrawScale = value; }
    }
    public const int DEFAULT_WIDTH = 1100;
    public const int DEFAULT_HEIGHT = 700;
    public const int DEFAULT_RENDER_HZ = 60;

    // Monitor
    private SilkNetImGuiMonitor _monitor = default!;
    public SilkNetImGuiMonitor Monitor => _monitor;

    // Instrumentations panel
    private SilkNetImGuiStatsPanel _statsPanel = default!;
    public SilkNetImGuiStatsPanel StatsPanel => _statsPanel;

    // Debug Info panel
    private SilkNetImGuiDebugPanel _debugInfoPanel = default!;
    public SilkNetImGuiDebugPanel DebugInfoPanel => _debugInfoPanel;

    // Logs panel
    private SilkNetImGuiLogsPanel _logsPanel = default!;
    public SilkNetImGuiLogsPanel LogsPanel => _logsPanel;

    // Menu
    private SilkNetImGuiMenu _menu = default!;
    private bool _statsWasEnabled = false;
    //private bool _logsWasEnabled = false;

    private readonly List<ISilkNetImGuiWindow> _imGuiWindows = new List<ISilkNetImGuiWindow>();
    private bool _atLeastOneImGuiWindowHasFocus => _imGuiWindows.Any(x => x.Visible && x.WindowIsFocused);

    // Error dialog state
    private bool _showErrorDialog;
    private string _errorDialogTitle = string.Empty;
    private string _errorDialogMessage = string.Empty;
    private bool _errorDialogIsCritical;
    private bool _exceptionExit;


    // GL and other ImGui resources
    private GL _gl = default!;
    private ImGuiController _imGuiController = default!;

    private SKImage _logoImage;
    private SKRect _logoImageDest;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="window"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public SilkNetHostApp(
        SystemList<SilkNetInputHandlerContext, NAudioAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        IWindow window,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig

        ) : base("SilkNet", systemList, loggerFactory)
    {
        _emulatorConfig = emulatorConfig;
        _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;
        _window = window;
        _logStore = logStore;
        _logConfig = logConfig;
        _defaultAudioEnabled = true;
        _defaultAudioVolumePercent = 20.0f;

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(SilkNetHostApp).Name);
    }


    public void Run()
    {
        _window.Load += OnLoad;
        _window.Closing += OnClosing;
        _window.Update += OnUpdate;
        //_window.Render += OnRender; // old rendering pipeline
        _window.Resize += OnResize;

        _window.Run();
        // Cleanup SilNet window resources
        _window?.Dispose();
    }

    protected void OnLoad()
    {
        SetUninitializedWindow();

        InitOpenGL();

        SetIcon();
        InitLogo();

        InitSkiaGlCanvasProvider();

        _inputHandlerContext = CreateInputHandlerContext();
        _audioHandlerContext = CreateAudioHandlerContext();

        base.SetContexts(() => _inputHandlerContext, () => _audioHandlerContext);
        base.InitInputHandlerContext();
        base.InitAudioHandlerContext();

        // New rendering pipeline configuration
        base.SetRenderConfig(
            (RenderTargetProvider rtp) =>
            {
                // Common source and render targets, independent of emulated system and the host renderer
                rtp.AddRenderTargetType<SkiaCanvasTwoLayerRenderTarget>(() => new SkiaCanvasTwoLayerRenderTarget(
                    new RenderSize(CurrentRunningSystem!.Screen.VisibleWidth, CurrentRunningSystem!.Screen.VisibleHeight),
                    () => _skiaGlCanvasProvider.Canvas,
                    flush: true));

                // Legacy: Simplified custom drawing with Skia commands. Supports characters and sprites. No bitmaps.
                rtp.AddRenderTargetType<C64LegacyRenderTarget>(() => new C64LegacyRenderTarget(
                    (C64)CurrentRunningSystem,
                    () => _skiaGlCanvasProvider.Canvas,
                    flush: true));
                // Legacy: Simplified custom drawing with Skia commands. Supports characters and sprites. No bitmaps.
                rtp.AddRenderTargetType<C64LegacyRenderTarget2>(() => new C64LegacyRenderTarget2(
                    (C64)CurrentRunningSystem,
                    () => _skiaGlCanvasProvider.Canvas,
                    flush: true));
                // Legacy: Simplified custom drawing with Skia commands. Supports characters and sprites. No bitmaps.
                rtp.AddRenderTargetType<C64LegacyRenderTarget2b>(() => new C64LegacyRenderTarget2b(
                    (C64)CurrentRunningSystem,
                    () => _skiaGlCanvasProvider.Canvas,
                    flush: true));

                // GPU based custom source + render targets, specific to emulated system and the host renderer
                rtp.AddRenderTargetType<C64SilkNetOpenGlRendererTarget>(() => new C64SilkNetOpenGlRendererTarget(
                    (C64)CurrentRunningSystem,
                    ((C64HostConfig)CurrentHostSystemConfig).SilkNetOpenGlRendererConfig,
                    _gl,
                    _window
                    ));

                // Experimental Skia C64 command based target. WIP.
                rtp.AddRenderTargetType<SkiaCommandTarget>(() => new SkiaCommandTarget(
                    () => _skiaGlCanvasProvider.Canvas,
                    useCellCoordinates: true,
                    flush: true));

            },
            () =>
            {
                var renderloop = new SilkOnRenderLoop(
                    _window,
                    OnBeforeRender,
                    OnAfterRender,
                    shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                return renderloop;
            });

        ConfigureSilkNetInput();

        InitImGui();

        // Init main menu UI
        _menu = new SilkNetImGuiMenu(this, _emulatorConfig.DefaultEmulator, _defaultAudioEnabled, _defaultAudioVolumePercent, _loggerFactory);

        // Create other UI windows
        _statsPanel = CreateStatsUI();
        _debugInfoPanel = CreateDebugUI();
        _monitor = CreateMonitorUI(_statsPanel, _debugInfoPanel, _emulatorConfig.Monitor);
        _logsPanel = CreateLogsUI(_logStore, _logConfig);

        // Add all ImGui windows to a list
        _imGuiWindows.Add(_menu);
        _imGuiWindows.Add(_statsPanel);
        _imGuiWindows.Add(_debugInfoPanel);
        _imGuiWindows.Add(_monitor);
        _imGuiWindows.Add(_logsPanel);

        // Default system selected
        SelectSystem(_emulatorConfig.DefaultEmulator).Wait();
    }

    private void InitSkiaGlCanvasProvider()
    {
        _skiaGlCanvasProvider?.Dispose();
        _skiaGlCanvasProvider = GetSkiaGlCanvasProvider();
    }

    private void SetIcon()
    {
        //RawImage icon = SilkNetImageLoader.ReadFileAsRawImage("../../../../../../resources/images/favicon.ico");
        RawImage icon = SilkNetImageLoader.ReadFileAsRawImage("Highbyte.DotNet6502.App.SilkNetNative.Resources.Images.favicon.ico", isEmbeddedResource: true);
        _window.SetWindowIcon(ref icon);
    }

    protected void OnClosing()
    {
        base.Close();
    }

    public override void OnAfterSelectSystem()
    {
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        // Make sure to adjust window size and render frequency to match the system that is about to be started
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            var screen = systemAboutToBeStarted.Screen;
            _window.Size = new Vector2D<int>((int)(screen.VisibleWidth * CanvasScale), (int)(screen.VisibleHeight * CanvasScale));
            _window.UpdatesPerSecond = screen.RefreshFrequencyHz;

            InitSkiaGlCanvasProvider();
        }
        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        {
            // Init monitor for current system started if this system was not started before
            _monitor.Init(CurrentSystemRunner!);
        }

        //_window.Focus();
    }

    public override void OnBeforeStop()
    {
        DisableStatsPanel();
    }

    public override void OnAfterClose()
    {
        // Dispose Monitor/Instrumentations panel
        //_monitor.Cleanup();
        //_statsPanel.Cleanup();
        //_debugInfoPanel.Cleanup();
        DestroyImGuiController();

        _skiaGlCanvasProvider?.Dispose();

        // Cleanup contexts
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();

        _gl?.Dispose();
    }

    /// <summary>
    /// Handles exceptions that occur in event handlers like OnUpdate, OnBeforeRender, and OnAfterRender.
    /// Logs the exception and shows a popup dialog with options to continue or exit.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="methodName">The name of the method where the exception occurred</param>
    private void HandleEventHandlerException(Exception exception, string methodName)
    {
        _logger.LogError(exception, "Unhandled exception in {MethodName}: {Message}", methodName, exception.Message);
        Console.WriteLine($"Exception in {methodName}: {exception.Message}");
        Console.WriteLine($"Stack trace: {exception.StackTrace}");

        // Determine if this is a critical exception
        _errorDialogIsCritical = exception is OutOfMemoryException || exception is StackOverflowException;

        // Set up the error dialog
        _errorDialogTitle = _errorDialogIsCritical ? "Critical Error" : "Error";
        _errorDialogMessage = $"An error occurred in {methodName}:\n\n{exception.Message}\n\nException Type: {exception.GetType().Name}";

        if (_errorDialogIsCritical)
        {
            _errorDialogMessage += "\n\nThis is a critical error that may require restarting the application.";
        }

        _showErrorDialog = true;

        if (EmulatorState == EmulatorState.Running)
            Pause();
    }

    /// <summary>
    /// Renders the error dialog popup if it should be shown.
    /// </summary>
    private async Task RenderErrorDialog()
    {
        if (!_showErrorDialog || _exceptionExit)
            return;

        // Open the popup first (must happen before BeginPopupModal)
        ImGui.OpenPopup(_errorDialogTitle);

        // Center the popup
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(500, 200), ImGuiCond.Appearing);

        // Create the popup modal
        if (ImGui.BeginPopupModal(_errorDialogTitle, ref _showErrorDialog, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            // Display the error message
            ImGui.TextWrapped(_errorDialogMessage);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Center the buttons
            var buttonWidth = 100f;
            var totalButtonWidth = buttonWidth * 2 + ImGui.GetStyle().ItemSpacing.X;
            var windowWidth = ImGui.GetWindowSize().X;
            var buttonStartX = (windowWidth - totalButtonWidth) * 0.5f;

            ImGui.SetCursorPosX(buttonStartX);

            // Continue button
            if (ImGui.Button("Continue", new Vector2(buttonWidth, 0)))
            {
                _showErrorDialog = false;
                ImGui.CloseCurrentPopup();
                if (EmulatorState == EmulatorState.Paused)
                    await Start();
            }

            ImGui.SameLine();

            // Exit button
            if (ImGui.Button("Exit", new Vector2(buttonWidth, 0)))
            {
                _logger.LogInformation("User chose to exit application after error dialog.");
                _exceptionExit = true;
                return;
            }

            ImGui.EndPopup();
        }
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
        try
        {
            // Don't update emulator state when app is quitting
            if (_exceptionExit || _inputHandlerContext.Quit || _monitor.Quit)
            {
                _window.Close();
                return;
            }

            base.RunEmulatorOneFrame();
        }
        catch (Exception ex)
        {
            HandleEventHandlerException(ex, nameof(OnUpdate));
        }
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = false;
        shouldReceiveInput = false;

        // Don't update emulator state when monitor is visible
        if (_monitor.Visible)
            return;

        shouldRun = true;

        // Only receive input to emulator when no ImGui window has focus
        if (!_atLeastOneImGuiWindowHasFocus)
            shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
            _monitor.Enable(execEvaluatorTriggerResult);
    }

    /// <summary>
    /// Callback from SilkOnRenderLoop (new rendering pipleline) before the emulator has been rendered to the screen.
    /// </summary>
    public void OnBeforeRender(double deltaTime)
    {
        try
        {
            // Make sure ImGui is up-to-date
            _imGuiController.Update((float)deltaTime);

            var emulatorWillBeRendered = EmulatorState == EmulatorState.Running;
            // If any ImGui window is visible, make sure to clear Gl buffer before rendering emulator
            if (emulatorWillBeRendered)
            {
                if (_monitor.Visible || _statsPanel.Visible || _debugInfoPanel.Visible || _logsPanel.Visible)
                    _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            }
        }
        catch (Exception ex)
        {
            HandleEventHandlerException(ex, nameof(OnBeforeRender));
        }
    }

    /// <summary>
    /// Callback from SilkOnRenderLoop (new rendering pipleline) after the emulator has been rendered to the screen.
    /// </summary>
    public void OnAfterRender(double deltaTime)
    {
        try
        {
            bool emulatorRendered = EmulatorState == EmulatorState.Running;

            if (emulatorRendered)
            {
                // Flush the SkiaSharp Context (NOTE: not necessary if configured IRenderFrameTarget Skia implementation with flush: true) 
                //_skiaGlCanvasProvider.GetGRContext().Flush();

                // Render monitor if enabled and emulator was rendered
                if (_monitor.Visible)
                    _monitor.PostOnRender();

                // Render stats if enabled and emulator was rendered
                if (_statsPanel.Visible)
                    _statsPanel.PostOnRender();

                // Render debug info if enabled and emulator was rendered
                if (_debugInfoPanel.Visible)
                    _debugInfoPanel.PostOnRender();
            }
            else
            {
                DrawLogo();
                _skiaGlCanvasProvider.GRContext.Flush();
            }

            // Render logs if enabled, regardless of if emulator was rendered or not
            if (_logsPanel.Visible)
                _logsPanel.PostOnRender();

            // Note: This check !emulatorRendered not needed if logo is drawn when emulator is not running (see above)
            // If emulator was not rendered, clear Gl buffer before rendering ImGui windows
            //if (!emulatorRendered)
            //{
            //    if (_menu.Visible)
            //        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            //    //Seems the canvas has to be drawn & flushed for ImGui stuff to be visible on top
            //    var canvas = _skiaGlCanvasProvider.Canvas;
            //    canvas.Clear();
            //    _skiaGlCanvasProvider.GRContext.Flush();
            //}

            if (_menu.Visible)
                _menu.PostOnRender();

            // Render error dialog if needed
            RenderErrorDialog();

            // Render any ImGui UI rendered above emulator.
            _imGuiController?.Render();
        }
        catch (Exception ex)
        {
            HandleEventHandlerException(ex, nameof(OnAfterRender));
        }
    }

    private void OnResize(Vector2D<int> vec2)
    {
    }

    private SkiaGlCanvasProvider GetSkiaGlCanvasProvider()
    {
        GRGlGetProcedureAddressDelegate getProcAddress = (name) =>
        {
            var addrFound = _window.GLContext!.TryGetProcAddress(name, out var addr);
            return addrFound ? addr : 0;
        };

        var skiaGlCanvasProvider = new SkiaGlCanvasProvider(
            getProcAddress,
            _window.FramebufferSize.X,
            _window.FramebufferSize.Y,
            _emulatorConfig.CurrentDrawScale * (_window.FramebufferSize.X / _window.Size.X));

        return skiaGlCanvasProvider;
    }

    private SilkNetInputHandlerContext CreateInputHandlerContext()
    {
        var inputHandlerContext = new SilkNetInputHandlerContext(_window, _loggerFactory);
        return inputHandlerContext;
    }

    private NAudioAudioHandlerContext CreateAudioHandlerContext()
    {
        // Output to NAudio built-in output (Windows only)
        //var wavePlayer = new WaveOutEvent
        //{
        //    NumberOfBuffers = 2,
        //    DesiredLatency = 100,
        //}

        // Output to OpenAL (cross platform) instead of via NAudio built-in output (Windows only)
        var wavePlayer = new SilkNetOpenALWavePlayer()
        {
            NumberOfBuffers = 2,
            DesiredLatency = 40
        };

        return new NAudioAudioHandlerContext(
            wavePlayer,
            initialVolumePercent: 20);
    }

    private void ConfigureSilkNetInput()
    {
        // Listen to keys to enable monitor, logs, stats, etc.
        var inputContext = _inputHandlerContext.InputContext;
        if (inputContext.Keyboards == null || inputContext.Keyboards.Count == 0)
            throw new DotNet6502Exception("Keyboard not found");
        var primaryKeyboard = inputContext.Keyboards[0];

        // Listen to special key that will show/hide overlays for monitor/stats
        primaryKeyboard.KeyDown += OnKeyDown;
    }

    public void SetVolumePercent(float volumePercent)
    {
        _defaultAudioVolumePercent = volumePercent;
        _audioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    private void SetUninitializedWindow()
    {
        _window.Size = new Vector2D<int>(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        _window.UpdatesPerSecond = DEFAULT_RENDER_HZ;
    }

    private void InitOpenGL()
    {
        _gl = GL.GetApi(_window);
    }

    private void InitImGui()
    {
        // Init ImGui resource
        _imGuiController = new ImGuiController(
            _gl,
            _window, // pass in our window
            _inputHandlerContext.InputContext // input context
        );
    }

    private SilkNetImGuiMonitor CreateMonitorUI(SilkNetImGuiStatsPanel statsPanel, SilkNetImGuiDebugPanel debugInfoPanel, MonitorConfig monitorConfig)
    {
        // Init Monitor ImGui resources 
        var monitor = new SilkNetImGuiMonitor(monitorConfig);
        monitor.MonitorStateChange += (s, monitorEnabled) => _inputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
        monitor.MonitorStateChange += (s, monitorEnabled) =>
        {
            if (monitorEnabled)
            {
                statsPanel.Disable();
                debugInfoPanel.Disable();
            }
        };
        return monitor;
    }

    private SilkNetImGuiStatsPanel CreateStatsUI()
    {
        return new SilkNetImGuiStatsPanel(GetStats);
    }

    private SilkNetImGuiDebugPanel CreateDebugUI()
    {
        return new SilkNetImGuiDebugPanel(() => CurrentRunningSystem!.DebugInfo);
    }

    private SilkNetImGuiLogsPanel CreateLogsUI(DotNet6502InMemLogStore logStore, DotNet6502InMemLoggerConfiguration logConfig)
    {
        return new SilkNetImGuiLogsPanel(logStore, logConfig);
    }

    private void DestroyImGuiController()
    {
        _imGuiController?.Dispose();
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int x)
    {
        if (key == Key.F6)
            ToggleMainMenu();
        if (key == Key.F10)
            ToggleLogsPanel();

        if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
        {
            if (key == Key.F11)
                ToggleStatsPanel();
            if (key == Key.F12)
                ToggleMonitor();
        }
    }

    private void ToggleMainMenu()
    {
        if (_menu.Visible)
            _menu.Disable();
        else
            _menu.Enable();
    }

    public float Scale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set { _emulatorConfig.CurrentDrawScale = value; }
    }

    public void ToggleMonitor()
    {
        // Only be able to toggle monitor if emulator is running or paused
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (_statsPanel.Visible)
        {
            _statsWasEnabled = true;
            _statsPanel.Disable();
        }

        if (_monitor.Visible)
        {
            _monitor.Disable();
            if (_statsWasEnabled)
            {
                CurrentRunningSystem!.InstrumentationEnabled = true;
                _statsPanel.Enable();
            }
        }
        else
        {
            _monitor.Enable();
        }
    }

    public void ToggleStatsPanel()
    {
        // Only be able to toggle stats if emulator is running or paused
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (_monitor.Visible)
            return;

        if (_statsPanel.Visible)
        {
            DisableStatsPanel();
        }
        else
        {
            EnableStatsPanel();
        }
    }

    private void DisableStatsPanel()
    {
        if (_statsPanel.Visible)
        {
            _statsPanel.Disable();
            _debugInfoPanel.Disable();
        }
        CurrentRunningSystem!.InstrumentationEnabled = false;
        _statsWasEnabled = false;
    }

    private void EnableStatsPanel()
    {
        CurrentRunningSystem!.InstrumentationEnabled = true;
        if (!_monitor.Visible)
        {
            _statsPanel.Enable();
            _debugInfoPanel.Enable();
        }
    }

    public void ToggleLogsPanel()
    {
        if (_monitor.Visible)
            return;

        if (_logsPanel.Visible)
        {
            //_logsWasEnabled = true;
            _logsPanel.Disable();
        }
        else
        {
            _logsPanel.Enable();
        }
    }

    private void InitLogo()
    {
        //string logoFile = "Resources/Images/Logo.png";
        //_logoImage = SKImage.FromEncodedData(logoFile);

        string logoResourcePath = "Highbyte.DotNet6502.App.SilkNetNative.Resources.Images.logo.png";
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream? resourceStream = assembly.GetManifestResourceStream(logoResourcePath))
        {
            if (resourceStream == null)
                throw new Exception($"Cannot open stream to resource {logoResourcePath} in current assembly.");
            _logoImage = SKImage.FromEncodedData(resourceStream);
        }

        float logo_width = 256;
        float logo_height = 256;
        float logo_x = DEFAULT_WIDTH / 2 - logo_width / 2;
        float logo_y = DEFAULT_HEIGHT / 2 - logo_height / 2;

        var scale = _emulatorConfig.CurrentDrawScale;

        var left = logo_x / scale;
        var top = logo_y / scale;
        var right = left + (logo_width / scale);
        var bottom = top + (logo_height / scale);

        _logoImageDest = new SKRect(left, top, right, bottom);
    }

    private void DrawLogo()
    {
        var canvas = _skiaGlCanvasProvider.Canvas;
        canvas.Clear();
        canvas.DrawImage(_logoImage, _logoImageDest);

        // Flush the SkiaSharp Context.
        _skiaGlCanvasProvider.GRContext.Flush();
    }
}
