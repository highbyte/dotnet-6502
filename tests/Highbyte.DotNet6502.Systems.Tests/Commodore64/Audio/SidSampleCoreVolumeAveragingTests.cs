using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Audio;

/// <summary>
/// The $D418 master volume is applied as its average over the SID cycles between two output
/// samples. With register writes landing on their exact cycle, a read-modify-write on $D418
/// (NMOS write-back of the read value, then the result, one cycle apart) is a one-cycle volume
/// dip; point-sampling it at full amplitude whenever the sample point happened to fall on it made
/// a loader that does this every few hundred cycles crackle. Averaging gives the dip its true
/// weight of one cycle in about twenty.
/// </summary>
public class SidSampleCoreVolumeAveragingTests
{
    private const int SampleRate = 48000;
    private const int SidClock = SidSampleCore.PalSidClockHz;
    private const int VolumeRegister = SidSampleCore.VolumeRegisterOffset;

    private static float SteadyDcAtFullVolume(out SidSampleCore core)
    {
        core = new SidSampleCore(SampleRate, SidClock);
        core.WriteRegister(VolumeRegister, 0x0F);
        var buffer = new float[64];
        core.AdvanceCycles(SidClock / 100, buffer);      // 10 ms: well past any start-up transient
        var n = core.AdvanceCycles(SidClock / 1000, buffer);
        Assert.True(n > 10);
        return buffer[n - 1];
    }

    [Fact]
    public void A_one_cycle_volume_dip_contributes_one_cycle_of_the_sample_window()
    {
        var dc = SteadyDcAtFullVolume(out var core);
        var buffer = new float[64];

        // Land the dip mid-window: advance a few cycles into the next sample period first.
        core.AdvanceCycles(5, buffer);
        core.WriteRegister(VolumeRegister, 0x00);
        core.AdvanceCycles(1, buffer);
        core.WriteRegister(VolumeRegister, 0x0F);
        var n = core.AdvanceCycles(40, buffer);           // completes the affected sample and one more
        Assert.True(n >= 1);

        var dipped = buffer[0];
        var cyclesPerSample = SidClock / (float)SampleRate;   // ~20.5
        var expected = dc * (1f - 1f / cyclesPerSample);
        Assert.InRange(dipped, expected - dc * 0.02f, expected + dc * 0.02f);
        Assert.True(dipped > dc * 0.9f, $"a 1-cycle dip must not drop the sample to {dipped} (steady {dc})");
    }

    [Fact]
    public void A_volume_held_for_half_a_sample_window_halves_that_samples_dc()
    {
        var dc = SteadyDcAtFullVolume(out var core);
        var buffer = new float[64];

        // Re-align to a sample boundary: advance until a sample is emitted.
        while (core.AdvanceCycles(1, buffer) == 0) { }
        core.WriteRegister(VolumeRegister, 0x00);
        core.AdvanceCycles(10, buffer);
        core.WriteRegister(VolumeRegister, 0x0F);
        var n = core.AdvanceCycles(11, buffer);
        Assert.Equal(1, n);

        var cyclesPerSample = SidClock / (float)SampleRate;
        var expected = dc * ((cyclesPerSample - 10f) / cyclesPerSample);
        Assert.InRange(buffer[0], expected - dc * 0.06f, expected + dc * 0.06f);
    }

    [Fact]
    public void Steady_volume_is_unaffected_by_averaging()
    {
        var dc = SteadyDcAtFullVolume(out var core);
        var buffer = new float[64];
        var n = core.AdvanceCycles(SidClock / 1000, buffer);
        for (var i = 0; i < n; i++)
            Assert.Equal(dc, buffer[i]);
    }
}
