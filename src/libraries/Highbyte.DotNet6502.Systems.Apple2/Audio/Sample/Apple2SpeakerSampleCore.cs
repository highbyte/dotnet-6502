namespace Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;

/// <summary>
/// Turns the speaker's one-bit level over time into PCM.
///
/// This is the whole audio model for the machine, and it is a resampler rather than a synthesiser —
/// there is no chip to emulate, only a cone position and the cycles it held each position for.
///
/// <para>
/// <b>Averaging, not point-sampling.</b> One output sample spans about
/// <see cref="CyclesPerSample"/> CPU cycles, and software routinely toggles faster than that:
/// pulse-width modulation is how the Apple II fakes intermediate levels and plays sampled audio at
/// all. Point-sampling the level once per output sample would alias every one of those toggles
/// into noise, so each sample is the <em>average</em> level across its window. That is a box
/// filter — the cheapest honest anti-aliasing there is — and it reproduces PWM for free, because
/// the average of a pulse train is exactly its duty cycle.
/// </para>
///
/// <para>
/// <b>DC blocking.</b> A cone left parked on one side is a constant offset, not a sound. Left in,
/// it wastes headroom, clips, and thumps whenever a program leaves the speaker resting the other
/// way round. The real speaker is AC-coupled and hears only changes, so a one-pole high-pass does
/// the same job here.
/// </para>
/// </summary>
public sealed class Apple2SpeakerSampleCore
{
    public const int DefaultSampleRateHz = 44100;

    /// <summary>
    /// Where the resting level sits after the DC blocker has settled, and what silence sounds like.
    /// </summary>
    public const float Silence = 0f;

    /// <summary>
    /// Peak amplitude of the raw square before DC blocking. Well below full scale: a square wave is
    /// already the loudest thing a sample can be for a given peak, and the Apple II speaker is a
    /// small harsh thing that is unpleasant at full volume.
    /// </summary>
    public const float Amplitude = 0.25f;

    /// <summary>
    /// High-pass corner in Hz. Low enough to leave the lowest tones a program might produce alone,
    /// high enough to remove a resting offset within a few milliseconds.
    /// </summary>
    public const double DcBlockerCornerHz = 30.0;

    public int SampleRateHz { get; }

    /// <summary>CPU cycles per output sample — about 23 at 44.1 kHz on a 1.02 MHz machine.</summary>
    public double CyclesPerSample { get; }

    private readonly double _dcBlockerCoefficient;

    private bool _level;

    // Partial output sample being accumulated: how many cycles it has so far, and the integral of
    // the level over them.
    private double _cyclesIntoSample;
    private double _levelIntegral;

    // One-pole high-pass state.
    private float _dcPreviousInput;
    private float _dcPreviousOutput;

    public Apple2SpeakerSampleCore(double cpuHz, int sampleRateHz = DefaultSampleRateHz)
    {
        if (cpuHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(cpuHz), cpuHz, "CPU frequency must be positive.");
        if (sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, "Sample rate must be positive.");

        SampleRateHz = sampleRateHz;
        CyclesPerSample = cpuHz / sampleRateHz;
        _dcBlockerCoefficient = 1.0 - (2.0 * Math.PI * DcBlockerCornerHz / sampleRateHz);
        PrimeDcBlocker();
    }

    private float LevelValue => _level ? Amplitude : -Amplitude;

    /// <summary>
    /// Starts the high-pass already holding the current level, so beginning or resetting does not
    /// look like an edge. Without this, the filter's zeroed history against a non-zero resting
    /// level is a step, and a step through a high-pass is a click — on every reset.
    /// </summary>
    private void PrimeDcBlocker()
    {
        _dcPreviousInput = LevelValue;
        _dcPreviousOutput = 0f;
    }

    /// <summary>The cone position the next advanced cycles will be counted at.</summary>
    public bool Level => _level;

    /// <summary>
    /// Sets the cone position. Called after the cycles that ran at the previous position have been
    /// advanced, so a toggle takes effect from that point on.
    /// </summary>
    public void SetLevel(bool level) => _level = level;

    public void Reset()
    {
        _level = false;
        _cyclesIntoSample = 0;
        _levelIntegral = 0;
        PrimeDcBlocker();
    }

    /// <summary>
    /// Advances the given number of CPU cycles at the current level, writing any output samples
    /// that complete along the way.
    /// </summary>
    /// <returns>How many samples were written.</returns>
    public int AdvanceCycles(int cycles, Span<float> buffer)
    {
        if (cycles <= 0)
            return 0;

        var written = 0;
        double remaining = cycles;
        double levelValue = LevelValue;

        while (remaining > 0)
        {
            var cyclesToCompleteSample = CyclesPerSample - _cyclesIntoSample;

            if (remaining < cyclesToCompleteSample)
            {
                // Not enough to finish this sample; bank the partial contribution and stop.
                _levelIntegral += levelValue * remaining;
                _cyclesIntoSample += remaining;
                break;
            }

            _levelIntegral += levelValue * cyclesToCompleteSample;
            remaining -= cyclesToCompleteSample;

            var average = (float)(_levelIntegral / CyclesPerSample);
            _levelIntegral = 0;
            _cyclesIntoSample = 0;

            if (written < buffer.Length)
                buffer[written++] = BlockDc(average);
            // A full buffer means the caller under-sized it; the sample is dropped rather than
            // silently rolled into the next one, which would distort rather than just glitch.
        }

        return written;
    }

    /// <summary>One-pole high-pass: y[n] = x[n] - x[n-1] + R * y[n-1].</summary>
    private float BlockDc(float input)
    {
        var output = (float)(input - _dcPreviousInput + (_dcBlockerCoefficient * _dcPreviousOutput));
        _dcPreviousInput = input;
        _dcPreviousOutput = output;
        return output;
    }
}
