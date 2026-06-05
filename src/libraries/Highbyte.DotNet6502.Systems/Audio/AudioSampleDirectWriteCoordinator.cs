using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Coordinator for direct-write PCM sample targets. The provider writes samples directly into the
/// host target instead of first passing through a coordinator-owned pull ring buffer.
/// </summary>
public sealed class AudioSampleDirectWriteCoordinator : IAudioCoordinator, IDisposable
{
    private readonly IAudioSampleProvider _provider;
    private readonly IAudioSampleDirectWriteTarget _target;

    private readonly Instrumentations _instrumentations = new();
    public Instrumentations Instrumentations => _instrumentations;

    public AudioSampleDirectWriteCoordinator(IAudioSampleProvider provider, IAudioSampleDirectWriteTarget target)
    {
        _provider = provider;
        _target = target;
    }

    public void Init()
    {
        _target.InitDirect(_provider.SampleRateHz, _provider.ChannelCount);
        _provider.Init(_target.WriteSamples);
    }

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
