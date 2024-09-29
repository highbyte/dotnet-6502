using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems.Commodore64.Audio;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Monitor;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
using Highbyte.DotNet6502.Utils;
using Highbyte.DotNet6502.Systems.Commodore64.Utils;
using System.Text;

namespace Highbyte.DotNet6502.Systems.Commodore64;

public class C64 : ISystem, ISystemMonitor
{
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
    public Cia Cia { get; set; } = default!;
    public Sid Sid { get; set; } = default!;
    public Dictionary<string, byte[]> ROMData { get; set; } = default!;

    public bool AudioEnabled { get; private set; }
    public TimerMode TimerMode { get; private set; }

    public string ColorMapName { get; private set; } = default!;

    private readonly C64MonitorCommands _c64MonitorCommands = new C64MonitorCommands();
    private readonly ILogger _logger;
    public const ushort BASIC_LOAD_ADDRESS = 0x0801;

    private Action<InstructionExecResult>? _postInstructionAudioCallback = null;
    private Action<InstructionExecResult>? _postInstructionVideoCallback = null;

    // Instrumentations
    public bool InstrumentationEnabled { get; set; }
    public Instrumentations Instrumentations { get; } = new();
    private const string StatsCategory = "Custom";
    private readonly ElapsedMillisecondsTimedStatSystem _spriteCollisionStat;
    private readonly ElapsedMillisecondsTimedStatSystem _postInstructionAudioCallbackStat;
    private readonly ElapsedMillisecondsTimedStatSystem _postInstructionVideoCallbackStat;

    public bool RememberVic2RegistersPerRasterLine { get; set; } = true;

