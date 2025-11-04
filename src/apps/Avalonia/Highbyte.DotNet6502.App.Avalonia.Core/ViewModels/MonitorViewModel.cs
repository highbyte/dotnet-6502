using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.Monitor;
using Highbyte.DotNet6502.Monitor;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MonitorViewModel : ViewModelBase
{
    private readonly AvaloniaMonitor _monitor;

    private string _inputText = string.Empty;

    // Event to notify when monitor should be closed (for Continue or Quit commands)
    public event EventHandler? CloseRequested;

    public MonitorViewModel(AvaloniaMonitor monitor)
    {
        _monitor = monitor;

        // Initialize ReactiveUI commands
        SendCommand = ReactiveCommand.CreateFromTask(
            ExecuteSend,
            Observable.Return(true),
            RxApp.MainThreadScheduler);
        CloseCommand = ReactiveCommand.CreateFromTask(
            ExecuteClose,
            Observable.Return(true),
            RxApp.MainThreadScheduler);
    }

    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public ObservableCollection<MonitorEntry> OutputLines => _monitor.OutputLines;

    public ObservableCollection<StatusLineEntry> StatusLines => _monitor.StatusLines;

    public string InputText
    {
        get => _inputText;
        set => this.RaiseAndSetIfChanged(ref _inputText, value);
    }

    public bool IsMonitorVisible => _monitor.IsVisible;

    private async Task ExecuteSend()
    {
        Submit();
    }

    private async Task ExecuteClose()
    {
        RequestClose();
    }

    public CommandResult Submit()
    {
        var command = InputText?.TrimEnd() ?? string.Empty;
        InputText = string.Empty;
        var result = _monitor.ProcessCommand(command);

        // Raise event if monitor should be closed
        if (result == CommandResult.Continue || result == CommandResult.Quit)
        {
            RequestClose();
        }

        return result;
    }

    /// <summary>
    /// Request the monitor to be closed. This will trigger the CloseRequested event.
    /// </summary>
    public void RequestClose()
    {
        Console.WriteLine("MonitorViewModel: RequestClose called.");
        CloseRequested?.Invoke(this, EventArgs.Empty);
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
