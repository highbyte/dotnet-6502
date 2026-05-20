using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// Marker interface for the coordinator of a specific audio pipeline — it connects one
/// <see cref="IAudioSource"/> to one compatible <see cref="IAudioTarget"/> and drives the data
/// flow between them.
///
/// Audio counterpart of <see cref="Rendering.IRenderCoordinator"/>. Each audio output style has
/// its own coordinator with different cadence characteristics:
/// <list type="bullet">
/// <item><c>AudioCommandCoordinator</c> — push: synth commands are drained and applied to the
/// host backend (driven by the SID post-instruction callback).</item>
/// <item><c>AudioSampleCoordinator</c> — pull: a ring buffer bridges the emulator clock and the
/// host audio device callback. (Future style.)</item>
/// </list>
/// </summary>
public interface IAudioCoordinator : IAsyncDisposable
{
    Instrumentations Instrumentations { get; }

    /// <summary>Sets up the audio pipeline. Called once before playback.</summary>
    void Init();

    /// <summary>Starts (or resumes) audio output.</summary>
    void StartPlaying();

    /// <summary>Stops audio output.</summary>
    void StopPlaying();

    /// <summary>Pauses audio output without tearing down the pipeline.</summary>
    void PausePlaying();
}
