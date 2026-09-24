using Highbyte.DotNet6502.Systems.Oric.Audio;

namespace Highbyte.DotNet6502.Systems.Tests.Oric;

public sealed class Ay38912Tests
{
    private const int ClockHz = 1_000_000;

    [Fact]
    public void RegistersApplyHardwareBitMasks()
    {
        var ay = new Ay38912();
        ay.WriteRegister(1, 0xff);
        ay.WriteRegister(6, 0xff);
        ay.WriteRegister(8, 0xff);
        ay.WriteRegister(13, 0xff);

        Assert.Equal(0x0f, ay.ReadRegister(1));
        Assert.Equal(0x1f, ay.ReadRegister(6));
        Assert.Equal(0x1f, ay.ReadRegister(8));
        Assert.Equal(0x0f, ay.ReadRegister(13));
    }

    [Fact]
    public void SingleChannelHasFixedHeadroomAndThreeChannelsReachFullScale()
    {
        var ay = CreateDac();
        Assert.Equal(1f / 3, Render(ay, 1)[0], 6);
        ay.WriteRegister(9, 15);
        Assert.Equal(2f / 3, Render(ay, 1)[0], 6);
        ay.WriteRegister(10, 15);
        Assert.Equal(1f, Render(ay, 1)[0], 6);
    }

