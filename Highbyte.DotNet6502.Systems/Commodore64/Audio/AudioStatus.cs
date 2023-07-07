namespace Highbyte.DotNet6502.Systems.Commodore64.Audio
{
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
}
