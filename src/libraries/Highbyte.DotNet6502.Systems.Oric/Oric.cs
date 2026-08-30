using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Oric.Audio;
using Highbyte.DotNet6502.Systems.Oric.Config;
using Highbyte.DotNet6502.Systems.Oric.Hardware;
using Highbyte.DotNet6502.Systems.Oric.Input;
using Highbyte.DotNet6502.Systems.Oric.Render;
using Highbyte.DotNet6502.Systems.Oric.Tape;
using Highbyte.DotNet6502.Systems.Oric.Utils;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Oric;

/// <summary>Oric Atmos 48K with a PAL ULA, MOS 6522 and AY-3-8912.</summary>
public sealed class Oric : ISystem, ITextMode, IScreen, ISystemState
{
    public const string SystemName = "Oric";
    public const ushort ViaStartAddress = 0x0300;
    public const ushort ViaEndAddress = 0x03ff;
    public const ushort SystemRomStartAddress = 0xc000;
    public const int SystemRomSize = 0x4000;
    public const ushort BasicProgramDefaultStartAddress = 0x0501;
    public const ushort BasicProgramStartPointerAddress = 0x009a;
    public const ushort BasicProgramEndPointerAddress = 0x009c;
    public const ushort BasicArrayStartPointerAddress = 0x009e;
    public const ushort BasicFreeMemoryStartPointerAddress = 0x00a0;
    public const ushort KeyboardCharacterLatchAddress = 0x02df;
    private const string ViaIrqSource = "Oric VIA";
    private const ushort AtmosBasic11bTapeGetSyncAddress = 0xe735;
    private const ushort AtmosBasic11bTapeGetSyncReturnAddress = 0xe759;
    private const ushort AtmosBasic11bTapeReadByteAddress = 0xe6c9;
    private const ushort AtmosBasic11bTapeReadByteReturnAddress = 0xe6fb;
    private const ushort AtmosTapeReadByteValueAddress = 0x002f;

    private readonly byte[] _ram = new byte[Memory.MAX_MEMORY_SIZE];
    private readonly bool _hasSystemRom;
    private IRenderProvider? _renderProvider;
    private IAudioProvider? _audioProvider;
    private bool _ayBusCa2;
    private bool _ayBusCb2;

    public Oric() : this(new OricConfig(), new NullLoggerFactory()) { }

    public Oric(OricConfig config, ILoggerFactory loggerFactory, Dictionary<string, byte[]>? romData = null)
    {
        Keyboard = new OricKeyboard();
        Joystick = new OricJoystick(config);
        InputInjector = new OricInputInjector(this);
        TextPaste = new OricTextPaste(this, loggerFactory);
        BasicTokenParser = new OricBasicTokenParser(this, loggerFactory);
        Tape = new OricTape();
        Ay = new Ay38912();
        CPU = new CPU(loggerFactory, CpuModelIds.Nmos6502, config.CpuCompatibilityProfile);

        Via = new Via6522(
            readPortAInput: ReadViaPortAInput,
            writePortAOutput: _ => UpdateAyBus(),
            readPortBInput: ReadViaPortBInput,
            writePortBOutput: _ => { },
            writeCa2: value => { _ayBusCa2 = value; UpdateAyBus(); },
            writeCb2: value => { _ayBusCb2 = value; UpdateAyBus(); },
            irqChanged: UpdateViaIrq);

        Mem = new Memory(mapToDefaultRAM: false);
        Mem.MapRAM(0x0000, _ram);

        byte[]? systemRom = null;
        if (romData?.TryGetValue(OricSystemConfig.SystemRomName, out var suppliedRom) == true)
        {
            if (suppliedRom.Length != SystemRomSize)
                throw new DotNet6502Exception($"Oric system ROM must be exactly {SystemRomSize} bytes, but was {suppliedRom.Length} bytes.");
            systemRom = suppliedRom;
            Mem.MapROM(SystemRomStartAddress, systemRom);
        }
        _hasSystemRom = systemRom != null;

        Via.Map(Mem, ViaStartAddress, ViaEndAddress);
        DefaultExecOptions = new ExecOptions();

        if (_hasSystemRom)
            CPU.Reset(Mem);

        var rasterizer = new OricRasterizer(this);
        RenderProviders.Add(rasterizer);
        RenderProviders.Add(new OricVideoCommandStream(this));
        SetCurrentRenderProviderType(typeof(OricRasterizer));

        if (config.AudioEnabled)
        {
            AudioProviders.Add(new OricAySampleProvider(this));
            SetCurrentAudioProviderType(config.AudioProviderType ?? typeof(OricAySampleProvider));
        }

        InputConsumer = new OricInputHandler(this);
    }