    public C64BasicTokenParser BasicTokenParser { get; private set; }
    public C64TextPaste TextPaste { get; private set; }

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
    /// <param name="postInstructionCallback"></param>
    /// <param name="detailedStats"></param>
    /// <returns></returns>
    public ExecEvaluatorTriggerResult ExecuteOneFrame(
        SystemRunner systemRunner,
        IExecEvaluator? execEvaluator = null)
    {
        _postInstructionAudioCallbackStat.Reset(); // Reset stat, will be continiously updated after each instruction
        _postInstructionVideoCallbackStat.Reset(); // Reset stat, will be continiously updated after each instruction

        ulong cyclesToExecute = (Vic2.Vic2Model.CyclesPerFrame - Vic2.CyclesConsumedCurrentVblank);
        //_logger.LogTrace($"Executing one frame, {cyclesToExecute} CPU cycles.");

        ulong totalCyclesConsumed = 0;
        while (totalCyclesConsumed < cyclesToExecute)
        {
            ExecEvaluatorTriggerResult execEvaluatorTriggerResult = ExecuteOneInstruction(systemRunner, out InstructionExecResult instructionExecResult, execEvaluator);
            totalCyclesConsumed += instructionExecResult.CyclesConsumed;

            if (execEvaluatorTriggerResult.Triggered)
            {
                return execEvaluatorTriggerResult;
            }
        }

        _postInstructionAudioCallbackStat.Stop(); // Stop stat (was continiously updated after each instruction)
        _postInstructionVideoCallbackStat.Stop(); // Stop stat (was continiously updated after each instruction)

        // Check if any text should be pasted to the keyboard buffer (pasted text set by host system, and each character insterted to the C64 keyboard buffer one character per frame)
        TextPaste.InsertNextCharacterToKeyboardBuffer();

        // Update sprite collision state
        _spriteCollisionStat.Start();
        Vic2.SpriteManager.SetCollitionDetectionStatesAndIRQ();
        _spriteCollisionStat.Stop();

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
        SystemRunner systemRunner,
        out InstructionExecResult instructionExecResult,
        IExecEvaluator? execEvaluator = null)
    {
        // Execute one CPU instruction
        instructionExecResult = CPU.ExecuteOneInstructionMinimal(Mem);

        // Update CIA timers
        if (TimerMode == TimerMode.UpdateEachInstruction)
            Cia.ProcessTimers(instructionExecResult.CyclesConsumed);

        // Advance video raster
        var cycleOnRasterLineBeforeInstruction = Vic2.CyclesConsumedCurrentVblank;
        Vic2.AdvanceRaster(instructionExecResult.CyclesConsumed);

        // Handle audio processing after each instruction.
        if (AudioEnabled && _postInstructionAudioCallback != null)
        {
            _postInstructionAudioCallbackStat.Start(cont: true);
            _postInstructionAudioCallback.Invoke(instructionExecResult);
            _postInstructionAudioCallbackStat.Stop(cont: true);
        }

        // Handle video processing after each instruction.
        if (_postInstructionVideoCallback != null)
        {
            _postInstructionVideoCallbackStat.Start(cont: true);
            _postInstructionVideoCallback.Invoke(instructionExecResult);
            _postInstructionVideoCallbackStat.Stop(cont: true);
        }

        // Check for debugger breakpoints (or other possible IExecEvaluator implementations used).
        if (execEvaluator != null)
        {
            var execEvaluatorTriggerResult = execEvaluator.Check(instructionExecResult, CPU, Mem);
            if (execEvaluatorTriggerResult.Triggered)
            {
                return execEvaluatorTriggerResult;
            }
        }
        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    private C64(ILogger logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _spriteCollisionStat = Instrumentations.Add($"{StatsCategory}-SpriteCollision", new ElapsedMillisecondsTimedStatSystem(this));
        _postInstructionAudioCallbackStat = Instrumentations.Add($"{StatsCategory}-AudioPostInstrCallback", new ElapsedMillisecondsTimedStatSystem(this));
        _postInstructionVideoCallbackStat = Instrumentations.Add($"{StatsCategory}-VideoPostInstrCallback", new ElapsedMillisecondsTimedStatSystem(this));

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

        var logger = loggerFactory.CreateLogger<C64>();
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

        var cpu = CreateC64CPU(loggerFactory);
        var vic2 = Vic2.BuildVic2(vic2Model, c64);
        var sid = Sid.BuildSid(c64);

        var cia = new Cia(c64, c64Config, loggerFactory);

        c64.CPU = cpu;
        c64.Vic2 = vic2;
        c64.Cia = cia;
        c64.Sid = sid;

        var mem = c64.CreateC64Memory(ram, io, romData);
        c64.Mem = mem;

        c64.BasicTokenParser = new C64BasicTokenParser(c64, loggerFactory);
        c64.TextPaste = new C64TextPaste(c64, loggerFactory);

        // Configure the current memory configuration on startup
        SetStartupBank(c64);

        // Set program counter on startup to the address specified at the 6502 reset vector.
        c64.CPU.Reset(c64.Mem);

        logger.LogInformation("C64 created.");
        return c64;
    }

    private void MapLocationsOnCurrentCPUBank(Memory mem, bool mapIO)
    {
        // Address 0x01: IO Port. Controls bank switching and Cassette control
        mem.MapReader(0x01, IoPortLoad);
        mem.MapWriter(0x01, IoPortStore);

        if (mapIO)
        {
            // Map IO addresses at d000
            Vic2.MapIOLocations(mem);
            Cia.MapIOLocations(mem);
            Sid.MapIOLocations(mem);
        }
    }

    private static void SetStartupBank(C64 c64)
    {
        var mem = c64.Mem;

        // Initialize to bank 31
        mem.SetMemoryConfiguration(31);
        // GAME and EXROM on to start (cartridge). These "values" are not read/stored from a actual memory location, just 2 bits of data that the CPU uses together with the first 3 bits of 0x01 to determine which memory configuration to use
        c64.CurrentBank = 0x18;
        // HIMEM, LOMEM, CHAREN on to start
        mem.Write(1, 0x7);
    }

    private static CPU CreateC64CPU(ILoggerFactory loggerFactory)
    {
        var cpu = new CPU(loggerFactory);
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
            // TODO: Cartridge low + high mapping
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 15 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xa000, basic);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge low mapping
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
            // TODO: Cartridge low mapping
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 7 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge low + high mapping
            MapLocationsOnCurrentCPUBank(mem, mapIO: true);
        }
        foreach (var bank in new int[] { 6 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge high mapping
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
            // TODO: Cartridge low + high mapping
            MapLocationsOnCurrentCPUBank(mem, mapIO: false);
        }
        foreach (var bank in new int[] { 2 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram, preWriteIntercept: RamPreWriteIntercept);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge high mapping
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

    private void IoPortStore(ushort _, byte value)
    {
        var bank = CurrentBank;
        bank.ClearBits(0x07);            // Clear the first 3 bits
        bank |= (byte)(value & 0x07);    // Replace the first 3 bits with new ones
        CurrentBank = bank;
        Mem.SetMemoryConfiguration(CurrentBank);
    }

    private byte IoPortLoad(ushort _)
    {
        // For now, only the the first 3 bits which is the current bank
        return (byte)(CurrentBank & 0x07);
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

    private List<KeyValuePair<string, Func<string>>> BuildDebugInfo()
    {
        List<KeyValuePair<string, Func<string>>> debugInfoList = [
            new ("Cursor col,row", () => $"{Mem[0xd3].ToString()},{Mem[0xd6].ToString()}"),
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
            new ("Current screen line", () =>
            {
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

    public void SetPostInstructionVideoCallback(Action<InstructionExecResult> callback)
    {
        _postInstructionVideoCallback = callback;
    }

    public void SetPostInstructionAudioCallback(Action<InstructionExecResult> callback)
    {
        _postInstructionAudioCallback = callback;
    }

}
