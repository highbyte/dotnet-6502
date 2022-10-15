using System.Numerics;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Highbyte.DotNet6502.App.SkiaNative
{
    public class SilkNetImgUIMonitor : MonitorBase
    {
        private readonly MonitorOptions _monitorOptions;

        public bool Visible = false;
        public bool Quit = false;

        private string _monitorCmdString = "";

        private bool _hasBeenInitializedOnce = false;

        private const int POS_X = 10;
        private const int POS_Y = 10;
        private const int WIDTH = 620;
        private const int HEIGHT = 420;
        const int MONITOR_CMD_HISTORY_VIEW_ROWS = 20;
        const int MONITOR_CMD_LINE_LENGTH = 160;
        List<(string Message, MessageSeverity Severity)> _monitorCmdHistory = new();

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

        public SilkNetImgUIMonitor(
            SystemRunner systemRunner,
            MonitorOptions monitorOptions
            ) : base(systemRunner, monitorOptions)
        {
            _monitorOptions = monitorOptions;
        }

        public void PostOnRender(ImGuiController imGuiController, double deltaTime)
        {
            // Make sure ImGui is up-to-date
            imGuiController.Update((float)deltaTime);

            ImGui.SetWindowPos(new Vector2(POS_X, POS_Y));
            ImGui.SetWindowSize(new Vector2(WIDTH, HEIGHT));

            if (!_hasBeenInitializedOnce)
            {
                // Init monitor list of history commands with blanks
                for (int i = 0; i < MONITOR_CMD_HISTORY_VIEW_ROWS; i++)
                    WriteOutput("");

                // Show description and general help text first time
                ShowDescription();
                WriteOutput("");
                ShowHelp();

                _hasBeenInitializedOnce = true;
            }

            ImGui.Begin($"6502 Monitor: {SystemRunner.System.Name}");

            Vector4 textColor;
            foreach (var cmd in _monitorCmdHistory)
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

            ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(600);
            if (ImGui.InputText("", ref _monitorCmdString, MONITOR_CMD_LINE_LENGTH, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                WriteOutput(_monitorCmdString, MessageSeverity.Information);
                var commandResult = SendCommand(_monitorCmdString);
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
            }

            // When reaching this line, we may have destroyed the ImGui controller if we did a Quit or Continue as monitor command.
            if (!Visible)
                return;

            // CPU status
            ImGui.PushStyleColor(ImGuiCol.Text, s_StatusColor);
            ImGui.Text($"CPU: {OutputGen.GetProcessorState(Cpu, includeCycles: true)}");
            ImGui.PopStyleColor();

            // System status
            ImGui.PushStyleColor(ImGuiCol.Text, s_StatusColor);
            ImGui.Text($"SYS: {SystemRunner.System.SystemInfo}");
            ImGui.PopStyleColor();

            ImGui.End();

            imGuiController?.Render();
        }

        public void Enable()
        {
            Quit = false;
            Visible = true;
            base.Reset();   // Reset monitor working variables (like last disassembly location)
            OnMonitorStateChange(true);
        }

        public void Disable()
        {
            Visible = false;
            OnMonitorStateChange(false);
        }

        public override void LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            if (!Path.IsPathFullyQualified(fileName))
                fileName = $"{_monitorOptions.DefaultDirectory}/{fileName}";
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out fileLength,
                forceLoadAddress);
        }

        public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
        {
            if (!Path.IsPathFullyQualified(fileName))
                fileName = $"{_monitorOptions.DefaultDirectory}/{fileName}";

            BinarySaver.Save(
                Mem,
                fileName,
                startAddress,
                endAddress,
                addFileHeaderWithLoadAddress: addFileHeaderWithLoadAddress);
        }

        public override void WriteOutput(string message)
        {
            WriteOutput(message, MessageSeverity.Information);
        }

        public override void WriteOutput(string message, MessageSeverity severity)
        {
            _monitorCmdHistory.Add((message, severity));
            if (_monitorCmdHistory.Count > MONITOR_CMD_HISTORY_VIEW_ROWS)
                _monitorCmdHistory.RemoveAt(0);
        }
    }
}