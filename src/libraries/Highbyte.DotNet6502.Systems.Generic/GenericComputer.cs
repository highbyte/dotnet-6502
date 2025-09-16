using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Generic.Render;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Generic;

public class GenericComputer : ISystem, ITextMode, IScreen
{
    public const string SystemName = "Generic";
    public string Name => SystemName;
    public List<string> SystemInfo => new List<string> { "" };
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

    // How many 6502 CPU cycles this generic (fictional) computer should be able to execute per frame.
    // This should be adjusted to the performance of the machine the emulator is running on.
    // For comparison, a C64 runs about 16700 cycles per frame (1/60 sec).
    public ulong CPUCyclesPerFrame => _genericComputerConfig.CPUCyclesPerFrame;
    public ulong CyclesConsumedCurrentVblank { get; private set; } = 0;

    public Memory Mem { get; set; }
    public CPU CPU { get; set; }
    public IScreen Screen => this;

    public ExecOptions DefaultExecOptions { get; set; }

    public int TextCols => _genericComputerConfig.Memory.Screen.Cols;
    public int TextRows => _genericComputerConfig.Memory.Screen.Rows;
    public int CharacterWidth => 8;
    public int CharacterHeight => 8;

    public int DrawableAreaWidth => TextCols * CharacterWidth;
    public int DrawableAreaHeight => TextRows * CharacterHeight;
    public int VisibleWidth => (TextCols * CharacterWidth) + (2 * (_genericComputerConfig.Memory.Screen.BorderCols * CharacterWidth));
    public int VisibleHeight => (TextRows * CharacterHeight) + (2 * (_genericComputerConfig.Memory.Screen.BorderRows * CharacterHeight));
    public bool HasBorder => (VisibleWidth > DrawableAreaWidth) || (VisibleHeight > DrawableAreaHeight);
    public int VisibleLeftRightBorderWidth => (VisibleWidth - DrawableAreaWidth) / 2;
    public int VisibleTopBottomBorderHeight => (VisibleHeight - DrawableAreaHeight) / 2;
    public float RefreshFrequencyHz => _genericComputerConfig.ScreenRefreshFrequencyHz;

    private ILogger _logger;
    private GenericComputerConfig _genericComputerConfig;
    public GenericComputerConfig GenericComputerConfig => _genericComputerConfig;
    private readonly LegacyExecEvaluator _oneFrameExecEvaluator;

    private IRenderProvider? _renderProvider;
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = new();

    // Instrumentations
    public bool InstrumentationEnabled { get; set; } = false;

    public Instrumentations Instrumentations { get; } = new();

    private const string StatsCategoryRenderProvider = "RenderProvider";
    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerInstructionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerFrameStat;


    public GenericComputer() : this(new GenericComputerConfig(), new NullLoggerFactory()) { }
    public GenericComputer(GenericComputerConfig genericComputerConfig, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(typeof(GenericComputer).Name);

        _genericComputerConfig = genericComputerConfig;
        Mem = new Memory();
        CPU = new CPU(loggerFactory);
        DefaultExecOptions = new ExecOptions();

        _oneFrameExecEvaluator = new LegacyExecEvaluator(new ExecOptions { CyclesRequested = CPUCyclesPerFrame });

        CPU.InstructionExecuted += (s, e) => CPUCyclesConsumed(e.CPU, e.Mem, e.InstructionExecState.CyclesConsumed);

        InitEmulatorScreenMemory();

        ConfigureRenderer(genericComputerConfig);

        _renderProviderPerInstructionStat = Instrumentations.Add($"{StatsCategoryRenderProvider}-Instruction", new ElapsedMillisecondsTimedStatSystem(this));
        _renderProviderPerFrameStat = Instrumentations.Add($"{StatsCategoryRenderProvider}-Frame", new ElapsedMillisecondsTimedStatSystem(this));

        _logger.LogInformation($"Generic computer created.");
    }

    private void SetCurrentRenderProvider(Type? renderProviderType)
    {
        if (renderProviderType == null)
        {
            _renderProvider = null;
            return;
        }
        var renderProvider = RenderProviders.SingleOrDefault(rp => rp.GetType() == renderProviderType)
            ?? throw new ArgumentException("The specified render provider type is not available.");
        _renderProvider = renderProvider;
    }

