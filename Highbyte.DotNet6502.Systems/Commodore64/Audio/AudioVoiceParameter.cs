namespace Highbyte.DotNet6502.Systems.Commodore64.Audio
{
    public class AudioVoiceParameter
    {
        public AudioCommand AudioCommand { get; set; }
        public SidVoiceWaveForm SIDOscillatorType { get; set; }

        public float Gain { get; set; }
        public float Frequency { get; set; }
        public float PulseWidth { get; set; }
        public double AttackDurationSeconds { get; set; }
        public double DecayDurationSeconds { get; set; }
        public float SustainGain { get; internal set; }
        public double ReleaseDurationSeconds { get; set; }

        public static AudioVoiceParameter BuildAudioVoiceParameter(
            byte voice,
            AudioStatus audioStatus,
            InternalSidState sidState
            )
        {
            // TODO: Read clock speed from config, different for NTSC and PAL.
            float clockSpeed = 1022730;

            // ----------
            // Map SID register values to audio parameters usable by Web Audio, and what to do with the audio.
            // ----------
            var audioVoiceParameter = new AudioVoiceParameter
            {
                // What to do with the audio (Start ADS cycle, start Release cycle, stop audio, change frequency, change volume)
                AudioCommand = AudioCommandBuilder.GetAudioCommand(
                    voice,
                    audioStatus,
                    sidState),

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
            return audioVoiceParameter;
        }
    }
}
