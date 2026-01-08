using System.Runtime.Versioning;
using NAudio.Wave;

namespace Highbyte.DotNet6502.Impl.NAudio.WavePlayers;

/// <summary>
/// Null wave player for where NAudio cannot play audio.
/// </summary>
public partial class NullWavePlayer : IWavePlayer
{
    public float Volume { get; set; }

    public PlaybackState PlaybackState => PlaybackState.Stopped;

    public WaveFormat OutputWaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(1, 1);

    public event EventHandler<StoppedEventArgs> PlaybackStopped;

    public void Dispose() { }

    public void Init(IWaveProvider waveProvider) { }

    public void Pause() { }

    public void Play() { }

    public void Stop() { }

    public static NullWavePlayer Instance = new NullWavePlayer();
}
