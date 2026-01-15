using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Impl.Avalonia.Monitor;

/// <summary>
/// Avalonia-specific monitor implementation that owns its visibility state
/// and notifies subscribers via INotifyPropertyChanged.
/// </summary>
public class AvaloniaMonitor : MonitorBase, INotifyPropertyChanged
{
    private readonly MonitorConfig _monitorConfig;
    private const int MaxOutputLines = 500;

    private readonly ObservableCollection<MonitorEntry> _outputLines = new();
    private readonly ObservableCollection<StatusLineEntry> _statusLines = new();

    private readonly List<string> _history = new();
    private int _historyIndex;
    private bool _hasBeenInitialized;
    private bool _isVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public AvaloniaMonitor(SystemRunner systemRunner, MonitorConfig monitorConfig)
        : base(systemRunner, monitorConfig)
    {
        _monitorConfig = monitorConfig;
    }

    public ObservableCollection<MonitorEntry> OutputLines => _outputLines;

    public ObservableCollection<StatusLineEntry> StatusLines => _statusLines;

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    public void Enable(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        Reset();

        if (!_hasBeenInitialized)
        {
            ShowDescription();
            WriteOutput(string.Empty);
            ShowHelp();
            _hasBeenInitialized = true;
        }

        if (execEvaluatorTriggerResult != null)
            ShowInfoAfterBreakTriggerEnabled(execEvaluatorTriggerResult);

        RefreshStatus();
        ResetHistoryNavigation();
        IsVisible = true;
    }

    public void Disable()
    {
        IsVisible = false;
        ResetHistoryNavigation();
    }

    /// <summary>
    /// Toggle the monitor visibility. If visible, disable it; otherwise enable it.
    /// </summary>
    public void Toggle(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        if (IsVisible)
            Disable();
        else
            Enable(execEvaluatorTriggerResult);
    }

    public CommandResult ProcessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return CommandResult.Ok;

        AddToHistory(command);

        AppendOutput($"> {command}", MessageSeverity.Information, isCommand: true);

        var commandResult = SendCommand(command);
        RefreshStatus();

        if (commandResult == CommandResult.Quit || commandResult == CommandResult.Continue)
            Disable();

        return commandResult;
    }

    public string? GetPreviousHistoryEntry()
    {
        if (_history.Count == 0)
            return null;

        if (_historyIndex > 0)
            _historyIndex--;

        return _historyIndex >= 0 && _historyIndex < _history.Count
            ? _history[_historyIndex]
            : null;
    }

    public string? GetNextHistoryEntry()
    {
        if (_history.Count == 0)
            return null;

        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            return _history[_historyIndex];
        }

        _historyIndex = _history.Count;
        return string.Empty;
    }

    public void RefreshStatus()
    {
        var cpuState = OutputGen.GetProcessorStateDictionary(Cpu, includeCycles: true).ToList();
        var systemInfo = SystemRunner.System.SystemInfo.ToList();

        RunOnUiThread(() =>
        {
            _statusLines.Clear();

            // First line: CPU registers
            var cpuLine = new StatusLineEntry();
            foreach (var register in cpuState)
                cpuLine.AddItem(register.Key, register.Value);
            _statusLines.Add(cpuLine);

            // Additional lines: System info
            foreach (var info in systemInfo)
            {
                var sysLine = new StatusLineEntry();
                sysLine.AddItem("SYS", info);
                _statusLines.Add(sysLine);
            }
        });
    }

    public override bool LoadBinary(string fileName, out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        if (!Path.IsPathFullyQualified(fileName) && !string.IsNullOrWhiteSpace(_monitorConfig.DefaultDirectory))
            fileName = Path.Combine(_monitorConfig.DefaultDirectory, fileName);

        if (!File.Exists(fileName))
        {
            WriteOutput($"File not found: {fileName}", MessageSeverity.Error);
            loadedAtAddress = 0;
            fileLength = 0;
            return false;
        }

        try
        {
            BinaryLoader.Load(
                Mem,
                fileName,
                out loadedAtAddress,
                out fileLength,
                forceLoadAddress);

            WriteOutput($"File loaded at {loadedAtAddress.ToHex()}, length {fileLength.ToHex()}");

            if (afterLoadCallback != null)
            {
                afterLoadCallback(this, loadedAtAddress, fileLength);
            }
            else
            {
                Cpu.PC = loadedAtAddress;
            }

            RefreshStatus();
            return true;
        }
        catch (Exception ex)
        {
            WriteOutput($"Load error: {ex.Message}", MessageSeverity.Error);
            loadedAtAddress = 0;
            fileLength = 0;
            return false;
        }
    }

    public override bool LoadBinary(out ushort loadedAtAddress, out ushort fileLength, ushort? forceLoadAddress = null, Action<MonitorBase, ushort, ushort>? afterLoadCallback = null)
    {
        WriteOutput("Loading from file picker is not implemented.", MessageSeverity.Warning);
        loadedAtAddress = 0;
        fileLength = 0;
        return false;
    }

    public override void SaveBinary(string fileName, ushort startAddress, ushort endAddress, bool addFileHeaderWithLoadAddress)
    {
        if (!Path.IsPathFullyQualified(fileName) && !string.IsNullOrWhiteSpace(_monitorConfig.DefaultDirectory))
            fileName = Path.Combine(_monitorConfig.DefaultDirectory, fileName);

        try
        {
            BinarySaver.Save(
                Mem,
                fileName,
                startAddress,
                endAddress,
                addFileHeaderWithLoadAddress: addFileHeaderWithLoadAddress);

            WriteOutput($"Program saved to {fileName}");
        }
        catch (Exception ex)
        {
            WriteOutput($"Save error: {ex.Message}", MessageSeverity.Error);
        }
    }

    public override void WriteOutput(string message)
    {
        AppendOutput(message, MessageSeverity.Information);
    }

    public override void WriteOutput(string message, MessageSeverity severity)
    {
        AppendOutput(message, severity);
    }

    private void AppendOutput(string message, MessageSeverity severity, bool isCommand = false)
    {
        RunOnUiThread(() =>
        {
            _outputLines.Add(new MonitorEntry(message, severity, isCommand));

            if (_outputLines.Count > MaxOutputLines)
                _outputLines.RemoveAt(0);
        });
    }

    private void AddToHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        _history.Add(command);
        _historyIndex = _history.Count;
    }

    private void ResetHistoryNavigation()
    {
        _historyIndex = _history.Count;
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