    [Fact]
    public void AddingSilentEnvelopeChannelDoesNotChangeAnotherChannelsGain()
    {
        var ay = CreateDac();
        var before = Render(ay, 1)[0];
        ay.WriteRegister(11, 100);
        ay.WriteRegister(13, 4); // attack starts at zero
        ay.WriteRegister(9, 16);
        Assert.Equal(before, Render(ay, 1)[0]);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 16)]
    [InlineData(100, 1600)]
    [InlineData(0x1234, 74560)]
    [InlineData(65535, 1048560)]
    public void EnvelopeFirstTransitionUsesSixteenClocksPerPeriod(int period, int stepCycles)
    {
        var ay = CreateEnvelope(period, 0);
        var peak = Level(15);
        var nextLevel = Level(14);
        var before = Render(ay, stepCycles - 1);
        Assert.All(before, value => Assert.Equal(peak, value));
        Assert.Equal(nextLevel, Render(ay, 1)[0]);
        Assert.All(Render(ay, stepCycles - 1), value => Assert.Equal(nextLevel, value));
        Assert.Equal(Level(13), Render(ay, 1)[0]);
    }

    // Shape descriptions in the AY datasheet: the triangle endpoints last two steps.
    // Each literal gives the first two ramps. Subsequent ramps repeat or hold.
    [Theory]
    [InlineData(0x0, "FEDCBA98765432100000000000000000", false)]
    [InlineData(0x1, "FEDCBA98765432100000000000000000", false)]
    [InlineData(0x2, "FEDCBA98765432100000000000000000", false)]
    [InlineData(0x3, "FEDCBA98765432100000000000000000", false)]
    [InlineData(0x4, "0123456789ABCDEF0000000000000000", false)]
    [InlineData(0x5, "0123456789ABCDEF0000000000000000", false)]
    [InlineData(0x6, "0123456789ABCDEF0000000000000000", false)]
    [InlineData(0x7, "0123456789ABCDEF0000000000000000", false)]
    [InlineData(0x8, "FEDCBA9876543210FEDCBA9876543210", true)]
    [InlineData(0x9, "FEDCBA98765432100000000000000000", false)]
    [InlineData(0xa, "FEDCBA98765432100123456789ABCDEF", true)]
    [InlineData(0xb, "FEDCBA9876543210FFFFFFFFFFFFFFFF", false)]
    [InlineData(0xc, "0123456789ABCDEF0123456789ABCDEF", true)]
    [InlineData(0xd, "0123456789ABCDEFFFFFFFFFFFFFFFFF", false)]
    [InlineData(0xe, "0123456789ABCDEFFEDCBA9876543210", true)]
    [InlineData(0xf, "0123456789ABCDEF0000000000000000", false)]
    public void AllEnvelopeShapesMatchRampsAndHoldLevels(byte shape, string steps, bool repeats)
    {
        Assert.Equal(32, steps.Length);
        var ay = CreateEnvelope(1, shape);
        var samples = Render(ay, 64 * 16);
        for (var step = 0; step < 64; step++)
        {
            var digit = step < 32 || repeats ? steps[step % 32] : steps[^1];
            var expected = Level(Convert.ToInt32(digit.ToString(), 16));
            // Sample from the middle of each step, away from its boundary.
            Assert.Equal(expected, samples[step * 16 + 7]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(14)]
    public void RewritingShapeRestartsLevelAndFullStepInterval(byte shape)
    {
        var ay = CreateEnvelope(100, shape);
        Render(ay, 16 * 100 * 20 + 791);
        ay.WriteRegister(13, shape);
        var fresh = CreateEnvelope(100, shape);
        Assert.Equal(Render(fresh, 50_000), Render(ay, 50_000));
    }

    [Fact]
    public void AtmosExplosionPresetReachesSilenceAndCanRetrigger()
    {
        // Atmos BASIC 1.1b EXPLODE: R6=31, R7=0x47, R8..10=16,
        // R11=0, R12=24, R13=0. All channels use the same noise/envelope.
        var ay = new Ay38912();
        ay.WriteRegister(6, 31);
        ay.WriteRegister(7, 0x47);
        for (var channel = 8; channel <= 10; channel++) ay.WriteRegister(channel, 16);
        ay.WriteRegister(12, 24);
        ay.WriteRegister(13, 0);
        var samples = new float[66_150];
        var count = ay.AdvanceCycles(1_500_000, samples);
        Assert.Equal(samples.Length, count);
        Assert.Contains(samples[..60_000], sample => sample > 0);
        // Step 15 is zero at 1,474,560 cycles; allow the overlapping PCM interval.
        var firstSilentSample = (int)Math.Ceiling(1_474_560d * 44_100 / ClockHz);
        Assert.All(samples[firstSilentSample..], sample => Assert.Equal(0f, sample));
        ay.WriteRegister(13, 0);
        Assert.Contains(Render(ay, 4_000), sample => sample > 0);
    }

    [Fact]
    public void PeriodRegisterWritesDoNotRetriggerTheEnvelope()
    {
        var ay = CreateEnvelope(100, 0);
        Render(ay, 800);
        ay.WriteRegister(11, 200);
        Assert.Equal(Level(14), Render(ay, 800)[^1]);
        Assert.Equal(Level(14), Render(ay, 3199)[^1]);
        Assert.Equal(Level(13), Render(ay, 1)[0]);
    }

    [Fact]
    public void UltrasonicToneIsAveragedInsteadOfAliasingToAnAudibleSquareWave()
    {
        // Period one is 62.5 kHz. One sample per complete period must be its DC mean.
        var ay = CreateTone(sampleRate: 62_500);
        var samples = new float[100];
        Assert.Equal(100, ay.AdvanceCycles(1600, samples));
        Assert.All(samples, sample => Assert.Equal(1f / 6, sample, 6));
    }

    [Fact]
    public void FractionalPcmIntervalsMatchSquareWaveAreaAtOpeningNotePeriods()
    {
        foreach (var period in new[] { 1, 63, 71, 89, 126, 142, 178, 253, 284, 357 })
        {
            var ay = CreateTone();
            ay.WriteRegister(0, (byte)period);
            ay.WriteRegister(1, (byte)(period >> 8));
            var samples = Render(ay, 100_000);
            var sampleCycles = (double)ClockHz / 44_100;
            for (int i = 0; i < samples.Length; i++)
            {
                var start = i * sampleCycles;
                var end = (i + 1) * sampleCycles;
                var expected = (HighCyclesThrough(end, period) - HighCyclesThrough(start, period)) / sampleCycles / 3;
                Assert.Equal((float)expected, samples[i], 5);
            }
        }
    }

    private static double HighCyclesThrough(double cycle, int period)
    {
        var fullPeriod = 16 * period;
        var wholePeriods = Math.Floor(cycle / fullPeriod);
        var remainder = cycle - wholePeriods * fullPeriod;
        return wholePeriods * fullPeriod / 2 + Math.Max(0, remainder - fullPeriod / 2);
    }

    [Fact]
    public void FractionalSampleIncludesVolumeChangesAcrossCalls()
    {
        var ay = CreateDac(44_100);
        var buffer = new float[4];
        Assert.Equal(0, ay.AdvanceCycles(10, buffer));
        ay.WriteRegister(8, 0);
        Assert.Equal(1, ay.AdvanceCycles(13, buffer));
        Assert.Equal((1f / 3) * 10 * 44_100 / ClockHz, buffer[0], 6);
        Assert.Equal(1, ay.AdvanceCycles(23, buffer));
        Assert.Equal(0f, buffer[0]);
    }

    [Theory]
    [InlineData(44_100)]
    [InlineData(48_000)]
    [InlineData(2_000_000)]
    public void PcmIsIndependentOfInstructionChunking(int sampleRate)
    {
        var whole = CreateTone(sampleRate);
        var chunked = CreateTone(sampleRate);
        var expected = new float[2 * sampleRate / 100];
        var expectedCount = whole.AdvanceCycles(10_000, expected);
        var actual = new List<float>();
        var buffer = new float[100];
        for (int cycles = 0; cycles < 10_000;)
        {
            var chunk = Math.Min(7, 10_000 - cycles);
            var count = chunked.AdvanceCycles(chunk, buffer);
            actual.AddRange(buffer[..count]);
            cycles += chunk;
        }
        Assert.Equal(sampleRate / 100, expectedCount);
        Assert.Equal(expected[..expectedCount], actual);
    }

    [Fact]
    public void ResetDiscardsPartialPcmSample()
    {
        var ay = CreateDac(44_100);
        ay.AdvanceCycles(10, new float[1]);
        ay.Reset();
        Assert.All(Render(ay, 100), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void DroppedOutputDoesNotLeakIntoLaterSamples()
    {
        var reference = CreateTone();
        var limited = CreateTone();
        reference.AdvanceCycles(1000, new float[100]);
        limited.AdvanceCycles(1000, new float[1]);
        Assert.Equal(Render(reference, 1000), Render(limited, 1000));
    }

    [Fact]
    public void NoiseAndToneGatesCombineWithAndAndDisabledInputsAreHigh()
    {
        var tone = CreateTone(ClockHz);
        var noise = CreateTone(ClockHz);
        var combined = CreateTone(ClockHz);
        noise.WriteRegister(7, 0x37); // noise only, channel A
        combined.WriteRegister(7, 0x36); // tone AND noise, channel A
        var toneSamples = Render(tone, 2048);
        var noiseSamples = Render(noise, 2048);
        var combinedSamples = Render(combined, 2048);
        Assert.Contains(noiseSamples, value => value == 0);
        Assert.Contains(noiseSamples, value => value > 0);
        for (int i = 0; i < combinedSamples.Length; i++)
            Assert.Equal(toneSamples[i] > 0 && noiseSamples[i] > 0 ? Level(15) : 0, combinedSamples[i]);
    }

    private static Ay38912 CreateDac(int sampleRate = ClockHz)
    {
        var ay = new Ay38912(ClockHz, sampleRate);
        ay.WriteRegister(7, 0x3f);
        ay.WriteRegister(8, 15);
        return ay;
    }

    private static Ay38912 CreateTone(int sampleRate = 44_100)
    {
        var ay = CreateDac(sampleRate);
        ay.WriteRegister(0, 1);
        ay.WriteRegister(7, 0x3e);
        return ay;
    }

    private static Ay38912 CreateEnvelope(int period, byte shape)
    {
        var ay = CreateDac();
        ay.WriteRegister(8, 16);
        ay.WriteRegister(11, (byte)period);
        ay.WriteRegister(12, (byte)(period >> 8));
        ay.WriteRegister(13, shape);
        return ay;
    }

    private static float Level(int volume)
    {
        var ay = CreateDac();
        ay.WriteRegister(8, (byte)volume);
        return Render(ay, 1)[0];
    }

    private static float[] Render(Ay38912 ay, int cycles)
    {
        var samples = new float[(int)Math.Ceiling((double)cycles * ay.SampleRateHz / ClockHz) + 1];
        var count = ay.AdvanceCycles(cycles, samples);
        return samples[..count];
    }
}
