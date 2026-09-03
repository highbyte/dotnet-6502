using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Audio;

/// <summary>
/// The SID has no read path for its write-only registers: a read returns whatever the chip's
/// internal data bus still holds, which is the last byte written to or read from the chip, and
/// that value decays to 0 after a few thousand cycles. Software that does a read-modify-write on
/// a SID register (a loader's <c>DEC $D418</c> loading noise) therefore operates on the previous
/// value, not on 0.
/// </summary>
public class SidRegisterReadbackTests
{
    private const ushort Start = 0x1000;

    private static C64 Build(byte[] program)
    {
        var c64 = C64.BuildC64(new C64Config { LoadROMs = false, C64Model = "C64PAL", Vic2Model = "PAL" }, NullLoggerFactory.Instance);
        c64.Mem.StoreData(Start, program);
        c64.CPU.PC = Start;
        return c64;
    }

    private static void Step(C64 c64, int instructions = 1)
    {
        for (var i = 0; i < instructions; i++)
            c64.CPU.ExecuteOneInstructionMinimal(c64.Mem);
    }

    [Fact]
    public void A_write_only_register_reads_back_the_last_byte_written_to_the_chip()
    {
        // LDA #$5A ; STA $D400 ; LDA $D400 ; LDX $D418
        var c64 = Build([0xA9, 0x5A, 0x8D, 0x00, 0xD4, 0xAD, 0x00, 0xD4, 0xAE, 0x18, 0xD4]);

        Step(c64, 4);

        Assert.Equal(0x5A, c64.CPU.A);
        Assert.Equal(0x5A, c64.CPU.X);     // the latch is chip-wide, not per register
    }

    [Fact]
    public void The_latch_decays_to_zero_after_its_lifetime()
    {
        // LDA #$5A ; STA $D400 ; then a NOP loop, then LDA $D400
        var c64 = Build([0xA9, 0x5A, 0x8D, 0x00, 0xD4, 0xEA, 0xEA, 0xEA, 0xEA, 0x4C, 0x05, 0x10]);
        Step(c64, 2);
        var writtenAt = c64.CPU.BusCycles;

        while (c64.CPU.BusCycles - writtenAt < (ulong)InternalSidState.BusLatchDecayCycles - 10)
            Step(c64);
        Assert.Equal(0x5A, c64.Mem.Read(0xD400));

        while (c64.CPU.BusCycles - writtenAt < (ulong)InternalSidState.BusLatchDecayCycles + 10)
            Step(c64);
        Assert.Equal(0x00, c64.Mem.Read(0xD400));
    }

    [Fact]
    public void Dec_D418_counts_the_volume_down_from_the_previous_value()
    {
        // LDA #$0F ; STA $D418 ; DEC $D418 ; DEC $D418 ; DEC $D418
        var c64 = Build([0xA9, 0x0F, 0x8D, 0x18, 0xD4, 0xCE, 0x18, 0xD4, 0xCE, 0x18, 0xD4, 0xCE, 0x18, 0xD4]);
        var writes = new List<byte>();
        c64.Sid.InternalSidState.RegisterWriteSink = new RecordingSink(writes);

        Step(c64, 5);

        // Each DEC writes back what it read (the latch: the previous value), then the result.
        Assert.Equal([0x0F, 0x0F, 0x0E, 0x0E, 0x0D, 0x0D, 0x0C], writes);
        Assert.Equal(0x0C, c64.ReadIOStorage(0xD418));
    }

    [Fact]
    public void Reads_of_readable_registers_load_the_latch_too()
    {
        // LDA #$5A ; STA $D400 ; LDA $D419 (POTX = $FF) ; LDX $D400
        var c64 = Build([0xA9, 0x5A, 0x8D, 0x00, 0xD4, 0xAD, 0x19, 0xD4, 0xAE, 0x00, 0xD4]);

        Step(c64, 4);

        Assert.Equal(0xFF, c64.CPU.A);
        Assert.Equal(0xFF, c64.CPU.X);
    }

    [Fact]
    public void Nothing_has_touched_the_chip_reads_as_zero()
    {
        var c64 = Build([0xAD, 0x18, 0xD4]);   // LDA $D418

        Step(c64);

        Assert.Equal(0x00, c64.CPU.A);
    }

    [Theory]
    [InlineData(SidAddr.CUTLO)]
    [InlineData(SidAddr.CUTHI)]
    [InlineData(SidAddr.RESON)]
    public void Filter_register_writes_reach_the_audio_provider_and_read_back(ushort address)
    {
        var c64 = Build([0xA9, 0x77, 0x8D, (byte)(address & 0xFF), (byte)(address >> 8), 0xAD, (byte)(address & 0xFF), (byte)(address >> 8)]);
        var writes = new List<byte>();
        c64.Sid.InternalSidState.RegisterWriteSink = new RecordingSink(writes);

        Step(c64, 3);

        Assert.Equal([0x77], writes);
        Assert.True(c64.Sid.InternalSidState.IsRawSidRegChanged(address));
        Assert.Equal(0x77, c64.CPU.A);
    }

    private sealed class RecordingSink(List<byte> writes) : ISidRegisterWriteSink
    {
        public bool OnRegisterWrite(ushort address, byte value, ulong busCycle)
        {
            writes.Add(value);
            return false;
        }
    }
}
