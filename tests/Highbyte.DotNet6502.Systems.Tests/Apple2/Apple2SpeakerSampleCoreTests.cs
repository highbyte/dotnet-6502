using Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// The speaker resampler. Driven synchronously — feed it cycles and toggles, inspect the samples —
/// so the audio behaviour can be asserted without a sound device or a running machine.
/// </summary>
public class Apple2SpeakerSampleCoreTests
{
    private const double CpuHz = 1_020_484.0;
    private const int SampleRate = 44100;

    private static Apple2SpeakerSampleCore BuildCore() => new(CpuHz, SampleRate);

    /// <summary>Runs a square wave of the given half-period, returning the samples produced.</summary>
    private static List<float> RunSquareWave(
        Apple2SpeakerSampleCore core, int halfPeriodCycles, int toggles)
    {
        var samples = new List<float>();
        var buffer = new float[64];

        for (var i = 0; i < toggles; i++)
        {
            var written = core.AdvanceCycles(halfPeriodCycles, buffer);
            for (var s = 0; s < written; s++)
                samples.Add(buffer[s]);
            core.SetLevel(!core.Level);
        }
        return samples;
    }

    [Fact]
    public void One_Sample_Spans_About_Twenty_Three_Cycles()
    {
        var core = BuildCore();

        // 1.02 MHz / 44.1 kHz. The averaging window depends on this, so a wrong value would shift
        // every pitch the machine produces.
        Assert.InRange(core.CyclesPerSample, 23.0, 23.3);
    }

    [Fact]
    public void Cycles_Produce_Samples_At_The_Output_Rate()
    {
        var core = BuildCore();
        var buffer = new float[4096];

        // One second of CPU cycles must yield one second of samples.
        var total = 0;
        var remaining = (int)CpuHz;
        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, 1000);
            total += core.AdvanceCycles(chunk, buffer);
            remaining -= chunk;
        }

        Assert.InRange(total, SampleRate - 2, SampleRate + 2);
    }

    [Fact]
    public void A_Silent_Speaker_Produces_Silence()
    {
        var core = BuildCore();
        var buffer = new float[4096];

        // Never toggled: a constant level is a DC offset, not a sound, and must decay to nothing.
        core.AdvanceCycles(200_000, buffer);
        var written = core.AdvanceCycles(100_000, buffer);

        for (var i = 0; i < written; i++)
            Assert.Equal(Apple2SpeakerSampleCore.Silence, buffer[i], 3);
    }

    [Fact]
    public void A_Parked_Level_Decays_Rather_Than_Holding_An_Offset()
    {
        var core = BuildCore();
        var buffer = new float[4096];

        core.SetLevel(true);
        var written = core.AdvanceCycles(2_000, buffer);
        Assert.True(written > 0);
        var justAfterTheEdge = Math.Abs(buffer[0]);

        // Half a second later the offset must be gone.
        for (var i = 0; i < 20; i++)
            written = core.AdvanceCycles(25_000, buffer);

        Assert.True(justAfterTheEdge > 0.1f, $"Expected an audible edge, got {justAfterTheEdge}.");
        Assert.Equal(Apple2SpeakerSampleCore.Silence, buffer[written - 1], 3);
    }

    /// <summary>
    /// A 1 kHz tone is a toggle every half period. Measuring the produced samples' zero crossings
    /// checks the whole cycles-to-pitch chain, which is the thing a listener would notice first.
    /// </summary>
    [Fact]
    public void A_Toggle_Rate_Produces_The_Matching_Pitch()
    {
        var core = BuildCore();
        const double targetHz = 1000.0;
        var halfPeriodCycles = (int)Math.Round(CpuHz / targetHz / 2.0);

        var samples = RunSquareWave(core, halfPeriodCycles, toggles: 400);

        // Count rising zero crossings over the settled portion and convert to Hz.
        var settled = samples.Skip(samples.Count / 4).ToList();
        var crossings = 0;
        for (var i = 1; i < settled.Count; i++)
        {
            if (settled[i - 1] <= 0f && settled[i] > 0f)
                crossings++;
        }
        var seconds = settled.Count / (double)SampleRate;
        var measuredHz = crossings / seconds;

        Assert.InRange(measuredHz, targetHz * 0.97, targetHz * 1.03);
    }

    /// <summary>
    /// The reason for averaging rather than point-sampling. A pulse train faster than the sample
    /// rate is how the Apple II fakes intermediate levels; each output sample must land near the
    /// duty cycle, not snap to one extreme.
    /// </summary>
    [Fact]
    public void Toggling_Faster_Than_The_Sample_Rate_Averages_To_The_Duty_Cycle()
    {
        var core = BuildCore();
        var buffer = new float[64];
        var samples = new List<float>();

        // 4 cycles high, 12 low, repeatedly — a 25% duty cycle, far faster than one sample window.
        for (var i = 0; i < 20_000; i++)
        {
            core.SetLevel(true);
            var written = core.AdvanceCycles(4, buffer);
            for (var s = 0; s < written; s++) samples.Add(buffer[s]);

            core.SetLevel(false);
            written = core.AdvanceCycles(12, buffer);
            for (var s = 0; s < written; s++) samples.Add(buffer[s]);
        }

        // Expected level: 25% at +A and 75% at -A, i.e. -0.5 * A. The DC blocker removes the
        // steady part, so what matters is that the output is steady rather than swinging between
        // the extremes — point-sampling would give a full-amplitude square at an alias frequency.
        var settled = samples.Skip(samples.Count / 2).ToList();
        var peak = settled.Max(Math.Abs);

        Assert.True(
            peak < Apple2SpeakerSampleCore.Amplitude * 0.25f,
            $"Expected a steady averaged level, but saw swings up to {peak}.");
    }

    [Fact]
    public void Reset_Clears_The_Level_And_The_Filter()
    {
        var core = BuildCore();
        var buffer = new float[4096];

        core.SetLevel(true);
        core.AdvanceCycles(5_000, buffer);
        core.Reset();

        Assert.False(core.Level);
        var written = core.AdvanceCycles(5_000, buffer);
        for (var i = 0; i < written; i++)
            Assert.Equal(Apple2SpeakerSampleCore.Silence, buffer[i], 3);
    }
}
