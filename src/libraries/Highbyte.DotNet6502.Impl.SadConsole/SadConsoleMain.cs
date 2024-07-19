using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using SadConsole.Configuration;
namespace Highbyte.DotNet6502.Impl.SadConsole;

public class SadConsoleMain
{
    private readonly SadConsoleConfig _sadConsoleConfig;
    private SadConsoleScreenObject _sadConsoleScreen = default!;
    public SadConsoleScreenObject SadConsoleScreen => _sadConsoleScreen;

    private readonly SystemRunner _systemRunner;

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
        if (screen is not ITextMode textMode)
            throw new DotNet6502Exception("SadConsoleMain only supports system that implements ITextMode of Screen.");

        // int totalCols = (textMode.TextCols + (textMode.BorderCols * 2));
        // int totalRows = (textMode.TextRows + (textMode.BorderRows * 2));
        int totalCols = textMode.TextCols;
        int totalRows = textMode.TextRows;
        if (screen.HasBorder)
        {
            totalCols += (screen.VisibleLeftRightBorderWidth / textMode.CharacterWidth) * 2;
            totalRows += (screen.VisibleTopBottomBorderHeight / textMode.CharacterHeight) * 2;
        }

        global::SadConsole.Configuration.Builder builder
            = new global::SadConsole.Configuration.Builder()
            .SetScreenSize(totalCols * _sadConsoleConfig.FontScale, totalRows * _sadConsoleConfig.FontScale)
            .ConfigureFonts(_sadConsoleConfig.Font ?? string.Empty)
            .SetStartingScreen(CreateSadConsoleScreen)
            .IsStartingScreenFocused(false) // Let the object focused in the create method remain.
            .AddFrameUpdateEvent(UpdateSadConsole)
            ;

        // Start the game.
        global::SadConsole.Game.Create(builder);
        global::SadConsole.Game.Instance.Run();
        global::SadConsole.Game.Instance.Dispose();
    }

    /// <summary>
    /// Runs when SadConsole engine starts up
    /// </summary>
    private IScreenObject CreateSadConsoleScreen(Game game)
    {
        _sadConsoleScreen = new SadConsoleScreenObject((ITextMode)_systemRunner.System.Screen, _systemRunner.System.Screen, _sadConsoleConfig);
        _sadConsoleScreen.ScreenConsole.IsFocused = true;

        return _sadConsoleScreen;
    }

    /// <summary>
    /// Runs every frame.
    /// Responsible for letting the SadConsole engine interact with the emulator
    /// </summary>
    /// <param name="gameTime"></param>
    private void UpdateSadConsole(object? sender, GameHost e)
    {
        // Capture SadConsole input
        _systemRunner.ProcessInputBeforeFrame();

        // Run CPU for one frame
        var execEvaluatorTriggerResult = _systemRunner.RunEmulatorOneFrame();

        // Update SadConsole screen
        _systemRunner.Draw();

        if (execEvaluatorTriggerResult.Triggered)
        {
            // TODO: Show monitor?
            Debugger.Break();
            //Environment.Exit(0);
        }
    }
}
