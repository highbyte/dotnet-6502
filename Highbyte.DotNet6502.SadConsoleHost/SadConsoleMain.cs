using System;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.SadConsoleHost;
using SadConsole;
namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class SadConsoleMain
    {
        private readonly SadConsoleConfig _sadConsoleConfig;
        private SadConsoleScreenObject _sadConsoleScreen;
        public SadConsoleScreenObject SadConsoleScreen => _sadConsoleScreen;

        private readonly SystemRunner _systemRunner;
        private readonly int _updateEmulatorEveryXFrame;
        private int _frameCounter;

        public SadConsoleMain(
            SadConsoleConfig sadConsoleConfig,
            SystemRunner systemRunner,
            int updateEmulatorEveryXFrame = 0)
        {
            _sadConsoleConfig = sadConsoleConfig;
            _systemRunner = systemRunner;
            _updateEmulatorEveryXFrame = updateEmulatorEveryXFrame;
        }

        public void Run()
        {
            Settings.WindowTitle = _sadConsoleConfig.WindowTitle;

            // Setup the SadConsole engine and create the main window. 
            // If font is null or empty, the default SadConsole font will be used.
            var textMode = _systemRunner.System as ITextMode;

            SadConsole.Game.Create(
                (textMode.Cols + (textMode.BorderCols * 2)) * _sadConsoleConfig.FontScale,
                (textMode.Rows + (textMode.BorderRows * 2)) * _sadConsoleConfig.FontScale,
                _sadConsoleConfig.Font
                );

            //SadConsole.Game.Instance.DefaultFontSize = IFont.Sizes.One;

            // Hook the start event so we can add consoles to the system.
            SadConsole.Game.Instance.OnStart = InitSadConsole;

            // Hook the update event that happens each frame
            SadConsole.Game.Instance.FrameUpdate += UpdateSadConsole;

            // Hook the "after render"
            //SadConsole.Game.OnDraw = Screen.DrawFrame;

            // Start the game.
            SadConsole.Game.Instance.Run();
            SadConsole.Game.Instance.Dispose();
        }

        /// <summary>
        /// Runs when SadConsole engine starts up
        /// </summary>
        private void InitSadConsole()
        {
            // Create a SadConsole screen
            var textMode = _systemRunner.System as ITextMode;
            _sadConsoleScreen = new SadConsoleScreenObject(textMode, _sadConsoleConfig);

            SadConsole.Game.Instance.Screen = _sadConsoleScreen;
            SadConsole.Game.Instance.DestroyDefaultStartingConsole();

            // Start with focus on main console on current screen
            _sadConsoleScreen.IsFocused = true;
            _sadConsoleScreen.ScreenConsole.IsFocused = true;
        }

        /// <summary>
        /// Runs every frame.
        /// Responsible for letting the SadConsole engine interact with the emulator
        /// </summary>
        /// <param name="gameTime"></param>
        private void UpdateSadConsole(object sender, GameHost e)
        {
            _frameCounter++;
            if (_frameCounter >= _updateEmulatorEveryXFrame)
            {
                // Run emulator for one frame
                bool shouldContinue = _systemRunner.RunOneFrame();
                if (!shouldContinue)
                {
                    // Exit program
                    Environment.Exit(0);
                }
                _frameCounter = 0;
            }
        }
    }
}