    public string Name => SystemName;
    public List<string> SystemInfo => ["Oric Atmos", "48 KB RAM", "PAL"];
    public List<KeyValuePair<string, Func<string>>> DebugInfo =>
    [
        new("VIA IFR", () => $"${Via.InterruptFlags:x2}"),
        new("AY register", () => Ay.SelectedRegister.ToString()),
        new("Tape", () => Tape.IsInserted ? $"{Tape.Position}/{Tape.Length}" : "Ejected"),
    ];

    public CPU CPU { get; }
    public Memory Mem { get; }
    public IScreen Screen => this;
    public ExecOptions DefaultExecOptions { get; set; }

    public Via6522 Via { get; }
    public Ay38912 Ay { get; }
    public OricKeyboard Keyboard { get; }
    public OricJoystick Joystick { get; }
    public OricInputInjector InputInjector { get; }
    IInputInjector? ISystem.InputInjector => InputInjector;
    public OricTextPaste TextPaste { get; }
    public OricBasicTokenParser BasicTokenParser { get; }
    public OricTape Tape { get; }
    public bool HasSystemRom => _hasSystemRom;

    public int TextCols => OricConfig.Columns;
    public int TextRows => OricConfig.VisibleHeight / OricConfig.CharacterHeight;
    public int CharacterWidth => OricConfig.CharacterWidth;
    public int CharacterHeight => OricConfig.CharacterHeight;

    public int DrawableAreaWidth => OricConfig.VisibleWidth;
    public int DrawableAreaHeight => OricConfig.VisibleHeight;
    public int VisibleWidth => OricConfig.VisibleWidth;
    public int VisibleHeight => OricConfig.VisibleHeight;
    public bool HasBorder => false;
    public int VisibleLeftRightBorderWidth => 0;
    public int VisibleTopBottomBorderHeight => 0;
    public float RefreshFrequencyHz => OricConfig.ScreenRefreshFrequencyHz;
    public ulong CPUCyclesPerFrame => OricConfig.CpuCyclesPerFrame;
    public double CpuFrequencyHz => OricConfig.CpuFrequencyHz;

    public bool InstrumentationEnabled { get; set; }
    public Instrumentations Instrumentations { get; } = new();
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = [];
    public IAudioProvider? AudioProvider => _audioProvider;
    public List<IAudioProvider> AudioProviders { get; } = [];
    public IInputConsumer? InputConsumer { get; set; }

    public void SetCurrentRenderProviderType(Type? renderProviderType)
    {
        _renderProvider = renderProviderType is null
            ? null
            : RenderProviders.SingleOrDefault(provider => provider.GetType() == renderProviderType)
              ?? throw new ArgumentException($"Render provider type not found: {renderProviderType.FullName}");
    }

    public void SetCurrentAudioProviderType(Type? audioProviderType)
    {
        _audioProvider = audioProviderType is null
            ? null
            : AudioProviders.SingleOrDefault(provider => provider.GetType() == audioProviderType)
              ?? throw new ArgumentException($"Audio provider type not found: {audioProviderType.FullName}");
    }

