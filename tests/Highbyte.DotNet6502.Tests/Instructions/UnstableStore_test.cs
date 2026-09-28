using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests.Instructions;

/// <summary>
/// SHA, SHX, SHY and TAS: the NMOS stores whose value is ANDed with the high byte of the
/// un-indexed address plus one. The SingleStepTests corpus asserts every case with the bus free,
/// page crossings included; these tests cover what the corpus cannot: the AND dropping out when
/// the bus is taken in the cycle before the write, the address corruption staying in place then,
/// and the profile the opcodes are exposed in.
/// </summary>
public class UnstableStore_test
{
    private const ushort Start = 0x1000;

    // Stalls the read at one bus cycle, counted from the instruction's opcode fetch at cycle 1.
    private sealed class StallAt(ulong busCycle, ulong stall) : IBusStallSource
    {
        public ulong StallCyclesForRead(ulong currentBusCycle, out ulong nextCheckBusCycle)
        {
            nextCheckBusCycle = 0;
            return currentBusCycle == busCycle ? stall : 0;
        }
    }

    private static (CPU cpu, Memory mem) NewCpu(CpuCompatibilityProfile profile, params byte[] program)
    {
        var cpu = new CPU(profile);
        var mem = new Memory();
        mem.StoreData(Start, program);
        cpu.PC = Start;
        cpu.SP = 0xFF;
        return (cpu, mem);
    }

    [Theory]
    [InlineData(0x9F, "SHA")]
    [InlineData(0x9E, "SHX")]
    [InlineData(0x9C, "SHY")]
    [InlineData(0x9B, "TAS")]
    [InlineData(0x93, "SHA")]
    public void The_five_are_exposed_from_StableUnofficial_and_not_below(byte code, string mnemonic)
    {
        Assert.False(new CPU(CpuCompatibilityProfile.OfficialOnly).IsOpCodeDefined(code));
        var info = new CPU(CpuCompatibilityProfile.StableUnofficial).GetOpCodeInfo(code);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info.Value.Mnemonic);
    }

    [Fact]
    public void SHA_abs_Y_stores_A_and_X_and_the_high_byte_plus_one_in_5_cycles()
    {
        // SHA $12F0,Y with Y=$05: A & X & $13 -> $12F5
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x9F, 0xF0, 0x12);
        cpu.A = 0xFF; cpu.X = 0x3F; cpu.Y = 0x05;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(5UL, result.CyclesConsumed);
        Assert.Equal(0x3F & 0x13, mem[0x12F5]);
    }

    [Fact]
    public void TAS_sets_SP_to_A_and_X_before_the_store()
    {
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x9B, 0x00, 0x20);   // TAS $2000,Y
        cpu.A = 0xF7; cpu.X = 0x7F; cpu.Y = 0x00;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0xF7 & 0x7F, cpu.SP);
        Assert.Equal(0xF7 & 0x7F & 0x21, mem[0x2000]);
    }

    [Fact]
    public void A_bus_stall_in_the_cycle_before_the_write_drops_the_AND_from_the_value()
    {
        // SHX $2000,Y: opcode 1, operands 2-3, dummy read 4, write 5. Stalling the dummy read is
        // RDY low in the cycle before the write: the value written is X alone.
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x9E, 0x00, 0x20);
        cpu.X = 0xFF; cpu.Y = 0x10;
        cpu.BusStallSource = new StallAt(busCycle: 4, stall: 40);

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0xFF, mem[0x2010]);
        Assert.Equal(5 + 40UL, result.CyclesConsumed);
    }

    [Fact]
    public void A_bus_stall_earlier_in_the_instruction_leaves_the_AND_in_place()
    {
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x9E, 0x00, 0x20);
        cpu.X = 0xFF; cpu.Y = 0x10;
        cpu.BusStallSource = new StallAt(busCycle: 3, stall: 40);   // the high operand byte

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0xFF & 0x21, mem[0x2010]);
    }

    [Fact]
    public void With_a_page_crossing_the_ANDed_value_becomes_the_high_byte_of_the_address()
    {
        // SHY $20F0,X with X=$20 crosses into $21xx; Y=$C3 & $21 = $01, so the write lands at
        // $0110, not $2110.
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x9C, 0xF0, 0x20);
        cpu.Y = 0xC3; cpu.X = 0x20;

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x01, mem[0x0110]);
        Assert.Equal(0, mem[0x2110]);
    }

    [Fact]
    public void With_a_page_crossing_and_the_AND_dropped_out_the_address_is_still_corrupted()
    {
        // As above with RDY low before the write: the value is Y itself, the address still $01xx.
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x9C, 0xF0, 0x20);
        cpu.Y = 0xC3; cpu.X = 0x20;
        cpu.BusStallSource = new StallAt(busCycle: 4, stall: 1);

        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0xC3, mem[0x0110]);
        Assert.Equal(0, mem[0x2110]);
    }

    [Fact]
    public void SHA_ind_Y_takes_6_cycles_and_uses_the_pointers_high_byte()
    {
        // SHA ($80),Y with ($80) = $3400, Y = $02: A & X & $35 -> $3402
        var (cpu, mem) = NewCpu(CpuCompatibilityProfile.StableUnofficial, 0x93, 0x80);
        mem[0x80] = 0x00; mem[0x81] = 0x34;
        cpu.A = 0xFF; cpu.X = 0xFF; cpu.Y = 0x02;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(6UL, result.CyclesConsumed);
        Assert.Equal(0x35, mem[0x3402]);
    }
}
