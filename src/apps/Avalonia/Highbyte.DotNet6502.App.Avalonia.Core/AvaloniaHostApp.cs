using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.Render;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.App.Avalonia.Core.Views;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator in an Avalonia window
/// </summary>
public class AvaloniaHostApp : HostApp<AvaloniaInputHandlerContext, NullAudioHandlerContext>
{
    private readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly bool _defaultAudioEnabled;
    private readonly float _defaultAudioVolumePercent;

    private readonly SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext> _systemList;

    private AvaloniaInputHandlerContext _inputHandlerContext = default!;
    private NullAudioHandlerContext _audioHandlerContext = default!;

    private PeriodicAsyncTimer? _updateTimer;
    private EmulatorView _emulatorView = default!;

    // Public properties for external access
    public SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext> SystemList => _systemList;
    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    /// <summary>
    /// Constructor used from desktop app where we log to an in-memory log store that can be viewed in the UI
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public AvaloniaHostApp(
        SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig) : this(systemList, loggerFactory, emulatorConfig)
    {
        _logStore = logStore;
        _logConfig = logConfig;
    }

    /// <summary>
    /// Constructor used from browser app where we log to browser console
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    public AvaloniaHostApp(
        SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig
        ) : base("Avalonia", systemList, loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(AvaloniaHostApp).Name);
        _emulatorConfig = emulatorConfig;
        _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;
        _defaultAudioEnabled = true;
        _defaultAudioVolumePercent = 20.0f;
        _systemList = systemList;

        _inputHandlerContext = new AvaloniaInputHandlerContext();
        _audioHandlerContext = CreateAudioHandlerContext(); // For now, use a null audio implementation for Avalonia

        base.SetContexts(() => _inputHandlerContext, () => _audioHandlerContext);
        base.InitInputHandlerContext();
        base.InitAudioHandlerContext();

