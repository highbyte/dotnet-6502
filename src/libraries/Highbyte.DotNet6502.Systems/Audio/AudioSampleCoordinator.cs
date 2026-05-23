using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Coordinator for the PCM-sample audio style: wires an <see cref="IAudioSampleProvider"/> to an
/// <see cref="IAudioSampleTarget"/> by handing the target the provider's pull callback.
///
/// Audio counterpart of <see cref="AudioCommandCoordinator"/> but with pull cadence: the host
/// audio device drives the pace, asking the target for samples, which the target obtains via the
/// <see cref="AudioSampleReadCallback"/> wired to <see cref="IAudioSampleProvider.ReadSamples"/>.
/// The ring buffer that bridges the emulator and host clocks lives inside the provider; this
/// coordinator owns only the wiring and the lifecycle calls.
/// </summary>
public sealed class AudioSampleCoordinator : IAudioCoordinator, IDisposable
{
    private readonly IAudioSampleProvider _provider;
    private readonly IAudioSampleTarget _target;

    private readonly Instrumentations _instrumentations = new();
    public Instrumentations Instrumentations => _instrumentations;

    public AudioSampleCoordinator(IAudioSampleProvider provider, IAudioSampleTarget target)
    {
        _provider = provider;
        _target = target;
    }

    public void Init() => _target.Init(_provider.SampleRateHz, _provider.ChannelCount, _provider.ReadSamples);

    public void StartPlaying() => _target.StartPlaying();

    public void StopPlaying() => _target.StopPlaying();

    public void PausePlaying() => _target.PausePlaying();

    public void Dispose() => _target.Cleanup();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
