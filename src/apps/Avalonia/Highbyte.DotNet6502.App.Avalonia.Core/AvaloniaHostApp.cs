using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using Highbyte.DotNet6502.App.Avalonia.Core.Controls;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.Avalonia.Monitor;
using Highbyte.DotNet6502.Impl.Avalonia.Render;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.WebAudioAPI;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator in an Avalonia window
/// </summary>
public class AvaloniaHostApp : HostApp, INotifyPropertyChanged, IDebuggableHostApp, IRemotableHostApp
{
    private new readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;
    private readonly Func<string, IConfigurationSection, string?, Task>? _saveCustomConfigSection;

    /// <summary>
    /// Persists a (configSectionName, json, optional fileName) tuple to whatever backing
    /// store the host uses (file on desktop, localStorage in browser). Exposed so shell
    /// plugins can hand it to their per-system <c>ISystemConfigurer</c> when constructing
    /// it from DI.
    /// </summary>
    public Func<string, string, string?, Task>? SaveCustomConfigString => _saveCustomConfigString;
    private readonly Func<string, string?>? _loadScript;
    private readonly Action<string, string>? _saveScript;
    private readonly Action<string>? _deleteScript;
    private readonly Func<Task>? _loadExamples;
    private float _defaultAudioVolumePercent;

    private readonly SystemList _systemList;
    private readonly WavePlayerFactory _wavePlayerFactory;
    private AvaloniaInputHandlerContext _inputHandlerContext = default!;
    private NAudioAudioHandlerContext _audioHandlerContext = default!;

    internal new IScriptingEngine ScriptingEngine => base.ScriptingEngine;

    private PeriodicAsyncTimer? _updateTimer;

    private EmulatorDisplayControlBase? _renderControl;

    internal bool IsStatsPanelVisible
    {
        get
        {
            if (CurrentRunningSystem == null)
                return false;
            return CurrentRunningSystem.InstrumentationEnabled;
        }
    }

    private AvaloniaMonitor? _monitor;
    internal AvaloniaMonitor? Monitor => _monitor;

    /// <summary>
    /// When true, the emulator will not execute any instructions until an external debugger connects.
    /// Set before system start when debugging from the very first instruction is desired.
    /// Cleared automatically when the debugger attaches.
    /// </summary>
    public bool WaitForExternalDebugger { get; set; }

