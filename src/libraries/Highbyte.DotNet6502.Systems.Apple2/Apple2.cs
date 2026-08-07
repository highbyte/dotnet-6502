using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Highbyte.DotNet6502.Systems.Apple2.Input;
using Highbyte.DotNet6502.Systems.Apple2.Monitor;
using Highbyte.DotNet6502.Systems.Apple2.Utils;
using Highbyte.DotNet6502.Systems.Apple2.Audio.Sample;
using Highbyte.DotNet6502.Systems.Audio;
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

    /// <summary>
    /// Effective CPU frequency, derived from the frame timing rather than stated separately so the
    /// two cannot disagree. Audio pitch comes straight from this: a wrong value here detunes every
    /// sound the machine makes.
    /// </summary>
    public double CpuFrequencyHz => _apple2Config.CpuCyclesPerFrame * _apple2Config.ScreenRefreshFrequencyHz;

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

    /// <summary>The Disk II controller card in slot 6 (read-only; boots when a disk is inserted).</summary>
    public Disk2Controller DiskController { get; }

    /// <summary>The game port: pushbuttons and analog paddles. See <see cref="Apple2GamePort"/>.</summary>
    public Apple2GamePort GamePort { get; }

    /// <summary>The one-bit speaker. See <see cref="Apple2Speaker"/>.</summary>
    public Apple2Speaker Speaker { get; }

    /// <summary>Types text into the machine by feeding the keyboard latch, one char per frame.</summary>
    public Apple2TextPaste TextPaste { get; }

    /// <summary>Detokenizes the Applesoft program in memory to BASIC source text.</summary>
    public Apple2BasicTokenParser BasicTokenParser { get; }

    /// <summary>Remote-control input injector (generic keyboard.* remote commands).</summary>
    public Apple2InputInjector InputInjector { get; }
    IInputInjector? ISystem.InputInjector => InputInjector;

    private IRenderProvider? _renderProvider;
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = new();

    private IAudioProvider? _audioProvider;
    public IAudioProvider? AudioProvider => _audioProvider;
    public List<IAudioProvider> AudioProviders { get; } = new();

    public IInputConsumer? InputConsumer { get; set; }

    // Instrumentations
    public bool InstrumentationEnabled { get; set; } = false;
    public Instrumentations Instrumentations { get; } = new();

    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerInstructionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerFrameStat;
    private const string StatsCategoryRenderProvider = "RenderProvider";

    private readonly ElapsedMillisecondsTimedStatSystem _audioProviderPerInstructionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _audioProviderPerFrameStat;
    private const string StatsCategoryAudioProvider = "AudioProvider";

    private readonly bool _hasSystemRom;

    public Apple2() : this(new Apple2Config(), new NullLoggerFactory()) { }

    public Apple2(Apple2Config config, ILoggerFactory loggerFactory, Dictionary<string, byte[]>? romData = null)
    {
        _apple2Config = config;

        Keyboard = new Apple2Keyboard();
        CPU = new CPU(loggerFactory, config.CpuCompatibilityProfile);
        // The controller times its motor spin-down off the CPU's cumulative cycle count.
        DiskController = new Disk2Controller(() => CPU.ExecState.CyclesConsumed);
        GamePort = new Apple2GamePort(() => CPU.ExecState.CyclesConsumed);
        Speaker = new Apple2Speaker(() => CPU.ExecState.CyclesConsumed);
        SoftSwitches = new Apple2SoftSwitches(Keyboard, DiskController, GamePort, Speaker);
        TextPaste = new Apple2TextPaste(this, loggerFactory);
        BasicTokenParser = new Apple2BasicTokenParser(this, loggerFactory);
        InputInjector = new Apple2InputInjector(this);

        Mem = CreateMemory();
        DefaultExecOptions = new ExecOptions();

        _hasSystemRom = romData != null && MapROMs(romData);

        // Only reset through the ROM's reset vector when a ROM is actually present; without one
        // $FFFC/$FFFD read as the unconnected value and the CPU would start executing garbage.
        if (_hasSystemRom)
            CPU.Reset(Mem);

        RenderProviders.Add(new Apple2Rasterizer(this));
        RenderProviders.Add(new Apple2VideoCommandStream(this));
        SetCurrentRenderProvider(typeof(Apple2Rasterizer));

        AddAudioProviders(this, _apple2Config);

        _renderProviderPerInstructionStat = Instrumentations.Add(
            $"{StatsCategoryRenderProvider}-Instruction", new ElapsedMillisecondsTimedStatSystem(this));
        _renderProviderPerFrameStat = Instrumentations.Add(
            $"{StatsCategoryRenderProvider}-Frame", new ElapsedMillisecondsTimedStatSystem(this));
        _audioProviderPerInstructionStat = Instrumentations.Add(
            $"{StatsCategoryAudioProvider}-Instruction", new ElapsedMillisecondsTimedStatSystem(this));
        _audioProviderPerFrameStat = Instrumentations.Add(
            $"{StatsCategoryAudioProvider}-Frame", new ElapsedMillisecondsTimedStatSystem(this));
    }

    /// <summary>
    /// Builds the audio provider, if audio is on. With it off no provider is created, so
    /// <see cref="AudioProvider"/> stays null, the host builds no audio coordinator and the machine
    /// is silent — the same arrangement the C64 uses.
    /// </summary>
    private static void AddAudioProviders(Apple2 apple2, Apple2Config config)
    {
        if (!config.AudioEnabled)
            return;

        apple2.AudioProviders.Add(new Apple2SpeakerSampleProvider(apple2));

        // Only one provider exists — the machine emits no note or voice information a synth-command
        // stream could use — so an unset type simply means "the speaker".
        apple2.SetCurrentAudioProvider(config.AudioProviderType ?? typeof(Apple2SpeakerSampleProvider));
    }

    private void SetCurrentAudioProvider(Type? audioProviderType)
    {
        if (audioProviderType == null) { _audioProvider = null; return; }
        _audioProvider = AudioProviders.SingleOrDefault(ap => ap.GetType() == audioProviderType)
            ?? throw new ArgumentException($"Audio provider type not found: {audioProviderType.FullName}");
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

        if (romData.TryGetValue(Apple2SystemConfig.DISK2_ROM_NAME, out var disk2Rom))
            DiskController.SetBootRom(disk2Rom);

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
        _audioProviderPerInstructionStat.Reset();

        ulong totalCyclesConsumed = 0;
        while (totalCyclesConsumed < CPUCyclesPerFrame)
        {
            var triggerResult = ExecuteOneInstruction(out var instrResult, execEvaluator);
            totalCyclesConsumed += instrResult.CyclesConsumed;

            if (triggerResult.Triggered)
                return triggerResult;
        }

        _renderProviderPerInstructionStat.Stop();
        _audioProviderPerInstructionStat.Stop();

        // Deliver at most one pending pasted character per frame, gated on the previous one
        // having been consumed (strobe cleared).
        TextPaste.InsertNextCharacterToLatch();

        _renderProviderPerFrameStat.Start();
        _renderProvider?.OnEndFrame();
        _renderProviderPerFrameStat.Stop();

        _audioProviderPerFrameStat.Start();
        _audioProvider?.OnEndFrame();
        _audioProviderPerFrameStat.Stop();

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

        _audioProviderPerInstructionStat.Start(cont: true);
        _audioProvider?.OnAfterInstruction();
        _audioProviderPerInstructionStat.Stop(cont: true);

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public void Reset(ushort? cpuStartPos = null)
    {
        Keyboard.Reset();
        SoftSwitches.Reset();
        DiskController.Reset();

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

    /// <summary>
    /// Invalidates the Autostart Monitor's power-up byte so that the next <see cref="Reset()"/>
    /// takes the <em>cold</em>-start path.
    ///
    /// The ROM's reset handler compares $03F4 against the complement of the soft-entry vector's
    /// high byte: a match means "already initialised", and it warm-starts straight back into
    /// BASIC. Only the cold path scans the peripheral slots for a bootable card, so booting a
    /// disk on a machine that has already reached the BASIC prompt requires breaking that match
    /// first — which is exactly what pressing the real machine's power switch does.
    /// </summary>
    public void InvalidatePowerUpByte()
    {
        Mem[0x03F4] = (byte)(Mem[0x03F3] ^ 0xA5 ^ 0xFF);
    }

    bool ISystemState.IsSystemReady() => HasBasicStarted();
}
