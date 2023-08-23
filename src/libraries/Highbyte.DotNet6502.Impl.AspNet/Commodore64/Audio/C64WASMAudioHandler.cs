using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64.Audio;

public class C64WASMAudioHandler : IAudioHandler<C64, WASMAudioHandlerContext>, IAudioHandler
{
    // Set to true to stop and recreate oscillator before each audio. Set to false to reuse oscillator.
    // If true: for each audio played, the oscillator will be stopped, recreated, and started. This is the way WebAudio API is designed to work, but is very resource heavy if using the C#/.NET WebAudio wrapper classes, because new instances are created continuously.
    // If false: the oscillator is only created and started once. When audio is stopped, the gain (volume) is set to 0.
    public bool StopAndRecreateOscillator { get; private set; } = false;

    // This setting is only used if _stopAndRecreateOscillator is true.
    // If true: when audio is stopped (and gain/volume is set to 0), the oscillator is also disconnected from the audio context. This may help audio bleeding over when switching oscillator on same voice.
    // If false: when audio is stopped (and gain/volume is set to 0), the oscillator stays connected to the audio context. This may increase performance, but may lead to audio bleeding over when switching oscillators on same voice.
    public bool DisconnectOscillatorOnStop { get; private set; } = true;

    // private static Queue<InternalSidState> _sidStateChanges = new();

    private WASMAudioHandlerContext _audioHandlerContext = default!;
    internal WASMAudioHandlerContext AudioHandlerContext => _audioHandlerContext!;
    private AudioContextSync _audioContext => AudioHandlerContext!.AudioContext;

    internal GainNodeSync? CommonSIDGainNode { get; private set; }

    private readonly List<byte> _enabledVoices = new() { 1, 2, 3 }; // TODO: Set enabled voices via config.
    //private List<byte> _enabledVoices = new() { 1 }; // TODO: Set enabled voices via config.

    private readonly List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.Triangle, SidVoiceWaveForm.Sawtooth, SidVoiceWaveForm.Pulse, SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.
    //private List<SidVoiceWaveForm> _enabledOscillators = new() { SidVoiceWaveForm.RandomNoise }; // TODO: Set enabled oscillators via config.

    public Dictionary<byte, C64WASMVoiceContext> VoiceContexts = new()
        {
            {1, new C64WASMVoiceContext(1) },
            {2, new C64WASMVoiceContext(2) },
            {3, new C64WASMVoiceContext(3) },
        };

    private readonly List<string> _stats = new();

    private readonly ILogger _logger;

    public C64WASMAudioHandler(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(C64WASMAudioHandler).Name);
    }

    public List<string> GetStats()
    {
        return _stats;
    }

    public void Init(C64 system, WASMAudioHandlerContext audioHandlerContext)
    {
        _audioHandlerContext = audioHandlerContext;
        _audioHandlerContext.Init();

        // Create common gain node for all oscillators, which represent the SID volume.
        CreateGainNode();

        foreach (var key in VoiceContexts.Keys)
        {
            var voice = VoiceContexts[key];
            voice.Init(this, AddDebugMessage);
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

    public void StartPlaying()
    {
        // TODO: Any extra needed when resuming play? Oscillators will already be created/connected when new notes are played.
    }

    public void PausePlaying()
    {
        foreach (var voiceContext in VoiceContexts.Values)
        {
            voiceContext.Stop();
        }
    }

    public void StopPlaying()
    {
        foreach (var voiceContext in VoiceContexts.Values)
        {
            voiceContext.StopAllOscillatorsNow();   // Force stop all oscillators now. No more audio will be able to play
        }
    }

    private void CreateGainNode()
    {
        CommonSIDGainNode = GainNodeSync.Create(_audioContext.JSRuntime, _audioContext);
        // Associate CommonSIDGainNode (SID volume) -> MasterVolume -> AudioContext destination 
        CommonSIDGainNode.Connect(_audioHandlerContext.MasterVolumeGainNode);
        var destination = _audioContext.GetDestination();
        AudioHandlerContext.MasterVolumeGainNode.Connect(destination);
    }

    private void PlayAllVoices(InternalSidState internalSidState)
    {
        //var sidInternalStateClone = _sidStateChanges.Peek();

        var audioGlobalParameter = AudioGlobalParameter.BuildAudioGlobalParameter(internalSidState);
        if (audioGlobalParameter.AudioCommand == AudioGlobalCommand.ChangeVolume)
        {
            SetVolume(audioGlobalParameter.Gain);
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

    private void SetVolume(float gain)
    {
        // The current time is where the gain change starts
        var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();

        var gainAudioParam = CommonSIDGainNode!.GetGain();
        // Check if the gain of the actual oscillator is different from the new gain
        // (the gain could have changed by ADSR cycle, LinearRampToValueAtTimeAsync)
        var currentGainValue = gainAudioParam.GetCurrentValue();
        if (currentGainValue != gain)
        {
            AddDebugMessage($"Changing vol to {gain}.");
            gainAudioParam.SetValueAtTime(gain, currentTime);
        }
    }

    private void PlayVoice(C64WASMVoiceContext voiceContext, AudioVoiceParameter audioVoiceParameter)
    {
        AddDebugMessage($"Processing command: {audioVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);

        if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.Stop)
        {
            // Stop audio immediately
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

            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetFrequencyOnCurrentOscillator(audioVoiceParameter.Frequency, currentTime);
        }

        else if (audioVoiceParameter.AudioCommand == AudioVoiceCommand.ChangePulseWidth)
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

        //var time = DateTime.Now.ToString("HH:mm:ss.fff");
        //formattedMsg = $"{time}: {formattedMsg}";
        ////var threadId = Environment.CurrentManagedThreadId;
        ////_stats.Insert(0, $"{time} ({threadId}): {msg}");
        //_stats.Insert(0, formattedMsg);

        //if (_stats.Count > MAX_DEBUG_MESSAGES)
        //    _stats.RemoveAt(MAX_DEBUG_MESSAGES);
    }

    //private Task[] CreateSoundTasks(InternalSidState sidInternalStateClone)
    //{
    //    var playSoundTasks = new List<Task>();

    //    foreach (var voice in VoiceContexts.Keys)
    //    {
    //        var voiceContext = VoiceContexts[voice];
    //        var audioVoiceParameter = BuildWASMVoiceParameterFromC64Sid(voiceContext, sidInternalStateClone);
    //        if (audioVoiceParameter.AudioVoiceCommand == AudioVoiceCommand.None)
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