    private void ConfigureRenderer(GenericComputerConfig genericComputerConfig)
    {
        RenderProviders.Add(new GenericVideoCommandStream(this));

        SetCurrentRenderProvider(genericComputerConfig.RenderProviderType);
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
        SystemRunner systemRunner,
        IExecEvaluator? execEvaluator = null)
    {
        _renderProviderPerInstructionStat.Reset(); // Reset stat, will be continiously updated after each instruction

        // If we already executed cycles in current frame, reduce it from total.
        _oneFrameExecEvaluator.ExecOptions.CyclesRequested = CPUCyclesPerFrame - CyclesConsumedCurrentVblank;

        _logger.LogTrace($"Executing one frame, {_oneFrameExecEvaluator.ExecOptions.CyclesRequested} CPU cycles.");

        // Execute one frame worth of CPU cycles
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

        // If the custom ExecEvaluator said we shouldn't contine (for example a breakpoint), then indicate to caller that we shouldn't continue executing.
        if (execEvaluator != null)
        {
            var execEvaluatorTriggerResult = execEvaluator.Check(execState, CPU, Mem);
            if (execEvaluatorTriggerResult.Triggered)
                return execEvaluatorTriggerResult;
        }

        if (_genericComputerConfig.WaitForHostToAcknowledgeFrame)
        {
            // Tell CPU 6502 code that one frame worth of CPU cycles has been executed
            SetFrameCompleted();

            // Wait for CPU 6502 code has acknowledged that it knows a frame has completed.
            bool waitOk = WaitFrameCompletedAcknowledged(systemRunner);
            if (!waitOk)
                return ExecEvaluatorTriggerResult.CreateTrigger(ExecEvaluatorTriggerReasonType.Other, "WaitFrame failed"); ;
        }

        _renderProviderPerInstructionStat.Stop(); // Stop stat (was continiously updated after each instruction)


        // New render pipeline
        _renderProviderPerFrameStat.Start();
        _renderProvider?.OnEndFrame();
        _renderProviderPerFrameStat.Stop();

        // Return true to indicate execution was successfull and we should continue
        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        SystemRunner systemRunner,
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null)
    {
        var execState = CPU.ExecuteOneInstruction(Mem);

        instructionExecResult = execState.LastInstructionExecResult;

        _renderProviderPerInstructionStat.Start(cont: true);
        _renderProvider?.OnAfterInstruction();
        _renderProviderPerInstructionStat.Stop(cont: true);

        // Check for debugger breakpoints (or other possible IExecEvaluator implementations used).
        if (execEvaluator != null)
        {
            var execEvaluatorTriggerResult = execEvaluator.Check(execState, CPU, Mem);
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

    private bool WaitFrameCompletedAcknowledged(SystemRunner systemRunner)
    {
        // Keep on executing instructions until CPU 6502 code has cleared bit 0 in ScreenRefreshStatusAddress
        while (Mem.IsBitSet(_genericComputerConfig.Memory.Screen.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame))
        {
            var execEvaluatorTriggerResult = ExecuteOneInstruction(systemRunner, out _);
            // If an unhandled instruction or other configured trigger has activated, return false
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

    // TODO: When Memory Clone() method is working correctly, this method can begin to be used
    //public GenericComputer Clone()
    //{
    //    return new GenericComputer()
    //    {
    //        CPU = this.CPU.Clone(),
    //        Mem = this.Mem.Clone(),
    //        DefaultExecOptions = this.DefaultExecOptions.Clone(),
    //        _genericComputerConfig = this._genericComputerConfig,
    //        _logger = this._logger
    //    };
    //}

    public void Reset(ushort? cpuStartPos = null)
    {
        // TODO: Leave memory intact after reset?
        if (cpuStartPos == null)
            CPU.Reset(Mem);
        else
            CPU.PC = cpuStartPos.Value;
    }

    /// <summary>
    /// Set emulator screen memory initial state
    /// </summary>
    public void InitEmulatorScreenMemory()
    {
        var emulatorScreenConfig = _genericComputerConfig.Memory.Screen;
        // Common bg and border color for entire screen, controlled by specific address
        Mem[emulatorScreenConfig.ScreenBorderColorAddress] = emulatorScreenConfig.DefaultBorderColor;
        Mem[emulatorScreenConfig.ScreenBackgroundColorAddress] = emulatorScreenConfig.DefaultBgColor;

        var currentScreenAddress = emulatorScreenConfig.ScreenStartAddress;
        var currentColorAddress = emulatorScreenConfig.ScreenColorStartAddress;
        for (var row = 0; row < emulatorScreenConfig.Rows; row++)
        {
            for (var col = 0; col < emulatorScreenConfig.Cols; col++)
            {
                Mem[currentScreenAddress++] = 0x20;    // 32 (0x20) = space
                Mem[currentColorAddress++] = emulatorScreenConfig.DefaultFgColor;
            }
        }
    }


}
