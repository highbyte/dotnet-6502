using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace Highbyte.DotNet6502.Benchmarks;

[MemoryDiagnoser] // Memory diagnoser is used to measure memory allocations
//[ShortRunJob] // WARNING: ShortRunJob is a custom job runs faster than normal, but is less accurate.
//[DryJob] // DANGER: DryJob is a custom job that runs very quickly, but VERY INACCURATE. Use only to verify that benchmarks actual runs.
public class ExecuteAll6502InstructionsBenchmark
{
    private CPU _cpu = default!;
    private Memory _mem = default!;
    private ushort _startAddress;

    [ParamsSource(nameof(OpCodes))]
    public OpCodeId OpCode;

    public IEnumerable<OpCodeId> OpCodes => Enum.GetValues<OpCodeId>();

    // GlobalSetup is executed once, or if Params are used: once per each Params value combination
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("// " + "GlobalSetup");

        _cpu = new CPU();
        _mem = new Memory();
        _startAddress = 0xc000;
    }

    //[GlobalCleanup]
    //public void GlobalCleanup()
    //{
    //    Console.WriteLine("// " + "GlobalCleanup");
    //}

    //[Benchmark]
    //public void ExecIns()
    //{
    //    _cpu.PC = _startAddress;
    //    _mem.WriteByte(_startAddress, (byte)OpCode);
    //    _cpu.ExecuteOneInstruction(_mem);
    //}

    [Benchmark]
    public void ExecInsMinimal()
    {
        _cpu.PC = _startAddress;
        _mem.WriteByte(_startAddress, (byte)OpCode);
        _cpu.ExecuteOneInstructionMinimal(_mem);
    }
}
