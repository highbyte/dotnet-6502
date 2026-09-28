using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Tests;

/// <summary>
/// A bus master can stall CPU reads through <see cref="IBusStallSource"/>: the read happens once
/// the bus is released, the waiting cycles count as instruction cycles without accesses, and
/// writes are never stalled.
/// </summary>
public class CPUBusStallTests
{
    private const ushort Start = 0x1000;

    private sealed class ScriptedStalls : IBusStallSource
    {
        private readonly Dictionary<ulong, ulong> _stallAtBusCycle;
        public List<ulong> Consulted { get; } = new();

        public ScriptedStalls(params (ulong busCycle, ulong stall)[] stalls)
            => _stallAtBusCycle = stalls.ToDictionary(s => s.busCycle, s => s.stall);

        public ulong StallCyclesForRead(ulong busCycle, out ulong nextCheckBusCycle)
        {
            Consulted.Add(busCycle);
            nextCheckBusCycle = 0;   // consult on every read
            return _stallAtBusCycle.TryGetValue(busCycle, out var stall) ? stall : 0;
        }
    }

    private static (CPU cpu, Memory mem) NewCpu(params byte[] program)
    {
        var cpu = new CPU();
        var mem = new Memory();
        mem.StoreData(Start, program);
        cpu.PC = Start;
        cpu.SP = 0xFF;
        return (cpu, mem);
    }

    [Fact]
    public void A_stalled_read_adds_the_wait_to_the_bus_cycles_and_the_instruction()
    {
        var (cpu, mem) = NewCpu(0xEA, 0xEA);   // NOP ; NOP
        cpu.BusStallSource = new ScriptedStalls((busCycle: 3, stall: 40));   // the second NOP's opcode fetch

        var first = cpu.ExecuteOneInstructionMinimal(mem);
        var second = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(2UL, first.CyclesConsumed);
        Assert.Equal(2 + 40UL, second.CyclesConsumed);
        Assert.Equal(2 + 2 + 40UL, cpu.BusCycles);
    }

    [Fact]
    public void A_stall_shifts_the_cycles_of_the_accesses_that_follow_it()
    {
        // LDA $D012 -> the operand fetches are at bus cycles 2 and 3, the data read at 4.
        // Stalling cycle 2 by 10 moves the data read to cycle 14, which a mapped reader observes.
        var (cpu, mem) = NewCpu(0xAD, 0x12, 0xD0);
        ulong readAt = 0;
        mem.MapReader(0xD012, _ => { readAt = cpu.BusCycles; return 0x42; });
        cpu.BusStallSource = new ScriptedStalls((busCycle: 2, stall: 10));

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(0x42, cpu.A);
        Assert.Equal(14UL, readAt);
        Assert.Equal(4 + 10UL, result.CyclesConsumed);
    }

    [Fact]
    public void Writes_do_not_consult_the_stall_source()
    {
        // STA $2000: reads at bus cycles 1-3, the write at 4.
        var (cpu, mem) = NewCpu(0x8D, 0x00, 0x20);
        var stalls = new ScriptedStalls((busCycle: 4, stall: 99));
        cpu.BusStallSource = stalls;

        var result = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(4UL, result.CyclesConsumed);
        Assert.Equal(new ulong[] { 1, 2, 3 }, stalls.Consulted);
    }

    [Fact]
    public void The_source_is_not_consulted_before_the_cycle_it_asked_for()
    {
        var (cpu, mem) = NewCpu(0xEA, 0xEA, 0xEA);
        var calls = new List<ulong>();
        cpu.BusStallSource = new DelegateStalls((busCycle, out next) => { calls.Add(busCycle); next = busCycle + 4; return 0; });

        for (var i = 0; i < 3; i++)
            cpu.ExecuteOneInstructionMinimal(mem);   // bus cycles 1-6

        Assert.Equal(new ulong[] { 1, 5 }, calls);
    }

    [Fact]
    public void RequestBusStallCheck_consults_the_source_on_the_next_read()
    {
        var (cpu, mem) = NewCpu(0xEA, 0xEA);
        var calls = new List<ulong>();
        cpu.BusStallSource = new DelegateStalls((busCycle, out next) => { calls.Add(busCycle); next = ulong.MaxValue; return 0; });

        cpu.ExecuteOneInstructionMinimal(mem);
        cpu.RequestBusStallCheck();
        cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(new ulong[] { 1, 3 }, calls);
    }

    [Fact]
    public void Removing_the_source_stops_stalling()
    {
        var (cpu, mem) = NewCpu(0xEA, 0xEA);
        cpu.BusStallSource = new ScriptedStalls((busCycle: 3, stall: 40));
        cpu.BusStallSource = null;

        cpu.ExecuteOneInstructionMinimal(mem);
        var second = cpu.ExecuteOneInstructionMinimal(mem);

        Assert.Equal(2UL, second.CyclesConsumed);
    }

    private delegate ulong StallFunc(ulong busCycle, out ulong nextCheckBusCycle);

    private sealed class DelegateStalls(StallFunc func) : IBusStallSource
    {
        public ulong StallCyclesForRead(ulong busCycle, out ulong nextCheckBusCycle) => func(busCycle, out nextCheckBusCycle);
    }
}