    private bool _isExternalDebuggerAttached;
    /// <summary>
    /// Flag to indicate if an external debugger (e.g., VSCode) is attached.
    /// When true, the built-in monitor should not activate on breakpoints.
    /// </summary>
    public bool IsExternalDebuggerAttached
    {
        get => _isExternalDebuggerAttached;
        private set
        {
            if (_isExternalDebuggerAttached != value)
            {
                _isExternalDebuggerAttached = value;
                OnPropertyChanged(nameof(IsExternalDebuggerAttached));
            }
        }
    }
    private DebugAdapterLogic? _debugAdapter;
    public void SetExternalDebugAdapter(DebugAdapterLogic debugAdapter)
    {
        _originalBreakpointEvaluator = CurrentSystemRunner?.CustomExecEvaluator;
        CurrentSystemRunner?.SetCustomExecEvaluator(debugAdapter.GetBreakpointEvaluator());
        _debugAdapter = debugAdapter;
        WaitForExternalDebugger = false; // Debugger has taken over, clear the wait flag
        IsExternalDebuggerAttached = true;

        // Pause audio when debugger stops (breakpoint/pause), resume only on Continue (F5).
        // Audio intentionally stays paused during stepping (F10/F11).
        debugAdapter.OnDebuggerPaused = PauseAudio;
        debugAdapter.OnDebuggerResumed = ResumeAudio;
    }
    public void ClearExternalDebugAdapter()
    {
        if (_debugAdapter != null)
        {
            _debugAdapter.OnDebuggerPaused = null;
            _debugAdapter.OnDebuggerResumed = null;
        }
        _debugAdapter = null;
        if (CurrentSystemRunner != null)
        {
            if (_originalBreakpointEvaluator != null)
                CurrentSystemRunner.SetCustomExecEvaluator(_originalBreakpointEvaluator);
            else
                CurrentSystemRunner.ClearCustomExecEvaluator();
        }
        IsExternalDebuggerAttached = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Expose LoggerFactory for use in views that are note created through DI.
    internal ILoggerFactory LoggerFactory => _loggerFactory;

    // Expose LogStore for use in views that are not created through DI (e.g., to display logs in the UI).
    internal DotNet6502InMemLogStore? LogStore => _logStore;

    // Expose SystemLst and EmulatorConfig properties internal access
    internal SystemList SystemList => _systemList;
    internal EmulatorConfig EmulatorConfig => _emulatorConfig;

    /// <summary>
    /// The effective CORS proxy URL for cross-origin HTTP fetches (browser only; null on desktop).
    /// General browser setting from <see cref="EmulatorConfig.CorsProxyUrl"/>, used by the system
    /// download flows and URL-driven startup. Exposed publicly so the per-system shell view models
    /// can read it without access to the internal <see cref="EmulatorConfig"/>.
    /// </summary>
    public string? GetCorsProxyUrl() => _emulatorConfig.GetCorsProxyUrl();

    /// <summary>
    /// The app's base URL (origin + path) used to prefix generated shareable startup links, or null
    /// when sharing isn't available (desktop). Browser-only; set by the host at startup. Exposed so
    /// per-system shell view models can build share links without access to the internal config.
    /// </summary>
    public string? GetShareBaseUrl() => _emulatorConfig.ShareBaseUrl;

    // Expose InputHandlerContext for debug views
    internal AvaloniaInputHandlerContext InputHandlerContext => _inputHandlerContext;

    /// <summary>
    /// Constructor for AvaloniaHostApp.
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    /// <param name="saveCustomConfigString"></param>
    /// <param name="saveCustomConfigSection"></param>
    /// <param name="gamepad">Optional gamepad provider. Pass null to use a NullAvaloniaGamepad.</param>
    /// <param name="loadScript">Optional callback to load a script's source content by file name (browser: from localStorage).</param>
    /// <param name="saveScript">Optional callback to persist a script by file name and content (browser: to localStorage).</param>
    /// <param name="deleteScript">Optional callback to remove a script by file name (browser: from localStorage).</param>
    /// <param name="loadExamples">Optional callback to seed bundled example scripts into the host's script storage.</param>
    internal AvaloniaHostApp(
        SystemList systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        Func<string, string, string?, Task>? saveCustomConfigString,
        Func<string, IConfigurationSection, string?, Task>? saveCustomConfigSection,
        IGamepad? gamepad = null,
        Func<string, string?>? loadScript = null,
        Action<string, string>? saveScript = null,
        Action<string>? deleteScript = null,
        Func<Task>? loadExamples = null

        ) : base("Avalonia", systemList, loggerFactory, useStatsNamePrefix: false)
    {
        _loggerFactory = loggerFactory;
        _logStore = logStore;
        _logConfig = logConfig;
        _saveCustomConfigString = saveCustomConfigString;
        _saveCustomConfigSection = saveCustomConfigSection;
        _loadScript = loadScript;
        _saveScript = saveScript;
        _deleteScript = deleteScript;
        _loadExamples = loadExamples;

        _logger = loggerFactory.CreateLogger(typeof(AvaloniaHostApp).Name);
        _emulatorConfig = emulatorConfig;
        _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;
        _defaultAudioVolumePercent = 20.0f;
        _systemList = systemList;
        _wavePlayerFactory = new WavePlayerFactory(_loggerFactory, _emulatorConfig);

        _inputHandlerContext = new AvaloniaInputHandlerContext(gamepad);

        base.SetContexts(() => _inputHandlerContext);
        base.InitInputHandlerContext();

        ConfigureRender();
        ConfigureAudio();
    }

    private void ConfigureAudio()
    {
        // Audio pipeline configuration: register the NAudio host audio target.
        // The factory is invoked per emulator start; GetAudioHandlerContext() returns the
        // appropriate (real or silent) audio context for the current platform / config.
        base.SetAudioConfig(atp =>
        {
            atp.AddAudioTargetType<NAudioCommandTarget>(() =>
            {
                var audioHandlerContext = GetAudioHandlerContext();
                if (audioHandlerContext.IsInitialized)
                    audioHandlerContext.Cleanup();
                audioHandlerContext.Init();
                return new NAudioCommandTarget(audioHandlerContext, _loggerFactory);
            });
            atp.AddAudioTargetType<NAudioSampleTarget>(() =>
            {
                var audioHandlerContext = GetAudioHandlerContext();
                if (audioHandlerContext.IsInitialized)
                    audioHandlerContext.Cleanup();
                audioHandlerContext.Init();
                if (EmulatorConfig.BrowserSampleAudioMode != BrowserSampleAudioMode.Stable
                    && OperatingSystem.IsBrowser()
                    && audioHandlerContext.WavePlayer is WebAudioWavePlayer webAudioWavePlayer)
                {
                    var requireAudioWorklet = EmulatorConfig.BrowserSampleAudioMode == BrowserSampleAudioMode.DirectWriteAudioWorklet;
                    return new WebAudioSampleTarget(webAudioWavePlayer.Settings, audioHandlerContext, _loggerFactory, requireAudioWorklet);
                }

                var primeSilenceSamples = audioHandlerContext.WavePlayer is WebAudioWavePlayer
                    ? 0
                    : AudioSampleCoordinator.DefaultPrimeSilenceSamples;
                return new NAudioSampleTarget(audioHandlerContext, _loggerFactory, primeSilenceSamples);
            });
        });
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
                rtp.AddRenderTargetType<AvaloniaCommandTarget>(() => new AvaloniaCommandTarget(
                    cellWidth: 8,
                    cellHeight: 8,
                    fontSize: 8));
            },
            () =>
            {
                var renderloop = new AvaloniaInvalidateRenderLoop(
                    () => _renderControl,  // Use the registered render control set by EmulatorView
                    shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                return renderloop;
            });
    }

