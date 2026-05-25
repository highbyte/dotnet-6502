using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Render;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Vic20;

/// <summary>
/// VIC-20 system stub. Exercises the system-plugin contract end-to-end:
/// CPU + flat RAM, VIC-I text-mode video via the command-stream render path,
/// and a no-op keyboard handler — all without any real VIC-20 chip emulation.
/// </summary>
public class Vic20 : ISystem, ITextMode, IScreen
{
    public const string SystemName = "VIC-20";

    public string Name => SystemName;
    public List<string> SystemInfo => new() { "VIC-20 (stub)" };
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

    public CPU CPU { get; set; }
    public Memory Mem { get; set; }
    public IScreen Screen => this;

    public ExecOptions DefaultExecOptions { get; set; }

    // ITextMode
    public int TextCols => Vic20Config.Cols;
    public int TextRows => Vic20Config.Rows;
    public int CharacterWidth => 8;
    public int CharacterHeight => 8;

    // IScreen
    public int DrawableAreaWidth => TextCols * CharacterWidth;
    public int DrawableAreaHeight => TextRows * CharacterHeight;
    public int VisibleWidth => DrawableAreaWidth + 2 * (Vic20Config.BorderCols * CharacterWidth);
    public int VisibleHeight => DrawableAreaHeight + 2 * (Vic20Config.BorderRows * CharacterHeight);
    public bool HasBorder => true;
    public int VisibleLeftRightBorderWidth => Vic20Config.BorderCols * CharacterWidth;
    public int VisibleTopBottomBorderHeight => Vic20Config.BorderRows * CharacterHeight;
    public float RefreshFrequencyHz => _vic20Config.ScreenRefreshFrequencyHz;

    public ulong CPUCyclesPerFrame => _vic20Config.CpuCyclesPerFrame;

    private readonly Vic20Config _vic20Config;
    public Vic20Config Vic20Config => _vic20Config;

    private ulong _cyclesConsumedCurrentVblank = 0;

    private readonly LegacyExecEvaluator _oneFrameExecEvaluator;

    private IRenderProvider? _renderProvider;
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = new();

    public IInputConsumer? InputConsumer { get; set; }

    // Instrumentations
    public bool InstrumentationEnabled { get; set; } = false;
    public Instrumentations Instrumentations { get; } = new();

    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerInstructionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerFrameStat;

    private const string StatsCategoryRenderProvider = "RenderProvider";

    public Vic20() : this(new Vic20Config(), new NullLoggerFactory()) { }

    public Vic20(Vic20Config config, ILoggerFactory loggerFactory)
    {
        _vic20Config = config;
        Mem = new Memory();
        CPU = new CPU(loggerFactory);
        DefaultExecOptions = new ExecOptions();

        _oneFrameExecEvaluator = new LegacyExecEvaluator(
            new ExecOptions { CyclesRequested = CPUCyclesPerFrame });

        CPU.InstructionExecuted += (_, e) =>
            OnCPUCyclesConsumed(e.CPU, e.Mem, e.InstructionExecState.CyclesConsumed);

        InitScreenMemory();

        RenderProviders.Add(new Vic20VideoCommandStream(this));
        SetCurrentRenderProvider(typeof(Vic20VideoCommandStream));

        _renderProviderPerInstructionStat = Instrumentations.Add(
            $"{StatsCategoryRenderProvider}-Instruction", new ElapsedMillisecondsTimedStatSystem(this));
        _renderProviderPerFrameStat = Instrumentations.Add(
            $"{StatsCategoryRenderProvider}-Frame", new ElapsedMillisecondsTimedStatSystem(this));
    }

    private void SetCurrentRenderProvider(Type? renderProviderType)
    {
        if (renderProviderType == null) { _renderProvider = null; return; }
        _renderProvider = RenderProviders.SingleOrDefault(rp => rp.GetType() == renderProviderType)
            ?? throw new ArgumentException("Render provider type not found.");
    }

    private void InitScreenMemory()
    {
        // Blank the screen with spaces and default colors
        Mem[_vic20Config.BackgroundColorAddress] = _vic20Config.DefaultBgColor;
        Mem[_vic20Config.BorderColorAddress] = _vic20Config.DefaultBorderColor;

        var screenAddr = _vic20Config.ScreenStartAddress;
        var colorAddr = _vic20Config.ColorStartAddress;
        for (var i = 0; i < Vic20Config.Cols * Vic20Config.Rows; i++)
        {
            Mem[screenAddr++] = 0x20; // space
            Mem[colorAddr++] = _vic20Config.DefaultFgColor;
        }
    }

    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
    {
        _renderProviderPerInstructionStat.Reset();

        _oneFrameExecEvaluator.ExecOptions.CyclesRequested =
            CPUCyclesPerFrame - _cyclesConsumedCurrentVblank;

        ExecState execState;
        if (execEvaluator == null)
            execState = CPU.Execute(Mem, _oneFrameExecEvaluator);
        else
            execState = CPU.Execute(Mem, _oneFrameExecEvaluator, execEvaluator);

        if (execEvaluator != null)
        {
            var triggerResult = execEvaluator.Check(execState, CPU, Mem);
            if (triggerResult.Triggered) return triggerResult;
        }

        _renderProviderPerInstructionStat.Stop();

        _renderProviderPerFrameStat.Start();
        _renderProvider?.OnEndFrame();
        _renderProviderPerFrameStat.Stop();

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null)
    {
        if (execEvaluator != null)
        {
            byte opcodeAtPC = Mem[CPU.PC];
            bool isUnknown = !CPU.InstructionList.OpCodeDictionary.ContainsKey(opcodeAtPC);
            var preExecResult = isUnknown
                ? InstructionExecResult.UnknownInstructionResult(opcodeAtPC, CPU.PC)
                : InstructionExecResult.KnownInstructionResult(opcodeAtPC, CPU.PC, 0);

            var preCheckResult = execEvaluator.Check(preExecResult, CPU, Mem);
            if (preCheckResult.Triggered)
            {
                instructionExecResult = preExecResult;
                return preCheckResult;
            }
        }

        var execState = CPU.ExecuteOneInstruction(Mem);
        instructionExecResult = execState.LastInstructionExecResult;

        _renderProviderPerInstructionStat.Start(cont: true);
        _renderProvider?.OnAfterInstruction();
        _renderProviderPerInstructionStat.Stop(cont: true);

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    private void OnCPUCyclesConsumed(CPU cpu, Memory mem, ulong cyclesConsumed)
    {
        _cyclesConsumedCurrentVblank += cyclesConsumed;
        if (_cyclesConsumedCurrentVblank >= CPUCyclesPerFrame)
            _cyclesConsumedCurrentVblank = 0;
    }

    public void Reset(ushort? cpuStartPos = null)
    {
        if (cpuStartPos == null)
            CPU.Reset(Mem);
        else
            CPU.PC = cpuStartPos.Value;
    }
}
