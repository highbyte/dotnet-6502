namespace Highbyte.DotNet6502.CycleEnginePrototype.Tests;

/// <summary>
/// Runs the slice loop for thousands of instructions with bad lines and a timer IRQ active, and
/// requires both device policies to end in the same CPU state, at the same cycle, with the same
/// device state. Per-cycle sync is the oracle; lazy sync must match it exactly: same stalls, same
/// interrupt timing, same VIC fetches, just fewer scheduler calls. Any device's closed-form
/// Advance and any stall watermark is validated this way.
/// </summary>
public class WholeProgramEquivalenceTests
{
    private const int Instructions = 5000;

    private sealed record Outcome(ulong Cycle, byte A, byte X, byte SP, ushort PC, byte PS, ulong MasterCycle, long VicFetches, int CiaUnderflows, int RasterLine, int RasterCycle);

    private static Outcome Run(EngineKind kind)
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
        var mem = new Memory();
        var system = new SystemStub(cpu.CPUInterrupts) { BadLinesEnabled = true };
        system.StartCiaTimer(700);
        SliceProgram.Assemble(mem, cpu);
        var engine = EngineFactory.Create(kind, cpu, mem, system);

        for (var i = 0; i < Instructions; i++)
            engine.RunInstruction();
        engine.FlushDevices();

        return new Outcome(engine.Cycle, cpu.A, cpu.X, cpu.SP, cpu.PC, cpu.ProcessorStatus.Value,
            system.MasterCycle, system.VicFetchAccumulator, system.CiaUnderflows, system.RasterLine, system.RasterCycle);
    }

    [Fact]
    public void Lazy_Sync_Matches_PerCycle_Sync_On_Cpu_State_Cycle_Count_And_Device_State()
    {
        var reference = Run(EngineKind.AtomicPerCycle);

        Assert.Equal(reference, Run(EngineKind.AtomicLazy));

        Assert.Equal(reference.Cycle, reference.MasterCycle);
        Assert.True(reference.CiaUnderflows > 10, "the timer IRQ should have fired many times");
        Assert.True(reference.Cycle > (ulong)Instructions * 3, "stalls and dummy cycles should be included");
    }

    [Fact]
    public void Legacy_Runs_The_Same_Program_Without_Stalls()
    {
        var legacy = Run(EngineKind.Legacy);
        var candidate = Run(EngineKind.AtomicLazy);

        Assert.True(legacy.Cycle < candidate.Cycle, "the legacy executor has no RDY stalls, so it must consume fewer cycles");
        Assert.Equal(legacy.MasterCycle, legacy.Cycle);
    }
}
