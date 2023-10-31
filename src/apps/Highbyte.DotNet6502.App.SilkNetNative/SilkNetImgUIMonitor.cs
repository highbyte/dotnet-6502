using System.Numerics;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using NativeFileDialogSharp;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetImGuiMonitor : ISilkNetImGuiWindow
{
    private readonly MonitorConfig _monitorConfig;

    private SilkNetNativeMonitor _silkNetNativeMonitor = null!;

    public bool Visible { get; private set; }
    public bool WindowIsFocused { get; private set; }

    public bool Quit = false;


    private string _monitorCmdString = "";

    private bool _hasBeenInitializedOnce = false;

    private bool _setFocusOnInput = false;

    private const int POS_X = 300;
    private const int POS_Y = 2;
    private const int WIDTH = 720;
    private const int HEIGHT = 450;
    const int MONITOR_CMD_LINE_LENGTH = 200;

    static Vector4 s_InformationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    static Vector4 s_ErrorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    static Vector4 s_WarningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);

    static Vector4 s_StatusColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

    public event EventHandler<bool> MonitorStateChange;
    protected virtual void OnMonitorStateChange(bool monitorEnabled)
    {
        var handler = MonitorStateChange;
        handler?.Invoke(this, monitorEnabled);
    }

    public SilkNetImGuiMonitor(MonitorConfig monitorConfig)
    {
        _monitorConfig = monitorConfig;
    }

    public void Init(SystemRunner systemRunner)
    {
        _silkNetNativeMonitor = new SilkNetNativeMonitor(systemRunner, _monitorConfig);
        _hasBeenInitializedOnce = false;
    }

    public void PostOnRender()
    {
        if (_silkNetNativeMonitor == null || !Visible)
            return;

        ImGui.SetNextWindowSize(new Vector2(WIDTH, HEIGHT));
        ImGui.SetNextWindowPos(new Vector2(POS_X, POS_Y), ImGuiCond.Once);

        //ImGui.SetWindowPos(new Vector2(POS_X, POS_Y));
        //ImGui.SetWindowSize(new Vector2(WIDTH, HEIGHT));

        if (!_hasBeenInitializedOnce)
        {
            // Init monitor list of history commands with blanks
            for (int i = 0; i < SilkNetNativeMonitor.MONITOR_CMD_HISTORY_VIEW_ROWS; i++)
                _silkNetNativeMonitor.WriteOutput("");

            // Show description and general help text first time
            _silkNetNativeMonitor.ShowDescription();
            _silkNetNativeMonitor.WriteOutput("");
            _silkNetNativeMonitor.ShowHelp();

            _hasBeenInitializedOnce = true;
        }

        ImGui.Begin($"6502 Monitor: {_silkNetNativeMonitor.System.Name}");

        if (ImGui.IsWindowFocused())
        {
            _setFocusOnInput = true;
        }

        Vector4 textColor;
        foreach (var cmd in _silkNetNativeMonitor.MonitorCmdHistory)
        {
            textColor = cmd.Severity switch
            {
                MessageSeverity.Information => s_InformationColor,
                MessageSeverity.Warning => s_WarningColor,
                MessageSeverity.Error => s_ErrorColor,
                _ => s_InformationColor
            };
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.Text(cmd.Message);
            ImGui.PopStyleColor();
        }

        if (_setFocusOnInput)
        {
            ImGui.SetKeyboardFocusHere();
            _setFocusOnInput = false;
        }
        ImGui.PushItemWidth(600);
        if (ImGui.InputText("", ref _monitorCmdString, MONITOR_CMD_LINE_LENGTH, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _silkNetNativeMonitor.WriteOutput(_monitorCmdString, MessageSeverity.Information);
            var commandResult = _silkNetNativeMonitor.SendCommand(_monitorCmdString);
            _monitorCmdString = "";
            if (commandResult == CommandResult.Quit)
            {
                Quit = true;
                Disable();
            }
            else if (commandResult == CommandResult.Continue)
            {
                Disable();
            }
            _setFocusOnInput = true;
        }

        // When reaching this line, we may have destroyed the ImGui controller if we did a Quit or Continue as monitor command.
        if (!Visible)
            return;

        // CPU status
        ImGui.PushStyleColor(ImGuiCol.Text, s_StatusColor);
        ImGui.Text($"CPU: {OutputGen.GetProcessorState(_silkNetNativeMonitor.Cpu, includeCycles: true)}");
        ImGui.PopStyleColor();

        // System status
        ImGui.PushStyleColor(ImGuiCol.Text, s_StatusColor);
        foreach (var sysInfoRow in _silkNetNativeMonitor.System.SystemInfo)
        {
            ImGui.Text($"SYS: {sysInfoRow}");
        }

        ImGui.PopStyleColor();

        WindowIsFocused = ImGui.IsWindowFocused();

        ImGui.End();
    }

    public void Enable(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        Quit = false;
        Visible = true;
        _setFocusOnInput = true;
        _silkNetNativeMonitor.Reset();   // Reset monitor working variables (like last disassembly location)

        if (execEvaluatorTriggerResult != null)
            _silkNetNativeMonitor.ShowInfoAfterBreakTriggerEnabled(execEvaluatorTriggerResult);
        //else
        //    WriteOutput($"Monitor enabled manually.", MessageSeverity.Information);
        OnMonitorStateChange(true);
    }

    public void Disable()
    {
        Visible = false;
        OnMonitorStateChange(false);
    }
}
