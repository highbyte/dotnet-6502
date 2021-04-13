namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class SadConsoleEmulatorExecutor
    {
        private readonly Computer _emulatorComputer;
        private readonly EmulatorScreenConfig _emulatorScreenConfig;
        private readonly bool _executeUntilBRKInstruction;

        public SadConsoleEmulatorExecutor(
            Computer emulatorComputer,
            EmulatorScreenConfig emulatorScreenConfig,
            bool executeUntilBRKInstruction = false)
        {
            _emulatorComputer = emulatorComputer;
            _emulatorScreenConfig = emulatorScreenConfig;
            _executeUntilBRKInstruction = executeUntilBRKInstruction;
        }

        public bool ExecuteEmulator()
        {
            // Set emulator Refresh bit
            // Emulator will wait until this bit is set until "redrawing" new data into memory
            _emulatorComputer.Mem.SetBit(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame);

            bool shouldExecuteEmulator = true;
            while(shouldExecuteEmulator)
            {
                // Execute a number of instructions
                // TODO: Is there a more optimal number of instructions to execute before we check if emulator code has flagged it's done via memory flag?
                var execState = _emulatorComputer.Run(
                    new ExecOptions
                    {
                        MaxNumberOfInstructions = 10,
                        ExecuteUntilInstruction = _executeUntilBRKInstruction?OpCodeId.BRK:null                        
                    });

                // Check if we should quit entirely
                if(_executeUntilBRKInstruction && execState.LastOpCode.HasValue && execState.LastOpCode == OpCodeId.BRK.ToByte())
                    return false;

                shouldExecuteEmulator = !_emulatorComputer.Mem.IsBitSet(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);
            }
            
            // Clear the flag that the emulator set to indicate it's done.
            _emulatorComputer.Mem.ClearBit(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);

            // Return true to continue running
            return true;
        }
    }
}