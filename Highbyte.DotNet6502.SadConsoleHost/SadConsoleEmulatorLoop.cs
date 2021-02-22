using Microsoft.Xna.Framework;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class SadConsoleEmulatorLoop
    {
        private readonly SadConsoleEmulatorRenderer _renderer;
        private readonly SadConsoleEmulatorInput _input;
        private readonly SadConsoleEmulatorExecutor _executor;
        private readonly int _updateEmulatorEveryXFrame;

        int _frameCounter;

        public SadConsoleEmulatorLoop(
            SadConsoleEmulatorRenderer renderer, 
            SadConsoleEmulatorInput input,
            SadConsoleEmulatorExecutor executor,
            int updateEmulatorEveryXFrame = 0)
        {
            _renderer = renderer;
            _input = input;
            _executor = executor;
            _updateEmulatorEveryXFrame = updateEmulatorEveryXFrame;
            _frameCounter = 0;
        }

        /// <summary>
        /// This method is called once every frame, and is responsible for letting the SadConsole engine interact with the emulator
        /// </summary>
        /// <param name="gameTime"></param>    
        public void SadConsoleUpdate(GameTime gameTime)
        {
            _frameCounter++;
            if(_frameCounter >= _updateEmulatorEveryXFrame)
            {
                // --------------------------------------------------------------
                // Check for input on the host to be forwared to the emulator
                // --------------------------------------------------------------
                _input.CaptureInput();

                // --------------------------------------------------------------
                // Run code in Emulator until it's done for this frame
                // --------------------------------------------------------------
                _executor.ExecuteEmulator();

                // --------------------------------------------------------------
                // Render the data the emulator wrote to its screen memory
                // --------------------------------------------------------------
                _renderer.RenderEmulatorScreenMemory();

                _frameCounter = 0;
            }

        }
    }
}
