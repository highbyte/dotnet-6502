using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace Highbyte.DotNet6502.CycleEnginePrototype.Benchmarks;

internal sealed class PrototypeConfig : ManualConfig
{
    public PrototypeConfig()
    {
        AddJob(Job.Default);
        AddDiagnoser(MemoryDiagnoser.Default);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(maxDepth: 2, exportHtml: true)));
    }
}

/// <summary>
/// Cost of the cycle-stamped engine's two device policies relative to the production executor, on the same slice loop:
/// 100 iterations of 14 instructions, with the devices absent, ticking, or ticking with bad lines
/// (so reads stall). Every engine runs its own CPU, memory and device stub.
/// </summary>
[Config(typeof(PrototypeConfig))]
public class EngineBenchmarks
{
    public enum DeviceMode
    {
        None,
        Ticking,
        TickingWithBadLines,
    }

    private const int Iterations = 100;
    private const int Instructions = Iterations * SliceProgram.InstructionsPerIteration;

    [Params(DeviceMode.None, DeviceMode.Ticking, DeviceMode.TickingWithBadLines)]
    public DeviceMode Devices { get; set; }

    private ICycleEngine _legacy = default!;
    private ICycleEngine _atomicPerCycle = default!;
    private ICycleEngine _atomicLazy = default!;

    [GlobalSetup]
    public void Setup()
    {
        _legacy = Build(EngineKind.Legacy);
        _atomicPerCycle = Build(EngineKind.AtomicPerCycle);
        _atomicLazy = Build(EngineKind.AtomicLazy);
    }

    private ICycleEngine Build(EngineKind kind)
    {
        var cpu = new CPU(CpuCompatibilityProfile.FullUnofficial);
        var mem = new Memory();
        var system = new SystemStub(cpu.CPUInterrupts) { BadLinesEnabled = Devices == DeviceMode.TickingWithBadLines };
        if (Devices != DeviceMode.None)
            system.StartCiaTimer(3000);
        SliceProgram.Assemble(mem, cpu);
        return EngineFactory.Create(kind, cpu, mem, system, CpuFamily.Nmos, devicesEnabled: Devices != DeviceMode.None);
    }

    private static ulong Run(ICycleEngine engine)
    {
        for (var i = 0; i < Instructions; i++)
            engine.RunInstruction();
        return engine.Cycle;
    }

    [Benchmark(Baseline = true)]
    public ulong Legacy() => Run(_legacy);

    [Benchmark]
    public ulong AtomicPerCycle() => Run(_atomicPerCycle);

    [Benchmark]
    public ulong AtomicLazy() => Run(_atomicLazy);
}
