using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class StatsConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 52 * 2 + 1;    // To roughly match C64 emulator console width. Will not look if another system with different width is used.
    private const int USABLE_HEIGHT = 16;

    private readonly SadConsoleHostApp _sadConsoleHostApp;

    public event EventHandler<bool>? MonitorStateChange;

    private List<Label> _statsLabels;

    private string _emptyStatsRow = new string(' ', USABLE_WIDTH);

    /// <summary>
    /// Console to display the monitor
    /// </summary>
    /// <param name="sadConsoleHostApp"></param>
    /// <param name="monitorConfig"></param>
    public StatsConsole(SadConsoleHostApp sadConsoleHostApp)
        : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;

        // Initially not visible. Call Init() to initialize with the current system, then Enable() to show it.
        IsVisible = false;
        FocusedMode = FocusBehavior.None;

        Surface.DefaultForeground = SadConsoleUISettings.UIConsoleForegroundColor;
        Surface.DefaultBackground = SadConsoleUISettings.UIConsoleBackgroundColor;

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.ConsoleDrawBoxBorderParameters);

        CreateUIControls();
    }

    private void CreateUIControls()
    {
        _statsLabels = new List<Label>();
        for (int i = 0; i < 2; i++)
        {
            var statsLabel = CreateLabel(_emptyStatsRow, 1, 1 + i, $"statsLabel{i}");
            _statsLabels.Add(statsLabel);
        }

        //Helper function to create a label and add it to the console
        Label CreateLabel(string text, int col, int row, string? name = null)
        {
            var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
            Controls.Add(labelTemp);
            return labelTemp;
        }
    }

    protected override void OnIsDirtyChanged()
    {
        if (IsDirty)
            DisplayStats();
    }

    public void Refresh()
    {
        DisplayStats();
    }

    private void DisplayStats()
    {
        var system = _sadConsoleHostApp.CurrentRunningSystem!;

        var statsStrings = new List<string>();
        foreach ((string name, IStat stat) in _sadConsoleHostApp.GetStats().OrderBy(i => i.name))
        {
            if (stat.ShouldShow())
            {
                string line = name + ": " + stat.GetDescription();
                statsStrings.Add(line);
            }
        };

        // TODO: If there are more stats rows than can be displayed (i.e. not enough items in _statsLabels), then they are not displayed. Fix it?
        for (int i = 0; i < _statsLabels.Count; ++i)
        {
            if (i < statsStrings.Count)
                _statsLabels[i].DisplayText = statsStrings[i];
            else
                _statsLabels[i].DisplayText = _emptyStatsRow;
        }
    }

    public void Enable()
    {
        IsVisible = true;
        IsDirty = true; // Trigger draw of stats
    }

    public void Disable()
    {
        IsVisible = false;
    }
}
