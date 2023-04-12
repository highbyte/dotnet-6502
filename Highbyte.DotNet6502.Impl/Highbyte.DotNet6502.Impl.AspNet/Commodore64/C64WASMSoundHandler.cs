using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorDOMSync;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

public class C64WASMSoundHandler : ISoundHandler<C64, C64WASMSoundHandlerContext>, ISoundHandler
{

    public static Queue<InternalSidState> _sidStateChanges = new();

    private C64WASMSoundHandlerContext? _soundHandlerContext;

    private List<byte> _enabledVoices = new List<byte> { 1, 2, 3 }; // TODO: Set enabled voices via config.

    private List<string> _debugMessages = new();
    private const int MAX_DEBUG_MESSAGES = 20;
    private void AddDebugMessage(string msg)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;
        _debugMessages.Insert(0, $"{time} ({threadId}): {msg}");
        if (_debugMessages.Count > MAX_DEBUG_MESSAGES)
            _debugMessages.RemoveAt(MAX_DEBUG_MESSAGES);
    }

    public C64WASMSoundHandler()
    {
    }

    public void Init(C64 system, C64WASMSoundHandlerContext soundHandlerContext)
    {
        _soundHandlerContext = soundHandlerContext;
        _soundHandlerContext.Init();
    }

    public void Init(ISystem system, ISoundHandlerContext soundHandlerContext)
    {
        Init((C64)system, (C64WASMSoundHandlerContext)soundHandlerContext);
    }

    public void GenerateSound(C64 c64)
    {
        var sid = c64.Sid;
        if (!sid.InternalSidState.IsAudioChanged)
            return;

        var internalSidStateClone = sid.InternalSidState.Clone();
        sid.InternalSidState.ClearAudioChanged();

        //var internalSidStateClone = sid.InternalSidState;
        _sidStateChanges.Enqueue(internalSidStateClone);

        GenerateSound();

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

    private void GenerateSound()
    {
        var sidInternalStateClone = _sidStateChanges.Peek();
        foreach (var voice in _soundHandlerContext!.VoiceContexts.Keys)
        {
            if (!_enabledVoices.Contains(voice))
                continue;

            var voiceContext = _soundHandlerContext!.VoiceContexts[voice];
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, sidInternalStateClone);
            if (wasmSoundParameters.SoundCommand != SoundCommand.None)
            {
                AddDebugMessage($"BEGIN VOICE {voice}");
                PlaySound(voiceContext, wasmSoundParameters);
                AddDebugMessage($"END VOICE {voice}");
            }
        }

        _sidStateChanges.Dequeue();
    }

    private Task[] CreateSoundTasks(InternalSidState sidInternalStateClone)
    {
        var playSoundTasks = new List<Task>();

        foreach (var voice in _soundHandlerContext!.VoiceContexts.Keys)
        {
            var voiceContext = _soundHandlerContext!.VoiceContexts[voice];
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, sidInternalStateClone);
            if (wasmSoundParameters.SoundCommand == SoundCommand.None)
                continue;

            //await PlaySound(voiceContext, wasmSoundParameters);
            //var task = PlaySound(voiceContext, wasmSoundParameters);
            //var task = new Task(async () => await PlaySound(voiceContext, wasmSoundParameters));

            //var task = new Task(async () => await PlaySoundGated(voiceContext, wasmSoundParameters));
            var task = new Task(() => PlaySoundGated(voiceContext, wasmSoundParameters));
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
            Type = oscillatorType,

            // PeriodicWave used for SID pulse and random noise wave forms (mapped to WebAudio OscillatorType.Custom)
            PeriodicWaveOptions = (oscillatorType.HasValue && oscillatorType.Value == OscillatorType.Custom) ? GetPeriodicWaveOptions(voiceContext, sidState) : null,

            // Translate SID volume 0-15 to Gain 0.0-1.0
            // SID volume in lower 4 bits of SIGVOL register.
            Gain = Math.Clamp((float)(sidState.GetVolume() / 15.0f), 0.0f, 1.0f),

            // Translate SID frequency (0 - 65536) to actual frequency number
            // Frequency = (REGISTER VALUE * CLOCK / 16777216) Hz
            // where CLOCK equals the system clock frequency, 1022730 for American (NTSC)systems, 985250 for European(PAL)
            Frequency = sidState.GetFrequency(voice) * clockSpeed / 16777216.0f,

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

    private static OscillatorType? GetOscillatorType(InternalSidState sidState, byte voice)
    {
        var sidWaveForm = sidState.GetWaveForm(voice);
        OscillatorType? oscillatorType = sidWaveForm switch
        {
            SidVoiceWaveForm.Triangle => OscillatorType.Triangle,
            SidVoiceWaveForm.Sawtooth => OscillatorType.Sawtooth,

            // Note: You never set oscialltor type to custom manually; instead, use the setPeriodicWave() method to provide the data representing the waveform. Doing so automatically sets the type to custom.
            SidVoiceWaveForm.Pulse => OscillatorType.Custom,
            SidVoiceWaveForm.RandomNoise => OscillatorType.Custom,

            SidVoiceWaveForm.None => null,
            _ => null
        };
        return oscillatorType;
    }

    private PeriodicWaveOptions GetPeriodicWaveOptions(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        // TODO
        float[] real = new float[2] { 0, 1 };
        float[] imag = new float[2] { 0, 0 };
        return new PeriodicWaveOptions
        {
            Real = real,
            Imag = imag,
        };
    }

    private SoundCommand GetSoundCommand(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        SoundCommand command = SoundCommand.None;

        byte voice = voiceContext.Voice;
        var isGateOn = sidState.IsGateOn(voice);
        var waveForm = sidState.GetWaveForm(voice);
        var isFrequencyChanged = sidState.IsFrequencyChanged(voice);
        var isVolumeChanged = sidState.IsVolumeChanged;

        // New sound (ADS cycle) is started when
        // - Gate on and a waveform selected
        // - and no sound is playing (or when the release cycle has started)
        if (isGateOn
            && waveForm != SidVoiceWaveForm.None
            && (voiceContext.Status == SoundStatus.Stopped ||
                voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.StartADS;
        }

        // Release cycle can be started when
        // - and Gate is off
        // - ASD cycle has already been started
        else if (!isGateOn
                && voiceContext.Status == SoundStatus.ASDCycleStarted)
        {
            command = SoundCommand.StartRelease;
        }

        // Sound is stopped immediatley when
        // - Release cycle has already been started
        // - and no Waveform has been selected (= all SID waveform type selection bits are 0)
        else if (voiceContext.Status == SoundStatus.ASDCycleStarted
                && waveForm == SidVoiceWaveForm.None)
        {
            command = SoundCommand.Stop;
        }

        // Check if frequency has changed, and if any sound is currently playing.
        else if (isFrequencyChanged
                && (voiceContext.Status == SoundStatus.ASDCycleStarted
                || voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.ChangeFrequency;
        }

        // Check if volume has changed, and if any sound is currently playing.
        else if (isVolumeChanged
                && (voiceContext.Status == SoundStatus.ASDCycleStarted
                || voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.ChangeVolume;
        }

        return command;
    }

    //private async Task PlaySoundGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters)
    private void PlaySoundGated(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters)
    {
        voiceContext.SemaphoreSlim.Wait();
        PlaySound(voiceContext, wasmSoundParameters);
        voiceContext.SemaphoreSlim.Release();
    }

    private void PlaySound(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters)
    {
        if (wasmSoundParameters.SoundCommand == SoundCommand.Stop)
        {
            // Stop sound immediately
            if (voiceContext.Oscillator is null) return;
            voiceContext.Oscillator.Stop();
            voiceContext.Init();
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.StartADS)
        {
            if (voiceContext.Oscillator != null && voiceContext.GainNode != null)
            {
                voiceContext.Oscillator.Stop();
                voiceContext.GainNode.Disconnect();
                voiceContext.Oscillator.Disconnect();
            }

            if (wasmSoundParameters.Type == OscillatorType.Custom)
            {
                // Use custom waveform for SID pulse and random noise waveforms.
                voiceContext.Oscillator = OscillatorNodeSync.Create(
                    _soundHandlerContext!.JSRuntime,
                    _soundHandlerContext.AudioContext,
                    new()
                    {
                        Frequency = wasmSoundParameters.Frequency,
                    });

                var wave = _soundHandlerContext.AudioContext.CreatePeriodicWave(
                    wasmSoundParameters.PeriodicWaveOptions!.Real,
                    wasmSoundParameters.PeriodicWaveOptions!.Imag);

                voiceContext.Oscillator.SetPeriodicWave(wave);
            }
            else
            {
                // Use built-in Triangle and Sawtooth for corresponding SID waveforms.
                voiceContext.Oscillator = OscillatorNodeSync.Create(
                    _soundHandlerContext!.JSRuntime,
                    _soundHandlerContext.AudioContext,
                    new()
                    {
                        Type = wasmSoundParameters.Type!.Value,
                        Frequency = wasmSoundParameters.Frequency,
                    });
            }

            var callback = EventListener<EventSync>.Create(_soundHandlerContext.AudioContext.WebAudioHelper, _soundHandlerContext.AudioContext.JSRuntime, (e) =>
            {
                AddDebugMessage($"Sound stopped on voice {voiceContext.Voice}.");
                voiceContext.Status = SoundStatus.Stopped;
            });
            voiceContext.Oscillator.AddEndedEventListsner(callback);

            // Create GainNode
            voiceContext.GainNode = GainNodeSync.Create(_soundHandlerContext.JSRuntime, _soundHandlerContext.AudioContext);

            // Associate GainNode with AudioContext destination
            var destination = _soundHandlerContext.AudioContext.GetDestination();
            voiceContext.GainNode.Connect(destination);

            // Associate volume gain with Oscillator
            voiceContext.Oscillator.Connect(voiceContext.GainNode);

            // Set Attack/Decay/Sustain gain envelope
            var gainAudioParam = voiceContext.GainNode!.GetGain();
            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            gainAudioParam.SetValueAtTime(0, currentTime);
            gainAudioParam.LinearRampToValueAtTime(wasmSoundParameters.Gain, currentTime + wasmSoundParameters.AttackDurationSeconds);
            gainAudioParam.SetTargetAtTime(wasmSoundParameters.SustainGain, currentTime + wasmSoundParameters.AttackDurationSeconds, wasmSoundParameters.DecayDurationSeconds);

            AddDebugMessage($"Starting sound on voice {voiceContext.Voice} with freq {wasmSoundParameters.Frequency} with type {wasmSoundParameters.Type}");
            voiceContext.Status = SoundStatus.ASDCycleStarted;
            // Start sound 
            voiceContext.Oscillator.Start();
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.StartRelease)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            // The current time is where the Release cycle starts
            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();

            var gainAudioParam = voiceContext.GainNode.GetGain();
            gainAudioParam.CancelScheduledValues(currentTime);

            // Schedule a volume change from current gain level down to 0 during specified Release time 
            var currentGainValue = gainAudioParam.GetCurrentValue();
            gainAudioParam.SetValueAtTime(currentGainValue, currentTime);
            gainAudioParam.LinearRampToValueAtTime(0, currentTime + wasmSoundParameters.ReleaseDurationSeconds);

            AddDebugMessage($"Stopping sound on voice {voiceContext.Voice} at time now + {wasmSoundParameters.ReleaseDurationSeconds} seconds.");

            voiceContext.Status = SoundStatus.ReleaseCycleStarted;
            // Schedule Stop for the time when the Release period if sover
            voiceContext.Oscillator.Stop(currentTime + wasmSoundParameters.ReleaseDurationSeconds);

        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangeVolume)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            ChangeVolume(voiceContext, wasmSoundParameters, currentTime);
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangeFrequency)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();
            ChangeFrequency(voiceContext, wasmSoundParameters, currentTime);
        }
    }

    private void ChangeVolume(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters, double changeTime)
    {
        // The current time is where the gain change starts
        var gainAudioParam = voiceContext.GainNode!.GetGain();
        // Check if the gain of the actual oscillator is different from the new gain
        // (the gain could have changed by ADSR cycle, LinearRampToValueAtTimeAsync)
        var currentGainValue = gainAudioParam.GetCurrentValue();
        if (currentGainValue != wasmSoundParameters.Gain)
        {
            AddDebugMessage($"Changing vol on voice {voiceContext.Voice} to {wasmSoundParameters.Gain}.");
            gainAudioParam.SetValueAtTime(wasmSoundParameters.Gain, changeTime);
        }
    }

    private void ChangeFrequency(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters, double changeTime)
    {
        var frequencyAudioParam = voiceContext.Oscillator!.GetFrequency();
        // Check if the frequency of the actual oscillator is different from the new frequency
        // TODO: Is this necessary to check? Could the frequency have been changed in other way?
        var currentFrequencyValue = frequencyAudioParam.GetCurrentValue();
        if (currentFrequencyValue != wasmSoundParameters.Frequency)
        {
            AddDebugMessage($"Changing freq on voice {voiceContext.Voice} to {wasmSoundParameters.Frequency}.");
            frequencyAudioParam.SetValueAtTime(wasmSoundParameters.Frequency, changeTime);
        }
    }

    public List<string> GetDebugMessages()
    {
        return _debugMessages;
    }
}