    public override void OnAfterEmulatorStateChange()
    {
        OnPropertyChanged(nameof(EmulatorState));
    }

    public override void OnAfterHostSystemConfigUpdated()
    {
        OnPropertyChanged(nameof(CurrentHostSystemConfig));

        // A host-config update can flip config validity — e.g. ROMs downloaded during automated
        // startup turn a previously invalid C64 config valid. Re-run validation so the config-status
        // UI clears the now-resolved errors; without this the validation stays stale (it otherwise
        // only refreshes when the selected system changes, not when its config is updated in place).
        SafeAsyncHelper.Execute(ValidateConfigAsync);
    }

    public override void OnAfterSelectedSystemChanged()
    {
        OnPropertyChanged(nameof(SelectedSystemName));
        OnPropertyChanged(nameof(CurrentHostSystemConfig));

        SafeAsyncHelper.Execute(ValidateConfigAsync);
        base.OnAfterSelectedSystemChanged();
    }

    public override void OnAfterAllSystemConfigurationVariantsChanged()
    {
        OnPropertyChanged(nameof(AllSelectedSystemConfigurationVariants));
    }

    public override void OnAfterSelectedSystemVariantChanged()
    {
        OnPropertyChanged(nameof(SelectedSystemConfigurationVariant));
        base.OnAfterSelectedSystemVariantChanged();
    }

