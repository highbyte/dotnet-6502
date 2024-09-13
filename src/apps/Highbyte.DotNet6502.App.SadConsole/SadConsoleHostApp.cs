using Highbyte.DotNet6502.App.SadConsole.ConfigUI;
using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.NAudioOpenALProvider;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;
using SadConsole.Components;
using SadConsole.Configuration;
using SadConsole.Input;
using SadRogue.Primitives;
using Console = SadConsole.Console;

namespace Highbyte.DotNet6502.App.SadConsole;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator in a SadConsole Window
/// </summary>
public class SadConsoleHostApp : HostApp<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext>
{
    // --------------------
    // Injected variables
    // --------------------
    private readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly ILoggerFactory _loggerFactory;

    // --------------------
    // Other variables / constants
    // --------------------
    private ScreenObject? _sadConsoleScreen;

    private MenuConsole? _menuConsole;
    public MenuConsole MenuConsole => _menuConsole!;

    private Console? _systemMenuConsole;
    public Console? SystemMenuConsole => _systemMenuConsole;

    private EmulatorConsole? _sadConsoleEmulatorConsole;

    private MonitorConsole? _monitorConsole;
    //private MonitorConsole? _monitorConsole;
    private MonitorStatusConsole? _monitorStatusConsole;
    public event EventHandler<bool>? MonitorStateChange;
    protected virtual void OnMonitorStateChange(bool monitorEnabled)
    {
        var handler = MonitorStateChange;
        handler?.Invoke(this, monitorEnabled);
    }

    private SadConsoleRenderContext _renderContext = default!;
    private SadConsoleInputHandlerContext _inputHandlerContext = default!;
    private NAudioAudioHandlerContext _audioHandlerContext = default!;
    private InfoConsole _infoConsole;
    private const int MENU_POSITION_X = 0;
    private const int MENU_POSITION_Y = 0;

    private int StartupScreenWidth => MenuConsole.CONSOLE_WIDTH + 60;
    private int StartupScreenHeight => MenuConsole.CONSOLE_HEIGHT + 14;

    private const int STATS_UPDATE_EVERY_X_FRAME = 60 * 1;
    private const int DEBUGINFO_UPDATE_EVERY_X_FRAME = 10 * 1;

    private int _statsFrameCount = 0;
    private int _debugInfoFrameCount = 0;

