using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Highbyte.DotNet6502.Impl.Avalonia.Monitor;
using Highbyte.DotNet6502.Monitor;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class MonitorViewModel : ViewModelBase
{
    private readonly AvaloniaMonitor _monitor;
    private readonly AvaloniaHostApp _hostApp;

    private string _inputText = string.Empty;

    /// <summary>
    /// Exposes the underlying AvaloniaMonitor for views that need to subscribe to its events.
    /// </summary>
    public AvaloniaMonitor Monitor => _monitor;

    /// <summary>
    /// Gets whether an external debugger is currently attached.
    /// When true, the built-in monitor should not respond to F12 key presses.
    /// </summary>
    public bool IsExternalDebuggerAttached => _hostApp.IsExternalDebuggerAttached;

    public MonitorViewModel(AvaloniaMonitor monitor, AvaloniaHostApp hostApp)
    {
        _monitor = monitor;
        _hostApp = hostApp;

        // Initialize ReactiveUI commands
        SendCommand = ReactiveCommandHelper.CreateSafeCommand(
            ExecuteSend,
            canExecute: Observable.Return(true),
            outputScheduler: RxApp.MainThreadScheduler);
        CloseCommand = ReactiveCommandHelper.CreateSafeCommand(
            ExecuteClose,
            canExecute: Observable.Return(true),
            outputScheduler: RxApp.MainThreadScheduler);
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
        // Disable the monitor directly - AvaloniaMonitor will raise PropertyChanged
        _monitor.Disable();
    }

    public CommandResult Submit()
    {
        var command = InputText?.TrimEnd() ?? string.Empty;
        InputText = string.Empty;

        // ProcessCommand will call Disable() internally for Continue/Quit commands
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
