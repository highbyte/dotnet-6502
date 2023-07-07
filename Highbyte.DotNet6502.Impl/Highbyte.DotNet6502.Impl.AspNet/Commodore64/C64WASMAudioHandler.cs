using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

public class C64WASMAudioHandler : IAudioHandler<C64, WASMAudioHandlerContext>, IAudioHandler
{
    private static Queue<InternalSidState> _sidStateChanges = new();

    private WASMAudioHandlerContext? _audioHandlerContext;

    private List<byte> _enabledVoices = new() { 1, 2, 3 }; // TODO: Set enabled voices via config.
    //private List<byte> _enabledVoices = new() { 1 }; // TODO: Set enabled voices via config.

    private List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.Triangle, SidVoiceWaveForm.Sawtooth, SidVoiceWaveForm.Pulse, SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.
    //private List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.

    public Dictionary<byte, C64WASMVoiceContext> VoiceContexts = new()
        {
            {1, new C64WASMVoiceContext(1) },
            {2, new C64WASMVoiceContext(2) },
            {3, new C64WASMVoiceContext(3) },
        };

    private List<string> _debugMessages = new();
    private const int MAX_DEBUG_MESSAGES = 20;

    public C64WASMAudioHandler()
    {
    }
    public List<string> GetDebugMessages()
    {
        return _debugMessages;
    }

    public void Init(C64 system, WASMAudioHandlerContext audioHandlerContext)
    {
        _audioHandlerContext = audioHandlerContext;
        _audioHandlerContext.Init();

        foreach (var key in VoiceContexts.Keys)
        {
            var voice = VoiceContexts[key];
            voice.Init(audioHandlerContext, AddDebugMessage);
        }
    }

    public void Init(ISystem system, IAudioHandlerContext audioHandlerContext)
    {
        Init((C64)system, (WASMAudioHandlerContext)audioHandlerContext);
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

        //var internalSidStateClone = sid.InternalSidState.Clone();
        //sid.InternalSidState.ClearAudioChanged();
        //_sidStateChanges.Enqueue(internalSidStateClone);
        //GenerateAudio();

        PlayAllVoices(sid.InternalSidState);
        sid.InternalSidState.ClearAudioChanged();

        //var audioTasks = CreateAudioTasks(internalSidStateClone);
        //if (audioTasks.Length > 0)
        //{
        //    //Task.WaitAll(audioTasks);

        //    //var allTasksComplete = Task.WhenAll(audioTasks);
        //    //allTasksComplete.GetAwaiter();

        //    AddDebugMessage($"{Starting audio tasks for {audioTasks.Length} voices");
        //    foreach (var task in audioTasks)
        //    {
        //        task.Start();
        //    }
        //}
    }

    public void StopAllAudio()
    {
        if (_audioHandlerContext is null)
            return;
        foreach (var voiceContext in VoiceContexts.Values)
        {
            voiceContext.StopAllOscillatorsNow();
        }
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

    private void PlayVoice(C64WASMVoiceContext voiceContext, AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Processing command: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);

        if (audioVoiceParameter.AudioCommand == AudioCommand.Stop)
        {
            // Stop audio immediately
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
            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetVolume(audioVoiceParameter.Gain, currentTime);
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.ChangeFrequency)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
        }

        else if (audioVoiceParameter.AudioCommand == AudioCommand.ChangePulseWidth)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(audioVoiceParameter.SIDOscillatorType))
                return;

            // Set pulse width. Only applicable if current oscillator is a pulse oscillator.
            if (voiceContext.CurrentSidVoiceWaveForm != SidVoiceWaveForm.Pulse) return;
            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.C64WASMPulseOscillator.SetPulseWidth(audioVoiceParameter.Frequency, currentTime);
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

    //private Task[] CreateSoundTasks(InternalSidState sidInternalStateClone)
    //{
    //    var playSoundTasks = new List<Task>();

    //    foreach (var voice in VoiceContexts.Keys)
    //    {
    //        var voiceContext = VoiceContexts[voice];
    //        var audioVoiceParameter = BuildWASMVoiceParameterFromC64Sid(voiceContext, sidInternalStateClone);
    //        if (audioVoiceParameter.AudioCommand == AudioCommand.None)
    //            continue;

    //        //await PlaySound(voiceContext, audioVoiceParameter);
    //        //var task = PlaySound(voiceContext, audioVoiceParameter);
    //        var task = new Task(() => PlaySound(voiceContext, audioVoiceParameter));

    //        //var task = new Task(async () => await PlaySound(voiceContext, audioVoiceParameter));

    //        //var task = new Task(async () => await PlaySoundGated(voiceContext, audioVoiceParameter));
    //        //var task = new Task(() => PlaySoundGated(voiceContext, audioVoiceParameter));
    //        playSoundTasks.Add(task);
    //    }

    //    return playSoundTasks.ToArray();
    //}

    //private async Task PlayVoiceGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter audioVoiceParameter)
    //private void PlayVoiceGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter audioVoiceParameter)
    //{
    //    //voiceContext.SemaphoreSlim.Wait();
    //    voiceContext.SemaphoreSlim.WaitAsync().RunSynchronously();
    //    PlaySound(voiceContext, audioVoiceParameter);
    //    voiceContext.SemaphoreSlim.Release();
    //}
}
