using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

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
            //var wasmVoiceParameter = BuildWASMVoiceParameterFromC64Sid(voiceContext, sidInternalStateClone);
            var wasmVoiceParameter = BuildWASMVoiceParameterFromC64Sid(voiceContext, internalSidState);
            if (wasmVoiceParameter.AudioCommand != AudioCommand.None)
            {
                AddDebugMessage($"BEGIN VOICE", voice);
                PlayVoice(voiceContext, wasmVoiceParameter);
                AddDebugMessage($"END VOICE", voice);
            }
        }

        //_sidStateChanges.Dequeue();
    }

    private WASMVoiceParameter BuildWASMVoiceParameterFromC64Sid(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        // TODO: Read clock speed from config, different for NTSC and PAL.
        float clockSpeed = 1022730;

        var voice = voiceContext.Voice;

        // ----------
        // Map SID register values to audio parameters usable by Web Audio, and what to do with the audio.
        // ----------
        var wasmVoiceParameter = new WASMVoiceParameter
        {
            // What to do with the audio (Start ADS cycle, start Release cycle, stop audio, change frequency, change volume)
            AudioCommand = GetAudioCommand(voiceContext, sidState),

            // Oscillator type mapped from C64 SID wave form selection
            SIDOscillatorType = sidState.GetWaveForm(voice),

            // PeriodicWave used for SID pulse and random noise wave forms (mapped to WebAudio OscillatorType.Custom)
            //PeriodicWaveOptions = (oscillatorSpecialType.HasValue && oscillatorSpecialType.Value == OscillatorSpecialType.Noise) ? GetPeriodicWaveNoiseOptions(voiceContext, sidState) : null,

            // Translate SID volume 0-15 to Gain 0.0-1.0
            // SID volume in lower 4 bits of SIGVOL register.
            Gain = Math.Clamp((float)(sidState.GetVolume() / 15.0f), 0.0f, 1.0f),

            // Translate SID frequency (0 - 65536) to actual frequency number
            // Frequency = (REGISTER VALUE * CLOCK / 16777216) Hz
            // where CLOCK equals the system clock frequency, 1022730 for American (NTSC)systems, 985250 for European(PAL).
            // Range 0 Hz to about 4000 Hz.
            Frequency = sidState.GetFrequency(voice) * clockSpeed / 16777216.0f,

            // Translate 12 bit Pulse width (0 - 4095) to percentage
            // Pulse width % = (REGISTER VALUE / 40.95) %
            // The percentage is then transformed into a value between -1 and +1 that the .NET PulseOscillator uses.
            // Example: Register value of 0 => 0% => 0 value => (0 * 2) -1 => -1
            // Example: Register value of 2047 => approx 50% => 0.5 value => (0.5 * 2) -1 => 0
            // Example: Register value of 4095 => 100% => 1.0 value => (1.0 * 2) -1 => 1
            PulseWidth = (((sidState.GetPulseWidth(voice) / 40.95f) / 100.0f) * 2) - 1,

            // Translate SID attack duration in ms to seconds
            // Attack: 0-15, highest 4 bits in ATDCY
            // The values 0-15 represents different amount of milliseconds, read from lookup table.
            AttackDurationSeconds = sidState.GetAttackDuration(voice) / 1000.0,

            // Translate SID decay duration in ms to seconds
            // Decay: 0-15, lowest 4 bits in ATDCY
            // The values 0-15 represents different amount of milliseconds, read from lookup table.
            DecayDurationSeconds = sidState.GetDecayDuration(voice) / 1000.0,

            // Translate SID sustain volume 0-15 to Sustain Gain 0.0-1.0
            // Sustain level: 0-15, highest 4 bits in SUREL
            // The values 0-15 represents volume
            SustainGain = Math.Clamp((float)(sidState.GetSustainGain(voice) / 15.0f), 0.0f, 1.0f),

            // Translate SID release duration in ms to seconds
            // Release: 0-15, lowest 4 bits in SUREL
            // The values 0-15 represents different amount of milliseconds, read from lookup table.
            ReleaseDurationSeconds = sidState.GetReleaseDuration(voice) / 1000.0,
        };

        return wasmVoiceParameter;
    }

    private AudioCommand GetAudioCommand(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        AudioCommand command = AudioCommand.None;

        byte voice = voiceContext.Voice;
        var gateControl = sidState.GetGateControl(voice);
        var isFrequencyChanged = sidState.IsFrequencyChanged(voice);
        var isPulseWidthChanged = sidState.IsPulseWidthChanged(voice);
        var isVolumeChanged = sidState.IsVolumeChanged;

        // New audio (ADS cycle) is started when
        // - Starting ADS is selected in the SID gate register
        // - and no audio is playing (or when the release cycle has started)
        if (gateControl == InternalSidState.GateControl.StartAttackDecaySustain
            && (voiceContext.Status == AudioStatus.Stopped ||
                voiceContext.Status == AudioStatus.ReleaseCycleStarted))
        {
            command = AudioCommand.StartADS;
        }

        // Release cycle can be started when
        // - Starting Release is selected in the SID gate register
        // - ADS cycle has already been started
        // - or ADS cycle has already stopped (which in case nothing will really happen
        else if (gateControl == InternalSidState.GateControl.StartRelease
               && (voiceContext.Status == AudioStatus.ADSCycleStarted || voiceContext.Status == AudioStatus.Stopped))
        {
            command = AudioCommand.StartRelease;
        }

        // Audio is stopped immediately when
        // - Gate is off (in gate register when gate bit is 0 and no waveform selected)
        else if (gateControl == InternalSidState.GateControl.StopAudio)
        {
            command = AudioCommand.Stop;
        }

        // Check if frequency has changed, and if any audio is currently playing.
        else if (isFrequencyChanged
                && (voiceContext.Status == AudioStatus.ADSCycleStarted
                || voiceContext.Status == AudioStatus.ReleaseCycleStarted))
        {
            command = AudioCommand.ChangeFrequency;
        }

        // Check if pulsewidth has changed, and if any audio is currently playing.
        else if (isPulseWidthChanged
                && (voiceContext.Status == AudioStatus.ADSCycleStarted
                || voiceContext.Status == AudioStatus.ReleaseCycleStarted))
        {
            command = AudioCommand.ChangePulseWidth;
        }

        // Check if volume has changed, and if any audio is currently playing.
        else if (isVolumeChanged
                && (voiceContext.Status == AudioStatus.ADSCycleStarted
                || voiceContext.Status == AudioStatus.ReleaseCycleStarted))
        {
            command = AudioCommand.ChangeVolume;
        }

        return command;
    }

    private void PlayVoice(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmVoiceParameter)
    {
        AddDebugMessage($"Processing command: {wasmVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);

        if (wasmVoiceParameter.AudioCommand == AudioCommand.Stop)
        {
            // Stop audio immediately
            voiceContext.Stop();
        }

        else if (wasmVoiceParameter.AudioCommand == AudioCommand.StartADS)
        {
            // Skip starting audio if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(wasmVoiceParameter.SIDOscillatorType))
                return;

            voiceContext.StartAudioADSPhase(wasmVoiceParameter);
        }

        else if (wasmVoiceParameter.AudioCommand == AudioCommand.StartRelease)
        {
            // Skip stopping audio if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(wasmVoiceParameter.SIDOscillatorType))
                return;

            if (voiceContext.Status == AudioStatus.Stopped)
            {
                AddDebugMessage($"Voice status is already Stopped, Release phase will be ignored", voiceContext.Voice);
                return;
            }

            voiceContext.StartAudioReleasePhase(wasmVoiceParameter);
        }

        else if (wasmVoiceParameter.AudioCommand == AudioCommand.ChangeVolume)
        {
            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetVolume(wasmVoiceParameter.Gain, currentTime);
        }

        else if (wasmVoiceParameter.AudioCommand == AudioCommand.ChangeFrequency)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(wasmVoiceParameter.SIDOscillatorType))
                return;

            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetFrequencyOnCurrentOscillator(wasmVoiceParameter.Frequency, currentTime);
        }

        else if (wasmVoiceParameter.AudioCommand == AudioCommand.ChangePulseWidth)
        {
            // Skip changing frequency if specified oscillator is not enabled by config
            if (!_enabledOscillators.Contains(wasmVoiceParameter.SIDOscillatorType))
                return;

            // Set pulse width. Only applicable if current oscillator is a pulse oscillator.
            if (voiceContext.CurrentSidVoiceWaveForm != SidVoiceWaveForm.Pulse) return;
            var currentTime = _audioHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.C64WASMPulseOscillator.SetPulseWidth(wasmVoiceParameter.Frequency, currentTime);
        }

        AddDebugMessage($"Processing command done: {wasmVoiceParameter.AudioCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);
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
    //        var wasmVoiceParameter = BuildWASMVoiceParameterFromC64Sid(voiceContext, sidInternalStateClone);
    //        if (wasmVoiceParameter.AudioCommand == AudioCommand.None)
    //            continue;

    //        //await PlaySound(voiceContext, wasmVoiceParameter);
    //        //var task = PlaySound(voiceContext, wasmVoiceParameter);
    //        var task = new Task(() => PlaySound(voiceContext, wasmVoiceParameter));

    //        //var task = new Task(async () => await PlaySound(voiceContext, wasmVoiceParameter));

    //        //var task = new Task(async () => await PlaySoundGated(voiceContext, wasmVoiceParameter));
    //        //var task = new Task(() => PlaySoundGated(voiceContext, wasmVoiceParameter));
    //        playSoundTasks.Add(task);
    //    }

    //    return playSoundTasks.ToArray();
    //}

    //private async Task PlayVoiceGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmVoiceParameter)
    //private void PlayVoiceGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmVoiceParameter)
    //{
    //    //voiceContext.SemaphoreSlim.Wait();
    //    voiceContext.SemaphoreSlim.WaitAsync().RunSynchronously();
    //    PlaySound(voiceContext, wasmVoiceParameter);
    //    voiceContext.SemaphoreSlim.Release();
    //}
}
