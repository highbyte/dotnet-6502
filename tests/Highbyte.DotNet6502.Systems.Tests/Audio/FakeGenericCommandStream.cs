using Highbyte.DotNet6502.Systems.Audio;

namespace Highbyte.DotNet6502.Systems.Tests.Audio;

/// <summary>
/// A fake, non-C64 <see cref="IAudioCommandStream"/> used to prove the command-stream audio
/// pipeline (vocabulary, <see cref="AudioTargetProvider"/>, <see cref="AudioCommandCoordinator"/>)
/// is genuinely system-agnostic — it emits only the generic synth-voice command vocabulary from
/// core <c>Systems/Audio</c>, with no reference to the C64 / SID.
/// </summary>
public sealed class FakeGenericCommandStream : IAudioProvider, IAudioCommandStream
{
    public string Name => "FakeGenericCommandStream";

    public int VoiceCount { get; }

    public event Action<IAudioCommand>? CommandEmitted;

    public FakeGenericCommandStream(int voiceCount = 2)
    {
        VoiceCount = voiceCount;
    }

    /// <summary>Test hook: push a command through the stream as if the system produced it.</summary>
    public void Emit(IAudioCommand command) => CommandEmitted?.Invoke(command);

    public void OnAfterInstruction() { }

    public void OnEndFrame() { }
}