    private const int LOGS_UPDATE_EVERY_X_FRAME = 60 * 1;
    private int _logsFrameCount = 0;
    private DrawImage _logoDrawImage;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public SadConsoleHostApp(
        SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NAudioAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,

        EmulatorConfig emulatorConfig,
        //IWindow window,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig
        ) : base("SadConsole", systemList, loggerFactory)
    {
        _emulatorConfig = emulatorConfig;
        //_window = window;
        _logStore = logStore;
        _logConfig = logConfig;

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(SadConsoleHostApp).Name);
    }


    public void Run()
    {
        //SetUninitializedWindow();

        //_inputContext = CreateSilkNetInput();

        _renderContext = CreateRenderContext();
        _inputHandlerContext = CreateInputHandlerContext();
        _audioHandlerContext = CreateAudioHandlerContext();

        SetContexts(() => _renderContext, () => _inputHandlerContext, () => _audioHandlerContext);
        InitRenderContext();
        InitInputHandlerContext();
        InitAudioHandlerContext();

        // ----------
        // Main SadConsole screen
        // ----------

        var uiFont = _emulatorConfig.UIFont ?? string.Empty;
        var emulatorFonts = GetHostSystemConfigs().OfType<SadConsoleHostSystemConfigBase>().Where(x => !string.IsNullOrEmpty(x.Font)).Select(x => x.Font!).ToArray();

        var builder = new Builder()
            .SetScreenSize(StartupScreenWidth, StartupScreenHeight)
            .ConfigureFonts(customDefaultFont: uiFont, extraFonts: emulatorFonts)
            .SetStartingScreen(CreateMainSadConsoleScreen)
            .IsStartingScreenFocused(false) // Let the object focused in the create method remain.
            .AddFrameUpdateEvent(UpdateSadConsole)
            .AddFrameRenderEvent(RenderSadConsole)
            ;

        Settings.WindowTitle = "Highbyte.DotNet6502 emulator + SadConsole (with NAudio)";
        Settings.ResizeMode = Settings.WindowResizeOptions.None;

        // Start SadConsole window
        Game.Create(builder);
        Game.Instance.Run();
        // Continues here after SadConsole window is closed
        Game.Instance.Dispose();
    }


    private IScreenObject CreateMainSadConsoleScreen(GameHost gameHost)
    {
        //ScreenSurface screen = new(gameInstance.ScreenCellsX, gameInstance.ScreenCellsY);
        //return screen;
        _sadConsoleScreen = new ScreenObject();

        _menuConsole = new MenuConsole(this, _loggerFactory);
        _menuConsole.Position = (MENU_POSITION_X, MENU_POSITION_Y);
        _sadConsoleScreen.Children.Add(_menuConsole);

        // Info console
        _infoConsole = new InfoConsole(this, _logStore, _logConfig);
        _infoConsole.Position = (15, 30); // Temporary position while invisible. Will be moved after a system is started and info is enabled.
        _sadConsoleScreen.Children.Add(_infoConsole);

        // Monitor status console
        _monitorStatusConsole = new MonitorStatusConsole(this);
        _monitorStatusConsole.Position = (_menuConsole.Position.X + _menuConsole.Width, _menuConsole.Position.Y + _menuConsole.Height + 1); // Temporary position while invisible. Will be moved after a system is started and monitor is enabled.
        _sadConsoleScreen.Children.Add(_monitorStatusConsole);

        // Monitor console
        _monitorConsole = new MonitorConsole(this, _emulatorConfig.Monitor, _monitorStatusConsole.Refresh);
        _monitorConsole.IsVisible = false;
        _monitorConsole.Position = (_menuConsole.Position.X + _menuConsole.Width, _menuConsole.Position.Y); // Temporary position while invisible. Will be moved after a system is started and monitor is enabled.
        MonitorStateChange += (s, monitorEnabled) =>
        {
            //_inputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);

            if (monitorEnabled)
            {
                // Monitor console
                // Position monitor to the right of the emulator console
                _monitorConsole.UsePixelPositioning = true;
                // Note: _sadConsoleEmulatorConsole has already changed to UsePixelPositioning = true, so its Position.X is in pixels (not Width though).
                var emulatorMaxX = _sadConsoleEmulatorConsole.Position.X + ((int)(_sadConsoleEmulatorConsole.Width * _sadConsoleEmulatorConsole.Font.GlyphWidth * CommonHostSystemConfig.DefaultFontSize.GetFontSizeScaleFactor()));
                var infoConsoleMax = _infoConsole != null && _infoConsole.IsVisible ? _infoConsole.Position.X + _infoConsole.WidthPixels : 0;
                _monitorConsole.Position = new Point(Math.Max(emulatorMaxX, infoConsoleMax), 0);

                // Monitor status console
                _monitorStatusConsole.UsePixelPositioning = true;
                _monitorStatusConsole.Position = new Point(_monitorConsole.Position.X, _monitorConsole.Position.Y + (_monitorConsole.Height * _monitorConsole.Font.GlyphHeight));

                _sadConsoleEmulatorConsole.IsFocused = false;
                _sadConsoleEmulatorConsole.UseKeyboard = true;
            }
            else
            {
                _sadConsoleEmulatorConsole.IsFocused = true;
                _sadConsoleEmulatorConsole.UseKeyboard = false;
            }

            // Resize main window to fit menu, emulator, monitor and other visible consoles
            Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        };
        _sadConsoleScreen.Children.Add(_monitorConsole);

        // Logo
        int logoWidthAndHeight = 256; // Pixels
        _logoDrawImage = new DrawImage("Resources/Images/logo-256.png");
        _logoDrawImage.PositionMode = DrawImage.PositionModes.Pixels;
        //var logoX = (MenuConsole.CONSOLE_WIDTH * _menuConsole.Font.GlyphWidth) + ((StartupScreenWidth - MenuConsole.CONSOLE_WIDTH) * _menuConsole.Font.GlyphWidth - logoWidthAndHeight) / 2;
        //var logoY = ((MenuConsole.CONSOLE_HEIGHT * _menuConsole.Font.GlyphHeight) - logoWidthAndHeight) / 2;
        var logoX = (MenuConsole.CONSOLE_WIDTH * _menuConsole.Font.GlyphWidth) + 10;
        var logoY = 10;
        _logoDrawImage.PositionOffset = new Point(logoX, logoY);
        _sadConsoleScreen.SadComponents.Add(_logoDrawImage);

        //_sadConsoleScreen.IsFocused = true;
        _menuConsole.IsFocused = true;

        // Trigger sadConsoleHostApp.SelectSystem call which in turn may trigger other system-specific UI stuff.
        SelectSystem(SelectedSystemName).Wait();

        return _sadConsoleScreen;
    }

    public override void OnAfterSelectSystem()
    {
        // Set the default font size configured for the system
        _menuConsole.SetEmulatorFontSize(CommonHostSystemConfig.DefaultFontSize);

        // Clear any old system specific menu console
        if (_systemMenuConsole != null)
        {
            if (_sadConsoleScreen.Children.Contains(_systemMenuConsole))
                _sadConsoleScreen.Children.Remove(_systemMenuConsole);
            _systemMenuConsole.Dispose();
            _systemMenuConsole = null;
        }
        // Create system specific menu console
        if (SelectedSystemName == "C64")
        {
            _systemMenuConsole = new C64MenuConsole(this, _loggerFactory);
            _systemMenuConsole.Position = (MENU_POSITION_X, _menuConsole.Height);
            _sadConsoleScreen.Children.Add(_systemMenuConsole);
        }

        _infoConsole.ShowSelectedSystemInfoHelp();
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Create emulator console
        if (_sadConsoleEmulatorConsole != null)
        {
            if (_sadConsoleScreen.Children.Contains(_sadConsoleEmulatorConsole))
                _sadConsoleScreen.Children.Remove(_sadConsoleEmulatorConsole);
        }

        IFont font;
        if (!string.IsNullOrEmpty(CommonHostSystemConfig.Font))
        {
            var fontFileNameWithoutExtension = Path.GetFileNameWithoutExtension(CommonHostSystemConfig.Font);
            font = Game.Instance.Fonts[fontFileNameWithoutExtension];
        }
        else
        {
            font = Game.Instance.DefaultFont;
        }
        _sadConsoleEmulatorConsole = EmulatorConsole.Create(systemAboutToBeStarted, font, CommonHostSystemConfig.DefaultFontSize, SadConsoleUISettings.CreateEmulatorConsoleDrawBoxBorderParameters(font.SolidGlyphIndex));
        _sadConsoleEmulatorConsole.UsePixelPositioning = true;
        _sadConsoleEmulatorConsole.Position = new Point((_menuConsole.Position.X * _menuConsole.Font.GlyphWidth) + (_menuConsole.Width * _menuConsole.Font.GlyphWidth), 0);
        _sadConsoleEmulatorConsole.IsFocused = true;
        _sadConsoleScreen.Children.Add(_sadConsoleEmulatorConsole);

        // Resize main window to fit menu, emulator, and other consoles
        Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());

        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        // Init monitor for current system started if this system was not started before
        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        {
            _monitorConsole.Init();
        }

        SetEmulatorConsoleFocus();

        if (_infoConsole.IsVisible)
        {
            // Enable instrumentations if info console is visible
            CurrentRunningSystem!.InstrumentationEnabled = true;

            if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
            {
                // Enable info again to calculate its position to be below emulator console
                EnableInfo();
            }
        }
    }

    public override void OnBeforeStop()
    {
        // Disable monitor if it is visible
        if (_monitorConsole.IsVisible)
            DisableMonitor();
        // Clear stats in info console if it is visible (logs will still be shown)
        if (_infoConsole.IsVisible)
            ClearInfoStats();
    }
    public override void OnAfterStop()
    {
        // Remove the console containing the running system
        if (_sadConsoleEmulatorConsole != null)
        {
            if (_sadConsoleScreen.Children.Contains(_sadConsoleEmulatorConsole))
                _sadConsoleScreen.Children.Remove(_sadConsoleEmulatorConsole);
            _sadConsoleEmulatorConsole.Dispose();
            _sadConsoleEmulatorConsole = null;
        }
    }

    public override void OnAfterClose()
    {
        // Dispose Monitor/Instrumentations panel
        //_monitor.Cleanup();

        // Cleanup contexts
        _renderContext?.Cleanup();
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();
    }

    /// <summary>
    /// Runs on every Update Frame event.
    /// Runs the emulator logic for one frame.
    /// </summary>
    /// <param name=""></param>
    private void UpdateSadConsole(object? sender, GameHost gameHost)
    {
        // Handle UI-specific keyboard inputs such as toggle monitor, info, etc.
        HandleUIKeyboardInput();

        // RunEmulatorOneFrame() will first handle input, then emulator in run for one frame.
        RunEmulatorOneFrame();
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = false;
        shouldReceiveInput = false;

        // Don't update emulator state when monitor is enabled/visible
        if (_monitorConsole.IsVisible)
            return;

        shouldRun = true;

        // Only receive input to emulator if it has focus
        if (_sadConsoleEmulatorConsole.IsFocused)
            shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Push stats to info console
        if (CurrentRunningSystem!.InstrumentationEnabled)
        {
            _statsFrameCount++;
            if (_statsFrameCount >= STATS_UPDATE_EVERY_X_FRAME)
            {
                _statsFrameCount = 0;
                _infoConsole.UpdateStats();
            }
            _debugInfoFrameCount++;
            if (_debugInfoFrameCount >= DEBUGINFO_UPDATE_EVERY_X_FRAME)
            {
                _debugInfoFrameCount = 0;
                _infoConsole.UpdateSystemDebugInfo();
            }
        }

        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
            EnableMonitor(execEvaluatorTriggerResult);
    }

    /// <summary>
    /// Runs on every Render Frame event. Draws one emulator frame on screen.
    /// </summary>
    /// <param name="args"></param>
    private void RenderSadConsole(object? sender, GameHost gameHost)
    {
        // Draw emulator on screen
        DrawFrame();

        // Push logs to info console (logs should be updated on screen even if emulator is not running)
        _logsFrameCount++;
        if (_logsFrameCount >= LOGS_UPDATE_EVERY_X_FRAME)
        {
            _logsFrameCount = 0;
            _infoConsole.UpdateLogs(); ;
        }
    }

    public override void OnBeforeDrawFrame(bool emulatorWillBeRendered)
    {
        // If any ImGui window is visible, make sure to clear Gl buffer before rendering emulator
        if (emulatorWillBeRendered)
        {
        }
    }
    public override void OnAfterDrawFrame(bool emulatorRendered)
    {
        if (emulatorRendered)
        {
        }
    }


    private SadConsoleRenderContext CreateRenderContext()
    {
        return new SadConsoleRenderContext(() => _sadConsoleEmulatorConsole);
    }

    private SadConsoleInputHandlerContext CreateInputHandlerContext()
    {
        return new SadConsoleInputHandlerContext(_loggerFactory);
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

    public SadConsoleHostSystemConfigBase CommonHostSystemConfig => (SadConsoleHostSystemConfigBase)CurrentHostSystemConfig;

    private int CalculateWindowWidthPixels()
    {
        var menuConsoleWidthPixels = _menuConsole.WidthPixels;
        var emulatorConsoleWidthPixels = Math.Max((_sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.WidthPixels : 0)
                                            , (_infoConsole != null && _infoConsole.IsVisible ? _infoConsole.WidthPixels : 0));
        var monitorConsoleWidthPixels = (_monitorConsole != null && _monitorConsole.IsVisible ? _monitorConsole.WidthPixels : 0);
        var widthPixels = menuConsoleWidthPixels + emulatorConsoleWidthPixels + monitorConsoleWidthPixels;
        return widthPixels;
    }

    private int CalculateWindowHeightPixels()
    {
        var menuConsoleHeightPixels = _menuConsole.HeightPixels + (_systemMenuConsole != null ? _systemMenuConsole.HeightPixels : 0);
        var emulatorConsoleHeightPixels = (_sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.HeightPixels + (_infoConsole.IsVisible ? _infoConsole.HeightPixels : 0) : 0);
        var monitorConsoleHeightPixels = (_monitorConsole != null && _monitorConsole.IsVisible ? _monitorConsole.HeightPixels + _monitorStatusConsole.HeightPixels : 0);

        // Calculate Max of the variables above
        var heightPixels = new int[] { menuConsoleHeightPixels, emulatorConsoleHeightPixels, monitorConsoleHeightPixels }.Max();
        return heightPixels;
    }

    public void ToggleMonitor()
    {
        // Only be able to toggle monitor if emulator is running or paused
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (_monitorConsole!.IsVisible)
        {
            DisableMonitor();
        }
        else
        {
            EnableMonitor();
        }
    }

    public void DisableMonitor()
    {
        _monitorConsole.Disable();
        _monitorStatusConsole.Disable();
        OnMonitorStateChange(monitorEnabled: false);
    }
    public void EnableMonitor(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        _monitorConsole.Enable(execEvaluatorTriggerResult);
        _monitorStatusConsole.Enable();
        OnMonitorStateChange(monitorEnabled: true);
    }

    public void ToggleInfo()
    {
        if (_infoConsole!.IsVisible)
        {
            DisableInfo();
        }
        else
        {
            EnableInfo();
        }
    }

    public void DisableInfo()
    {
        _infoConsole.Disable();
        if (CurrentRunningSystem != null)
            CurrentRunningSystem!.InstrumentationEnabled = false;

        // Enable logo when info console is disabled (as it shouldn't be covered by the info console)
        if (!_sadConsoleScreen.SadComponents.Contains(_logoDrawImage))
            _sadConsoleScreen.SadComponents.Add(_logoDrawImage);

        // Resize main window to fit menu, emulator, monitor and other visible consoles
        Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        //OnStatsStateChange(statsEnabled: false);
    }

    public void EnableInfo()
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            CurrentRunningSystem!.InstrumentationEnabled = true;

        _infoConsole.Enable();
        // Assume _sadConsoleEmulatorConsole has enabled pixel positioning
        _infoConsole.UsePixelPositioning = true;
        if (_sadConsoleEmulatorConsole != null && _sadConsoleEmulatorConsole.IsVisible)
        {
            //_infoConsole.Position = (_sadConsoleEmulatorConsole.Position.X, _sadConsoleEmulatorConsole.Position.Y + (_sadConsoleEmulatorConsole.Height * (int)(_sadConsoleEmulatorConsole.Font.GlyphHeight * _emulatorConfig.FontSizeScaleFactor)));
            _infoConsole.Position = (_sadConsoleEmulatorConsole.Position.X, _sadConsoleEmulatorConsole.Position.Y + _sadConsoleEmulatorConsole.HeightPixels);
        }
        else
        {
            _infoConsole.Position = (_menuConsole.Position.X + _menuConsole.WidthPixels, _menuConsole.Position.Y);
        }

        if (_monitorConsole.IsVisible)
            EnableMonitor(); // Re-enable to trigger calculation of monitor console position

        // Resize main window to fit menu, emulator, monitor and other visible consoles
        Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());

        // Remove logo when info console is enabled (as it may partially cover the logo)
        if (_sadConsoleScreen.SadComponents.Contains(_logoDrawImage))
            _sadConsoleScreen.SadComponents.Remove(_logoDrawImage);

        //OnStatsStateChange(statsEnabled: true);
    }
    public void ClearInfoStats()
    {
        _infoConsole.ClearStats();
        _infoConsole.ClearSystemDebugInfo();
    }

    public void SetVolumePercent(float volumePercent)
    {
        _audioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    public void SetEmulatorConsoleFocus()
    {
        if (_sadConsoleEmulatorConsole != null)
            _sadConsoleEmulatorConsole.IsFocused = true;
    }

    private void HandleUIKeyboardInput()
    {
        var keyboard = GameHost.Instance.Keyboard;
        //if (keyboard.IsKeyPressed(Keys.F10))
        //    ToggleLogs();

        if (keyboard.IsKeyPressed(Keys.F11))
            ToggleInfo();

        if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
        {
            if (keyboard.IsKeyPressed(Keys.F12))
                ToggleMonitor();
        }

        if (EmulatorState == EmulatorState.Running)
        {
            if (keyboard.IsKeyPressed(Keys.F9))
            {
                if (_systemMenuConsole is C64MenuConsole c64MenuConsole)
                    c64MenuConsole.ToggleBasicAIAssistant();
            }
        }

    }
}
