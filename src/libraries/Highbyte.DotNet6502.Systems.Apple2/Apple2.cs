using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Monitor;
using Highbyte.DotNet6502.Systems.Apple2.Peripherals;
using Highbyte.DotNet6502.Systems.Apple2.Render;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Apple2;

/// <summary>
/// Apple II Plus system implementation.
///
/// The simplest machine in this repository: 48 KB of flat RAM, a 12 KB ROM, and a page of
/// memory-mapped soft switches. There is no timer chip and no interrupt source at all — the
/// Autostart Monitor and Applesoft BASIC poll the keyboard latch directly — so a frame is just
/// "run the CPU for a frame's worth of cycles, then render".
/// </summary>
public class Apple2 : ISystem, ITextMode, IScreen, ISystemState, ISystemMonitor
{
    public const string SystemName = "Apple2";

    /// <summary>
    /// Where a tokenized Applesoft BASIC program starts (TXTTAB points here after boot).
    /// </summary>
    public const ushort BASIC_LOAD_ADDRESS = 0x0801;

    public string Name => SystemName;
    public List<string> SystemInfo => new() { "Apple II Plus", "48 KB RAM" };
    public List<KeyValuePair<string, Func<string>>> DebugInfo => new();

    public CPU CPU { get; set; }
    public Memory Mem { get; set; }
    public IScreen Screen => this;

    public ExecOptions DefaultExecOptions { get; set; }

    // ITextMode
    public int TextCols => Apple2Config.Cols;
    public int TextRows => Apple2Config.Rows;
    public int CharacterWidth => Apple2Config.CharacterWidth;
    public int CharacterHeight => Apple2Config.CharacterHeight;

    // IScreen — the Apple II text field fills the active display, so there is no border area.
    public int DrawableAreaWidth => Apple2Config.DrawableAreaWidth;
    public int DrawableAreaHeight => Apple2Config.DrawableAreaHeight;
    public int VisibleWidth => Apple2Config.DrawableAreaWidth;
    public int VisibleHeight => Apple2Config.DrawableAreaHeight;
    public bool HasBorder => Apple2Config.HasBorder;
    public int VisibleLeftRightBorderWidth => 0;
    public int VisibleTopBottomBorderHeight => 0;
    public float RefreshFrequencyHz => _apple2Config.ScreenRefreshFrequencyHz;

    public ulong CPUCyclesPerFrame => _apple2Config.CpuCyclesPerFrame;

    private readonly Apple2Config _apple2Config;
    public Apple2Config Apple2Config => _apple2Config;

    /// <summary>RAM: $0000-$BFFF on a fully populated 48 KB machine.</summary>
    public const ushort RamStartAddress = 0x0000;
    public const int RamSize = 0xC000;

    /// <summary>System ROM (Applesoft BASIC + Autostart Monitor): $D000-$FFFF.</summary>
    public const ushort SystemRomStartAddress = 0xD000;
    public const int SystemRomSize = 0x3000;

    private readonly byte[] _ram = new byte[RamSize];

    /// <summary>
    /// The 64 glyph patterns from the character generator (2513) ROM, 8 scan lines each.
    /// Not part of the CPU address space — the chip is wired to the video circuitry only — so
    /// this is held here for the rasterizer rather than mapped into <see cref="Mem"/>.
    /// Null when no character ROM was supplied.
    /// </summary>
    public byte[]? CharacterRom { get; private set; }

    public Apple2Keyboard Keyboard { get; }
    public Apple2SoftSwitches SoftSwitches { get; }

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

    private readonly bool _hasSystemRom;

    public Apple2() : this(new Apple2Config(), new NullLoggerFactory()) { }

