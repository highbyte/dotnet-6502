using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Audio;

/// <summary>
/// SID register writes reach the sample core on the CPU bus cycle they happen, and OSC3/ENV3
/// reads see the core's state at the cycle of the read. The core here runs at one sample per
/// SID cycle, so a write's effect is visible at an exact sample index.
///
/// Layout of the observations: the provider only starts its clock at the first instruction
/// boundary it sees, so every program begins with one NOP (2 cycles) that is not sampled; sample
/// index k then corresponds to CPU bus cycle k + 3.
/// </summary>
public class C64SidExactWriteTimingTests
{
    private const int SidClockHz = SidSampleCore.PalSidClockHz;
    private const ushort Start = 0x1000;

    private sealed class Rig
    {
        public C64 C64 { get; }
        public C64SidSampleProvider Provider { get; }
        public List<float> Samples { get; } = new();

        public Rig(byte[] program)
        {
            C64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL", AudioEnabled = false }, NullLoggerFactory.Instance);
            Provider = new C64SidSampleProvider(C64, sampleRateHz: SidClockHz, sidClockHz: SidClockHz);
            Provider.Init(written =>
            {
                foreach (var s in written)
                    Samples.Add(s);
                return written.Length;
            });
            C64.Mem.StoreData(Start, program);
            C64.CPU.PC = Start;
        }

        public void Step()
        {
            C64.CPU.ExecuteOneInstructionMinimal(C64.Mem);
            Provider.OnAfterInstruction();
        }

        public void Step(int instructions)
        {
            for (var i = 0; i < instructions; i++)
                Step();
        }
    }

    [Fact]
    public void Volume_write_takes_effect_on_the_cycle_of_the_write_not_at_instruction_end()
    {
        // NOP (2, unsampled) | LDA #$0F (2) | STA $D418 (4: opcode, lo, hi, write on its 4th cycle) | NOP ...
        var rig = new Rig([0xEA, 0xA9, 0x0F, 0x8D, 0x18, 0xD4, 0xEA, 0xEA, 0xEA]);

        rig.Step(6);

        // Cycles 3,4 (LDA) and 5,6,7 (STA's first three) run with volume 0: silence.
        // The write lands on cycle 8, so the sample for cycle 8 (index 5) already carries the
        // volume DAC's DC level. The batched behavior would have placed it one sample later.
        Assert.True(rig.Samples.Count >= 8, $"only {rig.Samples.Count} samples");
        for (var i = 0; i < 5; i++)
            Assert.True(rig.Samples[i] == 0f, $"sample {i} (cycle {i + 3}) should be silent, was {rig.Samples[i]}");
        Assert.True(rig.Samples[5] != 0f, "sample 5 (cycle 8, the write cycle) should carry the volume step");
        Assert.Equal(rig.Samples[5], rig.Samples[6]);
    }

    [Fact]
    public void Write_timing_depends_on_the_bus_cycle_not_on_the_instruction_shape()
    {
        // Both programs write $0F to $D418 on bus cycle 13:
        //   A: NOP | LDA #$0F (2) | BIT $02 (3) | NOP (2) | STA $D418 (4, write on cycle 4 of 4)
        //   B: NOP | LDA #$0F (2) | NOP (2) | NOP (2) | STA $D418,X (5, X=0, write on cycle 5 of 5)
        var a = new Rig([0xEA, 0xA9, 0x0F, 0x24, 0x02, 0xEA, 0x8D, 0x18, 0xD4, 0xEA, 0xEA]);
        var b = new Rig([0xEA, 0xA9, 0x0F, 0xEA, 0xEA, 0x9D, 0x18, 0xD4, 0xEA, 0xEA]);
        b.C64.CPU.X = 0;

        a.Step(6);
        b.Step(6);

        Assert.Equal(a.Samples.Take(12), b.Samples.Take(12));
        Assert.Equal(0f, a.Samples[9]);          // cycle 12: still silent
        Assert.NotEqual(0f, a.Samples[10]);      // cycle 13: the write cycle
    }

    [Fact]
    public void Osc3_read_reflects_the_oscillator_at_the_cycle_of_the_read()
    {
        // Voice 3 runs a sawtooth at frequency $FFFF, so its 24-bit accumulator grows by almost
        // exactly one unit of OSC3 (the top 8 bits) per SID cycle. Two OSC3 reads 6 bus cycles
        // apart must therefore differ by about 6: the read sees the oscillator at the cycle of the
        // read, not frozen at the previous instruction boundary.
        var rig = new Rig(
        [
            0xEA,                   // NOP (clock start)
            0xA9, 0xFF,             // LDA #$FF
            0x8D, 0x0E, 0xD4,       // STA $D40E  FRELO3
            0x8D, 0x0F, 0xD4,       // STA $D40F  FREHI3
            0xA9, 0x21,             // LDA #$21   sawtooth, gate on
            0x8D, 0x12, 0xD4,       // STA $D412  VCREG3
            0xEA, 0xEA, 0xEA,       // NOPs
            0xAD, 0x1B, 0xD4,       // LDA $D41B  OSC3 (the read is the instruction's 4th cycle)
            0xEA,                   // NOP
            0xAD, 0x1B, 0xD4,       // LDA $D41B  again, 6 bus cycles later
        ]);

        rig.Step(10);                           // through the first OSC3 read
        var firstRead = rig.C64.CPU.A;
        var firstReadCycle = rig.C64.CPU.BusCycles;
        rig.Step(2);
        var secondRead = rig.C64.CPU.A;

        Assert.Equal(6ul, rig.C64.CPU.BusCycles - firstReadCycle);
        var expectedSecond = (byte)(firstRead + 6);
        Assert.InRange((byte)(secondRead - expectedSecond + 1), (byte)0, (byte)2);
    }

    [Fact]
    public void Writes_before_the_clock_starts_are_applied_at_the_first_instruction_boundary()
    {
        var rig = new Rig([0xA9, 0x0F, 0x8D, 0x18, 0xD4, 0xEA, 0xEA]);
        // No instruction boundary seen yet: the write goes to the batched path...
        rig.C64.CPU.ExecuteOneInstructionMinimal(rig.C64.Mem);   // LDA
        rig.C64.CPU.ExecuteOneInstructionMinimal(rig.C64.Mem);   // STA $D418 (no provider callback yet)
        Assert.True(rig.C64.Sid.InternalSidState.IsAudioChanged);

        rig.Provider.OnAfterInstruction();       // clock starts here and drains the batched write
        Assert.False(rig.C64.Sid.InternalSidState.IsAudioChanged);

        rig.Step(2);                             // two NOPs: 4 sampled cycles, all at volume $0F
        Assert.Equal(4, rig.Samples.Count);
        Assert.All(rig.Samples, s => Assert.NotEqual(0f, s));
    }

    [Fact]
    public void Snapshot_restore_re_evaluation_still_reaches_the_core()
    {
        var rig = new Rig([0xEA, 0xEA, 0xEA]);
        rig.Step();                              // clock started, silence
        rig.C64.Sid.InternalSidState.RegisterWriteSink = null;   // simulate a value restored into IO storage without a live write
        rig.C64.Mem[0xD418] = 0x0F;              // goes through SetSidRegValue: batched (no sink)
        rig.C64.Sid.InternalSidState.RegisterWriteSink = rig.Provider;
        rig.C64.Sid.InternalSidState.MarkAllRegistersChangedForSnapshotRestore();

        rig.Step(2);

        Assert.NotEqual(0f, rig.Samples[^1]);
    }
}
