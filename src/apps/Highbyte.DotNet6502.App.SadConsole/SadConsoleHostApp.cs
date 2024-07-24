using Highbyte.DotNet6502.App.SadConsole.SystemSetup;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using SadConsole.Configuration;
using SadRogue.Primitives;


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
    private EmulatorConsole? _sadConsoleEmulatorConsole;

    private SadConsoleRenderContext _renderContext = default!;
    private SadConsoleInputHandlerContext _inputHandlerContext = default!;
    private NullAudioHandlerContext _audioHandlerContext = default!;

    private const int MENU_POSITION_X = 0;
    private const int MENU_POSITION_Y = 0;

    private int StartupScreenWidth => MenuConsole.CONSOLE_WIDTH + 40;
    private int StartupScreenHeight => MenuConsole.CONSOLE_HEIGHT;

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

        // Set the default system
        SelectSystem(_emulatorConfig.DefaultEmulator);

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


    private IScreenObject CreateMainSadConsoleScreen(Game gameInstance)
    {
        //ScreenSurface screen = new(gameInstance.ScreenCellsX, gameInstance.ScreenCellsY);
        //return screen;
        _sadConsoleScreen = new ScreenObject();

        _menuConsole = MenuConsole.Create(this);
        _menuConsole.Position = (MENU_POSITION_X, MENU_POSITION_Y);
        _sadConsoleScreen.Children.Add(_menuConsole);

        //_sadConsoleScreen.IsFocused = true;
        _menuConsole.IsFocused = true;

        return _sadConsoleScreen;
    }

    public override void OnAfterSelectSystem()
    {
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
        //// Init monitor for current system started if this system was not started before
        //if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        //    _monitor.Init(CurrentSystemRunner!);
    }

    public override void OnAfterStop()
    {
        if (_sadConsoleEmulatorConsole != null)
        {
            if (_sadConsoleScreen.Children.Contains(_sadConsoleEmulatorConsole))
                _sadConsoleScreen.Children.Remove(_sadConsoleEmulatorConsole);
            _sadConsoleEmulatorConsole.Dispose();
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
        // RunEmulatorOneFrame() will first handle input, then emulator in run for one frame.
        RunEmulatorOneFrame();
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        // TODO: Check if montior is active? If no set shouldRun to false.
        shouldRun = true;

        // TODO: Check if emulator console has focus? If not, set shouldReceiveInput to false.
        shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        //// Show monitor if we encounter breakpoint or other break
        //if (execEvaluatorTriggerResult.Triggered)
        //    _monitor.Enable(execEvaluatorTriggerResult);
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
        int fontSizeAdjustment;
        // TODO: This is a bit of a hack for handling consoles with different font sizes, and positioning on main screen. Better way?
        if (_emulatorConfig.FontSizeScaleFactor > 1)
        {
            fontSizeAdjustment = (((int)_emulatorConfig.FontSizeScaleFactor - 1) * 16);
        }
        else
        {
            fontSizeAdjustment = 0;
        }
        var width = _menuConsole.WidthPixels + (_sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.WidthPixels + fontSizeAdjustment : 0);
        return width;
    }

    private int CalculateWindowHeightPixels()
    {
        var height = Math.Max(_menuConsole.HeightPixels, _sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.HeightPixels : 0);
        return height;
    }

    //private void OnKeyDown(IKeyboard keyboard, Key key, int x)
    //{
    //    if (key == Key.F6)
    //        ToggleMainMenu();
    //    if (key == Key.F10)
    //        ToggleLogsPanel();

    //    if (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused)
    //    {
    //        if (key == Key.F11)
    //            ToggleStatsPanel();
    //        if (key == Key.F12)
    //            ToggleMonitor();
    //    }
    //}
}
