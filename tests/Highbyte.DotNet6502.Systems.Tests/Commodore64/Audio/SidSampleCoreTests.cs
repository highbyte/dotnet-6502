using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Audio;

/// <summary>
/// Direct tests of the pure-logic SID core. No C64, no threading, no buffers — feed it cycles
/// and register writes, observe samples. These tests are the payoff for the design choice to
/// make <see cref="SidSampleCore"/> synchronously callable.
/// </summary>
public class SidSampleCoreTests
{
    // SID register offsets (relative to $D400).
    private const int FRELO1 = 0x00;
    private const int FREHI1 = 0x01;
    private const int PWLO1  = 0x02;
    private const int PWHI1  = 0x03;
    private const int VCREG1 = 0x04;
    private const int ATDCY1 = 0x05;
    private const int SUREL1 = 0x06;
    private const int SIGVOL = 0x18;

    // VCREG bits.
    private const byte WaveTriangle = 0x10;
    private const byte WaveSawtooth = 0x20;
    private const byte WavePulse    = 0x40;
    private const byte WaveNoise    = 0x80;
    private const byte GateOn       = 0x01;

    [Fact]
    public void Constructor_rejects_non_positive_clocks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SidSampleCore(sampleRateHz: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SidSampleCore(sidClockHz: 0));
    }

    [Fact]
    public void AdvanceCycles_with_zero_cycles_emits_no_samples()
    {
        var core = new SidSampleCore();
        var buffer = new float[16];

        int written = core.AdvanceCycles(0, buffer);
        Assert.Equal(0, written);
    }

    [Fact]
    public void AdvanceCycles_sample_count_matches_resample_ratio()
    {
        // PAL SID at 44.1 kHz: cycles × 44100 / 985248 samples per call.
        // 100_000 cycles → 4476 samples (within ±1 due to Bresenham phase).
        var core = new SidSampleCore(sampleRateHz: 44100, sidClockHz: 985248);
        var buffer = new float[8192];

        int written = core.AdvanceCycles(100_000, buffer);
        int expected = (int)(100_000L * 44100 / 985248);

        Assert.InRange(written, expected, expected + 1);
    }

    [Fact]
    public void AdvanceCycles_over_one_full_SID_second_emits_one_full_output_second()
    {
        var core = new SidSampleCore(sampleRateHz: 44100, sidClockHz: 985248);
        var buffer = new float[50_000];

        int written = core.AdvanceCycles(985_248, buffer);
        Assert.Equal(44_100, written);
    }

    [Fact]
    public void MasterVolume_zero_silences_all_voices()
    {
        var core = MakeSawtoothVoice1(frequency: 0x1D45);
        core.WriteRegister(SIGVOL, 0x00); // override the helper's volume=15
        AdvancePastAttack(core);

        var buffer = new float[1024];
        int written = core.AdvanceCycles(50_000, buffer);
        Assert.True(written > 0);

        for (int i = 0; i < written; i++)
            Assert.Equal(0f, buffer[i]);
    }

    [Fact]
    public void NoWaveform_outputs_silence_even_with_gate_on()
    {
        var core = new SidSampleCore();
        core.WriteRegister(SIGVOL, 0x0F);
        core.WriteRegister(FRELO1, 0x45);
        core.WriteRegister(FREHI1, 0x1D);
        core.WriteRegister(ATDCY1, 0x10);
        core.WriteRegister(SUREL1, 0xF0);
        core.WriteRegister(VCREG1, GateOn); // gate on but no waveform bits set

        var buffer = new float[1024];
        int written = core.AdvanceCycles(50_000, buffer);
        Assert.True(written > 0);

        for (int i = 0; i < written; i++)
            Assert.Equal(0f, buffer[i]);
    }

    [Fact]
    public void Sawtooth_voice_produces_nonzero_audio_after_attack()
    {
        var core = MakeSawtoothVoice1(frequency: 0x1D45); // ~440 Hz at PAL
        AdvancePastAttack(core);

        var buffer = new float[1024];
        int written = core.AdvanceCycles(20_000, buffer);
        Assert.True(written > 0);

        // Expect both positive and negative deflection (it's a centered waveform).
        bool sawPositive = false, sawNegative = false;
        for (int i = 0; i < written; i++)
        {
            if (buffer[i] > 0.001f) sawPositive = true;
            if (buffer[i] < -0.001f) sawNegative = true;
        }
        Assert.True(sawPositive, "Expected at least one positive sample.");
        Assert.True(sawNegative, "Expected at least one negative sample.");
    }

    [Fact]
    public void Samples_stay_in_normalised_range()
    {
        var core = MakeSawtoothVoice1(frequency: 0x4000); // high frequency
        AdvancePastAttack(core);

        var buffer = new float[8192];
        int written = core.AdvanceCycles(200_000, buffer);

        for (int i = 0; i < written; i++)
            Assert.InRange(buffer[i], -1f, 1f);
    }

    [Fact]
    public void Pulse_waveform_produces_two_distinct_output_levels()
    {
        var core = new SidSampleCore();
        core.WriteRegister(SIGVOL, 0x0F);
        core.WriteRegister(FRELO1, 0x00);
        core.WriteRegister(FREHI1, 0x80); // very high frequency to see both states quickly
        core.WriteRegister(PWLO1, 0x00);
        core.WriteRegister(PWHI1, 0x08);  // PW = $800 = 50% duty
        core.WriteRegister(ATDCY1, 0x00); // fastest attack
        core.WriteRegister(SUREL1, 0xF0);
        core.WriteRegister(VCREG1, (byte)(WavePulse | GateOn));
        AdvancePastAttack(core);

        var buffer = new float[2048];
        int written = core.AdvanceCycles(50_000, buffer);

        var distinct = new HashSet<float>();
        for (int i = 0; i < written; i++)
            distinct.Add(buffer[i]);

        // 50% pulse with one voice produces exactly two output levels (high/low).
        Assert.True(distinct.Count >= 2,
            $"Expected at least two distinct pulse levels, got {distinct.Count}.");
    }

    [Fact]
    public void Gate_off_transitions_envelope_toward_zero()
    {
        var core = MakeSawtoothVoice1(frequency: 0x1D45);
        AdvancePastAttack(core);

        // Drain a few samples while the voice is sustaining.
        var sustainBuf = new float[256];
        int sustainCount = core.AdvanceCycles(20_000, sustainBuf);
        float sustainPeak = 0f;
        for (int i = 0; i < sustainCount; i++)
            sustainPeak = Math.Max(sustainPeak, Math.Abs(sustainBuf[i]));
        Assert.True(sustainPeak > 0.01f, $"Expected non-silent sustain, peak={sustainPeak}.");

        // Drop gate (waveform stays selected). Release rate is the fastest available (0).
        core.WriteRegister(VCREG1, WaveSawtooth);

        // Skip ahead by a long stretch to let release run to completion.
        var releaseBuf = new float[8192];
        core.AdvanceCycles(1_000_000, releaseBuf);

        // Now sample a fresh window — should be silent (envelope at 0).
        var finalBuf = new float[1024];
        int finalCount = core.AdvanceCycles(50_000, finalBuf);
        float finalPeak = 0f;
        for (int i = 0; i < finalCount; i++)
            finalPeak = Math.Max(finalPeak, Math.Abs(finalBuf[i]));
        Assert.Equal(0f, finalPeak);
    }

    [Fact]
    public void WriteRegister_out_of_range_is_silently_ignored()
    {
        var core = new SidSampleCore();
        core.WriteRegister(-1, 0xFF);
        core.WriteRegister(SidSampleCore.RegisterCount, 0xFF);
        core.WriteRegister(int.MaxValue, 0xFF);

        // No exception — and a subsequent AdvanceCycles still works.
        var buffer = new float[16];
        Assert.Equal(0, core.AdvanceCycles(0, buffer));
    }

    [Fact]
    public void AdvanceCycles_drops_excess_when_destination_too_small()
    {
        var core = new SidSampleCore();
        var tinyBuffer = new float[2];

        // 100_000 cycles would normally produce ~4476 samples. Expect 2 written, the rest dropped.
        int written = core.AdvanceCycles(100_000, tinyBuffer);
        Assert.Equal(2, written);
    }

    // --- Helpers ---------------------------------------------------------

    private static SidSampleCore MakeSawtoothVoice1(ushort frequency)
    {
        var core = new SidSampleCore();
        core.WriteRegister(FRELO1, (byte)(frequency & 0xFF));
        core.WriteRegister(FREHI1, (byte)(frequency >> 8));
        core.WriteRegister(ATDCY1, 0x10);                        // attack=1, decay=0 (fast)
        core.WriteRegister(SUREL1, 0xF0);                        // sustain=15, release=0
        core.WriteRegister(SIGVOL, 0x0F);                        // master volume max
        core.WriteRegister(VCREG1, (byte)(WaveSawtooth | GateOn));
        return core;
    }

    private static void AdvancePastAttack(SidSampleCore core)
    {
        // Attack rate 1 = 32 cycles/step × 255 steps ≈ 8160 cycles; plus a margin to settle.
        var scratch = new float[2048];
        core.AdvanceCycles(20_000, scratch);
    }
}