    public void SetVolumePercent(float volumePercent)
    {
        _defaultAudioVolumePercent = volumePercent;
        _audioHandlerContext?.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    // --- Snapshot host-config block (host-app-specific settings) ---
    // No host-specific settings are captured yet: audio *enabled* lives in the portable system config
    // (ISystemConfig.AudioEnabled), so it is restored via the systemConfig block through the normal
    // build/start path. Audio *volume* is host-specific but must be applied to the live audio context
    // (only takes effect while running), which the paused-on-restore load flow does not cleanly
    // support — deferred. The base capture (null) / apply (no-op) are used until a host-specific
    // setting that can be applied safely is added; the hooks + HostName tag remain for that.

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Force a full GC to free up memory, so it won't risk accumulate memory usage if GC has not run for a while.
        var m0 = GC.GetTotalMemory(forceFullCollection: true);
        _logger.LogInformation("Allocated memory before starting emulator: " + m0);

        _inputHandlerContext.ClearKeysDown();

        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        var screen = CurrentRunningSystem!.Screen;

        // Automatically adjust scale if emulator dimensions are too wide/tall.
        Scale = GetUsefulScaleBasedOnEmulatorScreenDimensions(screen, Scale, alwaysUseMaxScale: false);

        // Stop and dispose any existing update timer
        StopAndDisposeUpdateTimer();

        // Create timer for current system on initial start. Assume Stop() sets _updateTimer to null.
        _updateTimer = CreateAsyncUpdateTimerForSystem(CurrentSystemRunner!.System);

        _updateTimer.Start();

        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        {
            _monitor?.Disable();
            _monitor = new AvaloniaMonitor(CurrentSystemRunner!, _emulatorConfig.Monitor);
            // Notify subscribers that a new Monitor instance is available
            OnPropertyChanged(nameof(Monitor));

        }

        base.OnAfterStart(emulatorStateBeforeStart);
    }

    public override void OnAfterPause()
    {
        base.OnAfterPause();
        _updateTimer?.Stop();
    }

    public override void OnBeforeStop()
    {
        SetStatisticsPanelVisible(false);

        if (_monitor != null)
        {
            _monitor.Disable();
            _monitor = null;
            // Notify subscribers that Monitor is no longer available
            OnPropertyChanged(nameof(Monitor));
        }
    }

    public override void OnAfterStop()
    {
        StopAndDisposeUpdateTimer();
        base.OnAfterStop();
    }

    protected override IScriptingTickTimer CreateScriptingTickTimer(double intervalMs) =>
        new PeriodicAsyncTimer { IntervalMilliseconds = intervalMs };

    protected override void OnScriptingEngineSet()
    {
        _ = Dispatcher.UIThread.InvokeAsync(DrainPendingScriptActionsAsync);
    }

