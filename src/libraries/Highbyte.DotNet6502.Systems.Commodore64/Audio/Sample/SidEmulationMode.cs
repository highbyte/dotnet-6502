namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

/// <summary>
/// Selects the accuracy / performance trade-off for <see cref="SidSampleCore"/>.
/// </summary>
public enum SidEmulationMode
{
    /// <summary>
    /// Full sample-accurate emulation: combined waveforms, hard sync, ring modulation,
    /// TEST-bit hold behaviour, OSC3/ENV3 readback. Inner loop takes auto fast paths when the
    /// current SID state doesn't actually use the advanced features, so simple tunes don't pay
    /// extra cost. Default.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Single waveform per voice (lowest-numbered bit wins),
    /// no hard sync, no ring modulation, no TEST-bit hold, no OSC3/ENV3 readback. Lower CPU,
    /// some tunes (combined-waveform leads, sync basses, $D41B-driven effects) will sound wrong.
    /// </summary>
    Fast = 1,
}
