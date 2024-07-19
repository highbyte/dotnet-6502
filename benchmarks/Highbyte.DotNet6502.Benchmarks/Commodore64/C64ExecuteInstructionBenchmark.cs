using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64;

[MemoryDiagnoser] // Memory diagnoser is used to measure memory allocations
//[ShortRunJob] // WARNING: ShortRunJob is a custom job runs faster than normal, but is less accurate.
//[DryJob] // DANGER: DryJob is a custom job that runs very quickly, but VERY INACCURATE. Use only to verify that benchmarks actual runs.
public class C64ExecuteInstructionBenchmark
{
    private C64 _c64WithInstrumentation = default!;
    private C64 _c64WithoutInstrumentation = default!;
    private SystemRunner _systemRunnerWithInstrumentation = default!;
    private SystemRunner _systemRunnerWithoutInstrumentation = default!;
    private ushort _startAddress;

    //[Params(1)]
    [Params(1, 100, 1000)]
    public int NumberOfInstructionsToExecute;

    // GlobalSetup is executed once, or if Params are used: once per each Params value combination
    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("// " + "GlobalSetup");

        var c64ConfigWithInstrumentation = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
            LoadROMs = false,
            TimerMode = TimerMode.UpdateEachRasterLine,
            AudioEnabled = true,
            InstrumentationEnabled = true
        };
        _c64WithInstrumentation = C64.BuildC64(c64ConfigWithInstrumentation, new NullLoggerFactory());

        c64ConfigWithInstrumentation.InstrumentationEnabled = false;
        _c64WithoutInstrumentation = C64.BuildC64(c64ConfigWithInstrumentation, new NullLoggerFactory());

        _startAddress = 0xc000;

        LoadProgram(_c64WithInstrumentation.Mem, _startAddress);
        LoadProgram(_c64WithoutInstrumentation.Mem, _startAddress);

        _systemRunnerWithInstrumentation = new SystemRunner(_c64WithInstrumentation);
        _systemRunnerWithoutInstrumentation = new SystemRunner(_c64WithoutInstrumentation);
    }

    private void LoadProgram(Memory mem, ushort startAddress)
    {
        var bytes = new byte[64 * 1024];
        // Random code (could include illegal opcodes)
        //new Random(42).NextBytes(bytes);
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)OpCodeId.RTS;
        }
        mem.StoreData(0, bytes);

        // JSR + LDA_I + RTS + LDA_I + JMP -> 5 instructions
        // Code at start address
        //ushort branchAddress = (ushort)(_startAddress + 0x10);
        //var address = _startAddress;
        //mem.WriteByte(ref address, OpCodeId.JSR);
        //mem.WriteWord(ref address, branchAddress);
        //mem.WriteByte(ref address, OpCodeId.LDA_I);
        //mem.WriteByte(ref address, 42);
        //mem.WriteByte(ref address, OpCodeId.JMP_ABS);
        //mem.WriteWord(ref address, _startAddress); // Jump back to start address

        //// Code at branch jsr address
        //address = branchAddress;
        //mem.WriteByte(ref address, OpCodeId.LDA_I);
        //mem.WriteByte(ref address, 21);
        //mem.WriteByte(ref address, OpCodeId.RTS);

    }

    //[GlobalCleanup]
    //public void GlobalCleanup()
    //{
    //    Console.WriteLine("// " + "GlobalCleanup");
    //}

    [Benchmark(Baseline = true)]
    public void ExecInsWithoutInstrumentation()
    {
        _c64WithoutInstrumentation.CPU.PC = _startAddress;
        for (var i = 0; i < NumberOfInstructionsToExecute; i++)
        {
            _c64WithoutInstrumentation.ExecuteOneInstruction(_systemRunnerWithoutInstrumentation, out InstructionExecResult instructionExecResult);
        }
    }

    [Benchmark]
    public void ExecInsWithInstrumentation()
    {
        _c64WithInstrumentation.CPU.PC = _startAddress;
        for (var i = 0; i < NumberOfInstructionsToExecute; i++)
        {
            _c64WithInstrumentation.ExecuteOneInstruction(_systemRunnerWithInstrumentation, out InstructionExecResult instructionExecResult);
        }
    }
}
