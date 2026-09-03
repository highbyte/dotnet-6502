using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Audio;

/// <summary>
/// The filter registers $D415-$D417 must be routed through <see cref="InternalSidState"/> like every
/// other SID register, otherwise the audio providers never see cutoff, resonance or routing changes.
/// </summary>
public class SidFilterRegisterTests
{
    private static C64 Build() =>
        C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);

    [Theory]
    [InlineData(SidAddr.CUTLO)]
    [InlineData(SidAddr.CUTHI)]
    [InlineData(SidAddr.RESON)]
    public void Filter_register_writes_are_recorded_as_changed_sid_registers(ushort address)
    {
        var c64 = Build();
        c64.Sid.InternalSidState.ClearAudioChanged();

        c64.Mem.Write(address, 0x77);

        Assert.True(c64.Sid.InternalSidState.IsRawSidRegChanged(address));
        Assert.Equal(0x77, c64.Sid.InternalSidState.GetRawSidRegValue(address));
        Assert.True(c64.Sid.InternalSidState.IsAudioChanged);
    }

    [Fact]
    public void Filter_cutoff_written_through_memory_changes_the_filtered_output()
    {
        // A sawtooth voice routed through a low-pass filter: with the cutoff at its minimum almost
        // nothing passes, with the cutoff at its maximum the waveform comes through. If the cutoff
        // registers are not mapped, both runs produce identical output.
        var closed = RenderRms(cutHi: 0x00);
        var open = RenderRms(cutHi: 0xFF);

        Assert.True(open > 0.001, $"open={open}");
        Assert.True(open > closed * 2, $"open={open} closed={closed}");
    }

    [Theory]
    [InlineData(0x00)]  // no resonance
    [InlineData(0xF0)]  // maximum resonance
    public void Fully_open_cutoff_keeps_the_filter_stable(byte resonance)
    {
        var core = new SidSampleCore(sampleRateHz: 22050);
        core.WriteRegister(SidSampleCore.VolumeRegisterOffset, 0x1F);
        core.WriteRegister(SidSampleCore.FilterCutLoOffset, 0x07);
        core.WriteRegister(SidSampleCore.FilterCutHiOffset, 0xFF);
        core.WriteRegister(SidSampleCore.FilterResRoutOffset, (byte)(resonance | 0x01));
        core.WriteRegister(1, 0x40);   // voice 1 frequency
        core.WriteRegister(6, 0xF0);   // sustain
        core.WriteRegister(4, 0x21);   // sawtooth, gate on

        var buffer = new float[4096];
        for (var i = 0; i < 20; i++)
        {
            var n = core.AdvanceCycles(SidSampleCore.PalSidClockHz / 10, buffer);
            for (var j = 0; j < n; j++)
                Assert.True(float.IsFinite(buffer[j]) && MathF.Abs(buffer[j]) <= 1f, $"sample {j} in block {i} = {buffer[j]}");
        }
    }

    private static double RenderRms(byte cutHi)
    {
        var c64 = Build();
        var samples = new List<float>();
        var provider = new C64SidSampleProvider(c64);
        provider.Init(s => { samples.AddRange(s.ToArray()); return s.Length; });

        c64.Mem.Write(SidAddr.SIGVOL, 0x1F);   // low-pass, full volume
        c64.Mem.Write(SidAddr.CUTLO, 0x00);
        c64.Mem.Write(SidAddr.CUTHI, cutHi);
        c64.Mem.Write(SidAddr.RESON, 0x01);    // voice 1 through the filter, no resonance
        c64.Mem.Write(SidAddr.FRELO1, 0x00);
        c64.Mem.Write(SidAddr.FREHI1, 0x40);
        c64.Mem.Write(SidAddr.ATDCY1, 0x00);
        c64.Mem.Write(SidAddr.SUREL1, 0xF0);
        c64.Mem.Write(SidAddr.VCREG1, 0x21);   // sawtooth, gate on

        // NOP loop; each instruction advances the provider by its cycle count.
        c64.Mem.StoreData(0x1000, [0xEA, 0x4C, 0x00, 0x10]);
        c64.CPU.PC = 0x1000;
        for (var i = 0; i < 20_000; i++)
        {
            c64.CPU.ExecuteOneInstructionMinimal(c64.Mem);
            provider.OnAfterInstruction();
        }

        // Drop the attack transient, then measure the AC level (the $D418 DC term is identical in both runs).
        var tail = samples.Skip(samples.Count / 2).ToArray();
        var mean = tail.Average();
        return Math.Sqrt(tail.Average(x => (x - mean) * (x - mean)));
    }
}
