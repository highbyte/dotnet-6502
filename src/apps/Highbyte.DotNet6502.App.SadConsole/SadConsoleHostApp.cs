using Highbyte.DotNet6502.App.SadConsole.ConfigUI;
using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using SadConsole.Configuration;
using SadConsole.Input;
using SadRogue.Primitives;
using Console = SadConsole.Console;


namespace Highbyte.DotNet6502.App.SadConsole;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator in a SadConsole Window
/// </summary>
public class SadConsoleHostApp : HostApp<SadConsoleRenderContext, SadConsoleInputHandlerContext, NullAudioHandlerContext>
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
    //private readonly IMapper _mapper;

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
    private NullAudioHandlerContext _audioHandlerContext = default!;
    private const int MENU_POSITION_X = 0;
    private const int MENU_POSITION_Y = 0;

    private int StartupScreenWidth => MenuConsole.CONSOLE_WIDTH + 40;
    private int StartupScreenHeight => MenuConsole.CONSOLE_HEIGHT + 14;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public SadConsoleHostApp(
        SystemList<SadConsoleRenderContext, SadConsoleInputHandlerContext, NullAudioHandlerContext> systemList,
        ILoggerFactory loggerFactory,

        EmulatorConfig emulatorConfig,
        Dictionary<string, IHostSystemConfig> hostSystemConfigs,
        //IWindow window,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig
        //IMapper mapper

        ) : base("SadConsole", systemList, hostSystemConfigs, loggerFactory)
    {
        _emulatorConfig = emulatorConfig;
        //_window = window;
        _logStore = logStore;
        _logConfig = logConfig;

        _loggerFactory = loggerFactory;
        //_mapper = mapper;
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
        string[]? emulatorFonts = string.IsNullOrEmpty(CommonHostSystemConfig.Font) ? null : [CommonHostSystemConfig.Font];

        var builder = new Builder()
            .SetScreenSize(StartupScreenWidth, StartupScreenHeight)
            .ConfigureFonts(customDefaultFont: uiFont, extraFonts: emulatorFonts)
            .SetStartingScreen(CreateMainSadConsoleScreen)
            .IsStartingScreenFocused(false) // Let the object focused in the create method remain.
            .AddFrameUpdateEvent(UpdateSadConsole)
            .AddFrameRenderEvent(RenderSadConsole)
            ;

        Settings.WindowTitle = _emulatorConfig.WindowTitle;
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

        _menuConsole = MenuConsole.Create(this);
        _menuConsole.Position = (MENU_POSITION_X, MENU_POSITION_Y);
        _sadConsoleScreen.Children.Add(_menuConsole);

        // Monitor status console
        _monitorStatusConsole = new MonitorStatusConsole(this);
        _monitorStatusConsole.Position = (_menuConsole.Position.X + _menuConsole.Width, _menuConsole.Position.Y + _menuConsole.Height + 1); // Temporary position while invisible. Will be moved after a system is started.
        _sadConsoleScreen.Children.Add(_monitorStatusConsole);

        // Monitor console
        _monitorConsole = new MonitorConsole(this, _emulatorConfig.Monitor, _monitorStatusConsole.Refresh);
        _monitorConsole.IsVisible = false;
        _monitorConsole.Position = (_menuConsole.Position.X + _menuConsole.Width, _menuConsole.Position.Y); // Temporary position while invisible. Will be moved after a system is started.
        MonitorStateChange += (s, monitorEnabled) =>
        {
            //_inputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);
            //if (monitorEnabled)
            //    statsPanel.Disable();

            if (monitorEnabled)
            {
                // Monitor console
                // Position monitor to the right of the emulator console
                _monitorConsole.UsePixelPositioning = true;
                //_monitorConsole.Position = new Point((_sadConsoleEmulatorConsole.Position.X * _sadConsoleEmulatorConsole.Font.GlyphWidth) + (_sadConsoleEmulatorConsole.Width * _sadConsoleEmulatorConsole.Font.GlyphWidth), 0);
                // Note: _sadConsoleEmulatorConsole has already changed to UsePixelPositioning = true, so its Position.X is in pixels (not Width though).
                _monitorConsole.Position = new Point(_sadConsoleEmulatorConsole.Position.X + (_sadConsoleEmulatorConsole.Width * _sadConsoleEmulatorConsole.Font.GlyphWidth), 0);
                _sadConsoleEmulatorConsole.IsFocused = false;

                // Monitor status console
                _monitorStatusConsole.UsePixelPositioning = true;
                _monitorStatusConsole.Position = new Point(_monitorConsole.Position.X, _monitorConsole.Position.Y + (_monitorConsole.Height * _monitorConsole.Font.GlyphHeight));

            }
            else
            {
                if (_sadConsoleEmulatorConsole != null)
                    _sadConsoleEmulatorConsole.IsFocused = true;
                else
                    _menuConsole.IsFocused = true;

                //if (_statsWasEnabled)
                //{
                //    CurrentRunningSystem!.InstrumentationEnabled = true;
                //    _statsPanel.Enable();
                //}
            }

            // Resize main window to fit menu, emulator, monitor and other visible consoles
            Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        };
        _sadConsoleScreen.Children.Add(_monitorConsole);


        //_sadConsoleScreen.IsFocused = true;
        _menuConsole.IsFocused = true;

        // Trigger sadConsoleHostApp.SelectSystem call which in turn may trigger other system-specific UI stuff.
        SelectSystem(SelectedSystemName);

        return _sadConsoleScreen;
    }

    public override void OnAfterSelectSystem()
    {
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
            _systemMenuConsole = C64MenuConsole.Create(this, _loggerFactory);
            _systemMenuConsole.Position = (MENU_POSITION_X, _menuConsole.Height);
            _sadConsoleScreen.Children.Add(_systemMenuConsole);
        }
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
        _sadConsoleEmulatorConsole = EmulatorConsole.Create(systemAboutToBeStarted, font, _emulatorConfig.FontSize, SadConsoleUISettings.ConsoleDrawBoxBorderParameters);
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
        _sadConsoleEmulatorConsole.IsFocused = true;
    }

    public override void OnAfterStop()
    {
        // Disable monitor if it is visible
        if (_monitorConsole.IsVisible)
        {
            _monitorConsole.Disable();
            // Resize window to that monitor console is not shown.
            Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        }

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
        // Handle UI-specific keyboard inputs such as toggle monitor, stats panel etc.
        HandleUIKeyboardInput();

        // RunEmulatorOneFrame() will first handle input, then emulator in run for one frame.
        RunEmulatorOneFrame();
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = false;
        shouldReceiveInput = false;

        // Don't update emulator state when monitor is visible
        if (_monitorConsole.IsVisible)
            return;

        shouldRun = true;

        // Only receive input to emulator if it has focus
        if (_sadConsoleEmulatorConsole.IsFocused)
            shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
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
    }


    private SadConsoleRenderContext CreateRenderContext()
    {
        return new SadConsoleRenderContext(() => _sadConsoleEmulatorConsole);
    }

    private SadConsoleInputHandlerContext CreateInputHandlerContext()
    {
        return new SadConsoleInputHandlerContext(_loggerFactory);
    }

    private NullAudioHandlerContext CreateAudioHandlerContext()
    {
        return new NullAudioHandlerContext();
    }

    private SadConsoleHostSystemConfigBase CommonHostSystemConfig => (SadConsoleHostSystemConfigBase)GetHostSystemConfig();

    private int CalculateWindowWidthPixels()
    {
        int emulatorConsoleFontSizeAdjustment;
        // TODO: This is a bit of a hack for handling consoles with different font sizes, and positioning on main screen. Better way?
        if (_emulatorConfig.FontSizeScaleFactor > 1)
        {
            emulatorConsoleFontSizeAdjustment = (((int)_emulatorConfig.FontSizeScaleFactor - 1) * 16);
        }
        else
        {
            emulatorConsoleFontSizeAdjustment = 0;
        }

        var emulatorConsoleWidthPixels = (_sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.WidthPixels + emulatorConsoleFontSizeAdjustment : 0);
        var monitorConsoleWidthPixels = (_monitorConsole != null && _monitorConsole.IsVisible ? _monitorConsole.WidthPixels : 0);
        var widthPixels = _menuConsole.WidthPixels + emulatorConsoleWidthPixels + monitorConsoleWidthPixels;
        return widthPixels;
    }

    private int CalculateWindowHeightPixels()
    {
        var heightPixels = Math.Max(_menuConsole.HeightPixels + (_systemMenuConsole != null ? _systemMenuConsole.HeightPixels : 0), _sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.HeightPixels : 0);
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

    private void HandleUIKeyboardInput()
    {
        var keyboard = GameHost.Instance.Keyboard;
        //if (keyboard.IsKeyPressed(Keys.F10))
        //    ToggleLogsPanel();

        if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
        {
            //if (keyboard.IsKeyPressed(Keys.F11))
            //    ToggleStatsPanel();
            if (keyboard.IsKeyPressed(Keys.F12))
                ToggleMonitor();
        }
    }
}
