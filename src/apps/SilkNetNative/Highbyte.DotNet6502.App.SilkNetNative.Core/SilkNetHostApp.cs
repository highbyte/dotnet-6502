using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.SilkNetOpenAL;
using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;

namespace Highbyte.DotNet6502.App.SilkNetNative.Core;

public class SilkNetHostApp : HostApp
{
    // --------------------
    // Injected variables
    // --------------------
    private new readonly ILogger _logger;
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
    private SkiaGlCanvasProvider? _skiaGlCanvasProvider;

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

    // Set to true when a fatal error occurs during OnLoad (no emulator systems, an invalid
    // DefaultEmulator, ...). The render loop then shows a quit-only error dialog instead of the
    // normal emulator UI, and the per-frame update is skipped.
    private bool _startupFailed;


    // GL and other ImGui resources
    private GL _gl = default!;
    private ImGuiController _imGuiController = default!;

    private SKImage? _logoImage;
    private SKRect _logoImageDest;

    private SkiaGlCanvasProvider GetSkiaGlCanvasProviderOrThrow()
    {
        return _skiaGlCanvasProvider ?? throw new InvalidOperationException("Skia GL canvas provider has not been initialized.");
    }

    private SKImage GetLogoImageOrThrow()
    {
        return _logoImage ?? throw new InvalidOperationException("Logo image has not been initialized.");
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="window"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    /// <summary>
    /// Function the host uses to resolve a per-system <see cref="IImGuiMenuContributor"/>
    /// from the plug-in container, given the system name. Returns null for systems whose
    /// shell plug-in contributes no menu — the menu then draws nothing system-specific.
    /// </summary>
    private readonly Func<string, IImGuiMenuContributor?> _resolveMenuContributor;

    /// <summary>
    /// Engine plug-ins that contribute system-specific render targets. Invoked from the
    /// render-config callback in <c>OnLoad</c> so no system-specific render code lives here.
    /// </summary>
    private readonly IReadOnlyList<ISilkNetRenderTargetPlugin> _renderTargetPlugins;

    /// <summary>
    /// The main ImGui menu, created during <c>OnLoad</c>. Exposed so the plug-in DI
    /// container can resolve it as <see cref="ISilkNetMenuHost"/> after construction.
    /// </summary>
    public SilkNetImGuiMenu? Menu => _menu;

    public SilkNetHostApp(
        SystemList systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        IWindow window,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        Func<string, IImGuiMenuContributor?> resolveMenuContributor,
        IReadOnlyList<ISilkNetRenderTargetPlugin> renderTargetPlugins
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

        _resolveMenuContributor = resolveMenuContributor;
        _renderTargetPlugins = renderTargetPlugins;
    }


    public void Run()
    {
        try
        {
            _window.Load += OnLoad;
            _window.Closing += OnClosing;
            _window.Update += OnUpdate;
            _window.Resize += OnResize;

            _logger.LogInformation("Starting Silk.NET window event loop...");

            _window.Run();

            _logger.LogInformation("Silk.NET window event loop exited normally.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in Run() method");
            throw;
        }
        finally
        {
            // Cleanup SilNet window resources
            _logger.LogInformation("Disposing Silk.NET window...");
            _window?.Dispose();
        }
    }

    protected void OnLoad()
    {
        try
        {
            _logger.LogInformation("OnLoad: Starting window initialization...");

            SetUninitializedWindow();
            _logger.LogInformation("OnLoad: Window settings configured.");

            InitOpenGL();
            _logger.LogInformation("OnLoad: OpenGL initialized.");

            SetIcon();
            _logger.LogInformation("OnLoad: Icon set.");

            InitLogo();
            _logger.LogInformation("OnLoad: Logo initialized.");

            InitSkiaGlCanvasProvider();
            _logger.LogInformation("OnLoad: Skia GL canvas provider initialized.");

            _inputHandlerContext = CreateInputHandlerContext();
            _logger.LogInformation("OnLoad: Input handler context created.");

            _audioHandlerContext = CreateAudioHandlerContext();
            _logger.LogInformation("OnLoad: Audio handler context created.");

            base.SetContexts(() => _inputHandlerContext);
            base.InitInputHandlerContext();
            _logger.LogInformation("OnLoad: Input handler context initialized.");

            _audioHandlerContext.Init();
            _logger.LogInformation("OnLoad: Audio handler context initialized.");

            // Audio pipeline configuration: register both NAudio host audio targets — command-stream
            // (synth) and PCM-sample (sample-accurate SID core). The audio coordinator picks the
            // one that matches the system's selected audio provider.
            base.SetAudioConfig(atp =>
            {
                atp.AddAudioTargetType<NAudioCommandTarget>(
                    () => new NAudioCommandTarget(_audioHandlerContext, _loggerFactory));
                atp.AddAudioTargetType<NAudioSampleTarget>(
                    () => new NAudioSampleTarget(_audioHandlerContext, _loggerFactory));
            });
            _logger.LogInformation("OnLoad: Audio configuration set.");

            // New rendering pipeline configuration
            base.SetRenderConfig(
                (RenderTargetProvider rtp) =>
                {
                    // System-agnostic render targets — available regardless of the emulated system.
                    rtp.AddRenderTargetType<SkiaCanvasTwoLayerRenderTarget>(() => new SkiaCanvasTwoLayerRenderTarget(
                        new RenderSize(CurrentRunningSystem!.Screen.VisibleWidth, CurrentRunningSystem!.Screen.VisibleHeight),
                        () => GetSkiaGlCanvasProviderOrThrow().Canvas,
                        flush: true));

                    // Experimental Skia command based target. WIP.
                    rtp.AddRenderTargetType<SkiaCommandTarget>(() => new SkiaCommandTarget(
                        () => GetSkiaGlCanvasProviderOrThrow().Canvas,
                        useCellCoordinates: true,
                        flush: true));

                    // System-specific render targets come from engine plug-ins (Impl.SilkNet.<System>).
                    var renderContext = new SilkNetRenderContext(
                        _gl,
                        _window,
                        () => GetSkiaGlCanvasProviderOrThrow().Canvas,
                        () => CurrentRunningSystem!,
                        () => CurrentHostSystemConfig);
                    foreach (var renderTargetPlugin in _renderTargetPlugins)
                        renderTargetPlugin.RegisterRenderTargets(rtp, renderContext);
                },
                () =>
                {
                    var renderloop = new SilkOnRenderLoop(
                        _window,
                        _loggerFactory.CreateLogger(nameof(SilkOnRenderLoop)),
                        OnBeforeRender,
                        OnAfterRender,
                        shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                    return renderloop;
                });
            _logger.LogInformation("OnLoad: Render configuration set.");

            ConfigureSilkNetInput();
            _logger.LogInformation("OnLoad: Silk.NET input configured.");

            InitImGui();
            _logger.LogInformation("OnLoad: ImGui initialized.");

            // From here ImGui can render, so a fatal error below is shown as a quit-only dialog
            // (see the catch). A friendly message for the no-systems case:
            if (AvailableSystemNames.Count == 0)
                throw new DotNet6502Exception(
                    "No emulator systems are available.\n" +
                    "Check the 'EnabledSystems' setting in appsettings.json, and that the system " +
                    "plug-in assemblies are deployed with the application.");

            // Init main menu UI
            _menu = new SilkNetImGuiMenu(this, _emulatorConfig.DefaultEmulator, _defaultAudioEnabled, _defaultAudioVolumePercent, _loggerFactory, _resolveMenuContributor);
            _logger.LogInformation("OnLoad: Main menu created.");

            // Create other UI windows
            _statsPanel = CreateStatsUI();
            _debugInfoPanel = CreateDebugUI();
            _monitor = CreateMonitorUI(_statsPanel, _debugInfoPanel, _emulatorConfig.Monitor);
            _logsPanel = CreateLogsUI(_logStore, _logConfig);
            _logger.LogInformation("OnLoad: UI panels created.");

            // Add all ImGui windows to a list
            _imGuiWindows.Add(_menu);
            _imGuiWindows.Add(_statsPanel);
            _imGuiWindows.Add(_debugInfoPanel);
            _imGuiWindows.Add(_monitor);
            _imGuiWindows.Add(_logsPanel);

            // Default system selected
            SelectSystem(_emulatorConfig.DefaultEmulator).Wait();
            _logger.LogInformation("OnLoad: Initialization complete.");
        }
        catch (Exception ex)
        {
            var rootEx = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
            _logger.LogCritical(rootEx, "Fatal error during SilkNet startup.");

            // If ImGui was initialized, the render loop can show a quit-only error dialog.
            // If it was not, there is no way to render anything — let the exception propagate.
            if (_imGuiController == null)
                throw;

            BeginStartupErrorMode("The emulator could not start.\n\n" + rootEx.Message);
        }
    }

    /// <summary>
    /// Switches the app into startup-error mode: the render loop shows a quit-only error dialog
    /// instead of the emulator UI, and the per-frame update is skipped.
    /// </summary>
    private void BeginStartupErrorMode(string message)
    {
        _startupFailed = true;
        _errorDialogTitle = "Startup Error";
        _errorDialogMessage = message;
    }

    /// <summary>
    /// Renders the in-window startup-error dialog and records the user's quit choice.
    /// </summary>
    private void RenderStartupErrorDialog()
    {
        if (RenderQuitOnlyErrorDialog(_errorDialogTitle, _errorDialogMessage))
        {
            _logger.LogInformation("User chose to quit the application after a startup error.");
            _exceptionExit = true;
        }
    }

    /// <summary>
    /// Renders a modal ImGui error dialog whose only action is to quit. The popup has no close
    /// button, so it cannot be dismissed any other way. Returns <c>true</c> on the frame the Quit
    /// button is clicked. ImGui must already have a frame in progress (controller Update called).
    /// </summary>
    private static bool RenderQuitOnlyErrorDialog(string title, string message)
    {
        ImGui.OpenPopup(title);

        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(500, 200), ImGuiCond.Appearing);

        bool quitClicked = false;

        // No 'ref open' overload → the popup has no close button and can only be left via Quit.
        if (ImGui.BeginPopupModal(title, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            ImGui.TextWrapped(message);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var buttonWidth = 100f;
            var windowWidth = ImGui.GetWindowSize().X;
            ImGui.SetCursorPosX((windowWidth - buttonWidth) * 0.5f);

            if (ImGui.Button("Quit", new Vector2(buttonWidth, 0)))
                quitClicked = true;

            ImGui.EndPopup();
        }

        return quitClicked;
    }

    /// <summary>
    /// Runs a minimal Silk.NET window that shows only a quit-only error dialog. Used when startup
    /// fails before a full <see cref="SilkNetHostApp"/> can run (bad appsettings.json, a plug-in
    /// discovery or DI failure, ...). It creates its own window + GL + ImGui — no host app, system
    /// list or plug-ins. If even this minimal UI cannot be created, the error is written to the
    /// console — the last-resort fallback when no UI is possible.
    /// </summary>
    /// <param name="logger">Optional — may be null if startup failed before logging was set up.</param>
    public static void RunStartupErrorOnly(string message, ILogger? logger = null)
    {
        try
        {
            var windowOptions = WindowOptions.Default;
            windowOptions.Title = "DotNet 6502 Emulator - Startup Error";
            windowOptions.Size = new Vector2D<int>(560, 320);
            windowOptions.WindowBorder = WindowBorder.Fixed;
            windowOptions.WindowState = WindowState.Normal;

            using var window = Window.Create(windowOptions);

            GL? gl = null;
            ImGuiController? imGuiController = null;
            IInputContext? inputContext = null;

            window.Load += () =>
            {
                gl = GL.GetApi(window);
                inputContext = window.CreateInput();
                imGuiController = new ImGuiController(gl, window, inputContext);
            };

            window.Render += deltaTime =>
            {
                if (gl is null || imGuiController is null)
                    return;

                gl.ClearColor(0f, 0f, 0f, 1f);
                gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

                imGuiController.Update((float)deltaTime);
                bool quit = RenderQuitOnlyErrorDialog("Startup Error", message);
                imGuiController.Render();

                if (quit)
                    window.Close();
            };

            window.Closing += () =>
            {
                imGuiController?.Dispose();
                inputContext?.Dispose();
                gl?.Dispose();
            };

            window.Run();
        }
        catch (Exception ex)
        {
            // The UI itself could not be shown — the console is the only remaining fallback.
            logger?.LogCritical(ex, "Could not display the startup error UI. Original error: {Message}", message);
            System.Console.Error.WriteLine($"FATAL: could not display the startup error UI: {ex}");
            System.Console.Error.WriteLine($"Original startup error: {message}");
        }
    }

    private void InitSkiaGlCanvasProvider()
    {
        _skiaGlCanvasProvider?.Dispose();
        _skiaGlCanvasProvider = GetSkiaGlCanvasProvider();
    }

    private void SetIcon()
    {
        //RawImage icon = SilkNetImageLoader.ReadFileAsRawImage("../../../../../../../resources/images/favicon.ico");
        RawImage icon = SilkNetImageLoader.ReadFileAsRawImage("Highbyte.DotNet6502.App.SilkNetNative.Core.Resources.Images.favicon.ico", isEmbeddedResource: true);
        _window.SetWindowIcon(ref icon);
    }

    protected void OnClosing()
    {
        base.Close();
    }

    public override void OnAfterSelectedSystemChanged()
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
        if (!_showErrorDialog || _exceptionExit)
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
        // Startup-error mode has no emulator/monitor UI — nothing to update per frame.
        if (_startupFailed)
            return;

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
            // Update ImGuiController's internal window dimensions using reflection
            // This is necessary because programmatic window size changes don't trigger the Resize event in WSLg
            // and ImGuiController's internal state becomes stale
            var controllerType = _imGuiController.GetType();
            var windowWidthField = controllerType.GetField("_windowWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var windowHeightField = controllerType.GetField("_windowHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (windowWidthField != null && windowHeightField != null)
            {
                windowWidthField.SetValue(_imGuiController, _window.Size.X);
                windowHeightField.SetValue(_imGuiController, _window.Size.Y);
            }
            
            // Now Update() will use the correct dimensions
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
            // Startup-error mode: render only the quit-only error dialog (ImGui was already
            // updated in OnBeforeRender). None of the emulator/panel UI exists.
            if (_startupFailed)
            {
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
                RenderStartupErrorDialog();
                _imGuiController.Render();
                if (_exceptionExit)
                    _window.Close();
                return;
            }

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
                GetSkiaGlCanvasProviderOrThrow().GRContext.Flush();
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
            ObserveUiTask(RenderErrorDialog(), nameof(RenderErrorDialog));

            // Render any ImGui UI rendered above emulator.
            _imGuiController?.Render();
        }
        catch (Exception ex)
        {
            HandleEventHandlerException(ex, nameof(OnAfterRender));
        }
    }

    private void ObserveUiTask(Task task, string operationName)
    {
        _ = task.ContinueWith(
            static (completedTask, state) =>
            {
                if (completedTask.Exception is { } exception)
                {
                    var (hostApp, name) = ((SilkNetHostApp hostApp, string name))state!;
                    hostApp.HandleEventHandlerException(exception.GetBaseException(), name);
                }
            },
            (this, operationName),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OnResize(Vector2D<int> vec2)
    {
    }

    private SkiaGlCanvasProvider GetSkiaGlCanvasProvider()
    {
        GRGlGetProcedureAddressDelegate getProcAddress = (name) =>
        {
            _logger.LogDebug($"Getting OpenGL proc address for: {name}");
            var addrFound = _window.GLContext!.TryGetProcAddress(name, out var addr);
            _logger.LogDebug($"Address found: {addrFound}, Address: {addr}");
            return addrFound ? addr : 0;
        };

        var skiaGlCanvasProvider = new SkiaGlCanvasProvider(
            getProcAddress,
            _window.FramebufferSize.X,
            _window.FramebufferSize.Y,
            _loggerFactory.CreateLogger(nameof(SkiaGlCanvasProvider)),
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
            initialVolumePercent: 20,
            _loggerFactory);
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
        try
        {
            _logger.LogInformation("Getting OpenGL API...");
            _gl = GL.GetApi(_window) ?? throw new DotNet6502Exception("Failed to get OpenGL API from window");

            // Log OpenGL info for diagnostics
            try
            {
                var version = _gl.GetStringS(GLEnum.Version);
                var vendor = _gl.GetStringS(GLEnum.Vendor);
                var renderer = _gl.GetStringS(GLEnum.Renderer);

                _logger.LogInformation($"OpenGL Version: {version}");
                _logger.LogInformation($"OpenGL Vendor: {vendor}");
                _logger.LogInformation($"OpenGL Renderer: {renderer}");
            }
            catch (Exception glInfoEx)
            {
                _logger.LogWarning(glInfoEx, "Could not retrieve OpenGL information");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenGL");
            _logger.LogError("This may indicate missing graphics drivers or OpenGL support.");
            _logger.LogError("On Linux, ensure you have proper graphics drivers installed:");
            _logger.LogError("  - For Intel: sudo apt install mesa-utils libgl1-mesa-glx");
            _logger.LogError("  - For NVIDIA: Install proprietary NVIDIA drivers");
            _logger.LogError("  - For AMD: Install Mesa drivers");
            throw;
        }
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

        string logoResourcePath = "Highbyte.DotNet6502.App.SilkNetNative.Core.Resources.Images.logo.png";
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
        var skiaGlCanvasProvider = GetSkiaGlCanvasProviderOrThrow();
        var canvas = skiaGlCanvasProvider.Canvas;
        canvas.Clear();
        canvas.DrawImage(GetLogoImageOrThrow(), _logoImageDest);

        // Flush the SkiaSharp Context.
        skiaGlCanvasProvider.GRContext.Flush();
    }
}
