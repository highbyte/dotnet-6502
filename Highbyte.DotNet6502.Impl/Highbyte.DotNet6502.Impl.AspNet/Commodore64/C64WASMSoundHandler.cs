using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems;
using KristofferStrube.Blazor.WebAudio;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64;

public class C64WASMSoundHandler : ISoundHandler<C64, C64WASMSoundHandlerContext>, ISoundHandler
{
    private C64WASMSoundHandlerContext? _soundHandlerContext;

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

    public async Task GenerateSound(C64 c64)
    {
        var sid = c64.Sid;
        if (!sid.InternalSidState.AudioChanged)
            return;
        sid.InternalSidState.ClearAudioChanged();

        foreach (var voice in _soundHandlerContext!.VoiceContexts.Keys)
        {
            var voiceContext = _soundHandlerContext!.VoiceContexts[voice];
            var wasmSoundParameters = BuildWASMSoundParametersFromC64Sid(voiceContext, c64.Sid);
            if (wasmSoundParameters.SoundCommand == C64SoundCommand.None)
                continue;
            await PlaySound(voiceContext, wasmSoundParameters);
        }

        //PlaySound(wasmSoundParameters).GetAwaiter().GetResult();
        //PlaySound(wasmSoundParameters).Wait();
        //PlaySound(wasmSoundParameters).ConfigureAwait(false);

        //Task.Run(async () => await PlaySound(wasmSoundParameters)).Wait();
        //Task.Run(async () => await PlaySound(wasmSoundParameters)).GetAwaiter().GetResult();
        //Task.Run(async () => await PlaySound(wasmSoundParameters).ConfigureAwait(false)).Wait();

        //Task.Run(() => PlaySound(wasmSoundParameters)).Wait();
        //Task.Run(() => PlaySound(wasmSoundParameters)).GetAwaiter().GetResult();

        //PlaySound(wasmSoundParameters).GetAwaiter().GetResult();
        //PlaySound(wasmSoundParameters);

        //var task = Task.Run(async () => await PlaySound(wasmSoundParameters));
        //task.Start();
        //task.Wait();

        //await Task.Run(async () =>
        //{
        //    await PlaySound(wasmSoundParameters);
        //});


        //await Task.CompletedTask;
        //return;


    }

    public async Task GenerateSound(ISystem system)
    {
        await GenerateSound((C64)system);
    }

