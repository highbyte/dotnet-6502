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
        var oscillatorType = GetOscillatorType(sidState, voice, out OscillatorSpecialType? oscillatorSpecialType);

        var soundParameters = new WASMVoiceParameter
        {
            // What to do with the sound (Start ADS cycle, start Release cycle, stop sound, change frequency, change volume)
            SoundCommand = GetSoundCommand(voiceContext, sidState),

            // Oscillator type mapped from C64 SID wave form selection
            Type = oscillatorType,

            // Custom oscillator type (pulse or noise)
            SpecialType = oscillatorSpecialType,

            // PeriodicWave used for SID pulse and random noise wave forms (mapped to WebAudio OscillatorType.Custom)
            PeriodicWaveOptions = (oscillatorSpecialType.HasValue && oscillatorSpecialType.Value == OscillatorSpecialType.Noise) ? GetPeriodicWaveNoiseOptions(voiceContext, sidState) : null,

            // Translate SID volume 0-15 to Gain 0.0-1.0
            // SID volume in lower 4 bits of SIGVOL register.
            Gain = Math.Clamp((float)(sidState.GetVolume() / 15.0f), 0.0f, 1.0f),

            // Translate SID frequency (0 - 65536) to actual frequency number
            // Frequency = (REGISTER VALUE * CLOCK / 16777216) Hz
            // where CLOCK equals the system clock frequency, 1022730 for American (NTSC)systems, 985250 for European(PAL)
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

    private static OscillatorType? GetOscillatorType(
        InternalSidState sidState,
        byte voice,
        out OscillatorSpecialType? oscillatorCustomType)
    {
        var sidWaveForm = sidState.GetWaveForm(voice);
        OscillatorType? oscillatorType = sidWaveForm switch
        {
            SidVoiceWaveForm.Triangle => OscillatorType.Triangle,
            SidVoiceWaveForm.Sawtooth => OscillatorType.Sawtooth,

            SidVoiceWaveForm.Pulse => null, // See oscillatorCustomType
            // Note: You never set oscialltor type to custom manually; instead, use the setPeriodicWave() method to provide the data representing the waveform. Doing so automatically sets the type to custom.
            SidVoiceWaveForm.RandomNoise => OscillatorType.Custom,

            SidVoiceWaveForm.None => null,
            _ => null
        };

        oscillatorCustomType = sidWaveForm switch
        {
            SidVoiceWaveForm.Pulse => OscillatorSpecialType.Pulse,   // Special CustomPulseOcillatorNode
            SidVoiceWaveForm.RandomNoise => OscillatorSpecialType.Noise, // Standard WebAudio OscillatorNode with type Custom, and PeriodicWave.
            _ => null
        };
        return oscillatorType;
    }

    private PeriodicWaveOptions GetPeriodicWaveNoiseOptions(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        // TODO: Can a PeriodicWave really be use to create white noise?
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
        var isPulseWidthChanged = sidState.IsPulseWidthChanged(voice);
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

        // Check if frequency has changed, and if any sound is currently playing.
        else if (isPulseWidthChanged
                && (voiceContext.Status == SoundStatus.ASDCycleStarted
                || voiceContext.Status == SoundStatus.ReleaseCycleStarted))
        {
            command = SoundCommand.ChangePulseWidth;
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
        var currentTime = _soundHandlerContext!.AudioContext.GetCurrentTime();

        if (wasmSoundParameters.SoundCommand == SoundCommand.Stop)
        {
            // Stop sound immediately
            voiceContext.Stop();
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.StartADS)
        {
            // Stop any existing playing sound
            voiceContext.Stop();

            // Create GainNode
            voiceContext.GainNode = GainNodeSync.Create(_soundHandlerContext.JSRuntime, _soundHandlerContext.AudioContext);

            // Set Attack/Decay/Sustain gain envelope
            var gainAudioParam = voiceContext.GainNode!.GetGain();
            gainAudioParam.SetValueAtTime(0, currentTime);
            gainAudioParam.LinearRampToValueAtTime(wasmSoundParameters.Gain, currentTime + wasmSoundParameters.AttackDurationSeconds);
            gainAudioParam.SetTargetAtTime(wasmSoundParameters.SustainGain, currentTime + wasmSoundParameters.AttackDurationSeconds, wasmSoundParameters.DecayDurationSeconds);

            // Associate GainNode with AudioContext destination 
            var destination = _soundHandlerContext.AudioContext.GetDestination();
            voiceContext.GainNode.Connect(destination);

            // Define callback handler to know when an oscillator has stopped playing.
            var callback = EventListener<EventSync>.Create(_soundHandlerContext.AudioContext.WebAudioHelper, _soundHandlerContext.AudioContext.JSRuntime, (e) =>
            {
                AddDebugMessage($"Sound stopped on voice {voiceContext.Voice}.");
                voiceContext.Status = SoundStatus.Stopped;
            });

            if (wasmSoundParameters.Type == OscillatorType.Custom && wasmSoundParameters.SpecialType == OscillatorSpecialType.Noise)
            {
                voiceContext.PulseOscillator = null;

                // TODO: investigate these for noise generation
                //       https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API/Advanced_techniques#the_noise_%E2%80%94_random_noise_buffer_with_a_biquad_filter
                //       https://ui.dev/web-audio-api
                //       https://codepen.io/2kool2/pen/xrLeMq
                //       https://dev.opera.com/articles/drum-sounds-webaudio/

            }

            else if (wasmSoundParameters.Type is null && wasmSoundParameters.SpecialType == OscillatorSpecialType.Pulse)
            {
                // Use custom PulseOscialltor for pulse wave
                voiceContext.PulseOscillator = CustomPulseOscillatorNodeSync.Create(
                    _soundHandlerContext!.JSRuntime,
                    _soundHandlerContext.AudioContext,
                    new()
                    {
                        Frequency = wasmSoundParameters.Frequency,

                        //Pulse width - 1 to + 1 = ratio of the waveform's duty (power) cycle /mark-space
                        //DefaultWidth = -1.0   // 0% duty cycle  - silent
                        //DefaultWidth = -0.5f  // 25% duty cycle
                        //DefaultWidth = 0      // 50% duty cycle
                        //DefaultWidth = 0.5f   // 75% duty cycle
                        //DefaultWidth = 1.0f   // 100% duty cycle 
                        DefaultWidth = wasmSoundParameters.PulseWidth
                    });

                // Pulse width modulation
                voiceContext.PulseWidthGainNode = GainNodeSync.Create(
                    _soundHandlerContext!.JSRuntime,
                    _soundHandlerContext.AudioContext,
                    new()
                    {
                        Gain = 0
                    });
                voiceContext.PulseWidthGainNode.Connect(voiceContext.PulseOscillator.WidthGainNode);

                //var widthDepthGainNodeAudioParam = voiceContext.PulseWidthGainNode.GetGain();
                //var oscWidthDepth = 0.5f;   // LFO depth - Pulse modulation depth (percent) 
                //var oscWidthAttack = 0.05f;
                //var oscWidthDecay = 0.4f;
                //var oscWidthSustain = 0.4f;
                //var oscWidthRelease = 0.4f;
                //var widthDepthSustainTime = currentTime + oscWidthAttack + oscWidthRelease;
                //widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth, currentTime + oscWidthAttack);
                //widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0.5f * oscWidthDepth * oscWidthSustain, widthDepthSustainTime);
                //widthDepthGainNodeAudioParam.LinearRampToValueAtTime(0, oscWidthSustain + oscWidthRelease);

                // Low frequency oscillator, use as base for Pulse Oscilator.
                // The Pulse Oscillator will transform the Triangle wave to a Square wave in the end.
                voiceContext.Oscillator = OscillatorNodeSync.Create(
                     _soundHandlerContext!.JSRuntime,
                     _soundHandlerContext.AudioContext,
                        new OscillatorOptions
                        {
                            Type = OscillatorType.Triangle,
                            Frequency = 10
                        });
                //voiceContext.Oscillator.Connect(detuneDepth);
                voiceContext.Oscillator.Connect(voiceContext.PulseWidthGainNode);

                // Set callback on Pulse Oscillator (which is the primary oscillator in this case)
                voiceContext.PulseOscillator.AddEndedEventListsner(callback);

                // Associate volume gain with Pulse Oscillator
                voiceContext.PulseOscillator.Connect(voiceContext.GainNode);

                AddDebugMessage($"Starting sound on voice {voiceContext.Voice} with freq {wasmSoundParameters.Frequency} with special type {wasmSoundParameters.SpecialType}");
                voiceContext.PulseOscillator.Start();   // Primary oscillator
                voiceContext.Oscillator.Start();        // Modulation oscillator
            }

            else
            {
                voiceContext.PulseOscillator = null;

                // Use WebAudio Oscialltor with Triangle or Sawtooth waveforms.
                voiceContext.Oscillator = OscillatorNodeSync.Create(
                    _soundHandlerContext!.JSRuntime,
                    _soundHandlerContext.AudioContext,
                    new()
                    {
                        Type = wasmSoundParameters.Type!.Value,
                        Frequency = wasmSoundParameters.Frequency,
                    });

                // Set callback on Oscillator
                voiceContext.Oscillator.AddEndedEventListsner(callback);

                // Associate volume gain with Oscillator
                voiceContext.Oscillator.Connect(voiceContext.GainNode);

                AddDebugMessage($"Starting sound on voice {voiceContext.Voice} with freq {wasmSoundParameters.Frequency} with type {wasmSoundParameters.Type}");
                voiceContext.Oscillator.Start();
            }

            voiceContext.Status = SoundStatus.ASDCycleStarted;
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.StartRelease)
        {
            if (voiceContext.GainNode == null) return;

            var gainAudioParam = voiceContext.GainNode.GetGain();
            gainAudioParam.CancelScheduledValues(currentTime);

            // Schedule a volume change from current gain level down to 0 during specified Release time 
            var currentGainValue = gainAudioParam.GetCurrentValue();
            gainAudioParam.SetValueAtTime(currentGainValue, currentTime);
            gainAudioParam.LinearRampToValueAtTime(0, currentTime + wasmSoundParameters.ReleaseDurationSeconds);

            AddDebugMessage($"Stopping sound on voice {voiceContext.Voice} at time now + {wasmSoundParameters.ReleaseDurationSeconds} seconds.");

            // Schedule Stop for oscillator when the Release period if over
            voiceContext.Oscillator?.Stop(currentTime + wasmSoundParameters.ReleaseDurationSeconds);
            voiceContext.PulseOscillator?.Stop(currentTime + wasmSoundParameters.ReleaseDurationSeconds);

            voiceContext.Status = SoundStatus.ReleaseCycleStarted;
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangeVolume)
        {
            if (voiceContext.GainNode == null) return;

            ChangeVolume(voiceContext, wasmSoundParameters, currentTime);
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangeFrequency)
        {
            if (voiceContext.GainNode == null) return;
            if (voiceContext.Oscillator == null && voiceContext.PulseOscillator == null) return;

            ChangeFrequency(voiceContext, wasmSoundParameters, currentTime);
        }
        else if (wasmSoundParameters.SoundCommand == SoundCommand.ChangePulseWidth)
        {
            if (voiceContext.PulseWidthGainNode == null) return;
            if (voiceContext.PulseOscillator == null) return;

            ChangePulseWidth(voiceContext, wasmSoundParameters, currentTime);
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
        AudioParamSync frequencyAudioParam = voiceContext.PulseOscillator != null
            ? voiceContext.PulseOscillator!.GetFrequency()
            : voiceContext.Oscillator!.GetFrequency();

        // Check if the frequency of the actual oscillator is different from the new frequency
        // TODO: Is this necessary to check? Could the frequency have been changed in other way?
        var currentFrequencyValue = frequencyAudioParam.GetCurrentValue();
        if (currentFrequencyValue != wasmSoundParameters.Frequency)
        {
            AddDebugMessage($"Changing freq on voice {voiceContext.Voice} to {wasmSoundParameters.Frequency}.");
            frequencyAudioParam.SetValueAtTime(wasmSoundParameters.Frequency, changeTime);
        }
    }

    private void ChangePulseWidth(C64WASMVoiceContext voiceContext, WASMVoiceParameter wasmSoundParameters, double changeTime)
    {
        var widthDepthGainNodeAudioParam = voiceContext.PulseWidthGainNode!.GetGain();

        // Check if the pulse width of the actual oscillator is different from the new frequency
        // TODO: Is this necessary to check? Could the pulse width have been changed in other way?
        var currentPulseWidthValue = widthDepthGainNodeAudioParam.GetCurrentValue();
        if (currentPulseWidthValue != wasmSoundParameters.PulseWidth)
        {
            AddDebugMessage($"Changing pulse width on voice {voiceContext.Voice} to {wasmSoundParameters.PulseWidth}.");
            widthDepthGainNodeAudioParam.SetValueAtTime(wasmSoundParameters.PulseWidth, changeTime);
        }
    }

    public List<string> GetDebugMessages()
    {
        return _debugMessages;
    }
}
