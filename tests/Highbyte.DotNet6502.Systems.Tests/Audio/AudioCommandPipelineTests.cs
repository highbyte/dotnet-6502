using Highbyte.DotNet6502.Systems.Audio;

namespace Highbyte.DotNet6502.Systems.Tests.Audio;

/// <summary>
/// Proves the command-stream audio pipeline is system-agnostic: a non-C64
/// <see cref="FakeGenericCommandStream"/> is matched, coordinated and executed against a host
/// target using only the generic synth-voice command vocabulary in core <c>Systems/Audio</c>.
/// </summary>
public class AudioCommandPipelineTests
{
    /// <summary>A minimal host <see cref="IAudioCommandTarget"/> that records what it received.</summary>
    private sealed class RecordingAudioCommandTarget : IAudioCommandTarget
    {
        public string Name => "RecordingAudioCommandTarget";
        public int? InitVoiceCount { get; private set; }
        public List<IAudioCommand> Executed { get; } = new();
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int PauseCount { get; private set; }
        public int CleanupCount { get; private set; }

        public void Init(int voiceCount) => InitVoiceCount = voiceCount;
        public void Execute(IAudioCommand command) => Executed.Add(command);
        public void StartPlaying() => StartCount++;
        public void StopPlaying() => StopCount++;
        public void PausePlaying() => PauseCount++;
        public void Cleanup() => CleanupCount++;
    }

    [Fact]
    public void AudioTargetProvider_matches_a_non_C64_command_stream_to_a_command_target()
    {
        // Arrange
        var provider = new AudioTargetProvider();
        provider.AddAudioTargetType<RecordingAudioCommandTarget>(() => new RecordingAudioCommandTarget());

        // Act
        var compatible = provider.GetCompatibleConcreteAudioProviderTypes(
            new List<Type> { typeof(FakeGenericCommandStream) });

        // Assert
        Assert.Contains(typeof(FakeGenericCommandStream), compatible);
    }

    [Fact]
    public void AudioTargetProvider_creates_a_command_target_for_a_non_C64_command_stream()
    {
        // Arrange
        var provider = new AudioTargetProvider();
        provider.AddAudioTargetType<RecordingAudioCommandTarget>(() => new RecordingAudioCommandTarget());

        // Act
        var target = provider.CreateAudioTargetByAudioProviderType(typeof(FakeGenericCommandStream));

        // Assert
        Assert.IsType<RecordingAudioCommandTarget>(target);
    }

    [Fact]
    public void AudioCommandCoordinator_Init_passes_the_streams_voice_count_to_the_target()
    {
        // Arrange
        var stream = new FakeGenericCommandStream(voiceCount: 5);
        var target = new RecordingAudioCommandTarget();
        using var coordinator = new AudioCommandCoordinator(stream, target);

        // Act
        coordinator.Init();

        // Assert
        Assert.Equal(5, target.InitVoiceCount);
    }

    [Fact]
    public void AudioCommandCoordinator_forwards_emitted_generic_commands_to_the_target()
    {
        // Arrange
        var stream = new FakeGenericCommandStream();
        var target = new RecordingAudioCommandTarget();
        using var coordinator = new AudioCommandCoordinator(stream, target);

        var volumeCommand = new SetVolumeAudioCommand(0.75f);
        var voiceCommand = new VoiceAudioCommand(
            Voice: 1,
            Parameter: new AudioVoiceParameter
            {
                AudioCommand = AudioVoiceCommand.StartADS,
                OscillatorType = AudioOscillatorType.Sawtooth,
                Frequency = 440f,
                SustainGain = 0.5f,
            });

        // Act
        stream.Emit(volumeCommand);
        stream.Emit(voiceCommand);

        // Assert
        Assert.Equal(new IAudioCommand[] { volumeCommand, voiceCommand }, target.Executed);
    }

    [Fact]
    public void AudioCommandCoordinator_stops_forwarding_after_dispose()
    {
        // Arrange
        var stream = new FakeGenericCommandStream();
        var target = new RecordingAudioCommandTarget();
        var coordinator = new AudioCommandCoordinator(stream, target);

        // Act
        coordinator.Dispose();
        stream.Emit(new SetVolumeAudioCommand(1.0f));

        // Assert
        Assert.Empty(target.Executed);
        Assert.Equal(1, target.CleanupCount);
    }
}
