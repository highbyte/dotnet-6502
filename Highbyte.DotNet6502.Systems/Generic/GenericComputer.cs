using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Systems.Generic
{
    public class  GenericComputer : ISystem, ITextMode, IScreen
    {
        public const string SystemName = "Generic";
        public string Name => SystemName;
        public string SystemInfo => "";

        // How many 6502 CPU cycles this generic (fictional) computer should be able to execute per frame.
        // This should be adjusted to the performance of the machine the emulator is running on.
        // For comparison, a C64 runs about 16700 cycles per frame (1/60 sec).
        // TODO: Should probably move to config instead of hardcoded constant.
        public const int CYCLES_PER_FRAME = 40000;
        public ulong CyclesConsumedCurrentVblank { get; private set; } = 0;

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

        private LegacyExecEvaluator _oneFrameExecEvaluator = new LegacyExecEvaluator(new ExecOptions { CyclesRequested = CYCLES_PER_FRAME });

        public GenericComputer() : this(new EmulatorScreenConfig()) { }
        public GenericComputer(EmulatorScreenConfig emulatorScreenConfig)
        {
            _emulatorScreenConfig = emulatorScreenConfig;
            Mem = new Memory();
            CPU = new CPU();
            DefaultExecOptions = new ExecOptions();

            CPU.InstructionExecuted += (s, e) => CPUCyclesConsumed(e.CPU, e.Mem, e.InstructionExecState.CyclesConsumed);
        }

        public void Run(IExecEvaluator? execEvaluator = null)
        {
            if (execEvaluator == null)
                execEvaluator = new LegacyExecEvaluator(DefaultExecOptions);
            CPU.Execute(
                Mem,
                execEvaluator);
        }

        public bool ExecuteOneFrame(IExecEvaluator? execEvaluator = null)
        {
            // TODO: The number of cycles per vblank should be configurable
            // If we already executed cycles in current frame, reduce it from total.
            _oneFrameExecEvaluator.ExecOptions.CyclesRequested = CYCLES_PER_FRAME - CyclesConsumedCurrentVblank;

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

            // If an unhandled instruction, return false
            if (!execState.LastOpCodeWasHandled)
                return false;
            // If the custom ExecEvaluator said we shouldn't contine (for example a breakpoint), then indicate to caller that we shouldn't continue executing.
            if (execEvaluator != null && !execEvaluator.Continue)
                return false;

            // Tell CPU 6502 code that one frame worth of CPU cycles has been executed
            SetFrameCompleted();

            // Wait for CPU 6502 code has acknowledged that it knows a frame has completed.
            bool waitOk = WaitFrameCompletedAcknowledged();
            if (!waitOk)
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

        private void SetFrameCompleted()
        {
            Mem.SetBit(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame);
        }

        private bool WaitFrameCompletedAcknowledged()
        {
            // Keep on executing instructions until CPU 6502 code has cleared bit 0 in ScreenRefreshStatusAddress
            while (Mem.IsBitSet(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame))
            {
                var ok = ExecuteOneInstruction();
                // If an unhandled instruction, return false
                if (!ok)
                    return false;
            }
            return true;
        }

        public void CPUCyclesConsumed(CPU cpu, Memory mem, ulong cyclesConsumed)
        {
            CyclesConsumedCurrentVblank += cyclesConsumed;
            if (CyclesConsumedCurrentVblank >= CYCLES_PER_FRAME)
            {
                CyclesConsumedCurrentVblank = 0;
                VerticalBlank(cpu);
            }
        }

        public void VerticalBlank(CPU cpu)
        {
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
            if (cpuStartPos == null)
                CPU.Reset(Mem);
            else
                CPU.PC = cpuStartPos.Value;
        }
    }
}