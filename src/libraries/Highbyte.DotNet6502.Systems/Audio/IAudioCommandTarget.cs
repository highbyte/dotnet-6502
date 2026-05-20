namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// The command-stream audio target style: a host synth backend that executes
/// <see cref="IAudioCommand"/>s produced by an <see cref="IAudioCommandStream"/>.
///
/// Audio counterpart of <see cref="Rendering.VideoCommands.ICommandTarget"/>. Implemented per host
/// technology (NAudio for desktop, WebAudio for browser).
/// </summary>
public interface IAudioCommandTarget : IAudioTarget
{
    /// <summary>
    /// Sets up the host audio backend. Called once before playback.
    /// </summary>
    /// <param name="voiceCount">Number of synth voices to allocate (from <see cref="IAudioCommandStream.VoiceCount"/>).</param>
    void Init(int voiceCount);

    /// <summary>Executes a single audio command against the host audio backend.</summary>
    void Execute(IAudioCommand command);

    /// <summary>Starts (or resumes) audio output.</summary>
    void StartPlaying();

    /// <summary>Stops audio output and silences all voices.</summary>
    void StopPlaying();

    /// <summary>Pauses audio output without tearing down the backend.</summary>
    void PausePlaying();

    /// <summary>Tears down the host audio backend.</summary>
    void Cleanup();
}
