using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;

namespace Highbyte.DotNet6502.Impl.AspNet
{
    public class WASMVoiceParameter
    {
        public SoundCommand SoundCommand { get; set; }
        public OscillatorType? Type { get; set; }
        public OscillatorSpecialType? SpecialType { get; set; }

        /// <summary>
        /// Used when Type is set to OscillatorType.Custom and SpecialType is set to OscillatorSpecialType.Noise.
        /// </summary>
        public PeriodicWaveOptions? PeriodicWaveOptions { get; internal set; }

        /// <summary>
        /// Used when Type is set to OscillatorType.Custom and SpecialType is set to OscillatorSpecialType.Pulse.
        /// </summary>
        public CustomPulseOscillatorOptions? CustomPulseOscillatorOptions { get; internal set; }

        public float Gain { get; set; }
        public float Frequency { get; set; }
        public float PulseWidth { get; set; }
        public double AttackDurationSeconds { get; set; }
        public double DecayDurationSeconds { get; set; }
        public float SustainGain { get; internal set; }
        public double ReleaseDurationSeconds { get; set; }
    }

    public enum SoundStatus
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

    public enum SoundCommand
    {
        None,
        StartADS,           // Start attack/decay/sustain cycle.
        StartRelease,       // Start release cycle, which will fade volume down to 0 during the release period.
        ChangeFrequency,    // Change frequency on current playing sound.
        ChangePulseWidth,    // Change pulse width on curren playing sound (only for pulse oscillator)
        ChangeVolume,       // Change volume on current playing sound.
        Stop                // Stop current playing sound right away.
    }

    public enum OscillatorSpecialType
    {
        Noise,  // Using the built-in WebAudio OscillatorNode with type "custom" and a random periodicWave definition that generates noise.
        Pulse   // Using a custom PulseOscillatorNode implementation.
    }

}
