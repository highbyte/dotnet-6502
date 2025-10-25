using System.Collections.ObjectModel;
using Highbyte.DotNet6502.App.Avalonia.Core.Monitor;
using Highbyte.DotNet6502.Monitor;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MonitorViewModel : ViewModelBase
{
    private readonly AvaloniaMonitor _monitor;

    private string _inputText = string.Empty;

    public MonitorViewModel(AvaloniaMonitor monitor)
    {
        _monitor = monitor;
    }

    public ObservableCollection<MonitorEntry> OutputLines => _monitor.OutputLines;

    public ObservableCollection<string> StatusLines => _monitor.StatusLines;

    public string InputText
    {
        get => _inputText;
        set => this.RaiseAndSetIfChanged(ref _inputText, value);
    }

    public bool IsMonitorVisible => _monitor.IsVisible;

    public CommandResult Submit()
    {
        var command = InputText?.TrimEnd() ?? string.Empty;
        InputText = string.Empty;
        return _monitor.ProcessCommand(command);
    }

    public void NavigateHistoryPrevious()
    {
        var previous = _monitor.GetPreviousHistoryEntry();
        if (previous != null)
            InputText = previous;
    }

    public void NavigateHistoryNext()
    {
        var next = _monitor.GetNextHistoryEntry();
        if (next != null)
            InputText = next;
    }

    public void RefreshStatus()
    {
        _monitor.RefreshStatus();
    }

    public void ClearInput()
    {
        InputText = string.Empty;
    }
}
