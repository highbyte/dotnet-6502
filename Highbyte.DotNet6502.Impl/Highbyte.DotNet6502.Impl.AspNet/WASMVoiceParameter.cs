using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync.Options;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.AspNet
{
    public class WASMVoiceParameter
    {
        public AudioCommand AudioCommand { get; set; }
        public SidVoiceWaveForm SIDOscillatorType { get; set; }

        ///// <summary>
        ///// Used when Type is set to OscillatorType.Custom and SpecialType is set to OscillatorSpecialType.Noise.
        ///// </summary>
        //public PeriodicWaveOptions? PeriodicWaveOptions { get; internal set; }

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

    public enum AudioStatus
    {
        /// <summary>
        /// Attack/Decay/Sustain cycle has been started.
        /// It's started by setting the Gate bit to 1 (and a waveform has been selected).
        /// </summary>
        ADSCycleStarted,
        /// <summary>
        /// Release cycle has been started.
        /// It's started by setting the Gate bit to 0.
        /// During release cycle, a new audio can be started by setting the Gate bit to 1 (this will stop current sound and start a new one)
        /// </summary>
        ReleaseCycleStarted,

        /// <summary>
        /// The audio has stopped playing.
        /// Happens by
        /// - release cycle has completed.
        /// - or stopping the audio right away by clearing all waveform selection bits
        /// </summary>
        Stopped
    }

    public enum AudioCommand
    {
        None,
        StartADS,           // Start attack/decay/sustain cycle.
        StartRelease,       // Start release cycle, which will fade volume down to 0 during the release period.
        ChangeFrequency,    // Change frequency on current playing audio.
        ChangePulseWidth,    // Change pulse width on current playing audio (only for pulse oscillator)
        ChangeVolume,       // Change volume on current playing audio.
        Stop                // Stop current playing audio right away.
    }

}