    public ExecEvaluatorTriggerResult ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
    {
        ulong totalCycles = 0;
        while (totalCycles < CPUCyclesPerFrame)
        {
            var result = ExecuteOneInstruction(out var instruction, execEvaluator);
            totalCycles += instruction.CyclesConsumed;
            if (result.Triggered)
                return result;
            if (CPU.IsHalted)
                break;
        }

        TextPaste.InsertNextCharacterToLatch();
        _renderProvider?.OnEndFrame();
        _audioProvider?.OnEndFrame();
        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null)
    {
        if (execEvaluator != null)
        {
            var opcode = Mem[CPU.PC];
            var preExecution = CPU.IsOpCodeDefined(opcode)
                ? InstructionExecResult.KnownInstructionResult(opcode, CPU.PC, 0)
                : InstructionExecResult.UnknownInstructionResult(opcode, CPU.PC);
            var preCheck = execEvaluator.Check(preExecution, CPU, Mem);
            if (preCheck.Triggered)
            {
                instructionExecResult = preExecution;
                return preCheck;
            }
        }

        ApplyTapeRomHook();
        instructionExecResult = CPU.ExecuteOneInstruction(Mem).LastInstructionExecResult;
        Via.ProcessCycles((int)instructionExecResult.CyclesConsumed);
        _renderProvider?.OnAfterInstruction();
        _audioProvider?.OnAfterInstruction();
        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public void Reset(ushort? cpuStartPos = null)
    {
        Keyboard.Reset();
        Joystick.ClearJoystickActions();
        InputInjector.Clear();
        TextPaste.Reset();
        Mem[KeyboardCharacterLatchAddress] = 0;
        Ay.Reset();
        Via.Reset();
        if (_renderProvider is OricRasterizer rasterizer)
            rasterizer.Reset();
        else if (_renderProvider is OricVideoCommandStream commandStream)
            commandStream.Reset();
        if (cpuStartPos.HasValue)
            CPU.PC = cpuStartPos.Value;
        else if (_hasSystemRom)
            CPU.Reset(Mem);
    }

    public bool IsSystemReady()
    {
        ReadOnlySpan<byte> ready = "READY"u8;
        for (var address = (int)OricRasterizer.TextScreenAddress;
             address <= 0xbfe0 - ready.Length;
             address++)
        {
            var match = true;
            for (var offset = 0; offset < ready.Length; offset++)
            {
                var value = (byte)(Mem[(ushort)(address + offset)] & 0x7f);
                if (value >= 'a' && value <= 'z')
                    value -= 0x20;
                if (value != ready[offset])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return true;
        }
        return false;
    }

    public ushort GetBasicProgramStartAddress() => Mem.FetchWord(BasicProgramStartPointerAddress);

    /// <summary>Returns the exclusive end address of the tokenized BASIC program.</summary>
    public ushort GetBasicProgramEndAddress() => Mem.FetchWord(BasicProgramEndPointerAddress);

    /// <summary>
    /// Inserts and rewinds a byte-level TAP image. Standard Atmos ROM tape routines consume the
    /// bytes in order, allowing a program to issue additional <c>CLOAD</c> commands for later files.
    /// </summary>
    public IReadOnlyList<OricTapFile> InsertTape(byte[] tapData, string? sourceName = null)
        => Tape.Insert(tapData, sourceName);

    public void EjectTape() => Tape.Eject();

    public void RewindTape() => Tape.Rewind();

    public bool SeekToPreviousTapeRecord() => Tape.SeekToPreviousRecord();

    public bool SeekToNextTapeRecord() => Tape.SeekToNextRecord();

    /// <summary>
    /// Directly loads one file from a TAP image without running the cassette routines. This is a
    /// host convenience for simple BASIC and machine-code images; multi-stage tapes should instead
    /// be inserted and loaded through the Atmos ROM.
    /// </summary>
    public OricTapFile LoadTap(byte[] tapData, int fileIndex = 0, bool honorAutoRun = true)
    {
        var files = OricTapParser.ParseAll(tapData);
        if ((uint)fileIndex >= (uint)files.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileIndex),
                $"The Oric TAP image contains {files.Count} file(s), so file index {fileIndex} cannot be loaded.");
        }

        var tapFile = files[fileIndex];
        LoadTapFile(tapFile, honorAutoRun);
        return tapFile;
    }

    /// <summary>
    /// Loads the first BASIC file from an Oric byte-level tape image directly into RAM and
    /// initialises the Extended BASIC memory pointers. Cassette signal emulation is not involved.
    /// </summary>
    public OricTapFile LoadBasicTap(byte[] tapData)
    {
        var tapFile = OricTapParser.Parse(tapData);
        if (!tapFile.IsBasic)
        {
            throw new InvalidDataException(
                $"The Oric TAP file '{tapFile.Name}' is not a BASIC program (type ${tapFile.FileType:X2}).");
        }
        LoadTapFile(tapFile, honorAutoRun: false);
        return tapFile;
    }

    /// <summary>Directly loads a parsed BASIC or machine-code tape file into Oric memory.</summary>
    public void LoadTapFile(OricTapFile tapFile, bool honorAutoRun = true)
    {
        ArgumentNullException.ThrowIfNull(tapFile);

        var expectedLength = tapFile.EndAddress - tapFile.StartAddress + 1;
        if (tapFile.Data.Length != expectedLength)
            throw new InvalidDataException("The Oric TAP payload length does not match its address range.");
        if (tapFile.EndAddress >= SystemRomStartAddress)
            throw new InvalidDataException("The Oric TAP payload overlaps the system ROM.");
        if (!tapFile.IsBasic && !tapFile.IsMachineCode)
        {
            throw new InvalidDataException(
                $"The Oric TAP file '{tapFile.Name}' has unsupported type ${tapFile.FileType:X2}.");
        }
        if (tapFile.IsBasic && tapFile.StartAddress != BasicProgramDefaultStartAddress)
        {
            throw new InvalidDataException(
                $"The Oric BASIC program must load at ${BasicProgramDefaultStartAddress:X4}, not ${tapFile.StartAddress:X4}.");
        }

        Mem.StoreData(tapFile.StartAddress, tapFile.Data);
        if (tapFile.IsBasic)
            InitBasicMemoryVariables(tapFile.StartAddress, tapFile.EndAddress);

        if (!honorAutoRun || !tapFile.IsAutoRun)
            return;

        if (tapFile.IsBasic)
            TextPaste.Paste("RUN\n");
        else
            CPU.PC = tapFile.StartAddress;
    }

