using Highbyte.DotNet6502.Systems;
namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleMain
{
    private readonly SadConsoleConfig _sadConsoleConfig;
    private SadConsoleScreenObject _sadConsoleScreen;
    public SadConsoleScreenObject SadConsoleScreen => _sadConsoleScreen;

    private readonly SystemRunner _systemRunner;
    private int _frameCounter;

    public SadConsoleMain(
        SadConsoleConfig sadConsoleConfig,
        SystemRunner systemRunner)
    {
        _sadConsoleConfig = sadConsoleConfig;
        _systemRunner = systemRunner;
    }

    public void Run()
    {
        Settings.WindowTitle = _sadConsoleConfig.WindowTitle;

        // Setup the SadConsole engine and create the main window. 
        // If font is null or empty, the default SadConsole font will be used.
        var screen = _systemRunner.System.Screen;
        var textMode = screen as ITextMode;

        // int totalCols = (textMode.TextCols + (textMode.BorderCols * 2));
        // int totalRows = (textMode.TextRows + (textMode.BorderRows * 2));
        int totalCols = textMode.TextCols;
        int totalRows = textMode.TextRows;
        if (screen.HasBorder)
        {
            totalCols += (screen.VisibleBorderWidth / textMode.CharacterWidth) * 2;
            totalRows += (screen.VisibleBorderHeight / textMode.CharacterHeight) * 2;
        }
        global::SadConsole.Game.Create(
            totalCols * _sadConsoleConfig.FontScale,
            totalRows * _sadConsoleConfig.FontScale,
            _sadConsoleConfig.Font
            );

        //SadConsole.Game.Instance.DefaultFontSize = IFont.Sizes.One;

        // Hook the start event so we can add consoles to the system.
        global::SadConsole.Game.Instance.OnStart = InitSadConsole;

        // Hook the update event that happens each frame
        global::SadConsole.Game.Instance.FrameUpdate += UpdateSadConsole;

        // Hook the "after render"
        //SadConsole.Game.OnDraw = Screen.DrawFrame;

        // Start the game.
        global::SadConsole.Game.Instance.Run();
        global::SadConsole.Game.Instance.Dispose();
    }

    /// <summary>
    /// Runs when SadConsole engine starts up
    /// </summary>
    private void InitSadConsole()
    {
        // Create a SadConsole screen
        var screen = _systemRunner.System.Screen;
        var textMode = screen as ITextMode;
        _sadConsoleScreen = new SadConsoleScreenObject(textMode, screen, _sadConsoleConfig);

        global::SadConsole.Game.Instance.Screen = _sadConsoleScreen;
        global::SadConsole.Game.Instance.DestroyDefaultStartingConsole();

        // Start with focus on main console on current screen
        _sadConsoleScreen.IsFocused = true;
        _sadConsoleScreen.ScreenConsole.IsFocused = true;
    }

    /// <summary>
    /// Runs every frame.
    /// Responsible for letting the SadConsole engine interact with the emulator
    /// </summary>
    /// <param name="gameTime"></param>
    private void UpdateSadConsole(object sender, GameHost e)
    {
        // Run emulator for one frame
        var execEvaluatorTriggerResult = _systemRunner.RunOneFrame(out _);
        if (execEvaluatorTriggerResult.Triggered)
        {
            // Exit program
            Environment.Exit(0);
        }
    }
}
