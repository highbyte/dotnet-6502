using System.Text;
using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Audio.Sample;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge;
using Highbyte.DotNet6502.Systems.Commodore64.Cartridge.SwiftLink;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Monitor;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomGeneral;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Render.VideoCommands;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.IEC;
using Highbyte.DotNet6502.Systems.Commodore64.Utils;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Utils;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64;

public class C64 : ISystem, ISystemMonitor, ISystemState, ISystemCleanup
{
    private const byte CpuPortBankBitsMask = 0x07;
    private const byte CpuPortDataDirectionResetValue = 0x2F;
    private const byte CpuPortDataResetValue = 0x37;
    private const byte CpuPortInputPullupMask = 0x17;

    public const string SystemName = "C64";
    public string Name => SystemName;
    public List<string> SystemInfo => BuildSystemInfo();
    public List<KeyValuePair<string, Func<string>>> DebugInfo { get; private set; }
    public C64ModelBase Model { get; private set; } = default!;

    public float CpuFrequencyHz => Model.CPUFrequencyHz;
    public CPU CPU { get; set; } = default!;
    public Memory Mem { get; set; } = default!;
    public IScreen Screen => Vic2.Vic2Screen!;

    public byte[] RAM { get; set; } = default!;
    public byte[] IO { get; set; } = default!;
    public byte CurrentBank { get; set; }
    public Vic2 Vic2 { get; set; } = default!;
    public Cia1 Cia1 { get; set; } = default!;
    public Cia2 Cia2 { get; set; } = default!;
    public Sid Sid { get; set; } = default!;
    public IECBus IECBus { get; set; } = default!;
    public Dictionary<string, byte[]> ROMData { get; set; } = default!;
    public C64CartridgeSlot CartridgeSlot { get; } = new();

    public bool AudioEnabled { get; private set; }
    public TimerMode TimerMode { get; private set; }

    public string ColorMapName { get; private set; } = default!;

    private readonly C64MonitorCommands _c64MonitorCommands = new C64MonitorCommands();
    private readonly ILogger _logger;
    public const ushort BASIC_LOAD_ADDRESS = 0x0801;

    private IRenderProvider? _renderProvider;
    public IRenderProvider? RenderProvider => _renderProvider;
    public List<IRenderProvider> RenderProviders { get; } = new();

    private IAudioProvider? _audioProvider;
    public IAudioProvider? AudioProvider => _audioProvider;
    public List<IAudioProvider> AudioProviders { get; } = new();

    /// <summary>
    /// The C64 input consumer (set by the host configurer in BuildSystemRunner). Reads host input
    /// through the neutral <see cref="IHostInputState"/> and applies it to CIA1.
    /// </summary>
    public IInputConsumer? InputConsumer { get; set; }

    // Instrumentations
    public bool InstrumentationEnabled { get; set; }
    public Instrumentations Instrumentations { get; } = new();
    private const string StatsCategory = "Custom";
    private readonly ElapsedMillisecondsTimedStatSystem _spriteCollisionStat;

    private const string StatsCategoryAudioProvider = "AudioProvider";
    private readonly ElapsedMillisecondsTimedStatSystem _audioProviderPerInstructionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _audioProviderPerFrameStat;

    private const string StatsCategoryRenderProvider = "RenderProvider";
    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerInstructionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _renderProviderPerFrameStat;

    public bool RememberVic2RegistersPerRasterLine { get; set; } = true;

    public C64BasicTokenParser BasicTokenParser { get; private set; } = default!;
    public C64TextPaste TextPaste { get; private set; } = default!;
    public C64InputInjector? InputInjector { get; private set; }
    IInputInjector? ISystem.InputInjector => InputInjector;