    /// <summary>
    /// Initialises Extended BASIC after a tokenized program has been placed directly in RAM.
    /// The end, array, and free-memory pointers use BASIC's exclusive program end address. An OSDK
    /// tape payload includes a trailing byte at that address, matching the state established by
    /// the Atmos ROM loader.
    /// </summary>
    public void InitBasicMemoryVariables(ushort loadedAtAddress, ushort programEndAddress)
    {
        if (loadedAtAddress == 0 ||
            programEndAddress <= loadedAtAddress ||
            programEndAddress >= SystemRomStartAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(programEndAddress), "The BASIC program does not fit in Oric RAM.");
        }

        Mem[(ushort)(loadedAtAddress - 1)] = 0;
        Mem.WriteWord(BasicProgramStartPointerAddress, loadedAtAddress);
        Mem.WriteWord(BasicProgramEndPointerAddress, programEndAddress);
        Mem.WriteWord(BasicArrayStartPointerAddress, programEndAddress);
        Mem.WriteWord(BasicFreeMemoryStartPointerAddress, programEndAddress);
    }

    /// <summary>
    /// Bridges the supported Atmos 1.1b ROM's byte-level tape routines to the virtual TAP stream.
    /// Software with custom pulse loaders still requires cassette-signal emulation.
    /// </summary>
    private void ApplyTapeRomHook()
    {
        if (!_hasSystemRom || !Tape.IsInserted)
            return;

        if (CPU.PC == AtmosBasic11bTapeGetSyncAddress)
        {
            if (Tape.SeekToNextSyncByte())
            {
                // Match the state produced by the ROM routine after it has read its three-byte
                // leader. In particular X must be zero: the standard loader stores it as the
                // initial tape-error count after finding the header marker.
                CPU.A = OricTapParser.SyncByte;
                CPU.X = 0;
                CPU.ProcessorStatus.Zero = true;
                CPU.ProcessorStatus.Negative = false;
                CPU.ProcessorStatus.Carry = true;
                CPU.PC = AtmosBasic11bTapeGetSyncReturnAddress;
            }
            return;
        }

        if (CPU.PC != AtmosBasic11bTapeReadByteAddress || !Tape.TryReadByte(out var value))
            return;

        CPU.A = value;
        CPU.ProcessorStatus.Zero = value == 0;
        CPU.ProcessorStatus.Negative = (value & 0x80) != 0;
        // E6C9 returns carry clear after a successfully decoded byte. Fast loaders such as The
        // Hobbit branch to their TAPE ERROR path when carry is set.
        CPU.ProcessorStatus.Carry = false;
        Mem[AtmosTapeReadByteValueAddress] = value;
        CPU.PC = AtmosBasic11bTapeReadByteReturnAddress;
    }

    private byte ReadViaPortAInput()
    {
        var input = _ayBusCa2 && !_ayBusCb2 ? Ay.ReadData() : (byte)0xff;
        return (byte)(input & Joystick.ReadPortAInput(
            Via.PortAOutput,
            Via.DataDirectionA,
            Via.PortBOutput,
            Via.DataDirectionB));
    }

    private byte ReadViaPortBInput()
    {
        var input = (byte)0xff;
        var selectedRow = Via.PortBOutput & 0x07;
        if (!Keyboard.IsSenseHigh(selectedRow, Ay.PortAOutput))
            input &= 0xf7;
        return input;
    }

    private void UpdateAyBus()
    {
        if (_ayBusCa2)
        {
            if (_ayBusCb2)
                Ay.SelectRegister(Via.PortAOutput);
        }
        else if (_ayBusCb2)
        {
            Ay.WriteData(Via.PortAOutput);
        }
    }

    private void UpdateViaIrq(bool active)
    {
        if (active)
            CPU.CPUInterrupts.SetIRQSourceActive(ViaIrqSource, autoAcknowledge: false);
        else
            CPU.CPUInterrupts.SetIRQSourceInactive(ViaIrqSource);
    }
}
