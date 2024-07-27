using System.ComponentModel;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Logging;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class InfoConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = 52 * 2 + 4;    // To roughly match C64 emulator console width. Will not look if another system with different width is used.
    public const int CONSOLE_HEIGHT = 16;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;

    private List<Label> _statsLabels;
    private string _emptyStatsRow = new string(' ', CONSOLE_WIDTH - 3);

    private ListBox _logsListBox;

    /// <summary>
    /// Console to display information, stats, and logs
    /// </summary>
    /// <param name="sadConsoleHostApp"></param>
    /// <param name="monitorConfig"></param>
    public InfoConsole(SadConsoleHostApp sadConsoleHostApp, DotNet6502InMemLogStore logStore, DotNet6502InMemLoggerConfiguration logConfig)
        : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;
        _logStore = logStore;
        _logConfig = logConfig;

        // Initially not visible. Call Init() to initialize with the current system, then Enable() to show it.
        IsVisible = false;
        FocusedMode = FocusBehavior.None;

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;

        CreateUIControls();
    }

    private void CreateUIControls()
    {

        Panel statsPanel = new Panel(10, 10); // TODO: What does size in constructor affect?
        {
            _statsLabels = new List<Label>();
            for (int i = 0; i < CONSOLE_HEIGHT - 5; i++)
            {
                var statsLabel = CreateLabel(_emptyStatsRow, 1, 1 + i, $"statsLabel{i}");
                _statsLabels.Add(statsLabel);
            }

            //Helper function to create a label and add it to the console
            Label CreateLabel(string text, int col, int row, string? name = null)
            {
                var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
                statsPanel.Add(labelTemp);
                return labelTemp;
            }
        }

        Panel logsPanel = new Panel(10, 10); // TODO: What does size in constructor affect?
        {
            var clearButton = new Button("Clear")
            {
                Name = "clearButton",
                Position = (0, 0),
            };
            clearButton.Click += (s, e) => { _logStore.Clear(); _logsListBox.Items.Clear(); IsDirty = true; };
            logsPanel.Add(clearButton);

            var systemLabel = CreateLabel("Log level:", 11, clearButton.Position.Y);
            ComboBox logLevelComboBox = new ComboBox(13, 13, 7, Enum.GetNames<LogLevel>().ToArray())
            {
                Position = (systemLabel.Bounds.MaxExtentX + 2, systemLabel.Position.Y),
                Name = "logLevelComboBox",
                SelectedItem = _logConfig.LogLevel.ToString(),
            };
            logLevelComboBox.SelectedItemChanged += (s, e) => { _logConfig.LogLevel = Enum.Parse<LogLevel>(logLevelComboBox.SelectedItem.ToString()); IsDirty = true; };
            logsPanel.Add(logLevelComboBox);


            var maxMessagesLabel = CreateLabel("Max messages:", 36, logLevelComboBox.Position.Y);
            ComboBox maxMessagesComboBox = new ComboBox(13, 13, 7, [10, 50, 100, 500])
            {
                Position = (maxMessagesLabel.Bounds.MaxExtentX + 2, maxMessagesLabel.Position.Y),
                Name = "maxMessagesComboBox",
                SelectedItem = _logStore.MaxLogMessages,
            };
            maxMessagesComboBox.SelectedItemChanged += (s, e) => { _logStore.MaxLogMessages = (int)maxMessagesComboBox.SelectedItem; IsDirty = true; };
            logsPanel.Add(maxMessagesComboBox);


            _logsListBox = new ListBox(Width - 2, Height - 6)
            {
                Name = "logsListBox",
                Position = (0, 2),
                CanFocus = false,
                IsScrollBarVisible = true,
                IsEnabled = true
            };
            logsPanel.Add(_logsListBox);

            Label CreateLabel(string text, int col, int row, string? name = null)
            {
                var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
                logsPanel.Add(labelTemp);
                return labelTemp;
            }
        }

        TabControl tab = new TabControl(new[] { new TabItem("Stats", statsPanel) { AutomaticPadding = 0 },
                                                new TabItem("Logs", logsPanel) { AutomaticPadding = 0 },
                                              },
                                              CONSOLE_WIDTH, CONSOLE_HEIGHT) { Name = "tab" };

        tab.Position = (0, 0);
        Controls.Add(tab);
    }

    protected override void OnIsDirtyChanged()
    {
    }

    public void UpdateStats()
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

    public void ClearStats()
    {
        for (int i = 0; i < _statsLabels.Count; ++i)
        {
            _statsLabels[i].DisplayText = _emptyStatsRow;
        }
    }

    public void UpdateLogs()
    {
        _logsListBox.Items.Clear();

        foreach (var line in _logStore.GetLogMessages())
        {
            var trimmedLine = line.TrimEnd('\r', '\n');
            //_logsListBox.Items.Add(new ColoredString(trimmedLine, foreground: Controls.ThemeColors.White, background: Controls.ThemeColors.ControlHostBackground));
            _logsListBox.Items.Add(trimmedLine);
        }
        _logsListBox.IsDirty = true;
    }

    public void Enable()
    {
        IsVisible = true;
        UpdateStats();
        UpdateLogs();
    }

    public void Disable()
    {
        IsVisible = false;
    }
}
