using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Highbyte.DotNet6502.Impl.NAudio.Commodore64;

public class C64NAudioAudioHandler : IAudioHandler<C64, NAudioAudioHandlerContext>, IAudioHandler
{
    private NAudioAudioHandlerContext? _audioHandlerContext;

    private MixingSampleProvider _mixer;
    public MixingSampleProvider Mixer => _mixer;

    private VolumeSampleProvider _sidVolumeControl;


    //private static Queue<InternalSidState> _sidStateChanges = new();

    private List<byte> _enabledVoices = new() { 1, 2, 3 }; // TODO: Set enabled voices via config.
    //private List<byte> _enabledVoices = new() { 1 }; // TODO: Set enabled voices via config.

    private List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.Triangle, SidVoiceWaveForm.Sawtooth, SidVoiceWaveForm.Pulse, SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.
    //private List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.

    public Dictionary<byte, C64NAudioVoiceContext> VoiceContexts = new()
        {
            {1, new C64NAudioVoiceContext(1) },
            {2, new C64NAudioVoiceContext(2) },
            {3, new C64NAudioVoiceContext(3) },
        };

    private List<string> _debugMessages = new();
    private const int MAX_DEBUG_MESSAGES = 20;

    public C64NAudioAudioHandler()
    {
    }

    public List<string> GetDebugMessages()
    {
        return _debugMessages;
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

        foreach (var voice in VoiceContexts.Keys)
        {
            if (!_enabledVoices.Contains(voice))
                continue;

            var voiceContext = VoiceContexts[voice];

            var audioVoiceParameter = AudioVoiceParameter.BuildAudioVoiceParameter(
                voiceContext.Voice,
                voiceContext.Status,
                internalSidState);

            if (audioVoiceParameter.AudioCommand != AudioCommand.None)
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

        if (audioVoiceParameter.AudioCommand == AudioCommand.Stop)
        {
            // StopWavePlayer audio immediately
            voiceContext.Stop();
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.StartADS)
        {
            // Skip starting audio if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            voiceContext.StartAudioADSPhase(audioVoiceParameter);
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.StartRelease)
        {
            // Skip stopping audio if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            if (voiceContext.Status == AudioStatus.Stopped)
            {
                AddDebugMessage($"Voice status is already Stopped, Release phase will be ignored", voiceContext.Voice);
                return;
            }

            voiceContext.StartAudioReleasePhase(audioVoiceParameter);
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.ChangeVolume)
        {
            // TODO: Move change volume to outside oscillator setting (as the C64 shared volume between all oscillators)    
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.ChangeFrequency)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            voiceContext.SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency);
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.ChangePulseWidth)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            // Set pulse width. Only applicable if current oscillator is a pulse oscillator.
            if (voiceContext.CurrentSidVoiceWaveForm != SidVoiceWaveForm.Pulse) return;
            // TODO:
            //voiceContext.SetPulseWidth(audioVoiceParameter.Frequency);
        }

        AddDebugMessage($"Processing command done: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);
    }

    private void AddDebugMessage(string msg, int voice, SidVoiceWaveForm? sidVoiceWaveForm = null, AudioStatus? audioStatus = null)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        string formattedMsg;
        if (sidVoiceWaveForm.HasValue && audioStatus.HasValue)
        {
            formattedMsg = $"{time} ({voice}-{sidVoiceWaveForm}-{audioStatus}): {msg}";
        }
        else if (sidVoiceWaveForm.HasValue && !audioStatus.HasValue)
        {
            formattedMsg = $"{time} ({voice}-{sidVoiceWaveForm}): {msg}";
        }
        else if (!sidVoiceWaveForm.HasValue && audioStatus.HasValue)
        {
            formattedMsg = $"{time} ({voice}-{audioStatus}): {msg}";
        }
        else
        {
            formattedMsg = $"{time} ({voice}): {msg}";
        }

        //var threadId = Environment.CurrentManagedThreadId;
        //_debugMessages.Insert(0, $"{time} ({threadId}): {msg}");
        _debugMessages.Insert(0, formattedMsg);

        if (_debugMessages.Count > MAX_DEBUG_MESSAGES)
            _debugMessages.RemoveAt(MAX_DEBUG_MESSAGES);
    }
}
