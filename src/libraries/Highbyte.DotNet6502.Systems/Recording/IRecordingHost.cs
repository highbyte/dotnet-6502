namespace Highbyte.DotNet6502.Systems.Recording;

public interface IRecordingHost
{
    RecordingSession? DemoSession { get; }
    bool IsPreparingPlayback { get; }
    bool SupportsRecording { get; }
    Task StartDemoRecordingAsync(Stream output);
    void StopDemoRecording();
    Task LoadDemoAsync(Stream input);
    Task RestartDemoAsync();
    void StopDemoPlayback();
}

public static class RecordingHostGuards
{
    public static void EnsureMachineMutationAllowed(this IHostApp host)
    {
        if (host is IRecordingHost demo && (demo.IsPreparingPlayback || demo.DemoSession?.Mode is RecordingMode.Playback or RecordingMode.Recording))
            throw new InvalidOperationException("Machine mutations are disabled during recording/replay. Stop the demo session first.");
    }

    public static void EnsureLiveInputAllowed(this IHostApp host)
    {
        if (host is IRecordingHost demo && (demo.IsPreparingPlayback || demo.DemoSession?.OwnsInput == true))
            throw new InvalidOperationException("Live machine input is disabled during replay, including while paused.");
    }
}
