using Highbyte.DotNet6502.Systems;
using SadConsole.UI;
using SadConsole.UI.Controls;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class MonitorStatusConsole : ControlsConsole
{
    public const int CONSOLE_WIDTH = USABLE_WIDTH + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    public const int CONSOLE_HEIGHT = USABLE_HEIGHT + (SadConsoleUISettings.UI_USE_CONSOLE_BORDER ? 2 : 0);
    private const int USABLE_WIDTH = 60;
    private const int USABLE_HEIGHT = 3;

    private readonly SadConsoleHostApp _sadConsoleHostApp;

    public event EventHandler<bool>? MonitorStateChange;

    private Label _processorStatusLabel;
    private List<Label> _sysInfoLabels;
    private string _emptyInfoRow = new string(' ', USABLE_WIDTH);


    /// <summary>
    /// Console to display the monitor
    /// </summary>
    /// <param name="sadConsoleHostApp"></param>
    /// <param name="monitorConfig"></param>
    public MonitorStatusConsole(SadConsoleHostApp sadConsoleHostApp)
        : base(CONSOLE_WIDTH, CONSOLE_HEIGHT)
    {
        _sadConsoleHostApp = sadConsoleHostApp;

        IsVisible = false;
        FocusedMode = FocusBehavior.None;

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.ConsoleDrawBoxBorderParameters);

        CreateUIControls();
    }

    private void CreateUIControls()
    {
        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;

        _processorStatusLabel = CreateLabel(_emptyInfoRow, 1, 1, "processorStatusLabel");
        _sysInfoLabels = new List<Label>();
        for (int i = 0; i < 2; i++)
        {
            var sysInfoLabel = CreateLabel(_emptyInfoRow, 1, _processorStatusLabel.Position.Y + 1 + i, $"sysInfoLabel{i}");
            _sysInfoLabels.Add(sysInfoLabel);
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
            DisplayCPUStatus();
    }

    public void Refresh()
    {
        DisplayCPUStatus();
    }

    private void DisplayCPUStatus()
    {
        if (_sadConsoleHostApp.EmulatorState == EmulatorState.Uninitialized)
        {
            _processorStatusLabel.DisplayText = "";
            for (int i = 0; i < _sysInfoLabels.Count; i++)
            {
                _sysInfoLabels[i].DisplayText = "";
            }
        }
        else
        {
            var system = _sadConsoleHostApp.CurrentRunningSystem!;

            _processorStatusLabel.DisplayText = $"CPU: {OutputGen.GetProcessorState(system.CPU, includeCycles: true)}";

            for (int i = 0; i < _sysInfoLabels.Count; ++i)
            {

                if (i < system.SystemInfo.Count)
                    // TODO: Is a new string every time needed here? If system.SystemInfo items does not change, then a list of pre-created string can be initialized once and then reused..
                    _sysInfoLabels[i].DisplayText = $"SYS: {system.SystemInfo[i]}";
                else
                    _sysInfoLabels[i].DisplayText = _emptyInfoRow;
            }
        }
    }

    public void Enable(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        IsVisible = true;
        IsDirty = true; // Trigger draw of CPU status and system info
    }

    public void Disable()
    {
        IsVisible = false;
    }
}
