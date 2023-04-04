using KristofferStrube.Blazor.WebAudio;

namespace Highbyte.DotNet6502.Impl.AspNet.Commodore64
{
    public class C64WASMVoiceParameter
    {
        public C64SoundCommand SoundCommand { get; set; }
        public OscillatorType? Type { get; set; }

        public float Gain { get; set; }
        public float Frequency { get; set; }
        public double AttackDurationSeconds { get; set; }
        public double DecayDurationSeconds { get; set; }
        public float SustainGain { get; internal set; }
        public double ReleaseDurationSeconds { get; set; }
    }

    public enum C64SoundStatus
    {
        /// <summary>
        /// Attack/Sustain/Decay cycle has been started.
        /// It's started by setting the Gate bit to 1 (and a waveform has been selected).
        /// </summary>
        ASDCycleStarted,
        /// <summary>
        /// Release cycle has been started.
        /// It's started by setting the Gate bit to 0.
        /// During relase cycle, a new sound can be started by setting the Gate bit to 1 (this will stop current sound and start a new one)
        /// </summary>
        ReleaseCycleStarted,

        /// <summary>
        /// The sound has stopped playing.
        /// Happens by
        /// - release cycle has completed.
        /// - or stopping the sound right away by clearing all waveform selection bits
        /// </summary>
        Stopped
    }

    public enum C64SoundCommand
    {
        None,
        StartADS,           // Start attack/decay/sustain cycle.
        StartRelease,       // Start release cycle, which will fade volume down to 0 during the release period.
        ChangeFrequency,    // Change frequency on current playing sound.
        ChangeVolume,       // Change volume on current playing sound.
        Stop                // Stop current playing sound right away.
    }
}
