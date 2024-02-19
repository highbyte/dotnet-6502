using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace Highbyte.DotNet6502.Benchmarks;

[MemoryDiagnoser] // Memory diagnoser is used to measure memory allocations
//[ShortRunJob] // WARNING: ShortRunJob is a custom job runs faster than normal, but is less accurate.
//[DryJob] // DANGER: DryJob is a custom job that runs very quickly, but VERY INACCURATE. Use only to verify that benchmarks actual runs.
public class Execute6502InstructionBenchmark
{
    private CPU _cpu = default!;
    private Memory _mem = default!;
    private ushort _startAddress;

    [Params(1)]
    //[Params(1, 10, 100)]
    public int NumberOfInstructionsToExecute;

    // GlobalSetup is executed once, or if Params are used: once per each Params value combination
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("// " + "GlobalSetup");

        _cpu = new CPU();
        _mem = new Memory();
        _startAddress = 0xc000;
        LoadProgram(_mem, _startAddress);

    }

    private void LoadProgram(Memory mem, ushort startAddress)
    {
        var bytes = new byte[64 * 1024];
        //// Random code (could include illegal opcodes)
        ////new Random(42).NextBytes(bytes);
        //for (int i = 0; i < bytes.Length; i++)
        //{
        //    bytes[i] = (byte)OpCodeId.NOP;
        //}
        //mem.StoreData(0, bytes);

        //JSR + LDA_I + RTS + LDA_I + JMP-> 5 instructions
        // Code at start address
        ushort branchAddress = (ushort)(_startAddress + 0x10);
        var address = _startAddress;
        mem.WriteByte(ref address, OpCodeId.JSR);
        mem.WriteWord(ref address, branchAddress);
        mem.WriteByte(ref address, OpCodeId.LDA_I);
        mem.WriteByte(ref address, 42);
        mem.WriteByte(ref address, OpCodeId.JMP_ABS);
        mem.WriteWord(ref address, _startAddress); // Jump back to start address

        // Code at branch jsr address
        address = branchAddress;
        mem.WriteByte(ref address, OpCodeId.LDA_I);
        mem.WriteByte(ref address, 21);
        mem.WriteByte(ref address, OpCodeId.RTS);

    }

    //[GlobalCleanup]
    //public void GlobalCleanup()
    //{
    //    Console.WriteLine("// " + "GlobalCleanup");
    //}

    [Benchmark]
    public void ExecIns()
    {
        _cpu.PC = _startAddress;
        for (int i = 0; i < NumberOfInstructionsToExecute; i++)
        {
            _cpu.ExecuteOneInstruction(_mem);
        }
    }

    [Benchmark]
    public void ExecInsMinimal()
    {
        _cpu.PC = _startAddress;
        for (int i = 0; i < NumberOfInstructionsToExecute; i++)
        {
            _cpu.ExecuteOneInstructionMinimal(_mem);
        }
    }
}
