using SadConsole;
namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class SadConsoleMain
    {
        private readonly SadConsoleConfig _sadConsoleConfig;
        private readonly EmulatorScreenConfig _emulatorScreenConfig;
        private readonly SadConsoleEmulatorLoop _sadConsoleEmulatorLoop;

        private SadConsoleScreen _sadConsoleScreen;
        public SadConsoleScreen SadConsoleScreen => _sadConsoleScreen;

        public SadConsoleMain(
            SadConsoleConfig sadConsoleConfig, 
            EmulatorScreenConfig emulatorScreenConfig,
            SadConsoleEmulatorLoop sadConsoleEmulatorLoop)
        {
            _sadConsoleConfig = sadConsoleConfig;
            _emulatorScreenConfig = emulatorScreenConfig;
            _sadConsoleEmulatorLoop = sadConsoleEmulatorLoop;
        }

        public void Run()
        {
            Settings.WindowTitle = _sadConsoleConfig.WindowTitle;

            // Setup the SadConsole engine and create the main window. 
            // If font is null or empty, the default SadConsole font will be used.
            SadConsole.Game.Create(
                (_emulatorScreenConfig.Cols + (_emulatorScreenConfig.BorderCols*2)) * _sadConsoleConfig.FontScale, 
                (_emulatorScreenConfig.Rows + (_emulatorScreenConfig.BorderRows*2)) * _sadConsoleConfig.FontScale,
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
        /// Runs every frame
        /// </summary>
        /// <param name="gameTime"></param>
        private void UpdateSadConsole(object sender, GameHost e)
        {
            _sadConsoleEmulatorLoop.SadConsoleUpdate(e);
        }

        /// <summary>
        /// Runs when SadConsole engine starts up
        /// </summary>
        private void InitSadConsole()
        {
            // Create a SadConsole screen
            _sadConsoleScreen = new SadConsoleScreen(_emulatorScreenConfig, _sadConsoleConfig);

            SadConsole.Game.Instance.Screen = _sadConsoleScreen;
            SadConsole.Game.Instance.DestroyDefaultStartingConsole();

            // Start with focus on main console on current screen
            _sadConsoleScreen.IsFocused = true;
            _sadConsoleScreen.ScreenConsole.IsFocused = true;
        }
    }
}