    public Apple2(Apple2Config config, ILoggerFactory loggerFactory, Dictionary<string, byte[]>? romData = null)
    {
        _apple2Config = config;

        Keyboard = new Apple2Keyboard();
        SoftSwitches = new Apple2SoftSwitches(Keyboard);

        Mem = CreateMemory();
        CPU = new CPU(loggerFactory, config.CpuCompatibilityProfile);
        DefaultExecOptions = new ExecOptions();

        _hasSystemRom = romData != null && MapROMs(romData);

        // Only reset through the ROM's reset vector when a ROM is actually present; without one
        // $FFFC/$FFFD read as the unconnected value and the CPU would start executing garbage.
        if (_hasSystemRom)
            CPU.Reset(Mem);

        RenderProviders.Add(new Apple2Rasterizer(this));
        RenderProviders.Add(new Apple2VideoCommandStream(this));
        SetCurrentRenderProvider(typeof(Apple2Rasterizer));

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

    private Memory CreateMemory()
    {
        var mem = new Memory(mapToDefaultRAM: false);

        // $0000-$BFFF: 48 KB RAM.
        mem.MapRAM(RamStartAddress, _ram);

        // $C000-$C0FF soft switches, $C100-$CFFF empty peripheral slots.
        SoftSwitches.MapIOLocations(mem);

        // $D000-$FFFF: ROM socket. Default to "no device" so a ROM-less instance (unit tests,
        // the no-op system in a host's system list) still has a fully mapped address space;
        // MapROMs() replaces the readers when an image is supplied.
        MapUnconnectedRange(mem, SystemRomStartAddress, SystemRomSize);

        return mem;
    }

    private static void MapUnconnectedRange(Memory mem, ushort startAddress, int length)
    {
        for (var offset = 0; offset < length; offset++)
        {
            var address = (ushort)(startAddress + offset);
            mem.MapReader(address, static _ => Apple2SoftSwitches.UnconnectedReadValue);
            mem.MapWriter(address, static (_, _) => { });
        }
    }

    /// <summary>Loads the supplied ROM images. Returns whether a system ROM was mapped.</summary>
    private bool MapROMs(Dictionary<string, byte[]> romData)
    {
        // The character generator is not mapped into the CPU address space — it feeds the video
        // circuitry only, so the rasterizer reads it directly from CharacterRom.
        if (romData.TryGetValue(Apple2SystemConfig.CHARGEN_ROM_NAME, out var characterRom))
            CharacterRom = ExtractCharacterRomImage(characterRom);

        if (!romData.TryGetValue(Apple2SystemConfig.SYSTEM_ROM_NAME, out var systemRom))
            return false;

        Mem.MapROM(SystemRomStartAddress, ExtractSystemRomImage(systemRom));
        return true;
    }

    /// <summary>
    /// Normalizes a character generator image to the 512 bytes holding the 64 glyph patterns.
    /// Dumps of the 2513 circulate padded: a 2 KB image repeats the 64-glyph set, once with
    /// bit 7 of every byte set and then the whole 1 KB block duplicated. Only the leading
    /// 512 bytes carry unique data, and the rasterizer masks the bits it uses, so taking the
    /// first 512 bytes is correct for every layout seen in the wild.
    /// </summary>
    public static byte[] ExtractCharacterRomImage(byte[] romImage)
    {
        ArgumentNullException.ThrowIfNull(romImage);

        if (romImage.Length == Apple2CharSet.CharacterRomSize)
            return romImage;

        if (romImage.Length > Apple2CharSet.CharacterRomSize)
            return romImage[..Apple2CharSet.CharacterRomSize];

        throw new DotNet6502Exception(
            $"Apple II character generator ROM image is too small: {romImage.Length} bytes, " +
            $"expected at least {Apple2CharSet.CharacterRomSize}.");
    }

    /// <summary>
    /// Normalizes a system ROM image to the 12 KB that occupies $D000-$FFFF.
    /// Apple II Plus ROMs circulate both as a trimmed 12 KB image and in an older
    /// emulator-distribution layout that spans $B000-$FFFF (20,480 bytes) whose leading bytes
    /// include the meaningless $C000-$CFFF I/O space. In every such layout the loadable part is
    /// the last 12 KB.
    /// </summary>
    public static byte[] ExtractSystemRomImage(byte[] romImage)
    {
        ArgumentNullException.ThrowIfNull(romImage);

        if (romImage.Length == SystemRomSize)
            return romImage;

        if (romImage.Length > SystemRomSize)
            return romImage[^SystemRomSize..];

        throw new DotNet6502Exception(
            $"Apple II system ROM image is too small: {romImage.Length} bytes, expected at least {SystemRomSize}.");
    }

    /// <summary>
    /// Executes one full video frame. There is no timer chip or interrupt source on this
    /// machine, so a frame is simply a fixed number of CPU cycles followed by a render.
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

        instructionExecResult = CPU.ExecuteOneInstruction(Mem).LastInstructionExecResult;

        _renderProviderPerInstructionStat.Start(cont: true);
        _renderProvider?.OnAfterInstruction();
        _renderProviderPerInstructionStat.Stop(cont: true);

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public void Reset(ushort? cpuStartPos = null)
    {
        Keyboard.Reset();
        SoftSwitches.Reset();

        if (cpuStartPos == null)
            CPU.Reset(Mem);
        else
            CPU.PC = cpuStartPos.Value;
    }

    private readonly Apple2MonitorCommands _apple2MonitorCommands = new();

    public ISystemMonitorCommands GetSystemMonitorCommands() => _apple2MonitorCommands;

    /// <summary>
    /// Initialise the Applesoft zero-page state after a tokenized BASIC program has been placed
    /// in memory manually (outside of Applesoft's own LOAD code), so that RUN and LIST work.
    ///
    /// The Apple II equivalent of <c>C64.InitBasicMemoryVariables</c>.
    /// </summary>
    /// <param name="loadedAtAddress">Where the program was placed (normally <see cref="BASIC_LOAD_ADDRESS"/>).</param>
    /// <param name="fileLength">Length of the tokenized program, including its terminating $00 $00 link.</param>
    public void InitBasicMemoryVariables(ushort loadedAtAddress, int fileLength)
    {
        // Applesoft requires the byte immediately before the program text to be zero.
        Mem[(ushort)(loadedAtAddress - 1)] = 0x00;

        // TXTTAB $67-$68: start of BASIC program text. Set explicitly so injection also works
        // when a program is placed before BASIC's cold start has run.
        Mem.WriteWord(0x67, loadedAtAddress);

        // The variable pointers must point one byte past the program's terminating $00 $00 link:
        // VARTAB $69-$6A  start of simple variables
        // ARYTAB $6B-$6C  start of arrays
        // STREND $6D-$6E  end of arrays (+1) / start of free space
        // PRGEND $AF-$B0  end of program (Applesoft LOAD/NEW keep this in sync with VARTAB)
        ushort varStartAddress = (ushort)(loadedAtAddress + fileLength);
        Mem.WriteWord(0x69, varStartAddress);
        Mem.WriteWord(0x6B, varStartAddress);
        Mem.WriteWord(0x6D, varStartAddress);
        Mem.WriteWord(0xAF, varStartAddress);
    }

    /// <summary>
    /// Returns the end address of the current tokenized BASIC program in memory, as an
    /// <em>exclusive</em> bound (one byte past the program's terminating $00 $00 link) — the
    /// convention <see cref="Utils.BinarySaver.BuildSaveData"/> and the monitor's save command
    /// expect, matching <c>C64.GetBasicProgramEndAddress</c>. That is exactly VARTAB.
    /// </summary>
    public ushort GetBasicProgramEndAddress()
    {
        return Mem.FetchWord(0x69);
    }

    /// <summary>
    /// Whether Applesoft BASIC has finished initialising. The Autostart Monitor writes the
    /// power-up byte at $03F4 as the complement of the high byte of the soft-entry vector; a
    /// matching pair means the cold start completed and the BASIC prompt is up.
    /// </summary>
    public bool HasBasicStarted()
    {
        if (!_hasSystemRom)
            return false;
        var softEvHigh = Mem[0x03F3];
        var powerUpByte = Mem[0x03F4];
        return powerUpByte == (byte)(softEvHigh ^ 0xA5);
    }

    bool ISystemState.IsSystemReady() => HasBasicStarted();
}
