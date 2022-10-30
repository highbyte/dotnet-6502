using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Threading;
using System.Web;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

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
        private readonly IJSRuntime _jsRuntime;
        private readonly Func<bool, Task> _setMonitorState;
        private readonly MonitorConfig _monitorConfig;

        public WasmMonitor(
            IJSRuntime jsRuntime,
            SystemRunner systemRunner,
            MonitorConfig monitorConfig,
            Func<bool, Task> setMonitorState

            ) : base(systemRunner, monitorConfig)
        {
            _jsRuntime = jsRuntime;
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
            var cpuStateDictionary = OutputGen.GetProcessorStateDictionary(Cpu, includeCycles: true);

            if (Status != "")
                Status += "<br />";
            foreach (var cpuState in cpuStateDictionary)
            {
                Status += $"{BuildHtmlString(cpuState.Key, "header")}: {BuildHtmlString(cpuState.Value, "value")} ";
            }

            var systemStatus = $"SYS: {SystemRunner.System.SystemInfo}";
            if (Status != "")
                Status += "<br />";
            Status += BuildHtmlString(systemStatus, "header");
        }

        public override void LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            throw new NotImplementedException("Loading file directly from url not implemented yet.");
        }

        public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null)
        {
            // Trigger the html file picker dialog to open. After the file is picked and uploaded, LoadBinaryFromUser below will be called.
            _jsRuntime.InvokeVoidAsync("clickId", "monitorFilePicker");

            fileLength = 0;
            loadedAtAddress = 0;
            // Return false to indicate the file wasn't loaded now, but will be later when user has picked file from upload file dialog.
            return false;
        }

        /// <summary>
        /// Called after Blazor InputFile component callback has uploaded the user selected local file.
        /// </summary>
        /// <param name="fileData"></param>
        /// <param name="forceLoadAddress"></param>
        public void LoadBinaryFromUser(byte[] fileData, ushort? forceLoadAddress = null)
        {
            BinaryLoader.Load(
                Mem,
                fileData,
                out ushort loadedAtAddress,
                out ushort fileLength,
                forceLoadAddress);

            WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");
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
                Output += BuildHtmlString(message, "info", startNewLine: true);
            else if (severity == MessageSeverity.Error)
                Output += BuildHtmlString(message, "error", startNewLine: true);
            else if (severity == MessageSeverity.Warning)
                Output += BuildHtmlString(message, "warning", startNewLine: true);
        }

        private string BuildHtmlString(string message, string cssClass, bool startNewLine = false)
        {
            string html = "";
            if (startNewLine)
                html += "<br />";
            html += $@"<span class=""{cssClass}"">{HttpUtility.HtmlEncode(message)}</span>";
            return html;
            
        }
    }
}
