using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

public class C64WASMSoundHandler : ISoundHandler<C64, WASMSoundHandlerContext>, ISoundHandler
{
    public static Queue<InternalSidState> _sidStateChanges = new();

    private WASMSoundHandlerContext? _soundHandlerContext;

    private List<byte> _enabledVoices = new List<byte> { 1, 2, 3 }; // TODO: Set enabled voices via config.

    public Dictionary<byte, C64WASMVoiceContext> VoiceContexts = new()
        {
            {1, new C64WASMVoiceContext(1) },
            {2, new C64WASMVoiceContext(2) },
            {3, new C64WASMVoiceContext(3) },
        };

    private List<string> _debugMessages = new();
    private const int MAX_DEBUG_MESSAGES = 20;

    private void AddDebugMessage(string msg, int voice, SidVoiceWaveForm? sidVoiceWaveForm = null, SoundStatus? soundStatus = null)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        string formattedMsg;
        if (sidVoiceWaveForm.HasValue && soundStatus.HasValue)
        {
            formattedMsg = $"{time} ({voice}-{sidVoiceWaveForm}-{soundStatus}): {msg}";
        }
        else if (sidVoiceWaveForm.HasValue && !soundStatus.HasValue)
        {
            formattedMsg = $"{time} ({voice}-{sidVoiceWaveForm}): {msg}";
        }
        else if (!sidVoiceWaveForm.HasValue && soundStatus.HasValue)
        {
            formattedMsg = $"{time} ({voice}-{soundStatus}): {msg}";
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

    public C64WASMSoundHandler()
    {
    }

    public void Init(C64 system, WASMSoundHandlerContext soundHandlerContext)
    {
        _soundHandlerContext = soundHandlerContext;
        _soundHandlerContext.Init();

        foreach (var key in VoiceContexts.Keys)
        {
            var voice = VoiceContexts[key];
            voice.Init(soundHandlerContext, AddDebugMessage, createAndStartOscillators: true);
        }
    }

    public void Init(ISystem system, ISoundHandlerContext soundHandlerContext)
    {
        Init((C64)system, (WASMSoundHandlerContext)soundHandlerContext);
    }

    public void StopAllSounds()
    {
        if (_soundHandlerContext is null)
            return;
        foreach (var voiceContext in VoiceContexts.Values)
        {
            voiceContext.Stop();
        }
    }

    public void GenerateSound(C64 c64)
    {
        var sid = c64.Sid;
        if (!sid.InternalSidState.IsAudioChanged)
            return;

        //var internalSidStateClone = sid.InternalSidState.Clone();
        //sid.InternalSidState.ClearAudioChanged();
        //_sidStateChanges.Enqueue(internalSidStateClone);
        //GenerateSound();

        GenerateSound(sid.InternalSidState);
        sid.InternalSidState.ClearAudioChanged();

        //var soundTasks = CreateSoundTasks(internalSidStateClone);
        //if (soundTasks.Length > 0)
        //{
        //    //Task.WaitAll(soundTasks);

        //    //var allTasksComplete = Task.WhenAll(soundTasks);
        //    //allTasksComplete.GetAwaiter();

        //    AddDebugMessage($"{Starting sound tasks for {soundTasks.Length} voices");
        //    foreach (var task in soundTasks)
        //    {
        //        task.Start();
        //    }
        //}
    }

    private void GenerateSound(InternalSidState internalSidState)
    {
        //var sidInternalStateClone = _sidStateChanges.Peek();

        foreach (var voice in VoiceContexts.Keys)
        {
            if (!_enabledVoices.Contains(voice))
                continue;

            var voiceContext = VoiceContexts[voice];
            //var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, sidInternalStateClone);
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, internalSidState);
            if (wasmSoundParameters.SoundCommand != SoundCommand.None)
            {
                AddDebugMessage($"BEGIN VOICE", voice);
                PlaySound(voiceContext, wasmSoundParameters);
                AddDebugMessage($"END VOICE", voice);
            }
        }

        //_sidStateChanges.Dequeue();
    }

