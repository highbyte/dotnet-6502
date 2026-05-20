using Highbyte.DotNet6502.Systems.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio;

/// <summary>
/// NAudio host target for the command-stream audio style.
///
/// Executes the system-agnostic <see cref="IAudioCommand"/>s produced by an
/// <see cref="IAudioCommandStream"/> against an NAudio synthesis graph. Desktop counterpart of
/// <c>WebAudioCommandTarget</c>; system-agnostic — it knows synth voices, not the emulated system.
/// The per-system sound-chip decode lives in the system's command stream (e.g. <c>C64SidCommandStream</c>).
/// </summary>
public class NAudioCommandTarget : IAudioCommandTarget
{
    public string Name => "NAudioCommandTarget";

    private readonly NAudioAudioHandlerContext _audioHandlerContext;
    private readonly ILogger _logger;

    private MixingSampleProvider _mixer = default!;
    private VolumeSampleProvider _sidVolumeControl = default!;

    /// <summary>Per-voice synth contexts, keyed by 1-based voice number. Sized in <see cref="Init"/>.</summary>
    public Dictionary<byte, NAudioVoiceContext> VoiceContexts { get; } = new();

    public NAudioCommandTarget(NAudioAudioHandlerContext audioHandlerContext, ILoggerFactory loggerFactory)
    {
        _audioHandlerContext = audioHandlerContext;
        _logger = loggerFactory.CreateLogger(typeof(NAudioCommandTarget).Name);
    }

    public void Init(int voiceCount)
    {
        // Setup audio rendering pipeline: Mixer -> Volume -> WavePlayer
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true }; // Always produce samples
        _sidVolumeControl = new VolumeSampleProvider(_mixer);

        // Initialize NAudio WavePlayer with the last entity in the audio rendering pipeline.
        _audioHandlerContext.ConfigureWavePlayer(_sidVolumeControl);
        // StartWavePlayer will not produce audio until oscillators are added to the Mixer.
        _audioHandlerContext.StartWavePlayer();

        VoiceContexts.Clear();
        for (byte voice = 1; voice <= voiceCount; voice++)
            VoiceContexts.Add(voice, new NAudioVoiceContext(voice));

        foreach (var voice in VoiceContexts.Values)
            voice.Init(_mixer, AddDebugMessage);
    }

    public void Execute(IAudioCommand command)
    {
        switch (command)
        {
            case SetVolumeAudioCommand changeVolume:
                _sidVolumeControl.Volume = changeVolume.Gain;
                break;
            case VoiceAudioCommand voiceCommand:
                PlayVoice(VoiceContexts[voiceCommand.Voice], voiceCommand.Parameter);
                break;
        }
    }

    public void StartPlaying()
    {
        _logger.LogInformation("StartPlaying called.");
        _audioHandlerContext.StartWavePlayer();
    }

    public void PausePlaying()
    {
        _logger.LogInformation("PausePlaying called.");
        _audioHandlerContext.PauseWavePlayer();
    }

    public void StopPlaying()
    {
        _logger.LogInformation("StopPlaying called.");

        foreach (var voiceContext in VoiceContexts.Values)
            voiceContext.StopAllOscillatorsNow();
        _audioHandlerContext.StopWavePlayer();
    }

    public void Cleanup() => StopPlaying();

    // Applies one decoded per-voice synth action. Oscillator-enabled filtering and the
    // "release on a stopped voice" check are done upstream by the command stream.
    private void PlayVoice(NAudioVoiceContext voiceContext, AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Processing command: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentOscillatorType, voiceContext.Status);

        switch (audioVoiceParameter.AudioCommand)
        {
            case AudioVoiceCommand.Stop:
                voiceContext.Stop();
                break;

            case AudioVoiceCommand.StartADS:
                voiceContext.StartAudioADSPhase(audioVoiceParameter);
                break;

            case AudioVoiceCommand.StartRelease:
                voiceContext.StartAudioReleasePhase(audioVoiceParameter);
                break;

            case AudioVoiceCommand.ChangeFrequency:
                voiceContext.SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
                break;

            case AudioVoiceCommand.ChangePulseWidth:
                // Pulse width only applies to the pulse oscillator.
                if (voiceContext.CurrentOscillatorType == AudioOscillatorType.Pulse)
                    voiceContext.SetPulseWidthOnCurrentOscillator(audioVoiceParameter.PulseWidth);
                break;
        }

        AddDebugMessage($"Processing command done: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentOscillatorType, voiceContext.Status);
    }

    private void AddDebugMessage(string msg, int? voice = null, AudioOscillatorType? oscillatorType = null, AudioVoiceStatus? audioStatus = null)
    {
        _logger.LogTrace(AudioDebug.Format(msg, voice, oscillatorType, audioStatus));
    }
}
