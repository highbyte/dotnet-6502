using Highbyte.DotNet6502.Monitor;
using SadConsole.Components;
using SadRogue.Primitives;
using Console = SadConsole.Console;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class MonitorConsole : Console
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 60;
    private const int USABLE_HEIGHT = 24;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly MonitorConfig _monitorConfig;
    private readonly Action _displayCPUStatus;
    private SadConsoleMonitor _monitor;
    public SadConsoleMonitor Monitor => _monitor;
    private readonly ClassicConsoleKeyboardHandler _keyboardHandlerObject;

    /// <summary>
    /// Console to display the monitor
    /// </summary>
    /// <param name="sadConsoleHostApp"></param>
    /// <param name="monitorConfig"></param>
    public MonitorConsole(SadConsoleHostApp sadConsoleHostApp, MonitorConfig monitorConfig, Action displayCPUStatus)
        : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
        _monitorConfig = monitorConfig;
        _displayCPUStatus = displayCPUStatus;

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
        Cursor.PrintAppearanceMatchesHost = false;  // Use custom colors when using Cursor.Print

        Surface.DefaultForeground = SadConsoleUISettings.ThemeColors.ControlHostForeground;
        Surface.DefaultBackground = SadConsoleUISettings.ThemeColors.ControlHostBackground;
        _monitorConfig = monitorConfig;
    }

    private void EnterPressedActionHandler(ClassicConsoleKeyboardHandler keyboardComponent, Cursor cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        //_monitor.WriteOutput(value, MessageSeverity.Information); // The entered command has already been printed to console here
        var commandResult = _monitor.SendCommand(value);

        _displayCPUStatus(); // Trigger draw of CPU status and system info

        if (commandResult == CommandResult.Quit)
        {
            Environment.Exit(0);
        }
        else if (commandResult == CommandResult.Continue)
        {
            _sadConsoleHostApp.DisableMonitor();
        }
    }

    private void MonitorOutputPrint(string message, MessageSeverity severity)
    {
        // Select Print foreground color based on severity
        var printForeground = severity switch
        {
            MessageSeverity.Information => SadConsoleUISettings.ThemeColors.ControlHostForeground,
            MessageSeverity.Warning => SadConsoleUISettings.ThemeColors.Yellow,
            MessageSeverity.Error => SadConsoleUISettings.ThemeColors.Red,
            _ => SadConsoleUISettings.ThemeColors.ControlHostForeground
        };

        Cursor.SetPrintAppearance(printForeground, Surface.DefaultBackground);
        Cursor.Print($"  {message}").NewLine();
        Cursor.SetPrintAppearance(Surface.DefaultForeground, Surface.DefaultBackground);
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

    /// <summary>
    /// Initializes the console for first use with help text.
    /// </summary>
    private void InitScreen()
    {
        // Note: Don't know why it's necessary to remove (and later add) the keyboard handler object for the Prompt to be displayed correctly.
        if (SadComponents.Contains(_keyboardHandlerObject))
            SadComponents.Remove(_keyboardHandlerObject);

        this.Clear();
        Cursor.Position = new Point(0, CONSOLE_HEIGHT);
        _keyboardHandlerObject.CursorLastY = CONSOLE_HEIGHT;

        DisplayInitialText();

        Surface.TimesShiftedUp = 0;

        SadComponents.Add(_keyboardHandlerObject);
    }

    private void DisplayInitialText()
    {
        Cursor.DisableWordBreak = false;

        _monitor.ShowDescription();

        _monitor.WriteOutput("");
        _monitor.WriteOutput("Type '?' for help.");
        _monitor.WriteOutput("Type '[command] -?' for help on command.");
        _monitor.WriteOutput("Examples:");
        _monitor.WriteOutput("  d");
        _monitor.WriteOutput("  d c000");
        _monitor.WriteOutput("  m c000");
        _monitor.WriteOutput("  z");
        _monitor.WriteOutput("  g");
        _monitor.WriteOutput("");

        //_monitor.ShowHelp();
        Cursor.DisableWordBreak = true;
    }

    // TODO: Implement Enable/Disable SadConsoleHostApp to call this Enable/Disable + MonitorStatusConsole.Enable/Disable
    public void Enable(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        _monitor.Reset();   // Reset monitor working variables (like last disassembly location)

        if (execEvaluatorTriggerResult != null)
            _monitor.ShowInfoAfterBreakTriggerEnabled(execEvaluatorTriggerResult);

        IsVisible = true;
        IsFocused = true;

        _displayCPUStatus(); // Trigger draw of CPU status and system info
    }

    public void Disable()
    {
        IsVisible = false;
        IsFocused = false;
    }
}
