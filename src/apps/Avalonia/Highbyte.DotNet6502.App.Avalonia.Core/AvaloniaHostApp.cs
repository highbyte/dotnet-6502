using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using Highbyte.DotNet6502.App.Avalonia.Core.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.Monitor;
using Highbyte.DotNet6502.App.Avalonia.Core.Render;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator in an Avalonia window
/// </summary>
public class AvaloniaHostApp : HostApp<AvaloniaInputHandlerContext, NullAudioHandlerContext>, INotifyPropertyChanged
{
    private readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly bool _defaultAudioEnabled;
    private readonly float _defaultAudioVolumePercent;

    private readonly SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext> _systemList;

    private AvaloniaInputHandlerContext _inputHandlerContext = default!;
    private NullAudioHandlerContext _audioHandlerContext = default!;

    private PeriodicAsyncTimer? _updateTimer;

    private EmulatorDisplayControlBase? _renderControl;

    public bool IsStatsPanelVisible
    {
        get
        {
            if (CurrentRunningSystem == null)
                return false;
            return CurrentRunningSystem.InstrumentationEnabled;
        }
    }

    private AvaloniaMonitor? _monitor;
    public AvaloniaMonitor? Monitor => _monitor;

    public bool IsMonitorVisible => _monitor?.IsVisible ?? false;


    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Expose LoggerFactory for use in views that are note created through DI.
    public ILoggerFactory LoggerFactory => _loggerFactory;

    // Expose LogStore for use in views that are not created through DI (e.g., to display logs in the UI).
    public DotNet6502InMemLogStore? LogStore => _logStore;

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
        _loggerFactory = loggerFactory;
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
        ) : base("Avalonia", systemList, loggerFactory, useStatsNamePrefix: false)
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
                    () => _renderControl,  // Use the registered render control set by EmulatorView
                    shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                return renderloop;
            });
    }

    public override void OnAfterEmulatorStateChange()
    {
        OnPropertyChanged(nameof(EmulatorState));
    }

    public override void OnAfterSelectedSystemChanged()
    {
        OnPropertyChanged(nameof(SelectedSystemName));

        ValidateConfigAsync();
    }

    public override void OnAfterAllSystemConfigurationVariantsChanged()
    {
        OnPropertyChanged(nameof(AllSelectedSystemConfigurationVariants));
    }

    public override void OnAfterSelectedSystemVariantChanged()
    {
        OnPropertyChanged(nameof(SelectedSystemConfigurationVariant));
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

        var screen = CurrentRunningSystem!.Screen;

        // Automatically adjust scale if emulator dimensions are too wide/tall.
        Scale = GetUsefulScaleBasedOnEmulatorScreenDimensions(screen, Scale, alwaysUseMaxScale: false);

        // Create timer for current system on initial start. Assume Stop() sets _updateTimer to null.
        if (_updateTimer == null)
        {
            _updateTimer = CreateAsyncUpdateTimerForSystem(CurrentSystemRunner!.System);
        }

        _updateTimer.Start();

        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        {
            DisableMonitor();
            _monitor = new AvaloniaMonitor(CurrentSystemRunner!, _emulatorConfig.Monitor);
        }
    }

    public override void OnAfterPause()
    {
        _updateTimer?.Stop();
    }

    public override void OnBeforeStop()
    {
        SetStatisticsPanelVisible(false);

        if (_monitor != null)
        {
            if (IsMonitorVisible)
                DisableMonitor();
            _monitor = null;
        }
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

        if (IsMonitorVisible)
        {
            DisableMonitor();
            _monitor = null;
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

        // MainView's content should be a Grid
        if (mainView.Content is not Grid mainViewGrid)
            return false;

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
            return;
        }

        if (IsMonitorVisible)
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
    public ObservableCollection<string> ValidationErrors
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

    private NullAudioHandlerContext CreateAudioHandlerContext()
    {
        return new NullAudioHandlerContext();
    }

    /// <summary>
    /// Register the render control from EmulatorView to be used by the rendering pipeline.
    /// This method should be called by EmulatorView when it's ready.
    /// </summary>
    /// <param name="renderControl"></param>
    public void RegisterRenderControl(EmulatorDisplayControlBase renderControl)
    {
        _renderControl = renderControl;
    }

    /// <summary>
    /// Toggle the visibility of the statistics panel
    /// </summary>
    public void ToggleStatisticsPanel()
    {
        if (CurrentRunningSystem == null) return;
        SetStatisticsPanelVisible(!CurrentRunningSystem.InstrumentationEnabled);
    }

    public void SetStatisticsPanelVisible(bool isVisible)
    {
        if (CurrentRunningSystem == null)
            return;
        CurrentRunningSystem.InstrumentationEnabled = isVisible;
        OnPropertyChanged(nameof(IsStatsPanelVisible));
    }

    public void ToggleMonitor(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        if (_monitor == null)
            return;

        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (IsMonitorVisible)
            DisableMonitor();
        else
            EnableMonitor(execEvaluatorTriggerResult);
    }

    public void EnableMonitor(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        _monitor?.Enable(execEvaluatorTriggerResult);
        OnPropertyChanged(nameof(IsMonitorVisible));
    }

    public void DisableMonitor()
    {
        _monitor?.Disable();
        OnPropertyChanged(nameof(IsMonitorVisible));
    }

    /// <summary>
    /// Receive Key Down event in emulator canvas.
    /// Also check for special non-emulator functions such as monitor and stats/debug
    /// </summary>
    /// <param name="key"></param>
    /// <param name="modifiers"></param>
    public void OnKeyDown(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        // Send event to emulator
        _inputHandlerContext.AddKeyDown(key);

        // Check for other emulator functions

        // If F11 is pressed, toggle statistics/debug view
        if (key == Key.F11)
        {
            _logger.LogInformation("F11 pressed - toggling statistics/debug view");
            ToggleStatisticsPanel();
        }
        // If F12 is pressed without Ctrl or Shift modifier, toggle monitor
        else if (key == Key.F12 && (modifiers & KeyModifiers.Control) == 0 && (modifiers & KeyModifiers.Shift) == 0
                 && (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused))
        {
            _logger.LogInformation("F12 pressed - toggling monitor");
            ToggleMonitor();
        }
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
    public void OnKeyUp(Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        // Send event to emulator
        _inputHandlerContext.RemoveKeyDown(key);
    }
}
