using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Coordinator for the command-stream audio style: connects an <see cref="IAudioCommandStream"/>
/// to an <see cref="IAudioCommandTarget"/> and forwards each emitted command to the host backend.
///
/// Audio counterpart of <see cref="Rendering.VideoCommands.CommandCoordinator"/>. The cadence is
/// push: the stream's <see cref="IAudioCommandStream.CommandEmitted"/> event (driven by the
/// system's per-instruction audio generation) is forwarded immediately — there is no per-frame
/// drain, because audio commands must be applied with low latency.
/// </summary>
public sealed class AudioCommandCoordinator : IAudioCoordinator, IDisposable
{
    private readonly IAudioCommandStream _stream;
    private readonly IAudioCommandTarget _target;

    private readonly Instrumentations _instrumentations = new();
    public Instrumentations Instrumentations => _instrumentations;

    // Audio counterpart of the render coordinator's FPS + FlushIfDirty: CommandsPerSecond is the
    // rate of audio commands forwarded to the host backend; Execute is the time spent applying one.
    private readonly PerSecondTimedStat _commandsPerSecond;
    private readonly ElapsedMillisecondsTimedStat _executeStat;

    public AudioCommandCoordinator(IAudioCommandStream stream, IAudioCommandTarget target)
    {
        _stream = stream;
        _target = target;
        _commandsPerSecond = _instrumentations.Add("CommandsPerSecond", new PerSecondTimedStat());
        _executeStat = _instrumentations.Add("Execute", new ElapsedMillisecondsTimedStat());
        _stream.CommandEmitted += OnCommandEmitted;
    }

    private void OnCommandEmitted(IAudioCommand command)
    {
        _commandsPerSecond.Update();
        _executeStat.Start();
        _target.Execute(command);
        _executeStat.Stop();
    }

    public void Init() => _target.Init(_stream.VoiceCount);

    public void StartPlaying() => _target.StartPlaying();

    public void StopPlaying() => _target.StopPlaying();

    public void PausePlaying() => _target.PausePlaying();

    public void Dispose()
    {
        _stream.CommandEmitted -= OnCommandEmitted;
        _target.Cleanup();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
