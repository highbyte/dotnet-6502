using System.Web;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWasmSkiaTest.Skia
{
    public class WasmMonitor : MonitorBase
    {
        public bool Visible { get; set; } = false;
        public string Output { get; set; } = "";
        public string Input { get; set; } = "";
        public string Status { get; set; } = "";

        private readonly List<string> _history = new List<string>();
        private int _historyIndex = 0;

        private bool _hasBeenInitializedOnce = false;
        private readonly Func<bool, Task> _setMonitorState;
        private readonly MonitorConfig _monitorConfig;

        public WasmMonitor(
            SystemRunner systemRunner,
            MonitorConfig monitorConfig,
            Func<bool, Task> setMonitorState

            ) : base(systemRunner, monitorConfig)
        {
            _monitorConfig = monitorConfig;
            _setMonitorState = setMonitorState;
        }

        public async void Enable()
        {
            Visible = true;
            await _setMonitorState(true);

            if (!_hasBeenInitializedOnce)
            {
                // Show description and general help text first time
                ShowDescription();
                WriteOutput("");
                ShowHelp();

                _hasBeenInitializedOnce = true;
            }

            DisplayStatus();
        }

        public async void Disable()
        {
            Visible = false;
            await _setMonitorState(false);
        }

        public void OnKeyDown(KeyboardEventArgs e)
        {
            if (!Visible)
                return;

            if (e.Key == "§" || e.Key == "~")
            {
                Disable();
                return;
            }

            if (e.Key == "ArrowUp" && _historyIndex > 0)
            {
                _historyIndex--;
                Input = _history[_historyIndex];
            }
            else if (e.Key == "ArrowDown" && _historyIndex + 1 < _history.Count)
            {
                _historyIndex++;
                Input = _history[_historyIndex];
            }
            // todo:  doesn't work right when typing new command.  Requires Enter to be pressed first sometimes
            // has to do with DOM focus I think
            // currently handling this with javascript as well
            else if (e.Key == "Escape")
            {
                Input = "";
                _historyIndex = _history.Count;
            }
        }

        public void OnKeyUp(KeyboardEventArgs e)
        {
            if (!Visible)
                return;

            if (e.Key != "Enter")
                return;

            var cmd = Input;
            if (!string.IsNullOrEmpty(cmd))
                _history.Add(cmd);

            _historyIndex = _history.Count;
            Input = "";

            ProcessMonitorCommand(cmd);
        }

        private void ProcessMonitorCommand(string cmd)
        {
            WriteOutput(cmd, MessageSeverity.Information);
            var commandResult = SendCommand(cmd);
            DisplayStatus();
            if (commandResult == CommandResult.Quit)
            {
                //Quit = true;
                Disable();
            }
            else if (commandResult == CommandResult.Continue)
            {
                Disable();
            }
        }

        private void DisplayStatus()
        {
            Status = "";

            var cpuStatus = $"CPU: {OutputGen.GetProcessorState(Cpu, includeCycles: true)}";
            if (Status != "")
                Status += "<br />";
            Status += $@"<span class=""info"">{HttpUtility.HtmlEncode(cpuStatus)}</span>";

            var systemStatus = $"SYS: {SystemRunner.System.SystemInfo}";
            if (Status != "")
                Status += "<br />";
            Status += $@"<span class=""info"">{HttpUtility.HtmlEncode(systemStatus)}</span>";
        }

        public override void LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            throw new NotImplementedException();
        }

        public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
        {
            throw new NotImplementedException();
        }

        public override void WriteOutput(string message)
        {
            WriteOutput(message, MessageSeverity.Information);
        }

        public override void WriteOutput(string message, MessageSeverity severity)
        {
            if (severity == MessageSeverity.Information)
                Output += $@"<br /><span class=""info"">{HttpUtility.HtmlEncode(message)}</span>";
            else if (severity == MessageSeverity.Error)
                Output += $@"<br / ><span class=""error"">{HttpUtility.HtmlEncode(message)}</span>";
            else if (severity == MessageSeverity.Warning)
                Output += $@"<br / ><span class=""warning"">{HttpUtility.HtmlEncode(message)}</span>";
        }
    }
}
