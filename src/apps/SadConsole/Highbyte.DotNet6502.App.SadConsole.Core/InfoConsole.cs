using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Logging;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole.Core;
internal class InfoConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = 52 * 2 + 4;    // To roughly match C64 emulator console width. Will not look if another system with different width is used.
    public const int CONSOLE_HEIGHT = 16;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;

    private readonly List<Label> _statsLabels = [];
    private readonly List<Label> _statsLabelValues = [];
    private string _emptyStatsLabelRow = new string(' ', CONSOLE_WIDTH - 3 - 40);
    private string _emptyStatsLabelValueRow = new string(' ', 8);

    private ListBox? _logsListBox;

    private TabItem? _systemInfoTab;

    // Debug info panel
    private readonly List<Label> _debugInfoLabels = [];
    private readonly List<Label> _debugInfoLabelValues = [];
    private string _emptyDebugInfoLabelRow = string.Empty;
    private string _emptyDebugInfoLabelValueRow = string.Empty;

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
        var themeColors = Controls.ThemeColors ?? SadConsoleUISettings.ThemeColors;

        Panel statsPanel = new Panel(10, 10); // TODO: What does size in constructor affect?
        {
            for (int i = 0; i < CONSOLE_HEIGHT - 5; i++)
            {
                var statsLabel = CreateLabel(_emptyStatsLabelRow, 1, 1 + i, $"statsLabel{i}");
                statsLabel.TextColor = themeColors.ControlForegroundNormal;
                _statsLabels.Add(statsLabel);

                var statsLabelValue = CreateLabel(_emptyStatsLabelValueRow, 1 + statsLabel.Width + 1, 1 + i, $"statsLabelValue{i}");
                statsLabelValue.TextColor = SadConsoleUISettings.ThemeColors.White;
                _statsLabelValues.Add(statsLabelValue);
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
            clearButton.Click += (s, e) => { _logStore.Clear(); GetLogsListBoxOrThrow().Items.Clear(); IsDirty = true; };
            logsPanel.Add(clearButton);

            var systemLabel = CreateLabel("Log level:", 11, clearButton.Position.Y);
            ComboBox logLevelComboBox = new ComboBox(13, 13, 7, Enum.GetNames<LogLevel>().ToArray())
            {
                Position = (systemLabel.Bounds.MaxExtentX + 2, systemLabel.Position.Y),
                Name = "logLevelComboBox",
                SelectedItem = _logConfig.LogLevel.ToString(),
            };
            logLevelComboBox.SelectedItemChanged += (s, e) =>
            {
                if (logLevelComboBox.SelectedItem is not string logLevelName)
                    return;

                _logConfig.LogLevel = Enum.Parse<LogLevel>(logLevelName);
                IsDirty = true;
            };
            logsPanel.Add(logLevelComboBox);


            var maxMessagesLabel = CreateLabel("Max messages:", 36, logLevelComboBox.Position.Y);
            ComboBox maxMessagesComboBox = new ComboBox(13, 13, 7, [10, 50, 100, 500])
            {
                Position = (maxMessagesLabel.Bounds.MaxExtentX + 2, maxMessagesLabel.Position.Y),
                Name = "maxMessagesComboBox",
                SelectedItem = _logStore.MaxLogMessages,
            };
            maxMessagesComboBox.SelectedItemChanged += (s, e) =>
            {
                if (maxMessagesComboBox.SelectedItem is not int maxMessages)
                    return;

                _logStore.MaxLogMessages = maxMessages;
                IsDirty = true;
            };
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

        // System debug info panel
        Panel debugInfoPanel = new Panel(10, 10);
        {
            var labelTitleLength = 28;
            _emptyDebugInfoLabelRow = new string(' ', labelTitleLength + 1);
            _emptyDebugInfoLabelValueRow = new string(' ', CONSOLE_WIDTH - 3 - labelTitleLength);

            for (int i = 0; i < CONSOLE_HEIGHT - 5; i++)
            {
                var label = CreateLabel(_emptyDebugInfoLabelRow, 1, 1 + i, $"DebugInfoLabel{i}");
                label.TextColor = themeColors.ControlForegroundNormal;
                _debugInfoLabels.Add(label);

                var labelValue = CreateLabel(_emptyDebugInfoLabelValueRow, 1 + labelTitleLength, 1 + i, $"DebugInfoLabelValue{i}");
                labelValue.TextColor = SadConsoleUISettings.ThemeColors.White;
                _debugInfoLabelValues.Add(labelValue);
            }

            //Helper function to create a label and add it to the console
            Label CreateLabel(string text, int col, int row, string? name = null)
            {
                var labelTemp = new Label(text) { Position = new Point(col, row), Name = name };
                debugInfoPanel.Add(labelTemp);
                return labelTemp;
            }
        }
        List<TabItem> tabs = new(){ new TabItem("Stats", statsPanel) { AutomaticPadding = 0 },
                                    new TabItem("Logs", logsPanel) { AutomaticPadding = 0 },
                                    new TabItem("Debug info", debugInfoPanel) { AutomaticPadding = 0 },
                                    };
        TabControl tab = new TabControl(tabs, CONSOLE_WIDTH, CONSOLE_HEIGHT) { Name = "tab" };
        tab.Position = (0, 0);
        Controls.Add(tab);
    }

    private ListBox GetLogsListBoxOrThrow()
    {
        return _logsListBox ?? throw new InvalidOperationException("Logs list box is not initialized.");
    }

    private TabControl GetTabControlOrThrow()
    {
        return Controls["tab"] as TabControl ?? throw new InvalidOperationException("Info tab control is not initialized.");
    }

    protected override void OnIsDirtyChanged()
    {
    }

    public void UpdateStats()
    {
        var system = _sadConsoleHostApp.CurrentRunningSystem!;

        var stats = _sadConsoleHostApp.GetStats().Where(i => i.stat.ShouldShow()).OrderBy(i => i.name).ToList();
        // TODO: If there are more stats rows than can be displayed (i.e. not enough items in _statsLabels), then they are not displayed. Fix it?
        for (int i = 0; i < _statsLabels.Count; ++i)
        {
            if (i < stats.Count)
            {
                var statItem = stats[i];
                _statsLabels[i].DisplayText = statItem.name;
                _statsLabelValues[i].DisplayText = statItem.stat.GetDescription();
            }
            else
            {
                _statsLabels[i].DisplayText = _emptyStatsLabelRow;
            }
        }
    }

    public void ClearStats()
    {
        for (int i = 0; i < _statsLabels.Count; ++i)
        {
            _statsLabels[i].DisplayText = _emptyStatsLabelRow;
            _statsLabelValues[i].DisplayText = _emptyStatsLabelValueRow;
        }
    }

    public void UpdateLogs()
    {
        var logsListBox = GetLogsListBoxOrThrow();
        logsListBox.Items.Clear();

        foreach (var line in _logStore.GetLogMessages().ToList())
        {
            var trimmedLine = line.TrimEnd('\r', '\n');
            //_logsListBox.Items.Add(new ColoredString(trimmedLine, foreground: Controls.ThemeColors.White, background: Controls.ThemeColors.ControlHostBackground));
            logsListBox.Items.Add(trimmedLine);
        }
        logsListBox.IsDirty = true;
    }

    public void UpdateSystemDebugInfo()
    {
        var system = _sadConsoleHostApp.CurrentRunningSystem;
        if (system == null)
        {
            ClearSystemDebugInfo();
            return;
        }

        // TODO: If there are more stats rows than can be displayed (i.e. not enough items in _statsLabels), then they are not displayed. Fix it?
        for (int i = 0; i < _debugInfoLabels.Count; ++i)
        {
            if (i < system.DebugInfo.Count)
            {
                var systemDebugInfo = system.DebugInfo[i];

                _debugInfoLabels[i].DisplayText = systemDebugInfo.Key;

                // Clear any previous value (it was longer then current value)
                _debugInfoLabelValues[i].DisplayText = _emptyDebugInfoLabelValueRow;
                // Set new value
                _debugInfoLabelValues[i].DisplayText = systemDebugInfo.Value();
            }
            else
            {
                _debugInfoLabels[i].DisplayText = _emptyDebugInfoLabelRow;
                _debugInfoLabelValues[i].DisplayText = _emptyDebugInfoLabelValueRow;
            }
        }
    }

    public void ClearSystemDebugInfo()
    {
        for (int i = 0; i < _debugInfoLabels.Count; ++i)
        {
            _debugInfoLabels[i].DisplayText = _emptyDebugInfoLabelRow;
            _debugInfoLabelValues[i].DisplayText = _emptyDebugInfoLabelValueRow;
        }
    }

    /// <summary>
    /// Show system info help for the selected system, and hides other system info help.
    /// </summary>
    public void ShowSelectedSystemInfoHelp()
    {
        var tab = GetTabControlOrThrow();
        if (_systemInfoTab != null && tab.Tabs.Contains(_systemInfoTab))
        {
            tab.SetActiveTab(0);
            tab.RemoveTab(_systemInfoTab);
            _systemInfoTab = null;
        }

        var infoContribution = _sadConsoleHostApp.GetActiveInfoContribution();
        if (infoContribution != null)
        {
            _systemInfoTab = new TabItem(infoContribution.TabTitle, infoContribution.InfoPanel);
            tab.AddTab(_systemInfoTab);
        }

        tab.IsDirty = true;
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
