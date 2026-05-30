using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Render;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Vic20.Video;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Vic20;

/// <summary>
/// VIC-20 system implementation.
/// Runs the 6502 CPU instruction-by-instruction (matching the C64 pattern) so that
/// VIA chip timers are advanced in lock-step with the CPU cycle count.
/// </summary>
public class Vic20 : ISystem, ITextMode, IScreen
{
    public const string SystemName = "VIC-20";

    public string Name => SystemName;
    public List<string> SystemInfo => new() { "VIC-20" };
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
    public Vic20VideoLayout CurrentVideoLayout => Vic20VideoLayout.FromMemory(Mem, _vic20Config);

    // VIA chips — created before memory mapping so MapIOLocations can wire callbacks.
    public Via1 Via1 { get; }
    public Via2 Via2 { get; }

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

    public Vic20(Vic20Config config, ILoggerFactory loggerFactory, Dictionary<string, byte[]>? romData = null)
    {
        _vic20Config = config;
        Mem = new Memory();
        CPU = new CPU(loggerFactory);
        DefaultExecOptions = new ExecOptions();

        // Create VIA chips before ROM mapping so they can register memory callbacks.
        Via1 = new Via1(this, loggerFactory);
        Via2 = new Via2(this, Via1);

        if (romData != null)
            MapROMs(romData);

        // Wire VIA I/O callbacks into the memory map.
        Via1.MapIOLocations(Mem);
        Via2.MapIOLocations(Mem);

        InitScreenMemory();

        if (romData != null)
            CPU.Reset(Mem);

        RenderProviders.Add(new Vic20Rasterizer(this));
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

    public void SetCurrentRenderProviderType(Type? renderProviderType) => SetCurrentRenderProvider(renderProviderType);

    private void MapROMs(Dictionary<string, byte[]> romData)
    {
        // VIC-20 ROM layout:
        //   $8000–$8FFF  Character ROM (4 KB) — VIC-I address space, also CPU-readable
        //   $C000–$DFFF  BASIC ROM     (8 KB)
        //   $E000–$FFFF  KERNAL ROM    (8 KB)
        if (romData.TryGetValue(Vic20SystemConfig.CHARGEN_ROM_NAME, out var chargen))
            Mem.MapROM(0x8000, chargen);
        if (romData.TryGetValue(Vic20SystemConfig.BASIC_ROM_NAME, out var basic))
            Mem.MapROM(0xC000, basic);
        if (romData.TryGetValue(Vic20SystemConfig.KERNAL_ROM_NAME, out var kernal))
            Mem.MapROM(0xE000, kernal);
    }

    private void InitScreenMemory()
    {
        Mem[Vic20VideoLayout.RegisterColumns] = Vic20VideoLayout.EncodeColumnsRegister(
            _vic20Config.ScreenStartAddress,
            Vic20Config.Cols);
        Mem[Vic20VideoLayout.RegisterRows] = Vic20VideoLayout.EncodeRowsRegister(Vic20Config.Rows);
        Mem[Vic20VideoLayout.RegisterAddress] = Vic20VideoLayout.EncodeAddressRegister(
            _vic20Config.ScreenStartAddress,
            0x8000);
        Mem[Vic20VideoLayout.RegisterAuxiliaryColor] = (byte)(0x00 << 4);

        // VIC-I $900F packs: background (bits 7-4) | reverse=1 (bit 3) | border (bits 2-0)
        Mem[Vic20VideoLayout.RegisterBackgroundBorderColor] = (byte)(((_vic20Config.DefaultBgColor & 0x0F) << 4)
                                                                    | 0x08
                                                                    | (_vic20Config.DefaultBorderColor & 0x07));

        var layout = CurrentVideoLayout;
        var screenAddr = layout.ScreenStartAddress;
        var colorAddr = layout.ColorStartAddress;
        for (var i = 0; i < layout.Columns * layout.Rows; i++)
        {
            Mem[screenAddr++] = 0x20; // space
            Mem[colorAddr++]  = _vic20Config.DefaultFgColor;
        }
    }

    /// <summary>
    /// Executes one full video frame: runs the CPU instruction-by-instruction for the
    /// correct number of cycles, then fires the VIA1 CA1 interrupt that simulates the
    /// VIC-I raster pulse the KERNAL uses for keyboard scan and cursor blink.
    /// </summary>
    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
    {
        _renderProviderPerInstructionStat.Reset();

        ulong totalCyclesConsumed = 0;
        while (totalCyclesConsumed < CPUCyclesPerFrame)
        {
            var triggerResult = ExecuteOneInstruction(out var instrResult, execEvaluator);
            totalCyclesConsumed += instrResult.CyclesConsumed;

            if (triggerResult.Triggered)
                return triggerResult;
        }

        _renderProviderPerInstructionStat.Stop();

        // Fire the VIC-I raster interrupt — on real hardware this is a signal on VIA1 CA1
        // that the KERNAL uses (with IER bit 1 = CA1 enabled) to run its IRQ handler,
        // which does keyboard scan, cursor blink, and other housekeeping.
        Via1.TriggerCA1(CPU);

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
            bool isUnknown  = !CPU.InstructionList.OpCodeDictionary.ContainsKey(opcodeAtPC);
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

        instructionExecResult = CPU.ExecuteOneInstruction(Mem).LastInstructionExecResult;

        // Advance VIA timers — same pattern as C64's CIA timer processing.
        Via1.ProcessTimers(instructionExecResult.CyclesConsumed);
        Via2.ProcessTimers(instructionExecResult.CyclesConsumed);

        _renderProviderPerInstructionStat.Start(cont: true);
        _renderProvider?.OnAfterInstruction();
        _renderProviderPerInstructionStat.Stop(cont: true);

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public void Reset(ushort? cpuStartPos = null)
    {
        if (cpuStartPos == null)
            CPU.Reset(Mem);
        else
            CPU.PC = cpuStartPos.Value;
    }
}
