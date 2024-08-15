using System.Net.NetworkInformation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
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

    private const int NUMBER_OF_SYS_INFO_ROWS = USABLE_HEIGHT - 1;  // 1 row for CPU state
    private string _emptyRow = new string(' ', USABLE_WIDTH);


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
        Surface.UsePrintProcessor = true;

        Controls.ThemeColors = SadConsoleUISettings.ThemeColors;
        Surface.DefaultBackground = Controls.ThemeColors.ControlHostBackground;
        Surface.DefaultForeground = Controls.ThemeColors.ControlHostForeground;
        Surface.Clear();

        if (SadConsoleUISettings.UI_USE_CONSOLE_BORDER)
            Surface.DrawBox(new Rectangle(0, 0, Width, Height), SadConsoleUISettings.UIConsoleDrawBoxBorderParameters);

        CreateUIControls();
    }

    private void CreateUIControls()
    {
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
            Surface.Print(1, 1, _emptyRow);
            for (int i = 0; i < NUMBER_OF_SYS_INFO_ROWS; i++)
            {
                Surface.Print(1, 2 + i, _emptyRow);
            }
        }
        else
        {
            var system = _sadConsoleHostApp.CurrentRunningSystem!;

            var cpuStateDictionary = OutputGen.GetProcessorStateDictionary(system.CPU, includeCycles: true);
            int row = 1;
            int col = 1;
            const string separator = ": ";
            foreach (var cpuState in cpuStateDictionary)
            {
                Surface.Print(col, 1, cpuState.Key, foreground: Controls.ThemeColors.ControlHostForeground, background: Controls.ThemeColors.ControlHostBackground);
                col += cpuState.Key.Length;
                Surface.Print(col, 1, separator, foreground: Controls.ThemeColors.ControlHostForeground, background: Controls.ThemeColors.ControlHostBackground);
                col += separator.Length;
                Surface.Print(col, 1, cpuState.Value, foreground: Controls.ThemeColors.White, background: Controls.ThemeColors.ControlHostBackground);
                col += cpuState.Value.Length + 1;
            }

            row = 2;
            col = 1;
            for (int i = 0; i < NUMBER_OF_SYS_INFO_ROWS; ++i)
            {
                if (i < system.SystemInfo.Count)
                    // TODO: Is a new string every time needed here? If system.SystemInfo items does not change, then a list of pre-created string can be initialized once and then reused..
                    Surface.Print(col, row, $"SYS: {system.SystemInfo[i]}", foreground: Controls.ThemeColors.ControlHostForeground, background: Controls.ThemeColors.ControlHostBackground);
                else
                    Surface.Print(col, row, _emptyRow, foreground: Controls.ThemeColors.ControlHostForeground, background: Controls.ThemeColors.ControlHostBackground);
                row++;
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
