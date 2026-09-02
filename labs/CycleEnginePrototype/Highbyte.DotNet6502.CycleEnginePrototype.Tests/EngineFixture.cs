using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.CycleEnginePrototype.Tests;

/// <summary>One CPU, memory, device stub and engine, arranged identically for every candidate.</summary>
internal sealed class EngineFixture
{
    public const ushort Start = 0x1000;

    public CPU Cpu { get; }
    public Memory Mem { get; }
    public SystemStub System { get; }
    public BusTraceRecorder Recorder { get; } = new();
    public ICycleEngine Engine { get; }

    private EngineFixture(CPU cpu, Memory mem, SystemStub system, ICycleEngine engine)
    {
        Cpu = cpu;
        Mem = mem;
        System = system;
        Engine = engine;
    }

    /// <summary>
    /// Builds a fixture with <paramref name="code"/> at <see cref="Start"/>, arranges CPU and memory,
    /// then starts recording the program page, the stack page, the data page and the vectors.
    /// </summary>
    public static EngineFixture Create(
        EngineKind kind,
        byte[] code,
        Action<CPU>? arrangeCpu = null,
        Action<Memory>? arrangeMem = null,
        CpuFamily family = CpuFamily.Nmos,
        bool badLines = false,
        bool record = true)
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
        var mem = new Memory();
        var system = new SystemStub(cpu.CPUInterrupts) { BadLinesEnabled = badLines };

        cpu.PC = Start;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = true;
        for (var i = 0; i < code.Length; i++)
            mem[(ushort)(Start + i)] = code[i];
        arrangeCpu?.Invoke(cpu);
        arrangeMem?.Invoke(mem);

        var fixture = new EngineFixture(cpu, mem, system, EngineFactory.Create(kind, cpu, mem, system, family, devicesEnabled: true));
        if (record)
        {
            fixture.Recorder.Watch(mem, 0x0F00, 0x0300);       // program area incl. the page below and above
            fixture.Recorder.Watch(mem, 0x0100, 0x0100);       // stack
            fixture.Recorder.Watch(mem, 0x1F00, 0x0100);       // subroutine / handler area
            fixture.Recorder.Watch(mem, 0x2000, 0x0200);       // data
            fixture.Recorder.Watch(mem, 0xFFFA, 6);            // vectors
        }
        return fixture;
    }

    public string Trace => Recorder.Describe();

    public static IEnumerable<object[]> Candidates()
        => EngineFactory.Candidates.Select(kind => new object[] { kind });

    /// <summary>Registers, flags and cycles of the production executor after the same single instruction.</summary>
    public static (byte A, byte X, byte Y, byte SP, ushort PC, byte PS, ulong Cycles) RunLegacy(
        byte[] code, Action<CPU>? arrangeCpu = null, Action<Memory>? arrangeMem = null)
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
        var mem = new Memory();
        cpu.PC = Start;
        cpu.SP = 0xFF;
        cpu.ProcessorStatus.InterruptDisable = true;
        for (var i = 0; i < code.Length; i++)
            mem[(ushort)(Start + i)] = code[i];
        arrangeCpu?.Invoke(cpu);
        arrangeMem?.Invoke(mem);
        var result = cpu.ExecuteOneInstructionMinimal(mem);
        return (cpu.A, cpu.X, cpu.Y, cpu.SP, cpu.PC, cpu.ProcessorStatus.Value, result.CyclesConsumed);
    }

    public void AssertMatchesLegacy(byte[] code, Action<CPU>? arrangeCpu = null, Action<Memory>? arrangeMem = null, bool ignoreBreakAndUnused = false)
    {
        var legacy = RunLegacy(code, arrangeCpu, arrangeMem);
        Assert.Equal(legacy.A, Cpu.A);
        Assert.Equal(legacy.X, Cpu.X);
        Assert.Equal(legacy.Y, Cpu.Y);
        Assert.Equal(legacy.SP, Cpu.SP);
        Assert.Equal(legacy.PC, Cpu.PC);
        var mask = ignoreBreakAndUnused ? 0xCF : 0xFF;
        Assert.Equal(legacy.PS & mask, Cpu.ProcessorStatus.Value & mask);
        Assert.Equal(legacy.Cycles, Engine.Cycle);
    }
}
