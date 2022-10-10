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
        public bool MonitorVisible = false;

        private static ImGuiController s_ImGuiController;
        private GL _gl;
        private IInputContext _inputContext;
        private string _monitorCmdString = "";

        const int MONITOR_CMD_HISTORY_VIEW_ROWS = 20;
        const int MONITOR_CMD_LINE_LENGTH = 80;
        List<(string Message, MessageSeverity Severity)> _monitorCmdHistory = new();
        private IKeyboard _primaryKeyboard;
        private readonly ISystem _system;
        public CPU Cpu { get { return _system.CPU; } }
        public Memory Mem { get { return _system.Mem; } }

        public event EventHandler<bool> MonitorStateChange;
        protected virtual void OnMonitorStateChange(bool monitorEnabled)
        {
            var handler = MonitorStateChange;
            handler?.Invoke(this, monitorEnabled);
        }


        public SilkNetImgUIMonitor(ISystem system) : base(system.CPU, system.Mem)
        {
            _system = system;
        }

        public void Init(IWindow window, IInputContext inputContext, GL? gl = null)
        {
            if (gl == null)
                _gl = GL.GetApi(window);
            else
                _gl = gl;

            _inputContext = inputContext;

            s_ImGuiController = new ImGuiController(
                _gl,
                window, // pass in our window
                inputContext // input context
            );

            InitMonitorUI();
        }

        private void InitMonitorUI()
        {
            ImGuiNET.ImGui.SetWindowPos(new Vector2(10, 10));

            // Init monitor list of history commands with blanks
            for (int i = 0; i < MONITOR_CMD_HISTORY_VIEW_ROWS; i++)
                WriteOutput("");

            // Show description and general help text first time
            ShowDescription();
            WriteOutput("");
            ShowHelp();

            // Listen to key to enable monitor
            if (_inputContext.Keyboards == null || _inputContext.Keyboards.Count == 0)
                throw new Exception("Keyboard not found");
            _primaryKeyboard = _inputContext.Keyboards[0];

            // Listen to special key that will show the monitor
            _primaryKeyboard.KeyDown += OnMonitorKeyDown;
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

            ImGui.Begin("6502 Monitor");
            ImGui.SetWindowSize(new Vector2(620, 400));
            //ImGuiNET.ImGui.Text("Commands");

            foreach (var cmd in _monitorCmdHistory)
            {
                ImGui.Text(cmd.Message);
            }

            ImGui.SetKeyboardFocusHere(0);
            ImGui.PushItemWidth(600);
            if (ImGui.InputText("", ref _monitorCmdString, MONITOR_CMD_LINE_LENGTH, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                WriteOutput(_monitorCmdString, MessageSeverity.Information);
                SendCommand(_monitorCmdString);
                _monitorCmdString = "";
                if (Quit)
                {
                    Disable();
                }
            }

            s_ImGuiController.Render();
        }

        public void Cleanup()
        {
            s_ImGuiController?.Dispose();
        }

        public void Enable()
        {
            Quit = false;
            MonitorVisible = true;
            OnMonitorStateChange(true);
        }

        public void Disable()
        {
            MonitorVisible = false;
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
