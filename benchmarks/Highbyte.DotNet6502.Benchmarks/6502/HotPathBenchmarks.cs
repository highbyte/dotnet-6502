using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Benchmarks;

/// <summary>
/// Adds <see cref="DisassemblyDiagnoser"/> on platforms where BenchmarkDotNet supports
/// it (Linux + Windows). On macOS the diagnoser fails the benchmark validator, so we
/// skip it and rely on a CI run for the disassembly verification step instead.
/// </summary>
internal class HotPathConfig : ManualConfig
{
    public HotPathConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(maxDepth: 2, exportHtml: true)));
        }
    }
}

/// <summary>
/// Micro-benchmarks for the per-CPU-step hot path.
///
/// At 1 MHz, the emulator calls these methods ~1 million times per second, so anything
/// that allocates, copies a large struct, or skips a JIT inlining heuristic shows up
/// linearly. These benchmarks isolate the smallest units on that path so refactors can
/// be validated empirically against a baseline before merge.
///
/// See <c>documents/features/hot-path-benchmarking-and-improvements.md</c> in the
/// dotnet-6502-design-log repo for the design notes that drove these picks.
/// </summary>
[Config(typeof(HotPathConfig))]
public class HotPathBenchmarks
{
    // --- ExecEvaluator.Check fixtures -----------------------------------------------

    private LegacyExecEvaluator _evaluatorNoConditions = default!;
    private LegacyExecEvaluator _evaluatorOneCondition = default!;
    private LegacyExecEvaluator _evaluatorAllConditions = default!;

    private ExecState _execState = default!;
    private CPU _cpuForCheck = default!;
    private Memory _memForCheck = default!;

    // --- CPU step fixtures ----------------------------------------------------------

    private CPU _cpuForStep = default!;
    private Memory _memForStep = default!;
    private ushort _startAddress;

    // Number of instructions to run for the aggregate throughput benchmark. Kept as a
    // constant rather than a [Params] so the benchmark output stays compact; tune here
    // if you want a different sample size.
    private const int RunInstructionCount = 1000;

    [GlobalSetup]
    public void Setup()
    {
        // ExecEvaluator.Check fixtures -- the cpu/mem references are passed through
        // but the legacy evaluator currently doesn't read them; keep real instances so
        // any future evaluator that does will still benchmark realistically.
        _cpuForCheck = new CPU();
        _memForCheck = new Memory();

        _evaluatorNoConditions = new LegacyExecEvaluator(new ExecOptions());

        _evaluatorOneCondition = new LegacyExecEvaluator(new ExecOptions
        {
            MaxNumberOfInstructions = ulong.MaxValue, // configured but never reached
        });

        _evaluatorAllConditions = new LegacyExecEvaluator(new ExecOptions
        {
            CyclesRequested = ulong.MaxValue,
            MaxNumberOfInstructions = ulong.MaxValue,
            ExecuteUntilPC = 0xFFFF,
            ExecuteUntilExecutedInstructionAtPC = 0xFFFF,
            ExecuteUntilInstruction = OpCodeId.BRK,
            ExecuteUntilInstructions = new List<byte> { 0x01, 0x02, 0x03, 0x04 },
            UnknownInstructionThrowsException = false,
            BRKInstructionStopsExecution = true,
        });

        // A realistic ExecState: one valid LDA_I result, partway through execution.
        // OpCode 0xA9 is LDA_I -- chosen because it's not in any of the stop sets above,
        // so Check returns NotTriggered (the common case).
        var seedResult = InstructionExecResult.KnownInstructionResult(
            opCodeByte: (byte)OpCodeId.LDA_I,
            atPC: 0xC000,
            cyclesConsumed: 2);
        _execState = ExecState.ExecStateAfterInstruction(seedResult);

        // CPU step fixtures -- a tight, deterministic program loop.
        _cpuForStep = new CPU();
        _memForStep = new Memory();
        _startAddress = 0xC000;
        LoadStepProgram(_memForStep, _startAddress);
        _cpuForStep.PC = _startAddress;
    }

    private static void LoadStepProgram(Memory mem, ushort startAddress)
    {
        // JSR -> LDA #42 -> JMP back; subroutine is LDA #21 + RTS.
        // Mix of addressing modes (Implied, Immediate, Absolute) without any stop-set
        // opcodes, so a 1000-instruction run doesn't terminate early.
        ushort branchAddress = (ushort)(startAddress + 0x10);
        var address = startAddress;
        mem.WriteByte(ref address, OpCodeId.JSR);
        mem.WriteWord(ref address, branchAddress);
        mem.WriteByte(ref address, OpCodeId.LDA_I);
        mem.WriteByte(ref address, 42);
        mem.WriteByte(ref address, OpCodeId.JMP_ABS);
        mem.WriteWord(ref address, startAddress);

        address = branchAddress;
        mem.WriteByte(ref address, OpCodeId.LDA_I);
        mem.WriteByte(ref address, 21);
        mem.WriteByte(ref address, OpCodeId.RTS);
    }

    // --- ExecEvaluator.Check ---------------------------------------------------------

    /// <summary>
    /// Baseline: the most common case -- evaluator with no stop conditions configured.
    /// Every benchmark below is reported as a ratio of this.
    /// </summary>
    [Benchmark(Baseline = true)]
    public ExecEvaluatorTriggerResult ExecEvaluator_Check_NotTriggered()
    {
        return _evaluatorNoConditions.Check(_execState, _cpuForCheck, _memForCheck);
    }

    /// <summary>
    /// One stop condition configured but not yet reached -- typical "run until
    /// instruction count" workload.
    /// </summary>
    [Benchmark]
    public ExecEvaluatorTriggerResult ExecEvaluator_Check_OneConditionConfigured()
    {
        return _evaluatorOneCondition.Check(_execState, _cpuForCheck, _memForCheck);
    }

    /// <summary>
    /// Worst case -- every <see cref="ExecOptions"/> slot configured. Stresses the full
    /// branch chain in Check.
    /// </summary>
    [Benchmark]
    public ExecEvaluatorTriggerResult ExecEvaluator_Check_AllConditionsConfigured()
    {
        return _evaluatorAllConditions.Check(_execState, _cpuForCheck, _memForCheck);
    }

    // --- CPU step --------------------------------------------------------------------

    /// <summary>
    /// One full CPU step via the minimal path (decode + execute, no events, no
    /// ExecEvaluator). Resets PC each invocation so the measurement is per-step.
    /// </summary>
    [Benchmark]
    public InstructionExecResult InstructionExecutor_OneStep()
    {
        _cpuForStep.PC = _startAddress;
        return _cpuForStep.ExecuteOneInstructionMinimal(_memForStep);
    }

    /// <summary>
    /// Aggregate per-instruction throughput over a small mixed program. Dominated by
    /// the steady-state cost of <see cref="ExecuteOneInstructionMinimal"/> rather than
    /// fixture overhead.
    /// </summary>
    [Benchmark]
    public void CPU_Run_1000Instructions()
    {
        _cpuForStep.PC = _startAddress;
        for (int i = 0; i < RunInstructionCount; i++)
        {
            _cpuForStep.ExecuteOneInstructionMinimal(_memForStep);
        }
    }
}
