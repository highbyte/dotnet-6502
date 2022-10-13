using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64
{
    public class C64 : ISystem, ITextMode, IScreen
    {
        public string Name => "Commodore 64";
        public string SystemInfo => BuildSystemInfo();
        public CPU CPU { get; set; }
        public Memory Mem { get; set; }
        public byte[] RAM { get; set; }
        public byte[] IO { get; set; }
        public byte CurrentBank { get; set; }
        public Vic2 Vic2 { get; set; }
        public Keyboard Keyboard { get; set; }

        public Dictionary<string, byte[]> ROMData { get; set; }

        public int Cols => Vic2.COLS;
        public int Rows => Vic2.ROWS;
        public int CharacterWidth => 8;
        public int CharacterHeight => 8;

        public int Width => Vic2.WIDTH;
        public int Height => Vic2.HEIGHT;
        public int VisibleWidth => Vic2.PAL_PIXELS_PER_LINE;
        public int VisibleHeight => Vic2.PAL_LINES_VISIBLE;
        public bool HasBorder => true;
        public int BorderWidth => (VisibleWidth - Width) / 2;
        public int BorderHeight => (VisibleHeight - Height) / 2;

        private LegacyExecEvaluator _oneFrameExecEvaluator = new LegacyExecEvaluator(new ExecOptions { CyclesRequested = Vic2.NTSC_NEW_CYCLES_PER_FRAME });

        public static ROM[] ROMS = new ROM[]
        {   
            // name, file, checksum 
            ROM.NewROM("basic",   "basic",   "79015323128650c742a3694c9429aa91f355905e"),
            ROM.NewROM("chargen", "chargen", "adc7c31e18c7c7413d54802ef2f4193da14711aa"),
            ROM.NewROM("kernal",  "kernal",  "1d503e56df85a62fee696e7618dc5b4e781df1bb"),
        };

        public bool ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
        {
            // TODO: The number of cycles per vblank should be configurable
            // If we already executed cycles in current frame, reduce it from total.
            _oneFrameExecEvaluator.ExecOptions.CyclesRequested = Vic2.NTSC_NEW_CYCLES_PER_FRAME - Vic2.CyclesConsumedCurrentVblank;

            // TODO: Create a pre-initialized array/list of two or one ExecEvaluator objects and reuse them to increase perf.
            List<IExecEvaluator> execEvaluators = new() { _oneFrameExecEvaluator };
            if (execEvaluator != null)
                execEvaluators.Add(execEvaluator);

            var execState = CPU.Execute(
                Mem,
                execEvaluators.ToArray());

            if (!execState.LastOpCodeWasHandled)
                return false;

            // If the custom ExecEvaluator said we shouldn't continue (for example a breakpoint), then indicate to caller that we shouldn't continue executing.
            if (execEvaluator != null && !execEvaluator.Continue)
                return false;

            // Return true to indicate execution was successfull and we should continue
            return true;
        }

        public bool ExecuteOneInstruction()
        {
            var execState = CPU.ExecuteOneInstruction(Mem);
            // If an unhandled instruction, return false
            if (!execState.LastOpCodeWasHandled)
                return false;
            // Return true to indicate execution was successfull
            return true;
        }

        private C64() { }

        public static C64 BuildC64()
        {
            var ram = new byte[64 * 1024];  // C64 has 64KB of RAM
            var romData = ROM.LoadROMS(
                Environment.ExpandEnvironmentVariables("%USERPROFILE%/Documents/C64/VICE/C64"), // TODO: ROM directory from config file
                 ROMS);
            var io = new byte[1 * 1024];  // 1KB of C64 IO addresses that is mapped to memory address range 0xd000 - 0xdfff in certain memory configuration.

            var mem = CreateC64Memory(ram, io, romData);

            var vic2 = Vic2.BuildVic2(ram, romData);
            var kb = new Keyboard();

            var cpu = CreateC64CPU(vic2, mem);
            var c64 = new C64
            {
                Mem = mem,
                CPU = cpu,
                RAM = ram,
                IO = io,
                Vic2 = vic2,
                Keyboard = kb,
                ROMData = romData
            };

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
            var kb = c64.Keyboard;

            for (int bank = 0; bank < 32; bank++)
            {
                mem.SetMemoryConfiguration(bank);

                // Address 0x01: IO Port. Controls bank switching and Cassette control
                mem.MapReader(0x01, c64.IoPortLoad);
                mem.MapWriter(0x01, c64.IoPortStore);


                // Address 0xd0180: "Memory setup" (VIC2 pointer for charset/bitmap & screen memory)
                mem.MapReader(Vic2Addr.MEMORY_SETUP, vic2.MemorySetupLoad);
                mem.MapWriter(Vic2Addr.MEMORY_SETUP, vic2.MemorySetupStore);

                // Address 0xd020: Border color
                mem.MapReader(Vic2Addr.BORDER_COLOR, vic2.BorderColorLoad);
                mem.MapWriter(Vic2Addr.BORDER_COLOR, vic2.BorderColorStore);
                // Address 0xd021: Background color
                mem.MapReader(Vic2Addr.BACKGROUND_COLOR, vic2.BackgroundColorLoad);
                mem.MapWriter(Vic2Addr.BACKGROUND_COLOR, vic2.BackgroundColorStore);

                // Address 0xdd00: "Port A" (VIC2 bank & serial bus)
                mem.MapReader(Vic2Addr.PORT_A, vic2.PortALoad);
                mem.MapWriter(Vic2Addr.PORT_A, vic2.PortAStore);


                // Address: 0x00c6: Keyboard buffer index
                mem.MapReader(0x00c6, kb.BufferIndexLoad);
                mem.MapWriter(0x00c6, kb.BufferIndexStore);
                // Address: 0x0277 - 0x0280: Keyboard buffer
                mem.MapRAM(0x0277, kb.Buffer);
                // Address: 0x0091: Stop key flag
                mem.MapReader(0x0091, kb.StopKeyFlagLoad);
                mem.MapWriter(0x0091, kb.StopKeyFlagStore);

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
            cpu.InstructionExecuted += (s, e) => vic2.CPUCyclesConsumed(e.CPU, e.Mem, e.InstructionExecState.CyclesConsumed);
            return cpu;
        }

        private static Memory CreateC64Memory(byte[] ram, byte[] io, Dictionary<string, byte[]> roms)
        {
            var basic = roms["basic"];
            var chargen = roms["chargen"];
            var kernal = roms["kernal"];

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
            return $"CPU bank: {CurrentBank} VIC2 bank: {Vic2.CurrentVIC2Bank} VblankCY: {Vic2.CyclesConsumedCurrentVblank}";
        }
    }
}