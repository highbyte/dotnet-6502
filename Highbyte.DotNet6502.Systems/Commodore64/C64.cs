using Highbyte.DotNet6502.Monitor.SystemSpecific;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Keyboard;
using Highbyte.DotNet6502.Systems.Commodore64.Models;
using Highbyte.DotNet6502.Systems.Commodore64.Monitor;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64;

public class C64 : ISystem, ISystemMonitor
{
    public const string SystemName = "C64";
    public string Name => SystemName;
    public string SystemInfo => BuildSystemInfo();
    public C64ModelBase Model { get; private set; }

    public float CpuFrequencyHz => Model.CPUFrequencyHz;
    public CPU CPU { get; set; }
    public Memory Mem { get; set; }
    public IScreen Screen => Vic2.Vic2Screen;

    public byte[] RAM { get; set; }
    public byte[] IO { get; set; }
    public byte CurrentBank { get; set; }
    public Vic2 Vic2 { get; set; }
    public Cia Cia { get; set; }
    public C64Keyboard Keyboard { get; set; }
    public Sid Sid { get; set; }
    public Dictionary<string, byte[]> ROMData { get; set; }


    public bool AudioEnabled { get; private set; }
    public TimerMode TimerMode { get; private set; }

    private LegacyExecEvaluator _oneFrameExecEvaluator;

    private C64MonitorCommands _c64MonitorCommands = new C64MonitorCommands();

    //public static ROM[] ROMS = new ROM[]
    //{   
    //    // name, file, checksum 
    //    ROM.NewROM(C64Config.BASIC_ROM_NAME,   "basic",   "79015323128650c742a3694c9429aa91f355905e"),
    //    ROM.NewROM(C64Config.CHARGEN_ROM_NAME, "chargen", "adc7c31e18c7c7413d54802ef2f4193da14711aa"),
    //    ROM.NewROM(C64Config.KERNAL_ROM_NAME,  "kernal",  "1d503e56df85a62fee696e7618dc5b4e781df1bb"),
    //};

    // Faster CPU execution, don't uses all the customization with statistics and execution events as "old" pipeline used.
    public bool ExecuteOneFrame(
        IExecEvaluator? execEvaluator = null,
        Action<ISystem, Dictionary<string, double>>? postInstructionCallback = null,
        Dictionary<string, double>? detailedStats = null)
    {
        var cyclesToExecute = Vic2.Vic2Model.CyclesPerFrame - Vic2.CyclesConsumedCurrentVblank;

        ulong totalCyclesConsumed = 0;
        bool exitValue = true;
        while (totalCyclesConsumed < cyclesToExecute)
        {
            var knownInstruction = CPU.ExecuteOneInstructionMinimal(Mem, out var instructionCyclesConsumed);
            if (!knownInstruction)
            {
                exitValue = false;
                break;
            }

            totalCyclesConsumed += instructionCyclesConsumed;

            if (TimerMode == TimerMode.UpdateEachInstruction)
                Cia.ProcessTimers(instructionCyclesConsumed);

            // Process video raster
            Vic2.AdvanceRaster(instructionCyclesConsumed);

            // Handle processing needed after each instruction, such as generating audio etc.
            if (AudioEnabled && postInstructionCallback != null)
                postInstructionCallback(this, detailedStats);

            // Check for debugger breakpoints (or other possible IExecEvaluator implementations used).
            if (execEvaluator != null && !execEvaluator.Check(null, CPU, Mem))
            {
                exitValue = false;
                break;
            }
        }

        return exitValue;
    }


    // Slower CPU execution, with customization such as statistics and execution events.
    //public bool ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
    //{
    //    if (_oneFrameExecEvaluator == null)
    //        _oneFrameExecEvaluator = new LegacyExecEvaluator(new ExecOptions { CyclesRequested = Vic2.Vic2Model.CyclesPerFrame });

    //    // If we already executed cycles in current frame, reduce it from total.
    //    _oneFrameExecEvaluator.ExecOptions.CyclesRequested = Vic2.Vic2Model.CyclesPerFrame - Vic2.CyclesConsumedCurrentVblank;

    //    ExecState execState;
    //    if (execEvaluator == null)
    //    {
    //        execState = CPU.Execute(
    //            Mem,
    //            _oneFrameExecEvaluator);
    //    }
    //    else
    //    {
    //        execState = CPU.Execute(
    //            Mem,
    //            _oneFrameExecEvaluator,
    //            execEvaluator
    //            );
    //    }

    //    if (!execState.LastOpCodeWasHandled)
    //        return false;

    //    // If the custom ExecEvaluator said we shouldn't continue (for example a breakpoint), then indicate to caller that we shouldn't continue executing.
    //    if (execEvaluator != null && !execEvaluator.Check(null, CPU, Mem))
    //        return false;

    //    // Return true to indicate execution was successfull and we should continue
    //    return true;
    //}

    public bool ExecuteOneInstruction()
    {
        var knownInstruction = CPU.ExecuteOneInstructionMinimal(Mem, out ulong cyclesConsumed);
        return knownInstruction;

        //var execState = CPU.ExecuteOneInstruction(Mem);
        //// If an unhandled instruction, return false
        //if (!execState.LastOpCodeWasHandled)
        //    return false;
        //// Return true to indicate execution was successfull
        //return true;
    }

    private C64() { }

