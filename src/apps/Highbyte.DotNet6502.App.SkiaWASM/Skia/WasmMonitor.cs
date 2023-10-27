using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia;

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

    private ushort? _lastTriggeredLoadBinaryForceLoadAddress = null;
    private Action<MonitorBase, ushort, ushort>? _lastTriggeredAfterLoadCallback = null;

    public WasmMonitor(
        IJSRuntime jsRuntime,
        SystemRunner systemRunner,
        EmulatorConfig emulatorConfig,
        Func<bool, Task> setMonitorState

        ) : base(systemRunner, emulatorConfig.Monitor)
    {
        _jsRuntime = jsRuntime;
        _setMonitorState = setMonitorState;
    }

    public async Task Enable(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        if (!_hasBeenInitializedOnce)
        {
            // Show description and general help text first time
            ShowDescription();
            WriteOutput("");
            ShowHelp();

            _hasBeenInitializedOnce = true;
        }

        if (execEvaluatorTriggerResult != null)
            base.ShowInfoAfterBreakTriggerEnabled(execEvaluatorTriggerResult);
        //else
        //    WriteOutput("Monitor enabled manually.");

        DisplayStatus();

        Visible = true;
        await _setMonitorState(true);
    }

    public async Task Disable()
    {
        Visible = false;
        await _setMonitorState(false);
    }

    public void OnKeyDown(KeyboardEventArgs e)
    {
        if (!Visible)
            return;

        if (e.Key == "F12")
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

        foreach (var sysInfoRow in SystemRunner.System.SystemInfo)
        {
            var systemStatus = $"SYS: {sysInfoRow}";
            if (Status != "")
                Status += "<br />";
            Status += BuildHtmlString(systemStatus, "header");
        }
    }

    public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        WriteOutput($"Loading file directly from url not implemented.", MessageSeverity.Warning);

        loadedAtAddress = 0;
        fileLength = 0;
        return false;
    }

    public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        // Remember what the user specified as load address (is null of not specified). 
        // This will be used later when LoadBinaryFromUser is called after user has opened file dialog and selected and uploaded file.
        _lastTriggeredLoadBinaryForceLoadAddress = forceLoadAddress;
        _lastTriggeredAfterLoadCallback = afterLoadCallback;

        // ConditionSet the html file picker dialog to open. After the file is picked and uploaded, LoadBinaryFromUser below will be called.
        _jsRuntime.InvokeVoidAsync("clickId", "monitorFilePicker");

        WriteOutput($"Waiting for file to be selected by user.");

        fileLength = 0;
        loadedAtAddress = 0;
        // Return false to indicate the file wasn't loaded now, but will be later when user has picked file from upload file dialog.
        return false;
    }

    /// <summary>
    /// Called after Blazor InputFile component callback has uploaded the user selected local file.
    /// </summary>
    /// <param name="fileData"></param>
    public async Task LoadBinaryFromUser(byte[] fileData)
    {
        try
        {
            BinaryLoader.Load(
                Mem,
                fileData,
                out ushort loadedAtAddress,
                out ushort fileLength,
                _lastTriggeredLoadBinaryForceLoadAddress);

            WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");

            // Set PC to start of loaded file.
            Cpu.PC = loadedAtAddress;

            if (_lastTriggeredAfterLoadCallback != null)
                _lastTriggeredAfterLoadCallback(this, loadedAtAddress, fileLength);

            DisplayStatus();

        }
        catch (Exception ex)
        {
            WriteOutput($"Load error: {ex.Message}", MessageSeverity.Error);
        }
    }

    public override async void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
    {
        try
        {
            // Ensure file has .prg extension if not specfied. When saving by issuing a browser file download, and saving a file with no extension, the browser will add .txt extension.
            string ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext))
                fileName += ".prg";

            var saveData = BinarySaver.BuildSaveData(Mem, startAddress, endAddress, addFileHeaderWithLoadAddress);
            var fileStream = new MemoryStream(saveData);
            using var streamRef = new DotNetStreamReference(stream: fileStream);

            // Invoke JS helper script to trigger save dialog to users browser downloads folder
            await _jsRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);

            WriteOutput($"Program downloaded to {fileName}");
        }
        catch (Exception ex)
        {
            WriteOutput($"Save error: {ex.Message}", MessageSeverity.Error);
        }
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

        _jsRuntime.InvokeVoidAsync("scrollBottom", "monitor-output");
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
