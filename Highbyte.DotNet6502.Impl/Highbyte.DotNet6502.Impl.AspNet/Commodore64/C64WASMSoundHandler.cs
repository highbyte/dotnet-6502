using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using KristofferStrube.Blazor.WebAudio;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.InternalSidState;
using KristofferStrube.Blazor.DOM;
using System.Diagnostics;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

public class C64WASMSoundHandler : ISoundHandler<C64, C64WASMSoundHandlerContext>, ISoundHandler
{
    private C64WASMSoundHandlerContext? _soundHandlerContext;

    private List<string> _debugMessages = new();
    private const int MAX_DEBUG_MESSAGES = 10;
    private void AddDebugMessage(string msg)
    {
        // Get time part only
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
        if (!sid.InternalSidState.HasAudioChanged)
            return;

        var internalSidStateClone = sid.InternalSidState.Clone();
        //var internalSidStateClone = sid.InternalSidState;

        //GenerateSoundAsync(internalSidStateClone).GetAwaiter().OnCompleted(
        //    () =>
        //        System.Diagnostics.Debug.WriteLine("Done audio")
        //    );
        //GenerateSoundAsync(internalSidStateClone);
        GenerateSoundAsync(internalSidStateClone).GetAwaiter();

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

        sid.InternalSidState.ClearAudioChanged();

    }
    private async Task GenerateSoundAsync(InternalSidState sidInternalStateClone)
    {
        foreach (var voice in _soundHandlerContext!.VoiceContexts.Keys)
        {
            var voiceContext = _soundHandlerContext!.VoiceContexts[voice];
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, sidInternalStateClone);
            if (wasmSoundParameters.SoundCommand == C64SoundCommand.None)
                continue;
            await PlaySound(voiceContext, wasmSoundParameters);
        }
    }

    private Task[] CreateSoundTasks(InternalSidState sidInternalStateClone)
    {
        var playSoundTasks = new List<Task>();

        foreach (var voice in _soundHandlerContext!.VoiceContexts.Keys)
        {
            var voiceContext = _soundHandlerContext!.VoiceContexts[voice];
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, sidInternalStateClone);
            if (wasmSoundParameters.SoundCommand == C64SoundCommand.None)
                continue;

            //await PlaySound(voiceContext, wasmSoundParameters);
            //var task = PlaySound(voiceContext, wasmSoundParameters);
            //var task = new Task(async () => await PlaySound(voiceContext, wasmSoundParameters));
            var task = new Task(async () => await PlaySoundGated(voiceContext, wasmSoundParameters));
            playSoundTasks.Add(task);
        }

        return playSoundTasks.ToArray();
    }

    public void GenerateSound(ISystem system)
    {
        GenerateSound((C64)system);
    }

    private C64WASMVoiceParameter BuildWASMSoundParametersFromC64Sid(C64WASMVoiceContext voiceContext, InternalSidState sidState)
    {
        // TODO: Read clock speed from config, different for NTSC and PAL.
        float clockSpeed = 1022730;

        var voice = voiceContext.Voice;

        // Get common memory locations
        ushort sigvol = SidAddr.SIGVOL;
        // Get voice-specific memory locations
        ushort vcreg = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.VCREG}{voice}"];
        ushort frelo = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.FRELO}{voice}"];
        ushort frehi = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.FREHI}{voice}"];
        ushort atdcy = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.ATDCY}{voice}"];
        ushort surel = SidAddr.VoiceRegisterMap[$"{SidVoiceRegisterType.SUREL}{voice}"];

        // ----------
        // Map voice Waveform to Web Audio oscillator type
        // ----------
        OscillatorType? oscillatorType = null;
        if (sidState[vcreg].IsBitSet(4))         // Select triangle waveform
        {
            oscillatorType = OscillatorType.Triangle;
        }
        else if (sidState[vcreg].IsBitSet(5))    // Select sawtooth waveform
        {
            oscillatorType = OscillatorType.Sawtooth;
        }
        else if (sidState[vcreg].IsBitSet(6))    // Select pulse waveform
        {
            // TODO: Specify custom waveform parameters for simulate pulse waveform
            oscillatorType = OscillatorType.Custom;
        }
        else if (sidState[vcreg].IsBitSet(7))    // Select random noise waveform
        {
            // TODO: Specify custom waveform parameters for simulate random noise waveform
            oscillatorType = OscillatorType.Custom;
        }

        // ----------
        // Start or stop playing
        // ----------
        // Starting the ADS (attack/decay/sustain) cycle
        // - setting the "Gate Bit" of VCREG to 1 (also requires that wave form has been selected)
        //
        // After a sounnd has been started, start the R (release) cycle
        // - setting the "Gate Bit" of VCREG to 0.
        //
        // After a sound has been started (or during release cycle), stopping a sound can be done by
        //  - setting waveform (type) bit back to 0
        //
        // After a sound has been started, the volume can be changed
        //
        // After a sound has been started, the frequency can be changed

        var command = C64SoundCommand.None;

        foreach (var voiceAction in sidState.ChannelAudioActions[voice])
        {
            // New sound can only be started when
            // - Gate bit and waveform bit is set
            // - and no sound is playing (or when the release cycle has started)
            if (voiceAction == SidVoiceActionType.GateSet
                && oscillatorType.HasValue
                && (voiceContext.Status == C64SoundStatus.Stopped ||
                    voiceContext.Status == C64SoundStatus.ReleaseCycleStarted)
                )
            {
                command = C64SoundCommand.StartADS;
            }

            // Release cycle can be started when
            // - ASD cycle has already been started
            // - and Gate bit is cleared
            // 
            else if (voiceContext.Status == C64SoundStatus.ASDCycleStarted
                && voiceAction == SidVoiceActionType.GateClear)
            {
                command = C64SoundCommand.StartRelease;
            }

            // Sound can be stopped immediatley when
            // - Release cycle has already been started
            // - and all Waveform selection bits are cleared
            else if (voiceAction == SidVoiceActionType.WaveFormClear
                && voiceContext.Status == C64SoundStatus.ASDCycleStarted)
            {
                command = C64SoundCommand.Stop;
            }

            // Unless a command has already been detected above, check other actions
            if (command == C64SoundCommand.None)
            {
                // Change frequency on any currently playing sound.
                if (voiceAction == SidVoiceActionType.FrequencySet
                    && (voiceContext.Status == C64SoundStatus.ASDCycleStarted
                    || (voiceContext.Status == C64SoundStatus.ReleaseCycleStarted))
                    )
                {
                    AddDebugMessage($"vcreg{voice} ({vcreg}) = {sidState[vcreg]}");

                    command = C64SoundCommand.ChangeFrequency;
                }
            }

            // Don't process any more actions
            if (command != C64SoundCommand.None)
                break;
        }

        // Unless a voice specific command has been found, check if any common stuff has changed such as volume
        if (command == C64SoundCommand.None && sidState.CommonAudioActions.Count > 0)
        {
            // Assume only one common action
            var commonAction = sidState.CommonAudioActions[0];
            if (commonAction == SidCommonActionType.VolumeSet)
                command = C64SoundCommand.ChangeVolume;
        }

        // ----------
        // Map SID register values to sound parameters usable by Web Audio
        // ----------
        var soundParameters = new C64WASMVoiceParameter
        {
            SoundCommand = command,

            // Oscillator type mapped from C64 SID wave form selection
            Type = oscillatorType,

            // Translate SID volume 0-15 to Gain 0.0-1.0
            // SID volume in lower 4 bits of SIGVOL register.
            Gain = Math.Clamp((float)((sidState[sigvol] & 0b00001111) / 15.0f), 0.0f, 1.0f),

            // Translate SID frequency (0 - 65536) to actual frequency number
            // Frequency = (REGISTER VALUE * CLOCK / 16777216) Hz
            // where CLOCK equals the system clock frequency, 1022730 for American (NTSC)systems, 985250 for European(PAL)
            Frequency = ByteHelpers.ToLittleEndianWord(sidState[frelo], sidState[frehi]) * clockSpeed / 16777216.0f,

            // Translate SID attack duration in ms to seconds
            // Attack: 0-15, highest 4 bits in ATDCY
            // The values 0-15 represents different amount of milliseconds, read from lookup table.
            AttackDurationSeconds = Sid.AttackDurationMs[sidState[atdcy] >> 4] / 1000.0,

            // Translate SID decay duration in ms to seconds
            // Decay: 0-15, lowest 4 bits in ATDCY
            // The values 0-15 represents different amount of milliseconds, read from lookup table.
            DecayDurationSeconds = Sid.DecayDurationMs[sidState[atdcy] & 0b00001111] / 1000.0,

            // Translate SID sustain volume 0-15 to Sustain Gain 0.0-1.0
            // Sustain level: 0-15, highest 4 bits in SUREL
            // The values 0-15 represents volume
            SustainGain = Math.Clamp((float)((sidState[surel] >> 4) / 15.0f), 0.0f, 1.0f),

            // Translate SID release duration in ms to seconds
            // Release: 0-15, lowest 4 bits in SUREL
            // The values 0-15 represents different amount of milliseconds, read from lookup table.
            ReleaseDurationSeconds = Sid.ReleaseDurationMs[sidState[surel] & 0b00001111] / 1000.0,
        };

        return soundParameters;
    }

    private async Task PlaySoundGated(C64WASMVoiceContext voiceContext, C64WASMVoiceParameter wasmSoundParameters)
    {
        await voiceContext.SemaphoreSlim.WaitAsync();
        await PlaySound(voiceContext, wasmSoundParameters);
        voiceContext.SemaphoreSlim.Release();
    }

    private async Task PlaySound(C64WASMVoiceContext voiceContext, C64WASMVoiceParameter wasmSoundParameters)
    {
        if (wasmSoundParameters.SoundCommand == C64SoundCommand.Stop)
        {
            // Stop sound immediately
            if (voiceContext.Oscillator is null) return;
            await voiceContext.Oscillator.StopAsync();
            voiceContext.Init();
        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.StartADS)
        {
            // The time the sound starts playing
            var currentTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();

            if (voiceContext.Oscillator != null && voiceContext.GainNode != null)
            {
                await voiceContext.Oscillator.StopAsync();
                await voiceContext.GainNode.DisconnectAsync();
                await voiceContext.Oscillator.DisconnectAsync();
            }

            // Create Oscillator
            OscillatorOptions oscillatorOptions = new()
            {
                Type = wasmSoundParameters.Type!.Value,
                Frequency = wasmSoundParameters.Frequency,
            };
            voiceContext.Oscillator = await OscillatorNode.CreateAsync(_soundHandlerContext.JSRuntime, _soundHandlerContext.AudioContext, oscillatorOptions);
            if (wasmSoundParameters.Type == OscillatorType.Custom)
            {
                // TODO: Set custom waveform
                //_soundHandlerContext.Oscillator.SetPeriodicWave(customWaveform);
            }

            // Set callback to know when Osciallator has finnished playing the current sound
            var callback = await EventListener<Event>.CreateAsync(_soundHandlerContext.AudioContext.JSRuntime, async (e) =>
            {
                AddDebugMessage($"Sound stopped on voice {voiceContext.Voice}.");
                voiceContext.Status = C64SoundStatus.Stopped;
            });
            await voiceContext.Oscillator.AddEndedEventListsnerAsync(callback);

            // Create GainNode
            voiceContext.GainNode = await GainNode.CreateAsync(_soundHandlerContext.JSRuntime, _soundHandlerContext.AudioContext);

            // Associate GainNode with AudioContext destination
            var destination = await _soundHandlerContext.AudioContext.GetDestinationAsync();
            await voiceContext.GainNode.ConnectAsync(destination);

            // Associate volume gain with Oscillator
            await voiceContext.Oscillator.ConnectAsync(voiceContext.GainNode);

            var gainAudioParam = await voiceContext.GainNode!.GetGainAsync();
            //await gainAudioParam.CancelScheduledValuesAsync(currentTime);

            await gainAudioParam.SetValueAtTimeAsync(0, currentTime);
            await gainAudioParam.LinearRampToValueAtTimeAsync(wasmSoundParameters.Gain, currentTime + wasmSoundParameters.AttackDurationSeconds);
            await gainAudioParam.SetTargetAtTimeAsync(wasmSoundParameters.SustainGain, currentTime + wasmSoundParameters.AttackDurationSeconds, wasmSoundParameters.DecayDurationSeconds);

            AddDebugMessage($"Starting sound on voice {voiceContext.Voice} with freq {wasmSoundParameters.Frequency}");
            voiceContext.Status = C64SoundStatus.ASDCycleStarted;
            // Start sound with a Attack/Decay/Sustain envelope
            await voiceContext.Oscillator.StartAsync();
        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.StartRelease)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            // The current time is where the Release cycle starts
            var currentTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();

            var gainAudioParam = await voiceContext.GainNode.GetGainAsync();
            await gainAudioParam.CancelScheduledValuesAsync(currentTime);

            // Schedule a volume change from current gain level down to 0 during specified Release time 
            var currentGainValue = await gainAudioParam.GetCurrentValueAsync();
            await gainAudioParam.SetValueAtTimeAsync(currentGainValue, currentTime);
            await gainAudioParam.LinearRampToValueAtTimeAsync(0, currentTime + wasmSoundParameters.ReleaseDurationSeconds);

            AddDebugMessage($"Stopping sound on voice {voiceContext.Voice} at time now + {wasmSoundParameters.ReleaseDurationSeconds} seconds.");

            voiceContext.Status = C64SoundStatus.ReleaseCycleStarted;
            // Schedule Stop for the time when the Release period if sover
            await voiceContext.Oscillator.StopAsync(currentTime + wasmSoundParameters.ReleaseDurationSeconds);

        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.ChangeVolume)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            var currentTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();
            await ChangeVolume(voiceContext, wasmSoundParameters, currentTime);
        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.ChangeFrequency)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            var currentTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();
            await ChangeFrequency(voiceContext, wasmSoundParameters, currentTime);
        }
    }

    private async Task ChangeVolume(C64WASMVoiceContext voiceContext, C64WASMVoiceParameter wasmSoundParameters, double changeTime)
    {
        // The current time is where the gain change starts
        var gainAudioParam = await voiceContext.GainNode!.GetGainAsync();
        // Check if the gain of the actual oscillator is different from the new gain
        // (the gain could have changed by ADSR cycle, LinearRampToValueAtTimeAsync)
        var currentGainValue = await gainAudioParam.GetCurrentValueAsync();
        if (currentGainValue != wasmSoundParameters.Gain)
        {
            AddDebugMessage($"Changing vol on voice {voiceContext.Voice} to {wasmSoundParameters.Gain}.");
            await gainAudioParam.SetValueAtTimeAsync(wasmSoundParameters.Gain, changeTime);
        }
    }

    private async Task ChangeFrequency(C64WASMVoiceContext voiceContext, C64WASMVoiceParameter wasmSoundParameters, double changeTime)
    {
        var frequencyAudioParam = await voiceContext.Oscillator!.GetFrequencyAsync();
        // Check if the frequency of the actual oscillator is different from the new frequency
        // TODO: Is this necessary to check? Could the frequency have been changed in other way?
        var currentFrequencyValue = await frequencyAudioParam.GetCurrentValueAsync();
        if (currentFrequencyValue != wasmSoundParameters.Frequency)
        {
            AddDebugMessage($"Changing freq on voice {voiceContext.Voice} to {wasmSoundParameters.Frequency}.");
            await frequencyAudioParam.SetValueAtTimeAsync(wasmSoundParameters.Frequency, changeTime);
        }
    }


    public List<string> GetDebugMessages()
    {
        return _debugMessages;
    }
}