    private byte _cpuPortDataDirectionRegister = CpuPortDataDirectionResetValue;
    private byte _cpuPortDataRegister = CpuPortDataResetValue;
    //public static ROM[] ROMS = new ROM[]
    //{   
    //    // name, file, checksum 
    //    ROM.NewROM(C64Config.BASIC_ROM_NAME,   "basic",   "79015323128650c742a3694c9429aa91f355905e"),
    //    ROM.NewROM(C64Config.CHARGEN_ROM_NAME, "chargen", "adc7c31e18c7c7413d54802ef2f4193da14711aa"),
    //    ROM.NewROM(C64Config.KERNAL_ROM_NAME,  "kernal",  "1d503e56df85a62fee696e7618dc5b4e781df1bb"),
    //};

    // Faster CPU execution, don't uses all the customization with statistics and execution events as "old" pipeline used.

    /// <summary>
    /// Executes on frame worth of C64 instructions.
    /// Uses the optimized CPU instruction execution (ExecuteOneInstructionMinimal).
    /// </summary>
    /// <param name="execEvaluator"></param>
    /// <returns></returns>
    public ExecEvaluatorTriggerResult ExecuteOneFrame(
        IExecEvaluator? execEvaluator = null)
    {
        _audioProviderPerInstructionStat.Reset(); // Reset stat, will be continuously updated after each instruction
        _renderProviderPerInstructionStat.Reset(); // Reset stat, will be continuously updated after each instruction

        ulong cyclesToExecute = (Vic2.Vic2Model.CyclesPerFrame - Vic2.CyclesConsumedCurrentVblank);
        //_logger.LogTrace($"Executing one frame, {cyclesToExecute} CPU cycles.");

        ulong totalCyclesConsumed = 0;
        while (totalCyclesConsumed < cyclesToExecute)
        {
            ExecEvaluatorTriggerResult execEvaluatorTriggerResult = ExecuteOneInstruction(out InstructionExecResult instructionExecResult, execEvaluator);
            totalCyclesConsumed += instructionExecResult.CyclesConsumed;

            if (execEvaluatorTriggerResult.Triggered)
            {
                return execEvaluatorTriggerResult;
            }
        }

        _audioProviderPerInstructionStat.Stop(); // Stop stat (was continuously updated after each instruction)
        _renderProviderPerInstructionStat.Stop(); // Stop stat (was continuously updated after each instruction)

        // Check if any text should be pasted to the keyboard buffer (pasted text set by host system, and each character insterted to the C64 keyboard buffer one character per frame)
        TextPaste.InsertNextCharacterToKeyboardBuffer();

        // Update sprite collision state
        _spriteCollisionStat.Start();
        Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();
        _spriteCollisionStat.Stop();

        // Audio generation at end of frame
        if (_audioProvider != null)
        {
            _audioProviderPerFrameStat.Start();
            _audioProvider.OnEndFrame();
            _audioProviderPerFrameStat.Stop();
        }

        // New render pipeline
        _renderProviderPerFrameStat.Start();
        _renderProvider?.OnEndFrame();
        _renderProviderPerFrameStat.Stop();

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    /// <summary>
    /// Executes on instruction, and all the processing needed after each instruction.
    /// </summary>
    /// <param name="systemRunner"></param>
    /// <param name="instructionExecResult"></param>
    /// <param name="detailedStats"></param>
    /// <param name="execEvaluator"></param>
    /// <returns></returns>
    public ExecEvaluatorTriggerResult ExecuteOneInstruction(
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null)
    {
        // Check BEFORE executing the instruction so that breakpoints trigger at the
        // correct address (i.e. before the instruction at that address runs).
        // Build a pre-execution result with the opcode at the current PC so evaluators
        // that check for BRK or unknown instructions work correctly pre-execution.
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

        // Execute one CPU instruction
        instructionExecResult = CPU.ExecuteOneInstructionMinimal(Mem);

        // Update CIA timers
        if (TimerMode == TimerMode.UpdateEachInstruction)
        {
            Cia1.ProcessTimers(instructionExecResult.CyclesConsumed);
            Cia2.ProcessTimers(instructionExecResult.CyclesConsumed);
        }

        // Update IEC bus devices
        IECBus.TickDevices();
        CartridgeSlot.Tick();

        // General emulator timing fix: devices tick after the CPU instruction has already
        // completed, so newly raised hardware IRQ/NMI lines must be serviced here to land
        // on the next instruction boundary instead of one instruction late.
        CPU.ProcessPendingInterrupts(Mem);

        // Advance video raster
        var cycleOnRasterLineBeforeInstruction = Vic2.CyclesConsumedCurrentVblank;
        Vic2.AdvanceRaster(instructionExecResult.CyclesConsumed);

        // Audio generation after each instruction (SID register writes happen between instructions).
        // _audioProvider is only set when audio is enabled (see ConfigureAudio).
        if (_audioProvider != null)
        {
            _audioProviderPerInstructionStat.Start(cont: true);
            _audioProvider.OnAfterInstruction();
            _audioProviderPerInstructionStat.Stop(cont: true);
        }

        // New render pipeline
        _renderProviderPerInstructionStat.Start(cont: true);
        _renderProvider?.OnAfterInstruction();
        _renderProviderPerInstructionStat.Stop(cont: true);

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    private C64(ILogger logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _spriteCollisionStat = Instrumentations.Add($"{StatsCategory}-SpriteCollision", new ElapsedMillisecondsTimedStatSystem(this));

        _audioProviderPerInstructionStat = Instrumentations.Add($"{StatsCategoryAudioProvider}-Instruction", new ElapsedMillisecondsTimedStatSystem(this));
        _audioProviderPerFrameStat = Instrumentations.Add($"{StatsCategoryAudioProvider}-Frame", new ElapsedMillisecondsTimedStatSystem(this));

        _renderProviderPerInstructionStat = Instrumentations.Add($"{StatsCategoryRenderProvider}-Instruction", new ElapsedMillisecondsTimedStatSystem(this));
        _renderProviderPerFrameStat = Instrumentations.Add($"{StatsCategoryRenderProvider}-Frame", new ElapsedMillisecondsTimedStatSystem(this));

        DebugInfo = BuildDebugInfo();
    }

    public static C64 BuildC64(
        C64Config c64Config,
        ILoggerFactory loggerFactory
        )
    {
        var c64Model = C64ModelInventory.C64Models[c64Config.C64Model];

        var ram = new byte[64 * 1024];  // C64 has 64KB of RAM
        Dictionary<string, byte[]> romData;
        if (c64Config.LoadROMs)
        {
            romData = ROM.LoadROMS(c64Config.ROMDirectory, c64Config.ROMs.ToArray());
        }
        else
        {
            // For unit testing, use empty ROMs
            romData = new Dictionary<string, byte[]>
            {
                {C64SystemConfig.KERNAL_ROM_NAME, new byte[8192] },
                {C64SystemConfig.BASIC_ROM_NAME, new byte[8192] },
                {C64SystemConfig.CHARGEN_ROM_NAME, new byte[4096] }
            };
        }

        var io = new byte[4 * 1024];  // 4KB of C64 IO addresses that is mapped to memory address range 0xd000 - 0xdfff in certain memory configurations.

        var vic2Model = c64Model.Vic2Models.Single(x => x.Name == c64Config.Vic2Model);

        var logger = loggerFactory.CreateLogger(nameof(C64));
        var c64 = new C64(logger, loggerFactory)
        {
            Model = c64Model,
            RAM = ram,
            IO = io,
            ROMData = romData,
            AudioEnabled = c64Config.AudioEnabled,
            TimerMode = c64Config.TimerMode,
            ColorMapName = c64Config.ColorMapName,
            InstrumentationEnabled = c64Config.InstrumentationEnabled
        };

        var cpu = CreateC64CPU(loggerFactory, c64Config.CpuCompatibilityProfile);
        var vic2 = Vic2.BuildVic2(vic2Model, c64);
        var sid = Sid.BuildSid(c64);

        var cia1 = new Cia1(c64, c64Config, loggerFactory);
        var cia2 = new Cia2(c64, loggerFactory);

        var iecHost = new IECHost();
        var iecBus = new IECBus(iecHost);
        var diskDrive1541 = new DiskDrive1541(loggerFactory);
        iecBus.Attach(diskDrive1541);

        SwiftLinkDevice? swiftLink = null;
        if (c64Config.SwiftLink.Enabled)
        {
            swiftLink = new SwiftLinkDevice(
                c64Config.SwiftLink.CartridgeIOAddress,
                loggerFactory.CreateLogger(nameof(SwiftLinkDevice)))
            {
                CpuInterrupts = cpu.CPUInterrupts,
                InterruptMode = c64Config.SwiftLink.InterruptMode,
                ReceiveMode = c64Config.SwiftLink.ReceiveMode,
                GetCurrentCycleCount = () => cpu.ExecState.CyclesConsumed,
            };
            c64.AttachCartridge(swiftLink);
        }

        c64.CPU = cpu;
        c64.Vic2 = vic2;
        c64.Cia1 = cia1;
        c64.Cia2 = cia2;
        c64.Sid = sid;
        c64.IECBus = iecBus;

        var mem = c64.CreateC64Memory(ram, io, romData);
        c64.Mem = mem;
        if (swiftLink != null)
        {
            // SwiftLink-specific compatibility hook: some modem software temporarily banks
            // out the mapped NMI vector area. SwiftLink can consult this callback and defer
            // asserting its NMI source until the currently mapped vector is usable again.
            swiftLink.CanDeliverNmi =
                () => c64.Mem.FetchWord(CPU.NonMaskableIRQHandlerVector) != 0;
        }

        c64.BasicTokenParser = new C64BasicTokenParser(c64, loggerFactory);
        c64.TextPaste = new C64TextPaste(c64, loggerFactory);
        c64.InputInjector = new C64InputInjector(c64);

        // Configure the current memory configuration on startup
        SetStartupBank(c64);

        ConfigureRenderer(c64, c64Config);
        ConfigureAudio(c64, c64Config);

        // Set program counter on startup to the address specified at the 6502 reset vector.
        c64.CPU.Reset(c64.Mem);

        logger.LogInformation("C64 created.");
        return c64;
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

    private static void ConfigureRenderer(C64 c64, C64Config config)
    {
        c64.RenderProviders.Add(new Vic2Rasterizer(c64));
        c64.RenderProviders.Add(new C64CustomRenderProvider(c64));
        c64.RenderProviders.Add(new C64GpuProvider(c64, useFineScrollPerRasterLine: true));
        c64.RenderProviders.Add(new C64VideoCommandStream(c64));

        c64.SetCurrentRenderProvider(config.RenderProviderType);
    }

    private static void ConfigureAudio(C64 c64, C64Config config)
    {
        // When audio is disabled, no providers are created — the host then builds no audio
        // coordinator and the system stays silent.
        if (!config.AudioEnabled)
            return;

        c64.AudioProviders.Add(new C64SidCommandStream(c64));
        c64.AudioProviders.Add(new C64SidSampleProvider(c64, sidClockHz: (int)c64.CpuFrequencyHz, mode: config.SidEmulationMode));

        // Default to the command-stream provider if the host didn't pick one.
        var selectedType = config.AudioProviderType ?? typeof(C64SidCommandStream);
        c64.SetCurrentAudioProvider(selectedType);
    }

    private void SetCurrentAudioProvider(Type? audioProviderType)
    {
        if (audioProviderType == null)
        {
            _audioProvider = null;
            return;
        }
        var audioProvider = AudioProviders.SingleOrDefault(ap => ap.GetType() == audioProviderType)
            ?? throw new ArgumentException($"The specified audio provider type {audioProviderType.FullName} is not available.");
        _audioProvider = audioProvider;
    }

    private void MapLocationsOnCurrentCPUBank(Memory mem, bool mapIO)
    {
        // Address 0x00: 6510 CPU data direction register.
        mem.MapReader(0x00, IoPortDirectionLoad);
        mem.MapWriter(0x00, IoPortDirectionStore);
        // Address 0x01: 6510 CPU data register. Controls bank switching and cassette signals.
        mem.MapReader(0x01, IoPortLoad);
        mem.MapWriter(0x01, IoPortStore);

        if (mapIO)
        {
            // Map IO addresses starting at d000
            Vic2.MapIOLocations(mem);
            Cia1.MapIOLocations(mem);
            Cia2.MapIOLocations(mem);
            Sid.MapIOLocations(mem);
            CartridgeSlot.MapIOLocations(mem);
        }
    }

    private static void SetStartupBank(C64 c64)
    {
        var mem = c64.Mem;

        // Preserve the cartridge control bits (GAME/EXROM) while restoring the 6510
        // processor port to the C64 startup defaults.
        c64.CurrentBank = 0x18;
        c64._cpuPortDataDirectionRegister = CpuPortDataDirectionResetValue;
        c64._cpuPortDataRegister = CpuPortDataResetValue;
        c64.ApplyCpuPortMemoryConfiguration();
    }

    private static CPU CreateC64CPU(ILoggerFactory loggerFactory, CpuCompatibilityProfile compatibilityProfile)
    {
        var cpu = new CPU(loggerFactory, compatibilityProfile);
        // The CPU execute method uses will not raise any events (like after instruction executed). Therefore advance VIC2 raster line etc needs to be manually called instead (see ExecuteOneFrame)
        //cpu.InstructionExecuted += (s, e) => vic2.AdvanceRaster(e.InstructionExecState.CyclesConsumed);
        return cpu;
    }

    private Memory CreateC64Memory(byte[] ram, byte[] io, Dictionary<string, byte[]> roms)
    {
        var basic = roms[C64SystemConfig.BASIC_ROM_NAME];
        var chargen = roms[C64SystemConfig.CHARGEN_ROM_NAME];
        var kernal = roms[C64SystemConfig.KERNAL_ROM_NAME];

        var mem = new Memory(numberOfConfigurations: 32, mapToDefaultRAM: false);

        mem.SetMemoryConfiguration(31);
        mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
        mem.MapROM(0xa000, basic);
        mem.MapRAM(0xd000, io);
        mem.MapROM(0xe000, kernal);
        MapLocationsOnCurrentCPUBank(mem, mapIO: true);

        foreach (var bank in new int[] { 30, 14 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 29, 13 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 28, 24 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 27 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xa000, basic);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 26, 10 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 25, 9 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xd000, chargen);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }

        foreach (var bank in new int[] { 23, 22, 21, 20, 19, 18, 17, 16 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            CartridgeSlot.MapROMLLocations(mem);
            CartridgeSlot.MapROMHLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 15 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xa000, basic);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            CartridgeSlot.MapROMLLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 12, 8, 4, 0 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 11 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xa000, basic);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            CartridgeSlot.MapROMLLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 7 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            CartridgeSlot.MapROMLLocations(mem);
            CartridgeSlot.MapROMHLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 6 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            CartridgeSlot.MapROMHLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 5 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 3 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            CartridgeSlot.MapROMLLocations(mem);
            CartridgeSlot.MapROMHLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 2 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            CartridgeSlot.MapROMHLocations(mem);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 1 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }

        return mem;
    }

    private bool RamPreWriteIntercept(ushort address, byte value)
    {
        Vic2.InspectVic2MemoryValueUpdateFromCPU(address, value);
        return true;
    }

    private void IoPortDirectionStore(ushort _, byte value)
    {
        _cpuPortDataDirectionRegister = value;
        ApplyCpuPortMemoryConfiguration();
    }

    private byte IoPortDirectionLoad(ushort _)
    {
        return _cpuPortDataDirectionRegister;
    }

    private void IoPortStore(ushort _, byte value)
    {
        _cpuPortDataRegister = value;
        ApplyCpuPortMemoryConfiguration();
    }

    private byte IoPortLoad(ushort _)
    {
        return GetCpuPortEffectiveValue();
    }

    private void ApplyCpuPortMemoryConfiguration()
    {
        var bank = CurrentBank;
        bank.ClearBits(CpuPortBankBitsMask);
        bank |= (byte)(GetCpuPortEffectiveValue() & CpuPortBankBitsMask);
        CurrentBank = bank;
        Mem.SetMemoryConfiguration(CurrentBank);
    }

    private byte GetCpuPortEffectiveValue()
    {
        // On the C64 the lower 3 banking lines and cassette sense line are pulled high
        // when configured as inputs. Compunet relies on INC/DEC $01 read-modify-write
        // preserving those pull-up semantics.
        var inputBits = (byte)(CpuPortInputPullupMask & ~_cpuPortDataDirectionRegister);
        var outputBits = (byte)(_cpuPortDataRegister & _cpuPortDataDirectionRegister);
        var upperBits = (byte)(_cpuPortDataRegister & 0b1100_0000);
        return (byte)(upperBits | outputBits | inputBits);
    }

    /// <summary>
    /// Writes byte to IO Storage, with address specified as location C64 memory map, and translated to VIC2 IO Storage address (-0xd000).
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    public void WriteIOStorage(ushort address, byte value)
    {
        IO[(ushort)(address - 0xd000)] = value;
    }
    /// <summary>
    /// Read byte to IO Storage, with address specified as location C64 memory map, and translated to VIC2 IO Storage address (-0xd000).
    /// </summary>
    /// <param name="address"></param>
    public byte ReadIOStorage(ushort address)
    {
        return IO[(ushort)(address - 0xd000)];
    }

    private List<string> BuildSystemInfo()
    {
        var row1 = $"Line: {Vic2.CurrentRasterLine} VblankCY: {Vic2.CyclesConsumedCurrentVblank} CPU bank: {CurrentBank} VIC2 bank: {Vic2.CurrentVIC2Bank}";
        var row2 = $"Model: {Model.Name} Freq: {Model.CPUFrequencyHz} VIC2 Model: {Vic2.Vic2Model.Name}";
        return new List<string>() { row1, row2 };
    }

    public void Cleanup()
    {
        CartridgeSlot.Dispose();
    }

    public void AttachCartridge(IC64Cartridge cartridge)
        => CartridgeSlot.Attach(cartridge);

    public void DetachCartridge()
        => CartridgeSlot.Detach();

    public void ResetAttachedCartridge()
        => CartridgeSlot.Reset();

    private List<KeyValuePair<string, Func<string>>> BuildDebugInfo()
    {
        List<KeyValuePair<string, Func<string>>> debugInfoList = [
            new ("Keyboard joystick enabled", () =>
            {
                return Cia1.Joystick.KeyboardJoystickEnabled.ToString();
            }),
            new ("Keyboard joystick #", () =>
            {
                return Cia1.Joystick.KeyboardJoystick.ToString();
            }),
            new ("Current Basic line #", () =>
            {
                // Address 0x39: Current BASIC line number.
                // Values:
                // $0000-$F9FF, 0-63999: Line number.
                // $FF00-$FFFF: Direct mode, no BASIC program is being executed.
                var currentBasicLineNumber = Mem.FetchWord(0x39);
                return currentBasicLineNumber switch
                {
                    < 0xf9ff => currentBasicLineNumber.ToString(),
                    >=0xff00 and <0xffff => "Direct mode",
                    _ => "Unknown"
                };
            }),
            new ("Cursor col,row", () =>
            {
                // Check that the are not in Basic run mode. If not, skip returning current screen line (only relevant when in direct mode).
                var currentBasicLineNumber = Mem.FetchWord(0x39);
                if (currentBasicLineNumber <0xf9ff)
                    return string.Empty;

                return $"{Mem[0xd3].ToString()},{Mem[0xd6].ToString()}";
            }),
            new ("Current screen line", () =>
            {
                // Check that the are not in Basic run mode. If not, skip returning current screen line (only relevant when in direct mode).
                var currentBasicLineNumber = Mem.FetchWord(0x39);
                if (currentBasicLineNumber <0xf9ff)
                    return string.Empty;

                // Address 0xd1/0xd2: Pointer to current line in screen memory.
                var screenMemLineStart = Mem.FetchWord(0xd1);
                var screenLineBytes = Mem.ReadData(screenMemLineStart, 40);
                var sb = new StringBuilder();
                for (var i = 0; i < screenLineBytes.Length; i++)
                {
                    var petsciiCode = Petscii.C64ScreenCodeToPetscII(screenLineBytes[i]);
                    var asciiCode = Petscii.PetscIIToAscII(petsciiCode);
                    sb.Append((char)asciiCode);
                }
                return sb.ToString();
            })
        ];

        return debugInfoList;
    }

    public ISystemMonitorCommands GetSystemMonitorCommands()
    {
        return _c64MonitorCommands;
    }

    /// <summary>
    /// Helper method to initialise the memory after a Basic program has been loaded to memory manually (outside of built-in C64 Kernal code).
    /// </summary>
    /// <param name="loadedAtAddress"></param>
    /// <param name="fileLength"></param>
    public void InitBasicMemoryVariables(ushort loadedAtAddress, int fileLength)
    {
        // The following memory locations are pointers to where Basic expects variables to be stored.
        // The address should be one byte after the Basic program end address after it's been loaded
        // VARTAB $002D-$002E   Pointer to the Start of the BASIC Variable Storage Area
        // ARYTAB $002F-$0030   Pointer to the Start of the BASIC Array Storage Area
        // STREND $0031-$0032   Pointer to End of the BASIC Array Storage Area (+1), and the Start of Free RAM
        // Ref: https://www.pagetable.com/c64ref/c64mem/
        ushort varStartAddress = (ushort)(loadedAtAddress + fileLength + 1);
        Mem.WriteWord(0x2d, varStartAddress);
        Mem.WriteWord(0x2f, varStartAddress);
        Mem.WriteWord(0x31, varStartAddress);
    }

    /// <summary>
    /// Returns the end address of the current Basic program in memory.
    /// </summary>
    /// <returns></returns>
    public ushort GetBasicProgramEndAddress()
    {
        return (ushort)(Mem.FetchWord(0x2d) - 1);
    }

    /// <summary>
    /// Checks if C64 BASIC has started and completed its initialization.
    /// This method examines the BASIC memory pointers to determine if BASIC has properly initialized.
    /// TXTAB pointer at 0x002B-0x002C should contain the start address of the BASIC program area (typically 0x0801).
    /// </summary>
    /// <returns>True if BASIC has started and initialized, false otherwise</returns>
    public bool HasBasicStarted()
    {
        // Check TXTAB pointer (0x002B-0x002C): Start of BASIC program text area
        // After BASIC initialization, this should point to 0x0801 (standard BASIC program start address)
        var txtabPointer = Mem.FetchWord(0x2B);

        // During startup, this pointer is 0x0000. After BASIC initialization, it's set to BASIC_LOAD_ADDRESS (0x0801)
        return txtabPointer == BASIC_LOAD_ADDRESS;
    }

    /// <inheritdoc/>
    bool ISystemState.IsSystemReady() => HasBasicStarted();
}
