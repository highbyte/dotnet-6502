using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Recording;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

public partial class MainViewModel
{
    private bool _demoBusy;
    private bool _hasPendingDemoSave;
    public bool IsDemoSectionExpanded { get; set; }
    public bool IsReplayActive => _hostApp.DemoSession?.OwnsInput == true;
    public bool IsDemoActive => _hostApp.DemoSession?.Mode is RecordingMode.Recording or RecordingMode.Playback;
    public bool CanMutateMachine => !IsDemoActive && !_demoBusy;
    public bool CanUseLiveMachineInput => !IsReplayActive && !_demoBusy;
    public bool CanStartDemoRecording => _hostApp.SupportsRecording && !IsDemoActive && !_demoBusy
        && !_hasPendingDemoSave && EmulatorState != EmulatorState.Uninitialized;
    public bool CanStopDemoRecording => !_demoBusy && _hostApp.DemoSession?.Mode == RecordingMode.Recording;
    public bool CanSaveDemo => !_demoBusy && _hasPendingDemoSave;
    public bool CanOpenDemo => !_demoBusy && _hostApp.DemoSession?.Mode != RecordingMode.Recording && !_hasPendingDemoSave;
    public bool CanControlReplay => !_demoBusy && IsReplayActive;
    public bool CanRestartDemo => !_demoBusy && _hostApp.HasLoadedDemo && _hostApp.DemoSession?.Mode != RecordingMode.Recording;
    public bool CanStepDemo => !_demoBusy && IsDemoActive && EmulatorState == EmulatorState.Paused;
    public string DemoAvailabilityText => EmulatorState == EmulatorState.Uninitialized
        ? "Start a supported machine to record, or open a demo."
        : _hostApp.SupportsRecording ? "Record input and replay demo files."
        : "Recording is unavailable for this system.";
    public bool IsDemoRecording => _hostApp.DemoSession?.Mode == RecordingMode.Recording;
    public bool IsDemoRunning => IsDemoActive && IsEmulatorRunning;
    public string DemoModeText => _hostApp.DemoSession?.Mode switch
    {
        RecordingMode.Recording => "Recording",
        RecordingMode.Playback => "Replay",
        _ => "",
    };
    public string DemoStatusText => IsDemoActive
        ? $"{DemoModeText}{(IsEmulatorPaused ? " paused" : "")} · {DemoDetailText}"
        : DemoDetailText;
    public string DemoDetailText
    {
        get
        {
            var session = _hostApp.DemoSession;
            if (session?.OwnsInput == true)
                return $"Live input disabled · {session.Frame} / {session.TotalFrames}";
            if (session?.Mode == RecordingMode.Recording)
                return $"Frame {session.Frame}";
            if (session?.CheckpointMismatch == true)
                return $"Replay diverged at frame {session.Frame} of {session.TotalFrames}";
            if (session?.Outcome == PlaybackOutcome.Failed)
                return $"Demo failed at frame {session.Frame} — see Log";
            if (_hasPendingDemoSave) return "Recording ready — save demo";
            if (session?.Outcome == PlaybackOutcome.Completed) return $"Replay complete · {session.Frame} frames";
            if (session?.Outcome == PlaybackOutcome.Stopped) return $"Replay stopped · frame {session.Frame}";
            return "";
        }
    }

    public void SetDemoBusy(bool busy) { _demoBusy = busy; RefreshDemoState(); }
    public void SetPendingDemoSave(bool pending) { _hasPendingDemoSave = pending; RefreshDemoState(); }
    public void ToggleDemoSection()
    {
        IsDemoSectionExpanded = !IsDemoSectionExpanded;
        this.RaisePropertyChanged(nameof(IsDemoSectionExpanded));
    }
    public void RefreshDemoState()
    {
        this.RaisePropertyChanged(nameof(IsReplayActive));
        this.RaisePropertyChanged(nameof(IsDemoActive));
        this.RaisePropertyChanged(nameof(CanMutateMachine));
        this.RaisePropertyChanged(nameof(CanUseLiveMachineInput));
        this.RaisePropertyChanged(nameof(CanStartDemoRecording));
        this.RaisePropertyChanged(nameof(CanStopDemoRecording));
        this.RaisePropertyChanged(nameof(CanSaveDemo));
        this.RaisePropertyChanged(nameof(CanOpenDemo));
        this.RaisePropertyChanged(nameof(CanControlReplay));
        this.RaisePropertyChanged(nameof(CanRestartDemo));
        this.RaisePropertyChanged(nameof(CanStepDemo));
        this.RaisePropertyChanged(nameof(IsDemoRecording));
        this.RaisePropertyChanged(nameof(IsDemoRunning));
        this.RaisePropertyChanged(nameof(DemoModeText));
        this.RaisePropertyChanged(nameof(DemoDetailText));
        this.RaisePropertyChanged(nameof(DemoStatusText));
        this.RaisePropertyChanged(nameof(DemoAvailabilityText));
        this.RaisePropertyChanged(nameof(CanLoadSnapshot));
        this.RaisePropertyChanged(nameof(CanUseNativeKeyboard));
    }
}