        ConfigureRender();
    }

    private void ConfigureRender()
    {
        // New rendering pipeline configuration
        base.SetRenderConfig(
            (RenderTargetProvider rtp) =>
            {
                // Common source and render targets, independent of emulated system and the host renderer
                rtp.AddRenderTargetType<AvaloniaBitmapTwoLayerRenderTarget>(() => new AvaloniaBitmapTwoLayerRenderTarget(
                    new RenderSize(CurrentRunningSystem!.Screen.VisibleWidth, CurrentRunningSystem!.Screen.VisibleHeight)));

                // Avalonia-specific command based target for Skia-like rendering using Avalonia drawing primitives
                rtp.AddRenderTargetType<AvaloniaCommandTarget>(() => new AvaloniaCommandTarget(cellWidth: 8, cellHeight: 8, fontSize: 8));
            },
            () =>
            {
                var renderloop = new AvaloniaInvalidateRenderLoop(
                    () => _emulatorView.RenderControl,
                    shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                return renderloop;
            });
    }

    public override void OnAfterSelectSystem()
    {
        // Additional setup after system selection if needed
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        Console.WriteLine($"Emulator started: {CurrentRunningSystem.Name} Variant: {SelectedSystemConfigurationVariant}");

        //((IAvaloniaRenderer)CurrentSystemRunner!.Renderer).SetNewFrameHasBeenDrawnCallback(() =>
        //{
        //    // Invalidate the display to trigger a redraw
        //    _emulatorView.DisplayControl!.InvalidateVisual();
        //});

        var screen = CurrentRunningSystem!.Screen;
        // Set the size of the display control based on the system screen size
        _emulatorView.ConfigureRendererControl(
            renderCoordinator: GetRenderCoordinator(),
            avaloniaBitmapRenderTarget: GetRenderTarget<IAvaloniaBitmapRenderTarget>()
            );
        _emulatorView.RenderControl!.SetDisplaySize(screen.VisibleWidth, screen.VisibleHeight);

        // Create timer for current system on initial start. Assume Stop() sets _updateTimer to null.
        if (_updateTimer == null)
        {
            _updateTimer = CreateAsyncUpdateTimerForSystem(CurrentSystemRunner!.System);
        }
        _updateTimer.Start();
    }

    public override void OnAfterPause()
    {
        _updateTimer?.Stop();
    }

    public override void OnAfterStop()
    {
        _updateTimer?.Stop();
        if (_updateTimer != null)
        {
            _updateTimer.Elapsed -= UpdateTimerElapsed;
            _updateTimer.Dispose();
            _updateTimer = null;
        }
    }

    public override void OnAfterClose()
    {
        // Cleanup contexts
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();

        // Cleanup timer
        _updateTimer?.Stop();
        if (_updateTimer != null)
        {
            _updateTimer.Elapsed -= UpdateTimerElapsed;
            _updateTimer.Dispose();
            _updateTimer = null;
        }
    }

    private PeriodicAsyncTimer CreateAsyncUpdateTimerForSystem(ISystem system)
    {
        // Number of milliseconds between each invocation of the main loop
        double updateIntervalMS = (1 / system.Screen.RefreshFrequencyHz) * 1000;

        var updateTimer = new PeriodicAsyncTimer
        {
            IntervalMilliseconds = updateIntervalMS
        };
        updateTimer.Elapsed += UpdateTimerElapsed;
        return updateTimer;
    }


    private void UpdateTimerElapsed(object? sender, EventArgs e)
    {
        RunEmulatorOneFrame();
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = true;
        shouldReceiveInput = true;

        // Don't update emulator state when emulator is not running
        if (EmulatorState != EmulatorState.Running)
        {
            shouldRun = false;
            shouldReceiveInput = false;
        }
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Handle any post-frame logic here, such as:
        // - Updating debug information
        // - Handling breakpoints
        // - Updating performance statistics
        // This can be expanded later as needed
    }

    public float Scale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set { _emulatorConfig.CurrentDrawScale = value; }
    }

    private NullAudioHandlerContext CreateAudioHandlerContext()
    {
        return new NullAudioHandlerContext();
    }

    //internal void SetEmulatorView(EmulatorView emulatorView)
    internal void SetEmulatorView(EmulatorView emulatorView)
    {
        _emulatorView = emulatorView;
    }

    /// <summary>
    /// Toggle the visibility of the statistics panel
    /// </summary>
    public void ToggleStatisticsPanel()
    {
        if (CurrentRunningSystem == null) return;
        var app = global::Avalonia.Application.Current;
        if (app == null)
            return;

        MainViewModel? viewModel = null;

        switch (app.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                viewModel = desktop.MainWindow?.DataContext as MainViewModel;
                break;

            case ISingleViewApplicationLifetime singleView:   // Used by Avalonia Browser / Mobile style hosting
                if (singleView.MainView is Control ctrl)
                    viewModel = ctrl.DataContext as MainViewModel;
                break;

                // (Optional future-proof) If you later multi-target a browser-specific lifetime interface:
                // case IBrowserApplicationLifetime browser:
                //     if (browser.MainView is Control bCtrl)
                //         viewModel = bCtrl.DataContext as MainViewModel;
                //     break;
        }

        if (viewModel == null)
            return;

        CurrentRunningSystem.InstrumentationEnabled = !CurrentRunningSystem.InstrumentationEnabled;
        viewModel.ToggleStatisticsPanel();
    }

    /// <summary>
    /// Receive Key Down event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyDown(Key key)
    {
        // Send event to emulator
        _inputHandlerContext.AddKeyDown(key);

        // Check for other emulator functions
        if (key == Key.F11)
        {
            _logger.LogInformation("F11 pressed - toggling statistics/debug view");
            // Toggle statistics/debug view
            ToggleStatisticsPanel();
        }
        // else if (key == "F12" && (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused))
        // {
        //     ToggleMonitor();
        // }
        // else if (key == "F9" && EmulatorState == EmulatorState.Running)
        // {
        //     var toggeledAssistantState = !((C64AspNetInputHandler)CurrentSystemRunner.InputHandler).CodingAssistantEnabled;
        //     ((C64AspNetInputHandler)CurrentSystemRunner.InputHandler).CodingAssistantEnabled = toggeledAssistantState;
        //     ((C64HostConfig)CurrentHostSystemConfig).BasicAIAssistantDefaultEnabled = toggeledAssistantState;
        // }
    }

    /// <summary>
    /// Receive Key Up event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="e"></param>
    public void OnKeyUp(Key key)
    {
        // Send event to emulator
        _inputHandlerContext.RemoveKeyDown(key);
    }

    /// <summary>
    /// Receive Focus on emulator canvas.
    /// </summary>
    /// <param name="e"></param>
    // public void OnFocus(FocusEventArgs e)
    // {
    //     _inputHandlerContext.OnFocus(e);
    // }
}
