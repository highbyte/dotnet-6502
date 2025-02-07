using System.Numerics;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetImGuiMonitor : ISilkNetImGuiWindow
{
    private readonly MonitorConfig _monitorConfig;

    private SilkNetNativeMonitor _silkNetNativeMonitor = null!;

    public bool Visible { get; private set; }
    public bool WindowIsFocused { get; private set; }

    public bool Quit = false;

    private bool _scrollToEnd = false;

    private string _monitorCmdString = "";

    private bool _hasBeenInitializedOnce = false;

    private bool _setFocusOnInput = false;

    private const int POS_X = 300;
    private const int POS_Y = 2;
    private const int WIDTH = 750;
    private const int HEIGHT = 642;
    private const int MONITOR_CMD_LINE_LENGTH = 200;
    private static Vector4 s_informationColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
    private static Vector4 s_errorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
    private static Vector4 s_warningColor = new Vector4(0.5f, 0.8f, 0.8f, 1);
    private static Vector4 s_statusColor = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

    private readonly bool _autoScroll = true;

    public event EventHandler<bool>? MonitorStateChange;
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
        ImGui.SetNextWindowCollapsed(false, ImGuiCond.Appearing);

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

        ImGui.Begin($"6502 Monitor: {_silkNetNativeMonitor.System.Name}", ImGuiWindowFlags.NoScrollbar);

        if (ImGui.IsWindowFocused())
        {
            //_setFocusOnInput = true;  // TODO: This is not working ok when child window contains a scrollbar (cannot select scrollbar when clicking outside child window)
        }

        if (ImGui.BeginChild("##scrolling", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            //ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            Vector4 textColor;
            foreach (var cmd in _silkNetNativeMonitor.MonitorCmdHistory)
            {
                textColor = cmd.Severity switch
                {
                    MessageSeverity.Information => s_informationColor,
                    MessageSeverity.Warning => s_warningColor,
                    MessageSeverity.Error => s_errorColor,
                    _ => s_informationColor
                };
                ImGui.PushStyleColor(ImGuiCol.Text, textColor);
                ImGui.Text(cmd.Message);
                ImGui.PopStyleColor();
            }

            //ImGui.PopStyleVar();

            if (_autoScroll)
            {
                // If a command was entered, scroll to the bottom of the scroll region.
                if (_scrollToEnd)
                {
                    ImGui.SetScrollHereY(1.0f); // 0.0f:top, 0.5f:center, 1.0f:bottom
                    ImGui.SetScrollHereX(0.0f); // 0.0f:left, 0.5f:center, 1.0f:right
                    _scrollToEnd = false;
                }

                // Keep up at the bottom of the scroll region if we were already at the bottom at the beginning of the frame.
                // Using a scrollbar or mouse-wheel will take away from the bottom edge.
                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1.0f); // 0.0f:top, 0.5f:center, 1.0f:bottom
            }
        }
        ImGui.EndChild();

        if (_setFocusOnInput)
        {
            ImGui.SetKeyboardFocusHere();
            _setFocusOnInput = false;
        }
        ImGui.PushItemWidth(700);
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
            _scrollToEnd = true;
        }

        // When reaching this line, we may have destroyed the ImGui controller if we did a Quit or Continue as monitor command.
        if (!Visible)
            return;

        // CPU status
        ImGui.PushStyleColor(ImGuiCol.Text, s_statusColor);
        ImGui.Text($"CPU: {OutputGen.GetProcessorState(_silkNetNativeMonitor.Cpu, includeCycles: true)}");
        ImGui.PopStyleColor();

        // System status
        ImGui.PushStyleColor(ImGuiCol.Text, s_statusColor);
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
        ImGui.SetWindowFocus(null);
    }
}
