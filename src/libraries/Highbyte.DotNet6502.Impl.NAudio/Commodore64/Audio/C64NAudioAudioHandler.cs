using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio.Commodore64.Audio;

public class C64NAudioAudioHandler : IAudioHandler<C64, NAudioAudioHandlerContext>, IAudioHandler
{
    private NAudioAudioHandlerContext? _audioHandlerContext;

    private MixingSampleProvider _mixer = default!;
    public MixingSampleProvider Mixer => _mixer;

    private VolumeSampleProvider _sidVolumeControl = default!;

    //private static Queue<InternalSidState> _sidStateChanges = new();

    private readonly List<byte> _enabledVoices = new() { 1, 2, 3 }; // TODO: Set enabled voices via config.
    //private List<byte> _enabledVoices = new() { 1 }; // TODO: Set enabled voices via config.

    private readonly List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.Triangle, SidVoiceWaveForm.Sawtooth, SidVoiceWaveForm.Pulse, SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.
    //private List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.

    public Dictionary<byte, C64NAudioVoiceContext> VoiceContexts = new()
        {
            {1, new C64NAudioVoiceContext(1) },
            {2, new C64NAudioVoiceContext(2) },
            {3, new C64NAudioVoiceContext(3) },
        };

    private readonly List<string> _stats = new();

    private readonly ILogger _logger;

    public C64NAudioAudioHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(C64NAudioAudioHandler).Name);
    }

    public List<string> GetStats()
    {
        return _stats;
    }

    public void Init(C64 system, NAudioAudioHandlerContext audioHandlerContext)
    {
        _audioHandlerContext = audioHandlerContext;

        // Setup audio rendering pipeline: Mixer -> SID Volume -> WavePlayer
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true }; // Always produce samples
        _sidVolumeControl = new VolumeSampleProvider(_mixer);

        // Initialize NAudio WavePlayer with the last entity in the audio rendering pipeline
        _audioHandlerContext.Init(_sidVolumeControl);

        foreach (var key in VoiceContexts.Keys)
        {
            var voice = VoiceContexts[key];
            voice.Init(this, AddDebugMessage);
        }
    }

    public void Init(ISystem system, IAudioHandlerContext audioHandlerContext)
    {
        Init((C64)system, (NAudioAudioHandlerContext)audioHandlerContext);
    }

    public void GenerateAudio(ISystem system)
    {
        GenerateAudio((C64)system);
    }

    public void GenerateAudio(C64 c64)
    {
        var sid = c64.Sid;
        if (!sid.InternalSidState.IsAudioChanged)
            return;

        PlayAllVoices(sid.InternalSidState);
        sid.InternalSidState.ClearAudioChanged();

    }

    public void StartPlaying()
    {
        _audioHandlerContext!.StartWavePlayer();
    }

    public void PausePlaying()
    {
        _audioHandlerContext!.PauseWavePlayer();
    }

    public void StopPlaying()
    {
        foreach (var voiceContext in VoiceContexts.Values)
        {
            voiceContext.StopAllOscillatorsNow();
        }
        _audioHandlerContext!.StopWavePlayer();
    }

    private void PlayAllVoices(InternalSidState internalSidState)
    {
        //var sidInternalStateClone = _sidStateChanges.Peek();

        var audioGlobalParameter = AudioGlobalParameter.BuildAudioGlobalParameter(internalSidState);
        if (audioGlobalParameter.AudioCommand == AudioGlobalCommand.ChangeVolume)
        {
            _sidVolumeControl.Volume = audioGlobalParameter.Gain;
            return;
        }

        foreach (var voice in VoiceContexts.Keys)
        {
            if (!_enabledVoices.Contains(voice))
                continue;

            var voiceContext = VoiceContexts[voice];

            var audioVoiceParameter = AudioVoiceParameter.BuildAudioVoiceParameter(
                voiceContext.Voice,
                voiceContext.Status,
                internalSidState);

            if (audioVoiceParameter.AudioCommand != AudioVoiceCommand.None)
            {
                AddDebugMessage($"BEGIN VOICE", voice);
                PlayVoice(voiceContext, audioVoiceParameter);
                AddDebugMessage($"END VOICE", voice);
            }
        }

        //_sidStateChanges.Dequeue();
    }

    private void PlayVoice(C64NAudioVoiceContext voiceContext, AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Processing command: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);

        if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.Stop)
        {
            // StopWavePlayer audio immediately
            voiceContext.Stop();
        }

        else if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.StartADS)
        {
            // Skip starting audio if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            voiceContext.StartAudioADSPhase(audioVoiceParameter);
        }

        else if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.StartRelease)
        {
            // Skip stopping audio if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            if (voiceContext.Status == AudioVoiceStatus.Stopped)
            {
                AddDebugMessage($"Voice status is already Stopped, Release phase will be ignored", voiceContext.Voice);
                return;
            }

            voiceContext.StartAudioReleasePhase(audioVoiceParameter);
        }

        else if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.ChangeFrequency)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            voiceContext.SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
        }

        else if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.ChangePulseWidth)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            // Set pulse width. Only applicable if current oscillator is a pulse oscillator.
            if (voiceContext.CurrentSidVoiceWaveForm != SidVoiceWaveForm.Pulse)
                return;
            voiceContext.SetPulseWidthOnCurrentOscillator(audioVoiceParameter.PulseWidth);
        }

        AddDebugMessage($"Processing command done: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);
    }

    private void AddDebugMessage(string msg, int? voice = null, SidVoiceWaveForm? sidVoiceWaveForm = null, AudioVoiceStatus? audioStatus = null)
    {
        string formattedMsg;
        if (sidVoiceWaveForm.HasValue && audioStatus.HasValue)
        {
            formattedMsg = $"(Voice{voice}-{sidVoiceWaveForm}-{audioStatus}): {msg}";
        }
        else if (sidVoiceWaveForm.HasValue && !audioStatus.HasValue)
        {
            formattedMsg = $"(Voice{voice}-{sidVoiceWaveForm}): {msg}";
        }
        else if (!sidVoiceWaveForm.HasValue && audioStatus.HasValue)
        {
            formattedMsg = $"(Voice{voice}-{audioStatus}): {msg}";
        }
        else if (voice.HasValue)
        {
            formattedMsg = $"(Voice{voice}): {msg}";
        }
        else
        {
            formattedMsg = $"{msg}";
        }

        _logger.LogDebug(formattedMsg);
    }
}