    public override void QuitApplication()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
        {
            _logger.LogWarning("QuitApplication is not supported in this host environment. Ignoring.");
            return;
        }
        Close();
        Dispatcher.UIThread.Post(() => lifetime.Shutdown());
    }

    public override void OnAfterClose()
    {
        base.OnAfterClose();

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

        if (_monitor != null)
        {
            _monitor.Disable();
            _monitor = null;
            // Notify subscribers that Monitor is no longer available
            OnPropertyChanged(nameof(Monitor));
        }
    }

    private float GetUsefulScaleBasedOnEmulatorScreenDimensions(IScreen emulatorScreenPixels, float currentScale, bool alwaysUseMaxScale)
    {
        // Try to get the actual available space based on screen resolution
        try
        {

            var app = global::Avalonia.Application.Current;
            if (app == null)
                return currentScale;

            // Get the main window based on application lifetime type
            Window? mainWindow = null;
            if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                mainWindow = desktop.MainWindow;
            }
            else
            {
                // For other (Browser/WASM): can't reliably get screen resolution, use fallback
                return currentScale;
            }
            if (mainWindow == null || !mainWindow.IsVisible)
                return currentScale;

            // Get host OS screen that (mostly) contains the main window
            var screenInfo = GetCurrentScreenInfo(mainWindow);
            if (screenInfo == null)
                return currentScale;

            // Calculate available size for emulator within the main window based on screen resolution
            var success = TryGetEmulatorMaxAvailableSize(mainWindow, screenInfo, out double availableWidth, out double availableHeight);
            if (!success || availableWidth <= 0 || availableHeight <= 0)
            {
                _logger.LogDebug("Could not determine available size for emulator based on screen resolution - using current scale.");
                return currentScale;
            }

            // Calculate the scale that fits the emulator within available space
            var scaleX = (float)availableWidth / emulatorScreenPixels.VisibleWidth;
            var scaleY = (float)availableHeight / emulatorScreenPixels.VisibleHeight;
            var maxScale = Math.Min(scaleX, scaleY);

            // Round scale down to nearest 0.5 step to prefer slightly smaller fit
            maxScale = (float)(Math.Floor(maxScale * 2) / 2);

            _logger.LogDebug(
               $"MaxScale calculation: " +
                  $"AvailableSize({availableWidth:F0}x{availableHeight:F0}) " +
                       $"EmulatorScreenSize({emulatorScreenPixels.VisibleWidth}x{emulatorScreenPixels.VisibleHeight}) " +
                            $"ScaleX({scaleX:F2}) ScaleY({scaleY:F2}) MaxScale({maxScale:F2})");

            // If the always using maxium scale was not requested, use the current scale if it currently fits (currentScale <= maxScale)
            if (!alwaysUseMaxScale && currentScale <= maxScale)
            {
                _logger.LogDebug(
                   $"Calculated max scale ({maxScale:F2}) is greater than or equal to current scale ({currentScale:F2}) - keeping current scale.");
                return currentScale;
            }

            _logger.LogDebug($"Using calculated max scale: {maxScale:F2}");
            return maxScale;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error calculating useful scale based on screen dimensions: {ex.Message}");
            // Fall through to return current scale on error
            return currentScale;
        }
    }

    private Screen? GetCurrentScreenInfo(Window mainWindow)
    {
        // Get the screen that contains the main window
        var screens = mainWindow.Screens;
        if (screens == null || screens.ScreenCount == 0)
            return null;

        // Get the screen bounds from the screen that the window is on
        // Use the window's center point to determine which screen it's on
        var windowCenter = new PixelPoint((int)(mainWindow.Position.X + mainWindow.Bounds.Width / 2), (int)(mainWindow.Position.Y + mainWindow.Bounds.Height / 2));
        var screenInfo = screens.ScreenFromPoint(windowCenter);

        if (screenInfo == null || screenInfo.Bounds.Width <= 0 || screenInfo.Bounds.Height <= 0)
        {
            // Fallback to primary screen if we can't determine which screen the window is on
            screenInfo = screens.Primary;
        }
        return screenInfo;
    }

    private bool TryGetEmulatorMaxAvailableSize(Window mainWindow, Screen screenInfo, out double availableWidth, out double availableHeight)
    {
        availableWidth = 0;
        availableHeight = 0;

        if (screenInfo == null || screenInfo.Bounds.Width <= 0 || screenInfo.Bounds.Height <= 0)
            return false;

        // Get the MainView from the window's content
        if (mainWindow.Content is not UserControl mainView)
            return false;

        // MainView's content is an outer overlay Grid; the inner content grid is named "MainContentGrid".
        if (mainView.Content is not Grid mainViewOuterGrid)
            return false;
        var mainViewGrid = mainViewOuterGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "MainContentGrid") ?? mainViewOuterGrid;

        // MainView grid structure (from MainView.axaml):
        // Columns: 0=Menu(200), 1=Emulator(*), 2=Stats(Auto)
        // Rows: 0=Auto, 1=*, 2=Auto
        // EmulatorView is in Row 0, Column 1

        // Calculate screen-based available space
        // Note: screenInfo.Bounds is in PHYSICAL pixels, need to convert to logical coordinates using Scaling
        double totalScreenWidthPhysical = screenInfo.Bounds.Width;
        double totalScreenHeightPhysical = screenInfo.Bounds.Height;
        double dpiScale = screenInfo.Scaling;

        // Convert physical pixels to logical coordinates
        double totalScreenWidth = totalScreenWidthPhysical / dpiScale;
        double totalScreenHeight = totalScreenHeightPhysical / dpiScale;

        _logger.LogDebug(
          $"Screen info: Physical({totalScreenWidthPhysical:F0}x{totalScreenHeightPhysical:F0}), " +
          $"Scaling({dpiScale:F2}), Logical({totalScreenWidth:F0}x{totalScreenHeight:F0})");

        // Calculate width consumed by other columns
        // Fixed widths are already in logical coordinates (specified in XAML), so use them as-is
        double widthColumn0 = 200; // Menu column fixed width (from MainView.axaml), already in logical units
        double widthColumn2 = 0; // Stats panel width (Auto) - calculate from actual content or use reasonable estimate

        // Try to get actual widths from the grid definitions
        if (mainViewGrid.ColumnDefinitions.Count >= 3)
        {
            var col0Def = mainViewGrid.ColumnDefinitions[0];
            var col2Def = mainViewGrid.ColumnDefinitions[2];

            // Column 0: Width="200"
            if (col0Def.Width.IsAbsolute)
                widthColumn0 = col0Def.Width.Value;

            // Column 2: Width="Auto" - estimate from content if possible
            if (col2Def.Width.IsAuto)
            {
                // For auto columns, we need to estimate. Check if we can get the actual width.
                // If column 2 (stats panel) is visible, try to get its width
                var statsPanel = FindChildByGrid(mainViewGrid, 0, 2) as Border;
                if (statsPanel?.IsVisible == true && statsPanel.Bounds.Width > 0)
                {
                    widthColumn2 = statsPanel.Bounds.Width;
                }
                else
                {
                    widthColumn2 = 0; // Assume 0 if not visible
                }
            }
        }

        // Calculate height consumed by other rows
        double heightRow1 = 120; // Information area estimated minimum height, in logical units

        // Calculate available space for the EmulatorView based on screen resolution
        // All values are now in logical coordinates
        availableWidth = totalScreenWidth - widthColumn0 - widthColumn2;
        availableHeight = totalScreenHeight - heightRow1;

        // Account for padding and borders in MainView center area
        double padding = 10 * 2; // Left and right padding in the border containing the emulator, in logical units
        availableWidth -= padding;

        return true;
    }

    /// <summary>
    /// Helper method to find a child control in a grid by row and column
    /// </summary>
    private Control? FindChildByGrid(Grid grid, int row, int column)
    {
        foreach (var child in grid.Children)
        {
            if (child is Control ctrl && Grid.GetRow(ctrl) == row && Grid.GetColumn(ctrl) == column)
                return ctrl;
        }
        return null;
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

    private void StopAndDisposeUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Elapsed -= UpdateTimerElapsed;
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _updateTimer = null;
        }
    }

    private void UpdateTimerElapsed(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(UpdateTimerElapsedAsync);

    private async Task UpdateTimerElapsedAsync()
    {
        RunEmulatorOneFrame();
        await DrainPendingScriptActionsAsync();
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
            return;
        }

        // If using built-in monitor, don't update emulator state when monitor is visible. This allows stepping through code and inspecting state in the monitor without the state changing under you.
        if (_monitor?.IsVisible == true)
        {
            shouldRun = false;
            shouldReceiveInput = false;
            return;
        }

        // Block execution until external debugger connects (for boot sequence debugging)
        if (WaitForExternalDebugger)
        {
            shouldRun = false;
            shouldReceiveInput = false;
            return;
        }

        // Pause emulator when VSCode debugger is stopped (at breakpoint or stepping)
        if (IsExternalDebuggerAttached && _debugAdapter?.IsStopped == true)
        {
            shouldRun = false;
            shouldReceiveInput = false;
            return;
        }
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Show monitor if we encounter breakpoint or other break
        // BUT: Skip if an external debugger (e.g., VSCode) is attached
        // The external debugger handles breakpoints, not the built-in monitor
        if (execEvaluatorTriggerResult.Triggered && !IsExternalDebuggerAttached)
        {
            _monitor?.Enable(execEvaluatorTriggerResult);
        }
    }

    internal float Scale
    {
        get { return _emulatorConfig.CurrentDrawScale; }
        set
        {
            if (_emulatorConfig.CurrentDrawScale != value)
            {
                _emulatorConfig.CurrentDrawScale = value;
                OnPropertyChanged(nameof(Scale));
            }
        }
    }

    private ObservableCollection<string> _validationErrors = new();
    private IExecEvaluator? _originalBreakpointEvaluator;

    internal ObservableCollection<string> ValidationErrors
    {
        get => _validationErrors;
        private set
        {
            _validationErrors = value;
            OnPropertyChanged(nameof(ValidationErrors));
        }
    }

    public async Task ValidateConfigAsync()
    {
        var (isValid, errors) = await IsValidConfigWithDetails();
        ValidationErrors = new ObservableCollection<string>(errors);
    }

    private NAudioAudioHandlerContext GetAudioHandlerContext()
    {
        bool useSilentAudio = (CurrentHostSystemConfig == null || !CurrentHostSystemConfig.SystemConfig.AudioEnabled)
                             || !(PlatformDetection.IsRunningOnDesktop() || PlatformDetection.IsRunningInWebAssembly());

        float masterVolumePercent = _defaultAudioVolumePercent;

        // Check if already created
        if (_audioHandlerContext != null)
        {
            // Check if audio settings changed that requires recreating the audio handler context
            bool hasAudioChanged =
                (useSilentAudio && _audioHandlerContext != NAudioAudioHandlerContext.SilentAudioHandlerContext)
                || (!useSilentAudio && _audioHandlerContext == NAudioAudioHandlerContext.SilentAudioHandlerContext)
                || (_audioHandlerContext.WavePlayer is IUsesProfile usesProfile && EmulatorConfig.AudioSettingsProfile != usesProfile.ProfileType);

            if (hasAudioChanged)
            {
                // Audio settings changed, cleanup existing context
                _logger.LogInformation("Audio settings changed, cleaning up existing audio handler context");
                // Remember existing volume setting
                masterVolumePercent = _audioHandlerContext == NAudioAudioHandlerContext.SilentAudioHandlerContext ? _defaultAudioVolumePercent : _audioHandlerContext.MasterVolumePercent; 
                _audioHandlerContext.Cleanup();
                _audioHandlerContext = default!;
            }
            else
            {
                // Nothing changed, return existing context
                return _audioHandlerContext;
            }
        }

        // Create new context
        if (useSilentAudio)
        {
            _logger.LogInformation("Creating silent audio handler context");
            _audioHandlerContext = NAudioAudioHandlerContext.SilentAudioHandlerContext;
            return _audioHandlerContext;
        }

        // Create appropriate wave player based on platform
        var wavePlayer = _wavePlayerFactory.CreateWavePlayer();

        // Create a new context
        _audioHandlerContext = new NAudioAudioHandlerContext(
                wavePlayer,
                initialVolumePercent: masterVolumePercent,
                _loggerFactory);

        _logger.LogInformation("Created new NAudioAudioHandlerContext with wave player: " + wavePlayer.GetType().Name);

        return _audioHandlerContext;
    }

    /// <summary>
    /// Register the render control from EmulatorView to be used by the rendering pipeline.
    /// This method should be called by EmulatorView when it's ready.
    /// </summary>
    /// <param name="renderControl"></param>
    internal void RegisterRenderControl(EmulatorDisplayControlBase renderControl)
    {
        _renderControl = renderControl;
    }

    /// <summary>
    /// Toggle the visibility of the statistics panel
    /// </summary>
    internal void ToggleStatisticsPanel()
    {
        if (CurrentRunningSystem == null) return;
        SetStatisticsPanelVisible(!CurrentRunningSystem.InstrumentationEnabled);
    }

    internal void SetStatisticsPanelVisible(bool isVisible)
    {
        if (CurrentRunningSystem == null)
            return;
        CurrentRunningSystem.InstrumentationEnabled = isVisible;
        OnPropertyChanged(nameof(IsStatsPanelVisible));
    }

    // Events for publishing key events
    public event EventHandler<HostKeyEventArgs>? KeyDownEvent;
    public event EventHandler<HostKeyEventArgs>? KeyUpEvent;

    /// <summary>
    /// Receive Key Down event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="key">Layout-dependent key, used for logical shortcuts (F11/F12, ...).</param>
    /// <param name="physicalKey">Physical key position, fed to the emulator input handler.</param>
    /// <param name="modifiers"></param>
    internal void OnKeyDown(Key key, PhysicalKey physicalKey, KeyModifiers modifiers = KeyModifiers.None)
    {
        // Send event to emulator. PhysicalKey stays authoritative, but the input context can
        // normalize logical Fn-generated navigation keys such as macOS PageUp/PageDown.
        _inputHandlerContext.AddKeyDown(key, physicalKey);

        // Publish KeyDown event for subscribers
        KeyDownEvent?.Invoke(this, new HostKeyEventArgs { Key = key, KeyModifiers = modifiers });

        // Check for other emulator functions

        // If F11 is pressed, toggle statistics/debug view
        if (key == Key.F11)
        {
            _logger.LogInformation("F11 pressed - toggling statistics/debug view");
            ToggleStatisticsPanel();
        }
        // If F12 is pressed without Ctrl or Shift modifier, toggle monitor
        else if (key == Key.F12 && (modifiers & KeyModifiers.Control) == 0 && (modifiers & KeyModifiers.Shift) == 0
                 && (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
                 && !IsExternalDebuggerAttached)
        {
            _logger.LogInformation("F12 pressed - toggling monitor");
            _monitor?.Toggle();
        }
    }

    /// <summary>
    /// Receive Key Up event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    internal void OnKeyUp(Key key, PhysicalKey physicalKey, KeyModifiers modifiers = KeyModifiers.None)
    {
        // Keep key-up normalization in sync with key-down for Fn-generated navigation keys.
        _inputHandlerContext.RemoveKeyDown(key, physicalKey);

        // Publish KeyUp event for subscribers
        KeyUpEvent?.Invoke(this, new HostKeyEventArgs { Key = key, KeyModifiers = modifiers });
    }

    internal async Task PersistEmulatorConfigAsync()
    {
        if (_saveCustomConfigString == null)
            return;
        var configSectionName = EmulatorConfig.ConfigSectionName;
        var json = _emulatorConfig.GetUserSettingsJson();
        await _saveCustomConfigString(configSectionName, json, null);
    }

    internal async Task PersistConfigStringAsync(string configSectionName, string json)
    {
        if (_saveCustomConfigString == null)
            return;
        if (json == null)
            return;
        await _saveCustomConfigString(configSectionName, json, null);
    }

    public async Task PersistConfigSectionAsync(string configSectionName, IConfigurationSection configSection)
    {
        if (_saveCustomConfigSection == null)
            return;
        if (configSection == null)
            return;
        await _saveCustomConfigSection(configSectionName, configSection, null);
    }

    internal bool CanManageScripts => ScriptingEngine.CanManageScripts;
    internal bool CanLoadExamples => _loadExamples != null;
    internal async Task LoadExamplesAsync()
    {
        if (_loadExamples == null) return;
        await _loadExamples();
        ScriptingEngine.ReloadAllScripts();  // pick up newly seeded scripts and refresh UI
    }
    internal string ScriptDirectory => ScriptingEngine.ScriptDirectory;
    internal void RefreshScripts() => ScriptingEngine.ReloadAllScripts();

    internal string? LoadScriptContent(string fileName) => _loadScript?.Invoke(fileName);

    internal void SaveScript(string fileName, string content)
    {
        _saveScript?.Invoke(fileName, content);
        ScriptingEngine.UpsertScript(fileName, content);
    }

    internal void DeleteScript(string fileName)
    {
        _deleteScript?.Invoke(fileName);
        ScriptingEngine.DeleteScript(fileName);
    }

    // IRemotableHostApp — screenshot capture
    public byte[]? CaptureScreenshotPng()
    {
        var renderTarget = GetRenderTarget<AvaloniaBitmapTwoLayerRenderTarget>();
        if (renderTarget == null) return null;
        using var ms = new MemoryStream();
        renderTarget.Bitmap.Save(ms);
        return ms.ToArray();
    }
}
