using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Systems.Generic;

public class GenericComputer : ISystem, ITextMode, IScreen
{
    public const string SystemName = "Generic";
    public string Name => SystemName;
    public string SystemInfo => "";

    // How many 6502 CPU cycles this generic (fictional) computer should be able to execute per frame.
    // This should be adjusted to the performance of the machine the emulator is running on.
    // For comparison, a C64 runs about 16700 cycles per frame (1/60 sec).
    public ulong CPUCyclesPerFrame => _genericComputerConfig.CPUCyclesPerFrame;
    public ulong CyclesConsumedCurrentVblank { get; private set; } = 0;

    public Memory Mem { get; set; }
    public CPU CPU { get; set; }
    public IScreen Screen => this;

    public ExecOptions DefaultExecOptions { get; set; }

    public int Cols => _genericComputerConfig.Memory.Screen.Cols;
    public int Rows => _genericComputerConfig.Memory.Screen.Rows;
    public int CharacterWidth => 8;
    public int CharacterHeight => 8;

    public int Width => Cols * CharacterWidth;
    public int Height => Rows * CharacterHeight;
    public int VisibleWidth => (Cols * CharacterWidth) + (2 * (_genericComputerConfig.Memory.Screen.BorderCols * CharacterWidth));
    public int VisibleHeight => (Rows * CharacterHeight) + (2 * (_genericComputerConfig.Memory.Screen.BorderRows * CharacterHeight));
    public bool HasBorder => (VisibleWidth > Width) || (VisibleHeight > Height);
    public int BorderWidth => (VisibleWidth - Width) / 2;
    public int BorderHeight => (VisibleHeight - Height) / 2;
    public float RefreshFrequencyHz => _genericComputerConfig.ScreenRefreshFrequencyHz;

    private readonly GenericComputerConfig _genericComputerConfig;
    private LegacyExecEvaluator _oneFrameExecEvaluator;

    public GenericComputer() : this(new GenericComputerConfig()) { }
    public GenericComputer(GenericComputerConfig genericComputerConfig)
    {
        _genericComputerConfig = genericComputerConfig;
        Mem = new Memory();
        CPU = new CPU();
        DefaultExecOptions = new ExecOptions();

        _oneFrameExecEvaluator = new LegacyExecEvaluator(new ExecOptions { CyclesRequested = CPUCyclesPerFrame });

        CPU.InstructionExecuted += (s, e) => CPUCyclesConsumed(e.CPU, e.Mem, e.InstructionExecState.CyclesConsumed);
    }

    public void Run(IExecEvaluator? execEvaluator = null)
    {
        if (execEvaluator == null)
            execEvaluator = new LegacyExecEvaluator(DefaultExecOptions);
        CPU.Execute(
            Mem,
            execEvaluator);
    }

    public ExecEvaluatorTriggerResult ExecuteOneFrame(
        IExecEvaluator? execEvaluator = null,
        Action<ISystem, Dictionary<string, double>>? postInstructionCallback = null,
        Dictionary<string, double>? detailedStats = null)
    {
        // If we already executed cycles in current frame, reduce it from total.
        _oneFrameExecEvaluator.ExecOptions.CyclesRequested = CPUCyclesPerFrame - CyclesConsumedCurrentVblank;

        ExecState execState;
        if (execEvaluator == null)
        {
            execState = CPU.Execute(
                Mem,
                _oneFrameExecEvaluator);
        }
        else
        {
            execState = CPU.Execute(
                Mem,
                _oneFrameExecEvaluator,
                execEvaluator
                );
        }

        // If an unhandled instruction, return false
        if (!execState.LastOpCodeWasHandled)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.UnknownInstruction, $"Unknown instruction {Mem[execState.PCBeforeLastOpCodeExecuted!.Value].ToHex()} at {execState.PCBeforeLastOpCodeExecuted!.Value.ToHex()}");


        if (postInstructionCallback != null)
            postInstructionCallback(this, detailedStats);

        // If the custom ExecEvaluator said we shouldn't contine (for example a breakpoint), then indicate to caller that we shouldn't continue executing.
        if (execEvaluator != null)
        {
            var execEvaluatorTriggerResult = execEvaluator.Check(null, CPU, Mem);
            if (execEvaluatorTriggerResult.Triggered)
                return execEvaluatorTriggerResult;
        }

        // Tell CPU 6502 code that one frame worth of CPU cycles has been executed
        SetFrameCompleted();

        // Wait for CPU 6502 code has acknowledged that it knows a frame has completed.
        bool waitOk = WaitFrameCompletedAcknowledged();
        if (!waitOk)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.Other, "WaitFrame failed"); ;

        // Return true to indicate execution was successfull and we should continue
        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        IExecEvaluator? execEvaluator = null)
    {
        var execState = CPU.ExecuteOneInstruction(Mem);
        // If an unhandled instruction, return false
        if (!execState.LastOpCodeWasHandled)
            return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.UnknownInstruction, $"Unknown instruction {Mem[execState.PCBeforeLastOpCodeExecuted!.Value].ToHex()} at {execState.PCBeforeLastOpCodeExecuted!.Value.ToHex()}");

        // Check for debugger breakpoints (or other possible IExecEvaluator implementations used).
        if (execEvaluator != null)
        {
            var execEvaluatorTriggerResult = execEvaluator.Check(null, CPU, Mem);
            if (execEvaluatorTriggerResult.Triggered)
            {
                return execEvaluatorTriggerResult;
            }
        }

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    private void SetFrameCompleted()
    {
        Mem.SetBit(_genericComputerConfig.Memory.Screen.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame);
    }

    private bool WaitFrameCompletedAcknowledged()
    {
        // Keep on executing instructions until CPU 6502 code has cleared bit 0 in ScreenRefreshStatusAddress
        while (Mem.IsBitSet(_genericComputerConfig.Memory.Screen.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame))
        {
            var execEvaluatorTriggerResult = ExecuteOneInstruction();
            // If an unhandled instruction, return false
            if (execEvaluatorTriggerResult.Triggered)
                return false;
        }
        return true;
    }

    public void CPUCyclesConsumed(CPU cpu, Memory mem, ulong cyclesConsumed)
    {
        CyclesConsumedCurrentVblank += cyclesConsumed;
        if (CyclesConsumedCurrentVblank >= CPUCyclesPerFrame)
        {
            CyclesConsumedCurrentVblank = 0;
            VerticalBlank(cpu);
        }
    }

    public void VerticalBlank(CPU cpu)
    {
    }

    public GenericComputer Clone()
    {
        return new GenericComputer(this._genericComputerConfig)
        {
            CPU = this.CPU.Clone(),
            Mem = this.Mem.Clone(),
            DefaultExecOptions = this.DefaultExecOptions.Clone()
        };
    }

    public void Reset(ushort? cpuStartPos = null)
    {
        // TODO: Leave memory intact after reset?
        if (cpuStartPos == null)
            CPU.Reset(Mem);
        else
            CPU.PC = cpuStartPos.Value;
    }
}