    public static C64 BuildC64(C64Config c64Config)
    {
        var c64Model = C64ModelInventory.C64Models[c64Config.C64Model];

        var ram = new byte[64 * 1024];  // C64 has 64KB of RAM

        var romData = ROM.LoadROMS(c64Config.ROMDirectory, c64Config.ROMs.ToArray());

        var io = new byte[1 * 1024];  // 1KB of C64 IO addresses that is mapped to memory address range 0xd000 - 0xdfff in certain memory configuration.

        var mem = CreateC64Memory(ram, io, romData);

        var vic2Model = c64Model.Vic2Models.Single(x => x.Name == c64Config.Vic2Model);
        var kb = new C64Keyboard();
        var sid = Sid.BuildSid();

        var c64 = new C64
        {
            Model = c64Model,
            Mem = mem,
            RAM = ram,
            IO = io,
            Keyboard = kb,
            Sid = sid,
            ROMData = romData,
            AudioEnabled = c64Config.AudioEnabled,
            TimerMode = c64Config.TimerMode,
        };
        var vic2 = Vic2.BuildVic2(ram, romData, vic2Model, c64);
        var cpu = CreateC64CPU(vic2, mem);
        c64.Vic2 = vic2;
        c64.CPU = cpu;

        c64.Cia = new Cia(c64);

        // Map specific memory addresses to different emulator actions            
        MapIOLocations(c64);

        // Configure the current memory configuration on startup
        SetStartupBank(c64);

        // Set program counter on startup to the address specified at the 6502 reset vector.
        c64.CPU.Reset(c64.Mem);

        return c64;
    }

    private static void MapIOLocations(C64 c64)
    {
        var mem = c64.Mem;
        var vic2 = c64.Vic2;
        var cia = c64.Cia;
        var kb = c64.Keyboard;
        var sid = c64.Sid;

        for (int bank = 0; bank < 32; bank++)
        {
            mem.SetMemoryConfiguration(bank);

            // Address 0x01: IO Port. Controls bank switching and Cassette control
            mem.MapReader(0x01, c64.IoPortLoad);
            mem.MapWriter(0x01, c64.IoPortStore);

            vic2.MapIOLocations(mem);
            cia.MapIOLocations(mem);
            kb.MapIOLocations(mem);
            sid.MapIOLocations(mem);
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

    private static CPU CreateC64CPU(Vic2 vic2, Memory mem)
    {
        var cpu = new CPU();
        // The CPU execute method uses will not raise any events (like after instruction executed). Therefore advance VIC2 raster line etc needs to be manually called instead (see ExecuteOneFrame)
        //cpu.InstructionExecuted += (s, e) => vic2.AdvanceRaster(e.InstructionExecState.CyclesConsumed);
        return cpu;
    }

    private static Memory CreateC64Memory(byte[] ram, byte[] io, Dictionary<string, byte[]> roms)
    {
        var basic = roms[C64Config.BASIC_ROM_NAME];
        var chargen = roms[C64Config.CHARGEN_ROM_NAME];
        var kernal = roms[C64Config.KERNAL_ROM_NAME];

        var mem = new Memory(numberOfConfigurations: 32, mapToDefaultRAM: false);

        mem.SetMemoryConfiguration(31);
        mem.MapRAM(0x0000, ram);
        mem.MapROM(0xa000, basic);
        mem.MapRAM(0xd000, io);
        mem.MapROM(0xe000, kernal);

        foreach (var bank in new int[] { 30, 14 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
        }
        foreach (var bank in new int[] { 29, 13 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapRAM(0xd000, io);
        }
        foreach (var bank in new int[] { 28, 24 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
        }
        foreach (var bank in new int[] { 27 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xa000, basic);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
        }
        foreach (var bank in new int[] { 26, 10 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
        }
        foreach (var bank in new int[] { 25, 9 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xd000, chargen);
        }

        foreach (var bank in new int[] { 23, 22, 21, 20, 19, 18, 17, 16 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapRAM(0xd000, io);
            // TODO: Cartridge low + high mapping
        }
        foreach (var bank in new int[] { 15 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xa000, basic);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge low mapping
        }
        foreach (var bank in new int[] { 12, 8, 4, 0 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
        }
        foreach (var bank in new int[] { 11 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xa000, basic);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge low mapping
        }
        foreach (var bank in new int[] { 7 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge low + high mapping
        }
        foreach (var bank in new int[] { 6 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapRAM(0xd000, io);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge high mapping
        }
        foreach (var bank in new int[] { 5 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapRAM(0xd000, io);
        }
        foreach (var bank in new int[] { 3 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge low + high mapping
        }
        foreach (var bank in new int[] { 2 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
            mem.MapROM(0xd000, chargen);
            mem.MapROM(0xe000, kernal);
            // TODO: Cartridge high mapping
        }
        foreach (var bank in new int[] { 1 })
        {
            mem.SetMemoryConfiguration(bank);
            mem.MapRAM(0x0000, ram);
        }
        return mem;
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

    private string BuildSystemInfo()
    {
        return $"Line: {Vic2.CurrentRasterLine} VblankCY: {Vic2.CyclesConsumedCurrentVblank} Model: {Model.Name} Freq: {Model.CPUFrequencyHz} CPU bank: {CurrentBank} VIC2 Model: {Vic2.Vic2Model.Name} VIC2 bank: {Vic2.CurrentVIC2Bank}";
    }

    public ISystemMonitorCommands GetSystemMonitorCommands()
    {
        return _c64MonitorCommands;
    }
}
