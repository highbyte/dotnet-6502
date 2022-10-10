using System.Numerics;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Highbyte.DotNet6502.App.SkiaNative
{
    public class SilkNetImgUIMonitor : MonitorBase
    {
        private readonly MonitorOptions _monitorOptions;

        public bool MonitorVisible = false;
        public bool Quit = false;

        private bool _hasBeenInitializedOnce = false;

        private IWindow _window;
        private GL _gl;
        private static IInputContext s_inputcontext;
        private IKeyboard _primaryKeyboard;
        private static ImGuiController s_ImGuiController;

        private string _monitorCmdString = "";

        private const int MONITOR_WIDTH = 620;
        private const int MONITOR_HEIGHT = 420;
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

        public void Init(IWindow window)
        {
            _window = window;
            _gl = GL.GetApi(window);
            s_inputcontext = window.CreateInput();

            // Listen to key to enable monitor
            if (s_inputcontext.Keyboards == null || s_inputcontext.Keyboards.Count == 0)
                throw new Exception("Keyboard not found");
            _primaryKeyboard = s_inputcontext.Keyboards[0];

            // Listen to special key that will show the monitor
            _primaryKeyboard.KeyDown += OnMonitorKeyDown;
        }

        private void CreateImGuiController()
        {
            s_ImGuiController = new ImGuiController(
                _gl,
                _window, // pass in our window
                s_inputcontext // input context
            );

            if (!_hasBeenInitializedOnce)
            {
                ImGuiNET.ImGui.SetWindowPos(new Vector2(10, 10));

                // Init monitor list of history commands with blanks
                for (int i = 0; i < MONITOR_CMD_HISTORY_VIEW_ROWS; i++)
                    WriteOutput("");

                // Show description and general help text first time
                ShowDescription();
                WriteOutput("");
                ShowHelp();

                _hasBeenInitializedOnce = true;
            }
        }

        private void DestroyImGuiController()
        {
            s_ImGuiController.Dispose();
        }

        public void PreOnRender(double deltaTime, bool clearOpenGL = true)
        {
            if (clearOpenGL)
                _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }

        public void PostOnRender(double deltaTime)
        {
            // Make sure ImGui is up-to-date
            s_ImGuiController.Update((float)deltaTime);

            ImGui.SetWindowSize(new Vector2(MONITOR_WIDTH, MONITOR_HEIGHT));

            ImGui.Begin($"6502 Monitor: {SystemRunner.System.Name}");
            //ImGuiNET.ImGui.Text("Output");

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
            if (!MonitorVisible)
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

            s_ImGuiController?.Render();
        }

        public void Cleanup()
        {
            s_inputcontext?.Dispose();
            _gl?.Dispose();
        }

        public void Enable()
        {
            Quit = false;
            MonitorVisible = true;
            CreateImGuiController();
            OnMonitorStateChange(true);
        }

        public void Disable()
        {
            MonitorVisible = false;
            DestroyImGuiController();
            OnMonitorStateChange(false);
        }

        private void OnMonitorKeyDown(IKeyboard keyboard, Key key, int x)
        {
            if (key == Key.F12)
            {
                if (MonitorVisible)
                    Disable();
                else
                    Enable();
            }
        }

        public override void LoadBinary(string fileName, out ushort loadedAtAddress, ushort? forceLoadAddress = null)
        {
            if (!Path.IsPathFullyQualified(fileName))
                fileName = $"{_monitorOptions.DefaultDirectory}/{fileName}";
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out int _,
                forceLoadAddress);
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
