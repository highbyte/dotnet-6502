namespace Highbyte.DotNet6502.Systems.Audio;

/// <summary>
/// A common interface that may be used inside an <see cref="ISystem"/> implementation as a common
/// way to generate the audio data for a system (e.g. decoding the C64 SID state).
///
/// Audio counterpart of <see cref="Rendering.IRenderGenerator"/>. Typically implemented on the
/// same class that provides the <see cref="IAudioSource"/> implementation exposed via
/// <see cref="ISystem.AudioProviders"/>.
/// </summary>
public interface IAudioGenerator
{
    /// <summary>
    /// Optionally called after each CPU instruction. The C64 SID audio is generated here, since
    /// SID register writes (and the resulting audio changes) happen between instructions.
    /// </summary>
    void OnAfterInstruction();

    /// <summary>
    /// Optionally called after all cycles/instructions for a frame have been processed.
    /// </summary>
    void OnEndFrame();
}
