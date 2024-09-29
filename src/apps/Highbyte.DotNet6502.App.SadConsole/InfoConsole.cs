using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class InfoConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = 52 * 2 + 4;    // To roughly match C64 emulator console width. Will not look if another system with different width is used.
    public const int CONSOLE_HEIGHT = 16;

    private readonly SadConsoleHostApp _sadConsoleHostApp;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;

    private List<Label> _statsLabels;
    private List<Label> _statsLabelValues;
    private string _emptyStatsLabelRow = new string(' ', CONSOLE_WIDTH - 3 - 40);
    private string _emptyStatsLabelValueRow = new string(' ', 8);

    private ListBox _logsListBox;

    private List<KeyValuePair<string, Panel>> _systemInfoPanels = new ();

    // Debug info panel
    private List<Label> _debugInfoLabels;
    private List<Label> _debugInfoLabelValues;
    private string _emptyDebugInfoLabelRow;
    private string _emptyDebugInfoLabelValueRow;

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
            _statsLabelValues = new List<Label>();
            for (int i = 0; i < CONSOLE_HEIGHT - 5; i++)
            {
                var statsLabel = CreateLabel(_emptyStatsLabelRow, 1, 1 + i, $"statsLabel{i}");
                statsLabel.TextColor = Controls.ThemeColors.ControlForegroundNormal;
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

        // System debug info panel
        Panel debugInfoPanel = new Panel(10, 10);
        {
            var labelTitleLength = 25;
            _debugInfoLabels = new List<Label>();
            _debugInfoLabelValues = new List<Label>();

            _emptyDebugInfoLabelRow = new string(' ', labelTitleLength + 1);
            _emptyDebugInfoLabelValueRow = new string(' ', CONSOLE_WIDTH - 3 - labelTitleLength);

            for (int i = 0; i < CONSOLE_HEIGHT - 5; i++)
            {
                var label = CreateLabel(_emptyDebugInfoLabelRow, 1, 1 + i, $"DebugInfoLabel{i}");
                label.TextColor = Controls.ThemeColors.ControlForegroundNormal;
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


        // C64-specific info panel
        Panel c64SystemInfoPanel = new Panel(10, 10);
        {
            const int colTab1 = 0; const int colTab2 = 30; const int colTab3 = 60;
            int row = 0;
            CreateLabel("C64 keyboard mapping", colTab1, row, Controls.ThemeColors.White);
            row++;
            CreateLabel("Command", colTab1, row, Controls.ThemeColors.Title);
            CreateLabel("C64 key", colTab2, row, Controls.ThemeColors.Title);
            CreateLabel("PC/Mac key", colTab3, row, Controls.ThemeColors.Title);
            row++;
            CreateLabel("Stop run Basic prg", colTab1, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("Run/stop", colTab2, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("Esc", colTab3, row, Controls.ThemeColors.ControlHostForeground);
            row++;
            CreateLabel("Soft reset", colTab1, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("Run/Stop + Restore", colTab2, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("Esc + PgUp (fn+ArrowUp on Mac)", colTab3, row, Controls.ThemeColors.ControlHostForeground);
            row++;
            CreateLabel("Change text color 1-8", colTab1, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("CTRL + numbers 1-8", colTab2, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("Tab + numbers 1-8", colTab3, row, Controls.ThemeColors.ControlHostForeground);
            row++;
            CreateLabel("Change text color 9-16", colTab1, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("C= + numbers 1-8", colTab2, row, Controls.ThemeColors.ControlHostForeground);
            CreateLabel("LeftCtrl + numbers 1-8", colTab3, row, Controls.ThemeColors.ControlHostForeground);

            Label CreateLabel(string text, int col, int row, Color? textColor = null, string? name = null)
            {
                if (textColor == null)
                    textColor = Surface.DefaultForeground;
                var labelTemp = new Label(text) { Position = new Point(col, row), Name = name, TextColor = textColor };
                c64SystemInfoPanel.Add(labelTemp);
                return labelTemp;
            }

            _systemInfoPanels.Add(new KeyValuePair<string, Panel>(key: C64.SystemName, value: c64SystemInfoPanel));
        }


        // Generic-specific info panel
        Panel genericSystemInfoPanel = new Panel(10, 10); // TODO: What does size in constructor affect?
        {
            int row = 0;
            CreateLabel("A generic 6502-based computer, with custom defined memory layout and IO functionallity.", 0, row, Controls.ThemeColors.ControlHostForeground);

            Label CreateLabel(string text, int col, int row, Color? textColor = null, string? name = null)
            {
                if (textColor == null)
                    textColor = Surface.DefaultForeground;
                var labelTemp = new Label(text) { Position = new Point(col, row), Name = name, TextColor = textColor };
                genericSystemInfoPanel.Add(labelTemp);
                return labelTemp;
            }
            _systemInfoPanels.Add(new KeyValuePair<string, Panel>(key: GenericComputer.SystemName, value: genericSystemInfoPanel));
        }


        List<TabItem> tabs = new(){ new TabItem("Stats", statsPanel) { AutomaticPadding = 0 },
                                    new TabItem("Logs", logsPanel) { AutomaticPadding = 0 },
                                    new TabItem("Debug info", debugInfoPanel) { AutomaticPadding = 0 },
                                    };
        TabControl tab = new TabControl(tabs, CONSOLE_WIDTH, CONSOLE_HEIGHT) { Name = "tab" };
        tab.Position = (0, 0);
        Controls.Add(tab);
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
        _logsListBox.Items.Clear();

        foreach (var line in _logStore.GetLogMessages().ToList())
        {
            var trimmedLine = line.TrimEnd('\r', '\n');
            //_logsListBox.Items.Add(new ColoredString(trimmedLine, foreground: Controls.ThemeColors.White, background: Controls.ThemeColors.ControlHostBackground));
            _logsListBox.Items.Add(trimmedLine);
        }
        _logsListBox.IsDirty = true;
    }

    public void UpdateSystemDebugInfo()
    {
        if (_sadConsoleHostApp.CurrentRunningSystem is C64 c64System)
        {
            UpdateC64DebugInfo();
        }
    }

    private void UpdateC64DebugInfo()
    {
        var c64 = (C64)_sadConsoleHostApp.CurrentRunningSystem!;

        // TODO: If there are more stats rows than can be displayed (i.e. not enough items in _statsLabels), then they are not displayed. Fix it?
        for (int i = 0; i < _debugInfoLabels.Count; ++i)
        {
            if (i < c64.DebugInfo.Count)
            {
                var c64DebugInfo = c64.DebugInfo[i];

                _debugInfoLabels[i].DisplayText = c64DebugInfo.Key;

                // Clear any previous value (it was longer then current value)
                _debugInfoLabelValues[i].DisplayText = _emptyDebugInfoLabelValueRow;
                // Set new value
                _debugInfoLabelValues[i].DisplayText = c64DebugInfo.Value();
            }
        }
    }
    public void ClearSystemDebugInfo()
    {
        if (_sadConsoleHostApp.CurrentRunningSystem is C64 c64System)
        {
            ClearC64DebugInfo();
        }
    }

    private void ClearC64DebugInfo()
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
        var tab = Controls["tab"] as TabControl;
        // Remove existing system info panel
        foreach (var systemInfoPanel in _systemInfoPanels)
        {
            var tabContainingExistingSystemInfoPanel = tab.Tabs.SingleOrDefault(x => x.Content == systemInfoPanel.Value);
            if (tabContainingExistingSystemInfoPanel != null)
            {
                tab.SetActiveTab(0);
                tab.RemoveTab(tabContainingExistingSystemInfoPanel);
            }
        }

        // Add current selected system info panel
        int systemInfoPanelNo = 1;
        foreach (var systemInfoPanel in _systemInfoPanels)
        {
            if (systemInfoPanel.Key == _sadConsoleHostApp.SelectedSystemName)
            {
                var tabHeader = systemInfoPanelNo == 1 ? $"{_sadConsoleHostApp.SelectedSystemName} info" : $"{_sadConsoleHostApp.SelectedSystemName} info {systemInfoPanelNo}";
                var tabItem = new TabItem($"{tabHeader}", systemInfoPanel.Value);
                tab.AddTab(tabItem);

                systemInfoPanelNo++;
            }
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