    private C64WASMVoiceParameter BuildWASMSoundParametersFromC64Sid(C64WASMVoiceContext voiceContext, Sid sid)
    {
        var mem = sid.InternalSidState;
        var voice = voiceContext.Voice;

        // ----------
        // Volume
        // ----------
        // SID volume in lower 4 bits of SIGVOL register.
        // Shared between all voices.
        var sidVolume = (byte)(mem[SidAddr.SIGVOL] & 0b00001111);
        // Gain. Translate SID volume 0-15 to Gain 0.0-1.0
        var gain = Math.Clamp((float)(sidVolume / 15.0f), 0.0f, 1.0f);

        // ----------
        // Waveform (type)
        // ----------
        ushort vcreg = voice switch
        {
            1 => SidAddr.VCREG1,
            2 => SidAddr.VCREG2,
            3 => SidAddr.VCREG3,
            _ => throw new ArgumentException($"Value '{voice}' is not a valid Voice.")
        };

        OscillatorType? type = null;
        if (mem[vcreg].IsBitSet(4))         // Select triangle waveform
        {
            type = OscillatorType.Triangle;
        }
        else if (mem[vcreg].IsBitSet(5))    // Select sawtooth waveform
        {
            type = OscillatorType.Sawtooth;
        }
        else if (mem[vcreg].IsBitSet(6))    // Select pulse waveform
        {
            // TODO: Specify custom waveform parameters for simulate pulse waveform
            type = OscillatorType.Custom;
        }
        else if (mem[vcreg].IsBitSet(7))    // Select random noise waveform
        {
            // TODO: Specify custom waveform parameters for simulate random noise waveform
            type = OscillatorType.Custom;
        }

        // ----------
        // Frequency
        // ----------
        // Frequency = (REGISTER VALUE * CLOCK / 16777216) Hz
        // where CLOCK equals the system clock frequency, 1022730 for American (NTSC)systems, 985250 for European(PAL)
        (ushort Lo, ushort Hi) fre = voice switch
        {
            1 => (SidAddr.FRELO1, SidAddr.FREHI1),
            2 => (SidAddr.FRELO2, SidAddr.FREHI2),
            3 => (SidAddr.FRELO3, SidAddr.FREHI3),
            _ => throw new ArgumentException($"Value '{voice}' is not a valid Voice.")
        };
        var sidFreq = ByteHelpers.ToLittleEndianWord(mem[fre.Lo], mem[fre.Hi]);

        float clockSpeed = 1022730; // TODO: Read clock speed from config, different for NTSC and PAL.
        var frequency = sidFreq * clockSpeed / 16777216.0f;

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
        if (voiceContext.Status != C64SoundStatus.ASDCycleStarted
            && type.HasValue
            && mem[vcreg].IsBitSet(0)
            )
        {
            // No sound was currently playing (a ASD cycle has not been started)
            command = C64SoundCommand.StartADS;
        }
        else if (voiceContext.Status == C64SoundStatus.ASDCycleStarted)
        {
            // A sound is currently playing (a ASD cycle has been started)

            // Start Release cycle if Gate Bit is cleared (0)
            if (!mem[vcreg].IsBitSet(0))
                command = C64SoundCommand.StartRelease;
            // If no wave form type has been selected, the sound should be stopped immediately.
            else if (!type.HasValue)
                command = C64SoundCommand.Stop;
        }

        // Unless a new sound is started or release cycle started, check if the gain or frequency has changed.
        if (command == C64SoundCommand.None)
        {
            if ((voiceContext.CurrentGain.HasValue && voiceContext.CurrentGain.Value != gain)
                || (voiceContext.CurrentFrequency.HasValue && voiceContext.CurrentFrequency.Value != frequency))
            {
                command = C64SoundCommand.ChangeGainAndFrequency;
            }
        }

        // Attack & Decay register
        ushort atdcy = voice switch
        {
            1 => SidAddr.ATDCY1,
            2 => SidAddr.ATDCY2,
            3 => SidAddr.ATDCY3,
            _ => throw new ArgumentException($"Value '{voice}' is not a valid Voice.")
        };

        // Attack: 0-15, highest 4 bits in ATDCY
        // The values 0-15 represents different amount of milliseconds, read from lookup table.
        var sidAttack = mem[atdcy] >> 4;
        var attackDurationMs = Sid.AttackDurationMs[sidAttack];

        // Decay: 0-15, lowest 4 bits in ATDCY
        // The values 0-15 represents different amount of milliseconds, read from lookup table.
        var sidDecay = mem[atdcy] & 0b00001111;
        var decayDurationMs = Sid.DecayDurationMs[sidDecay];


        // Sustain & Release register
        ushort surel = voice switch
        {
            1 => SidAddr.SUREL1,
            2 => SidAddr.SUREL2,
            3 => SidAddr.SUREL3,
            _ => throw new ArgumentException($"Value '{voice}' is not a valid Voice.")
        };

        // Sustain level: 0-15, highest 4 bits in SUREL
        // The values 0-15 represents volume
        var sidSustain = mem[surel] >> 4;
        var sustainVolume = Math.Clamp((float)(sidSustain / 15.0f), 0.0f, 1.0f);

        // Release: 0-15, lowest 4 bits in SUREL1
        // The values 0-15 represents different amount of milliseconds, read from lookup table.
        var sidRelease = mem[surel] & 0b00001111;
        var releaseDurationMs = Sid.ReleaseDurationMs[sidRelease];

        var soundParameters = new C64WASMVoiceParameter
        {
            SoundCommand = command,
            Type = type,
            Gain = gain,
            Frequency = frequency,
            AttackDurationSeconds = attackDurationMs / 1000.0,
            DecayDurationSeconds = decayDurationMs / 1000.0,
            SustainGain = sustainVolume,
            ReleaseDurationSeconds = releaseDurationMs / 1000.0,
        };

        return soundParameters;
    }

