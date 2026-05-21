namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// The command-stream audio source style: the system emits a stream of <see cref="IAudioCommand"/>s
/// and a host synth backend (NAudio, WebAudio) turns them into sound.
///
/// Audio counterpart of <see cref="Rendering.VideoCommands.IVideoCommandStream"/>. Unlike the
/// render command stream — which is drained once per frame — audio commands must be applied with
/// low latency, so this style is <em>push</em>: the stream raises <see cref="CommandEmitted"/> as
/// commands are produced (driven by the system's per-instruction audio generation), and the
/// coordinator forwards each to the host target immediately.
/// </summary>
public interface IAudioCommandStream : IAudioSource
{
    /// <summary>
    /// Number of synth voices the system produces. Passed by the coordinator to the host target's
    /// <see cref="IAudioCommandTarget.Init(int)"/> so the target can size its voice contexts
    /// without knowing the system (the C64 SID has 3).
    /// </summary>
    int VoiceCount { get; }

    /// <summary>
    /// Raised for each audio command produced. The <see cref="AudioCommandCoordinator"/>
    /// subscribes and forwards commands to the matched <see cref="IAudioCommandTarget"/>.
    /// </summary>
    event Action<IAudioCommand>? CommandEmitted;
}
