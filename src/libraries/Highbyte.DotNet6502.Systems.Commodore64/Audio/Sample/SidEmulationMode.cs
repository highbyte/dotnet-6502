namespace Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

/// <summary>
/// Selects the accuracy / performance trade-off for <see cref="SidSampleCore"/>.
/// </summary>
public enum SidEmulationMode
{
    /// <summary>
    /// Full sample-accurate emulation: combined waveforms, hard sync, ring modulation,
    /// TEST-bit hold behaviour, OSC3/ENV3 readback, and the SID filter (low/band/high-pass with
    /// resonance, per-voice routing, voice 3 disable). Inner loop takes auto fast paths when the
    /// current SID state doesn't actually use these features, so simple tunes don't pay extra
    /// cost. Default.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Single waveform per voice (lowest-numbered bit wins),
    /// no hard sync, no ring modulation, no TEST-bit hold, no OSC3/ENV3 readback, no filter,
    /// no voice 3 disable. Modestly lower CPU for tunes that actively use hard sync / combined
    /// waveforms (the main per-cycle saving comes from collapsing TickAllVoices's 3-pass sync
    /// path to a single fused loop); essentially identical to Auto for simple tunes. Many tunes
    /// will sound wrong.
    /// </summary>
    Fast = 1,
}