    private async Task PlaySound(C64WASMVoiceContext voiceContext, C64WASMVoiceParameter wasmSoundParameters)
    {
        await voiceContext.SemaphoreSlim.WaitAsync();

        if (wasmSoundParameters.SoundCommand == C64SoundCommand.Stop)
        {
            // Stop sound immediately
            if (voiceContext.Oscillator is null)
                return;
            await voiceContext.Oscillator.StopAsync();
            voiceContext.Init();
            voiceContext.Status = C64SoundStatus.Stopped;
        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.StartADS)
        {

            // the time the sound started playing
            var soundStartTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();

            // Attack -> Decay -> Sustain
            voiceContext.GainNode = await GainNode.CreateAsync(_soundHandlerContext.JSRuntime, _soundHandlerContext.AudioContext);
            var destination = await _soundHandlerContext.AudioContext.GetDestinationAsync();
            await voiceContext.GainNode.ConnectAsync(destination);
            var gainAudioParam = await voiceContext.GainNode.GetGainAsync();
            await gainAudioParam.SetValueAtTimeAsync(0, soundStartTime);
            await gainAudioParam.LinearRampToValueAtTimeAsync(wasmSoundParameters.Gain, soundStartTime + wasmSoundParameters.AttackDurationSeconds);
            await gainAudioParam.SetTargetAtTimeAsync(wasmSoundParameters.SustainGain, soundStartTime + wasmSoundParameters.AttackDurationSeconds, wasmSoundParameters.DecayDurationSeconds);

            // Configure oscillator
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

            // Associate volume gain with Oscillator
            await voiceContext.Oscillator.ConnectAsync(voiceContext.GainNode);
            // Start sound
            await voiceContext.Oscillator.StartAsync();

            voiceContext.CurrentGain = wasmSoundParameters.Gain;
            voiceContext.CurrentFrequency = wasmSoundParameters.Frequency;

            voiceContext.Status = C64SoundStatus.ASDCycleStarted;
        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.StartRelease)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            // The current time is where the Release cycle starts
            var soundReleaseTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();

            var gainAudioParam = await voiceContext.GainNode.GetGainAsync();
            await gainAudioParam.CancelScheduledValuesAsync(soundReleaseTime);

            // Play from current gain level down to 0 during specified Release time 
            var currentGainValue = await gainAudioParam.GetCurrentValueAsync();
            await gainAudioParam.SetValueAtTimeAsync(currentGainValue, soundReleaseTime);
            await gainAudioParam.LinearRampToValueAtTimeAsync(0, soundReleaseTime + wasmSoundParameters.ReleaseDurationSeconds);

            await voiceContext.Oscillator.StopAsync(soundReleaseTime + wasmSoundParameters.ReleaseDurationSeconds);

            // Note: Setting status Stopped here doesn't mean the sound isn't playing (it will for some time for duration of Release period),
            //       but rather indication that a new sound is now allowed to be started (setting gate bit to 1 in SID chip).
            voiceContext.Status = C64SoundStatus.Stopped;
        }
        else if (wasmSoundParameters.SoundCommand == C64SoundCommand.ChangeGainAndFrequency)
        {
            if (voiceContext.Oscillator == null || voiceContext.GainNode == null) return;

            if (voiceContext.CurrentGain.HasValue && voiceContext.CurrentGain.Value != wasmSoundParameters.Gain)
            {
                // The current time is where the gain change starts
                var gainAudioParam = await voiceContext.GainNode!.GetGainAsync();
                // Check if the gain of the actual oscillator is different from the new gain
                // (the gain could have changed by ADSR cycle, LinearRampToValueAtTimeAsync)
                var currentGainValue = await gainAudioParam.GetCurrentValueAsync();
                if (currentGainValue != wasmSoundParameters.Gain)
                {
                    var soundChangeTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();
                    await gainAudioParam.SetValueAtTimeAsync(wasmSoundParameters.Gain, soundChangeTime);

                    voiceContext.CurrentGain = wasmSoundParameters.Gain;
                }
            }

            if (voiceContext.CurrentFrequency.HasValue && voiceContext.CurrentFrequency.Value != wasmSoundParameters.Frequency)
            {
                var frequencyAudioParam = await voiceContext.Oscillator!.GetFrequencyAsync();
                // Check if the frequency of the actual oscillator is different from the new frequency
                // TODO: Is this necessary to check? Could the frequency have been changed in other way?
                var currentFrequencyValue = await frequencyAudioParam.GetCurrentValueAsync();
                if (currentFrequencyValue != wasmSoundParameters.Frequency)
                {
                    var soundChangeTime = await _soundHandlerContext!.AudioContext.GetCurrentTimeAsync();
                    await frequencyAudioParam.SetValueAtTimeAsync(wasmSoundParameters.Frequency, soundChangeTime);

                    voiceContext.CurrentFrequency = wasmSoundParameters.Frequency;
                }

            }
        }

        voiceContext.SemaphoreSlim.Release();
    }
}
