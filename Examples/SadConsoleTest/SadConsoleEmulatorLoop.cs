using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using Highbyte.DotNet6502;

namespace SadConsoleTest
{
    public class SadConsoleEmulatorLoop
    {
        private readonly SadConsoleEmulatorRenderer _renderer;
        private readonly Computer _emulatorComputer;
        private readonly EmulatorScreenConfig _emulatorScreenConfig;
        private readonly int _updateEmulatorEveryXFrame;

        int _frameCounter;

        public SadConsoleEmulatorLoop(
            SadConsoleEmulatorRenderer renderer, 
            Computer emulatorComputer,
            EmulatorScreenConfig emulatorScreenConfig,
            int updateEmulatorEveryXFrame = 0)
        {
            _renderer = renderer;
            _emulatorComputer = emulatorComputer;
            _emulatorScreenConfig = emulatorScreenConfig;
            _updateEmulatorEveryXFrame = updateEmulatorEveryXFrame;
            _frameCounter = 0;
        }

    
        public void SadConsoleUpdate(GameTime gameTime)
        {
            _frameCounter++;
            if(_frameCounter >= _updateEmulatorEveryXFrame)
            {
                // --------------------------------------------------------------
                // Run code in Emulator until it's done for this frame
                // --------------------------------------------------------------
                // Set emulator Refresh bit
                // Emulator will wait until this bit is set until "redrawing" new data into memory
                _emulatorComputer.Mem.SetBit(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.HostNewFrame);
                bool shouldExecuteEmulator = true;
                while(shouldExecuteEmulator)
                {
                    // Execute a number of instructions
                    // TODO: Is there a more optimal number of instructions to execute before we check if emulator code has flagged it's done via memory flag?
                    _emulatorComputer.Run(new ExecOptions{MaxNumberOfInstructions = 10});
                    shouldExecuteEmulator = !_emulatorComputer.Mem.IsBitSet(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);
                }
                
                // Clear the flag that the emulator set to indicate it's done.
                _emulatorComputer.Mem.ClearBit(_emulatorScreenConfig.ScreenRefreshStatusAddress, (int)ScreenStatusBitFlags.EmulatorDoneForFrame);

                // --------------------------------------------------------------
                // Render the data the emulator wrote to its screen memory
                // --------------------------------------------------------------
                _renderer.RenderEmulatorScreenMemory();

                _frameCounter = 0;
            }

        }
    }
}
