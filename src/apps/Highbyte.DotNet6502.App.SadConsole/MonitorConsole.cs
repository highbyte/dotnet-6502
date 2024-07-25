using Highbyte.DotNet6502.Monitor;
using SadConsole.Components;
using SadRogue.Primitives;
using Console = SadConsole.Console;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class MonitorConsole : Console
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 58;
    private const int USABLE_HEIGHT = 25;

    private const int CURSOR_START_Y = CONSOLE_HEIGHT - 6;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly MonitorConfig _monitorConfig;
    private SadConsoleMonitor _monitor;

    private readonly ClassicConsoleKeyboardHandler _keyboardHandlerObject;

    public event EventHandler<bool>? MonitorStateChange;
    protected virtual void OnMonitorStateChange(bool monitorEnabled)
    {
        var handler = MonitorStateChange;
        handler?.Invoke(this, monitorEnabled);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="sadConsoleHostApp"></param>
    /// <param name="monitorConfig"></param>
    public MonitorConsole(SadConsoleHostApp sadConsoleHostApp, MonitorConfig monitorConfig)
        : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
        _monitorConfig = monitorConfig;

        // Initially not visible. Call Init() to initialize with the current system, then Enable() to show it.
        IsVisible = false;

        // Custom keyboard handler that handles the cursor in the console,
        // and has call back for processing commands after pressing enter.
        _keyboardHandlerObject = new ClassicConsoleKeyboardHandler("> ");
        _keyboardHandlerObject.EnterPressedAction = EnterPressedActionHandler;
        SadComponents.Add(_keyboardHandlerObject);

        // Enable the keyboard
        UseKeyboard = true;

        // Disable the cursor because custom keyboard handler will process cursor
        Cursor.IsEnabled = false;
    }

    public void InitScreen()
    {
        // Note: Don't know why it's necessary to remove (and later add) the keyboard handler object for the Prompt to be displayed correctly.
        if (SadComponents.Contains(_keyboardHandlerObject))
            SadComponents.Remove(_keyboardHandlerObject);

        this.Clear();
        Cursor.Position = new Point(0, CURSOR_START_Y);
        _keyboardHandlerObject.CursorLastY = CURSOR_START_Y;

        DisplayInitialText();

        Surface.TimesShiftedUp = 0;

        SadComponents.Add(_keyboardHandlerObject);
    }

    private void EnterPressedActionHandler(ClassicConsoleKeyboardHandler keyboardComponent, Cursor cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        //_monitor.WriteOutput(value, MessageSeverity.Information); // The entered command has already been printed to console here
        var commandResult = _monitor.SendCommand(value);
        if (commandResult == CommandResult.Quit)
        {
            //Quit = true;
            Disable();
        }
        else if (commandResult == CommandResult.Continue)
        {
            Disable();
        }
    }

    private void MonitorOutputPrint(string message, MessageSeverity severity)
    {
        Cursor.Print($"  {message}").NewLine();
    }

    /// <summary>
    /// Initializes monitor for the current system.
    /// Should be called once after a system is started.
    /// </summary>
    public void Init()
    {
        _monitor = new SadConsoleMonitor(_sadConsoleHostApp.CurrentSystemRunner!, _monitorConfig, MonitorOutputPrint);

        InitScreen();
    }

    private void DisplayInitialText()
    {
        Cursor.DisableWordBreak = false;
        _monitor.ShowDescription();
        _monitor.WriteOutput("", MessageSeverity.Information);
        _monitor.WriteOutput("Type '?' for help.", MessageSeverity.Information);
        _monitor.WriteOutput("Type '[command] -?' for help on command.", MessageSeverity.Information);
        _monitor.WriteOutput("Examples:", MessageSeverity.Information);
        _monitor.WriteOutput("  d", MessageSeverity.Information);
        _monitor.WriteOutput("  d c000", MessageSeverity.Information);
        _monitor.WriteOutput("  m c000", MessageSeverity.Information);
        _monitor.WriteOutput("  z", MessageSeverity.Information);
        _monitor.WriteOutput("  g", MessageSeverity.Information);
        _monitor.WriteOutput("", MessageSeverity.Information);
        //_monitor.ShowHelp();
        Cursor.DisableWordBreak = true;
    }

    public void Enable(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        _monitor.Reset();   // Reset monitor working variables (like last disassembly location)

        if (execEvaluatorTriggerResult != null)
            _monitor.ShowInfoAfterBreakTriggerEnabled(execEvaluatorTriggerResult);

        IsVisible = true;
        IsFocused = true;

        OnMonitorStateChange(monitorEnabled: true);
    }

    public void Disable()
    {
        IsVisible = false;
        IsFocused = false;

        OnMonitorStateChange(monitorEnabled: false);
    }
}
