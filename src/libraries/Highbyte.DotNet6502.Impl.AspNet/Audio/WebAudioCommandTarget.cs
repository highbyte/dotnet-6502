using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth;
using Highbyte.DotNet6502.Impl.AspNet.Audio.WebAudioAPISynth.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Systems.Audio;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.AspNet.Audio;

/// <summary>
/// WebAudio (browser) host target for the command-stream audio style.
///
/// Executes the system-agnostic <see cref="IAudioCommand"/>s produced by an
/// <see cref="IAudioCommandStream"/> against a WebAudio synthesis graph. Browser counterpart of
/// <c>NAudioCommandTarget</c>; system-agnostic — it knows synth voices, not the emulated system.
/// The per-system sound-chip decode lives in the system's command stream (e.g. <c>C64SidCommandStream</c>).
/// </summary>
public class WebAudioCommandTarget : IAudioCommandTarget
{
    public string Name => "WebAudioCommandTarget";

    // Set to true to stop and recreate the oscillator before each audio, false to reuse it.
    // Reusing (false) is much cheaper with the C#/.NET WebAudio wrapper classes.
    public bool StopAndRecreateOscillator { get; private set; } = false;

    // Only used if StopAndRecreateOscillator is true: whether to also disconnect the oscillator
    // from the audio context when audio is stopped.
    public bool DisconnectOscillatorOnStop { get; private set; } = true;

    private readonly WASMAudioHandlerContext _audioHandlerContext;
    private readonly ILogger _logger;

    private AudioContextSync AudioContext => _audioHandlerContext.AudioContext;

    // Common gain node for all oscillators, representing the global synth volume.
    internal GainNodeSync CommonGainNode { get; private set; } = default!;

    /// <summary>Per-voice synth contexts, keyed by 1-based voice number. Sized in <see cref="Init"/>.</summary>
    public Dictionary<byte, WebAudioVoiceContext> VoiceContexts { get; } = new();

    public WebAudioCommandTarget(WASMAudioHandlerContext audioHandlerContext, ILoggerFactory loggerFactory)
    {
        _audioHandlerContext = audioHandlerContext;
        _logger = loggerFactory.CreateLogger(typeof(WebAudioCommandTarget).Name);
    }

    public void Init(int voiceCount)
    {
        CreateGainNode();

        VoiceContexts.Clear();
        for (byte voice = 1; voice <= voiceCount; voice++)
            VoiceContexts.Add(voice, new WebAudioVoiceContext(voice));

        foreach (var voice in VoiceContexts.Values)
            voice.Init(_audioHandlerContext, CommonGainNode, StopAndRecreateOscillator, DisconnectOscillatorOnStop, AddDebugMessage);
    }

    public void Execute(IAudioCommand command)
    {
        switch (command)
        {
            case SetVolumeAudioCommand changeVolume:
                SetVolume(changeVolume.Gain);
                break;
            case VoiceAudioCommand voiceCommand:
                PlayVoice(VoiceContexts[voiceCommand.Voice], voiceCommand.Parameter);
                break;
        }
    }

    public void StartPlaying()
    {
        // Nothing extra needed when resuming; oscillators are created/connected when notes play.
    }

    public void PausePlaying()
    {
        foreach (var voiceContext in VoiceContexts.Values)
            voiceContext.Stop();
    }

    public void StopPlaying()
    {
        foreach (var voiceContext in VoiceContexts.Values)
            voiceContext.StopAllOscillatorsNow();   // Force stop all oscillators now.
    }

    public void Cleanup() => StopPlaying();

    private void CreateGainNode()
    {
        CommonGainNode = GainNodeSync.Create(AudioContext.JSRuntime, AudioContext);
        // Associate CommonGainNode (synth volume) -> MasterVolume -> AudioContext destination.
        CommonGainNode.Connect(_audioHandlerContext.MasterVolumeGainNode);
        var destination = AudioContext.GetDestination();
        _audioHandlerContext.MasterVolumeGainNode.Connect(destination);
    }

    private void SetVolume(float gain)
    {
        var currentTime = AudioContext.GetCurrentTime();
        var gainAudioParam = CommonGainNode.GetGain();
        // The gain could have been changed by an ADSR cycle; only set it if different.
        if (gainAudioParam.GetCurrentValue() != gain)
        {
            AddDebugMessage($"Changing vol to {gain}.");
            gainAudioParam.SetValueAtTime(gain, currentTime);
        }
    }

    // Applies one decoded per-voice synth action. Oscillator-enabled filtering and the
    // "release on a stopped voice" check are done upstream by the command stream.
    private void PlayVoice(WebAudioVoiceContext voiceContext, AudioVoiceParameter audioVoiceParameter)
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
                voiceContext.SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, AudioContext.GetCurrentTime());
                break;

            case AudioVoiceCommand.ChangePulseWidth:
                // Pulse width only applies to the pulse oscillator.
                if (voiceContext.CurrentOscillatorType == AudioOscillatorType.Pulse)
                    voiceContext.PulseOscillator.SetPulseWidth(audioVoiceParameter.Frequency, AudioContext.GetCurrentTime());
                break;
        }

        AddDebugMessage($"Processing command done: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentOscillatorType, voiceContext.Status);
    }

    private void AddDebugMessage(string msg, int? voice = null, AudioOscillatorType? oscillatorType = null, AudioVoiceStatus? audioStatus = null)
    {
        _logger.LogDebug(AudioDebug.Format(msg, voice, oscillatorType, audioStatus));
    }
}
