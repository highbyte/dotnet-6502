using Highbyte.DotNet6502.Systems;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public class EmulatorStateFlags : ReactiveObject
{
    private EmulatorState _emulatorState;
    private bool _isSystemConfigValid;

    public EmulatorStateFlags(EmulatorState emulatorState)
    {
        _emulatorState = emulatorState;
        _isSystemConfigValid = false;
    }

    public EmulatorState EmulatorState
    {
        get => _emulatorState;
        set
        {
            this.RaiseAndSetIfChanged(ref _emulatorState, value);
            this.RaisePropertyChanged(string.Empty); // Notifies all properties
        }
    }

    public bool IsSystemConfigValid
    {
        get => _isSystemConfigValid;
        set
        {
            this.RaiseAndSetIfChanged(ref _isSystemConfigValid, value);
            this.RaisePropertyChanged(string.Empty); // Notifies all properties
        }
    }

    public bool IsEmulatorNotRunning => EmulatorState != EmulatorState.Running;
    public bool IsEmulatorRunning => EmulatorState == EmulatorState.Running;
    public bool IsSystemSelectionEnabled => EmulatorState == EmulatorState.Uninitialized;
    public bool IsVariantSelectionEnabled => EmulatorState == EmulatorState.Uninitialized;
    public bool IsStartButtonEnabled => _isSystemConfigValid && EmulatorState != EmulatorState.Running;
    public bool IsPauseButtonEnabled => EmulatorState == EmulatorState.Running;
    public bool IsStopButtonEnabled => EmulatorState != EmulatorState.Uninitialized;
    public bool IsResetButtonEnabled => EmulatorState != EmulatorState.Uninitialized;
    public bool IsMonitorButtonEnabled => EmulatorState != EmulatorState.Uninitialized;
    public bool IsStatsButtonEnabled => EmulatorState != EmulatorState.Uninitialized;
}
