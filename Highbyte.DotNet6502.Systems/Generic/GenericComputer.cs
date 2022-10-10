using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Systems.Generic
{
    public class GenericComputer : ISystem, ITextMode, IScreen
    {
        // How many 6502 CPU cycles this generic (fictional) computer should be able to execute per frame.
        // This should be adjusted to the performance of the machine the emulator is running on.
        // For comparison, a C64 runs about 16700 cycles per frame (1/60 sec).
        // TODO: Should probably move to config instead of hardcoded constant.
        public const int CYCLES_PER_FRAME = 40000;

        public string Name => "Generic";
        public string SystemInfo => "";

        public Memory Mem { get; set; }
        public CPU CPU { get; set; }
        public ExecOptions DefaultExecOptions { get; set; }

        public int Cols => _emulatorScreenConfig.Cols;
        public int Rows => _emulatorScreenConfig.Rows;
        public int CharacterWidth => 8;
        public int CharacterHeight => 8;

        public int Width => Cols * CharacterWidth;
        public int Height => Rows * CharacterHeight;
        public int VisibleWidth => (Cols * CharacterWidth) + (2 * (_emulatorScreenConfig.BorderCols * CharacterWidth));
        public int VisibleHeight => (Rows * CharacterHeight) + (2 * (_emulatorScreenConfig.BorderRows * CharacterHeight));
        public bool HasBorder => (VisibleWidth > Width) || (VisibleHeight > Height);
        public int BorderWidth => (VisibleWidth - Width) / 2;
        public int BorderHeight => (VisibleHeight - Height) / 2;

        private readonly EmulatorScreenConfig _emulatorScreenConfig;

        public GenericComputer() : this(new EmulatorScreenConfig()) { }
        public GenericComputer(EmulatorScreenConfig emulatorScreenConfig)
        {
            _emulatorScreenConfig = emulatorScreenConfig;
            Mem = new Memory();
            CPU = new CPU();
            DefaultExecOptions = new ExecOptions();
        }

        public bool ExecuteOneFrame()
        {
            // Execute a number of instructions
            // TODO: The number of instructions per vblank should be configurable
            var execState = CPU.Execute(
                Mem,
                new ExecOptions
                {
                    CyclesRequested = CYCLES_PER_FRAME,
                });
            // If an unhandled instruction, return false
            if (!execState.LastOpCodeWasHandled)
                return false;

            // Tell CPU 6502 code that one frame worth of CPU cycles has been executed
            SetFrameCompleted();

            // Wait for CPU 6502 code has acknowledged that it knows a frame has completed.
            bool waitOk = WaitFrameCompletedAcknowledged();
            if (!waitOk)
                return false;

            // Return true to continue running
            return true;
        }

        private void SetFrameCompleted()
        {
            Mem.SetBit(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame);
        }

        private bool WaitFrameCompletedAcknowledged()
        {
            // Keep on executing instructions until CPU 6502 code has cleared bit 0 in ScreenRefreshStatusAddress
            while (Mem.IsBitSet(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame))
            {
                var execState = CPU.Execute(
                    Mem,
                    new ExecOptions
                    {
                        MaxNumberOfInstructions = 1
                    });
                // If an unhandled instruction, return false
                if (!execState.LastOpCodeWasHandled)
                    return false;
            }
            return true;
        }

        public bool ExecuteOneInstruction()
        {
            var execState = CPU.Execute(
                Mem,
                new ExecOptions
                {
                    MaxNumberOfInstructions = 1,
                });
            if (!execState.LastOpCodeWasHandled)
                return false;

            return true;
        }

        public GenericComputer Clone()
        {
            return new GenericComputer(this._emulatorScreenConfig)
            {
                CPU = this.CPU.Clone(),
                Mem = this.Mem.Clone(),
                DefaultExecOptions = this.DefaultExecOptions.Clone()
            };
        }

        public void Reset(ushort? cpuStartPos = null)
        {
            // TODO: Leave memory intact after reset?

            if(cpuStartPos==null)
                cpuStartPos = Mem.FetchWord(CPU.ResetVector);

            CPU.PC = cpuStartPos.Value;
        }

        public ExecState Run()
        {
            return CPU.Execute(
                this.Mem,
                this.DefaultExecOptions
                );
        }

        public ExecState Run(ExecOptions execOptions)
        {
            return CPU.Execute(
                this.Mem,
                execOptions
                );
        }
    }
}