    private Task[] CreateSoundTasks(InternalSidState sidInternalStateClone)
    {
        var playSoundTasks = new List<Task>();

        foreach (var voice in VoiceContexts.Keys)
        {
            var voiceContext = VoiceContexts[voice];
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, sidInternalStateClone);
            if (wasmSoundParameters.SoundCommand == SoundCommand.None)
                continue;

            //await PlaySound(voiceContext, wasmSoundParameters);
            //var task = PlaySound(voiceContext, wasmSoundParameters);
            var task = new Task(() => PlaySound(voiceContext, wasmSoundParameters));

            //var task = new Task(async () => await PlaySound(voiceContext, wasmSoundParameters));

            //var task = new Task(async () => await PlaySoundGated(voiceContext, wasmSoundParameters));
            //var task = new Task(() => PlaySoundGated(voiceContext, wasmSoundParameters));
            playSoundTasks.Add(task);
        }

        return playSoundTasks.ToArray();
    }

    public void GenerateSound(ISystem system)
    {
        GenerateSound((C64)system);
    }

    private WASMVoiceParameter BuildWASMSoundParametersFromC64Sid(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        // TODO: Read clock speed from config, different for NTSC and PAL.
        float clockSpeed = 1022730;

        var voice = voiceContext.Voice;

        // ----------
        // Map SID register values to sound parameters usable by Web Audio, and what to do with the sound.
        // ----------
        var oscillatorType = GetOscillatorType(sidState, voice);

        var soundParameters = new WASMVoiceParameter
        {
            // What to do with the sound (Start ADS cycle, start Release cycle, stop sound, change frequency, change volume)
            SoundCommand = GetSoundCommand(voiceContext, sidState),

            // Oscillator type mapped from C64 SID wave form selection
            SIDOscillatorType = oscillatorType,

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

        return soundParameters;
    }

    private static SidVoiceWaveForm GetOscillatorType(
        InternalSidState sidState,
        byte voice)
    {
        var sidWaveForm = sidState.GetWaveForm(voice);
        return sidWaveForm;
    }

    //private PeriodicWaveOptions GetPeriodicWaveNoiseOptions(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    //{
    //    // TODO: Can a PeriodicWave really be use to create white noise?
    //    float[] real = new float[2] { 0, 1 };
    //    float[] imag = new float[2] { 0, 0 };
    //    return new PeriodicWaveOptions
    //    {
    //        Real = real,
    //        Imag = imag,
    //    };
    //}

    private SoundCommand GetSoundCommand(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        SoundCommand command = SoundCommand.None;

        byte voice = voiceContext.Voice;
        var gateControl = sidState.GetGateControl(voice);
        var isFrequencyChanged = sidState.IsFrequencyChanged(voice);
        var isPulseWidthChanged = sidState.IsPulseWidthChanged(voice);
        var isVolumeChanged = sidState.IsVolumeChanged;

        // New sound (ADS cycle) is started when
        // - Starting ADS is selected in the SID gate register
        // - and no sound is playing (or when the release cycle has started)
        if (gateControl == InternalSidState.GateControl.StartAttackDecaySustain
            && (voiceContext.Status == SoundStatus.Stopped ||
                voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.StartADS;
        }

        // Release cycle can be started when
        // - Starting Release is selected in the SID gate register
        // - ADS cycle has already been started
        // - or ADS cycle has already stopped (which in case nothing will really happen
        else if (gateControl == InternalSidState.GateControl.StartRelease
               && (voiceContext.Status == SoundStatus.ADSCycleStarted || voiceContext.Status == SoundStatus.Stopped))
        {
            command = SoundCommand.StartRelease;
        }

        // Sound is stopped immediately when
        // - Gate is off (in gate register when gate bit is 0 and no waveform selected)
        else if (gateControl == InternalSidState.GateControl.StopSound)
        {
            command = SoundCommand.Stop;
        }

        // Check if frequency has changed, and if any sound is currently playing.
        else if (isFrequencyChanged
                && (voiceContext.Status == SoundStatus.ADSCycleStarted
                || voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.ChangeFrequency;
        }

        // Check if frequency has changed, and if any sound is currently playing.
        else if (isPulseWidthChanged
                && (voiceContext.Status == SoundStatus.ADSCycleStarted
                || voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.ChangePulseWidth;
        }

        // Check if volume has changed, and if any sound is currently playing.
        else if (isVolumeChanged
                && (voiceContext.Status == SoundStatus.ADSCycleStarted
                || voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.ChangeVolume;
        }

        return command;
    }

    //private async Task PlaySoundGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters)
    //private void PlaySoundGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters)
    //{
    //    //voiceContext.SemaphoreSlim.Wait();
    //    voiceContext.SemaphoreSlim.WaitAsync().RunSynchronously();
    //    PlaySound(voiceContext, wasmSoundParameters);
    //    voiceContext.SemaphoreSlim.Release();
    //}

    private void PlaySound(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters)
    {
        AddDebugMessage($"Processing command: {wasmSoundParameters.SoundCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);

        if (wasmSoundParameters.SoundCommand == SoundCommand.Stop)
        {
            // Stop sound immediately
            voiceContext.Stop();
        }

        else if (wasmSoundParameters.SoundCommand == SoundCommand.StartADS)
        {
            // Connect the currently selected oscillator to the GainNode (will also disconnect any previously connected oscillator)
            voiceContext.ConnectOscillator(wasmSoundParameters.SIDOscillatorType);

            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();

            if (wasmSoundParameters.SIDOscillatorType == SidVoiceWaveForm.RandomNoise)
            {
                // Set frequency (playback rate) on current NoiseGenerator Oscillator
                voiceContext.SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);

                // Set Gain ADSR (will start playing immediately if oscillator is already started)
                voiceContext.SetGainADS(wasmSoundParameters, currentTime);
            }

            else if (wasmSoundParameters.SIDOscillatorType == SidVoiceWaveForm.Pulse)
            {
                // Set frequency on current Oscillator
                voiceContext.SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
                // Set pulsewidth on existing Oscillator (PulseOscillator)
                voiceContext.C64WASMPulseOscillator.SetPulseWidth(wasmSoundParameters.PulseWidth, currentTime);

                // Set Gain ADSR (will start playing immediately if oscillator is already started)
                voiceContext.SetGainADS(wasmSoundParameters, currentTime);
                // Set Pulse Width ADSR
                voiceContext.C64WASMPulseOscillator.SetPulseWidthDepthADSR(currentTime);
            }

            else if (wasmSoundParameters.SIDOscillatorType == SidVoiceWaveForm.Triangle)
            {
                // Set frequency on current Oscillator
                voiceContext.SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);

                // Set Gain ADSR (will start playing immediately if oscillator is already started)
                voiceContext.SetGainADS(wasmSoundParameters, currentTime);
            }

            else if (wasmSoundParameters.SIDOscillatorType == SidVoiceWaveForm.Sawtooth)
            {
                // Set frequency on current Oscillator
                voiceContext.SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);

                // Set Gain ADSR (will start playing immediately if oscillator is already started)
                voiceContext.SetGainADS(wasmSoundParameters, currentTime);
            }

            voiceContext.Status = SoundStatus.ADSCycleStarted;
            AddDebugMessage($"Status changed", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);

            // If SustainGain is 0, then we need to schedule a stop of the sound
            // when the attack + decay period is over.
            if (wasmSoundParameters.SustainGain == 0)
            {
                var waitSeconds = wasmSoundParameters.AttackDurationSeconds + wasmSoundParameters.DecayDurationSeconds;
                AddDebugMessage($"Scheduling voice stop now + {waitSeconds} seconds.", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);
                voiceContext.ScheduleSoundStopAfterDecay(waitMs: (int)(waitSeconds * 1000.0d));
            }
        }

        else if (wasmSoundParameters.SoundCommand == SoundCommand.StartRelease)
        {
            if (voiceContext.Status == SoundStatus.Stopped)
            {
                AddDebugMessage($"Voice status is already Stopped, Release phase will be ignored", voiceContext.Voice);
                return;
            }

            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetGainRelease(wasmSoundParameters, currentTime);
            voiceContext.ScheduleSoundStopAfterRelease(wasmSoundParameters.ReleaseDurationSeconds);

            voiceContext.Status = SoundStatus.ReleaseCycleStarted;
            AddDebugMessage($"Status changed", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);
        }

        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangeVolume)
        {
            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetVolume(wasmSoundParameters.Gain, currentTime);
        }

        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangeFrequency)
        {
            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.SetFrequencyOnCurrentOscillator(wasmSoundParameters.Frequency, currentTime);
        }

        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangePulseWidth)
        {
            // Set pulse width. Only applicable if current oscillator is a pulse oscillator.
            if (voiceContext.CurrentSidVoiceWaveForm != SidVoiceWaveForm.Pulse) return;
            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            voiceContext.C64WASMPulseOscillator.SetPulseWidth(wasmSoundParameters.Frequency, currentTime);
        }

        AddDebugMessage($"Processing command done: {wasmSoundParameters.SoundCommand}", voiceContext.Voice, voiceContext.CurrentSidVoiceWaveForm, voiceContext.Status);
    }

    public List<string> GetDebugMessages()
    {
        return _debugMessages;
    }
}
