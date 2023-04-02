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
        /// Is started by setting the Gate bit to 1, and a waveform has been selected.
        /// </summary>
        ASDCycleStarted,
        /// <summary>
        /// Release cycle has been started or sound has been stopped right away.
        /// - The Release cycle is started by setting the Gate bit to 0.
        /// - Stopping a sound right away is by setting no waveform.
        /// 
        /// In either case (even if Release has to reached 0 gain), a new sound can be started by again setting Gate bit to 1
        /// </summary>
        Stopped
    }

    public enum C64SoundCommand
    {
        None,
        StartADS,       // Start attack/decay/sustain cycle
        StartRelease,   // Start release cycle
        ChangeGainAndFrequency, // Change Gain and/or Frequency on currently playing sound
        Stop
    }
}
