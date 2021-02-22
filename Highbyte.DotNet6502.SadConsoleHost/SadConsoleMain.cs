using Microsoft.Xna.Framework;

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
            // Setup the SadConsole engine and create the main window. 
            // If font is null or empty, the default SadConsole font will be used.
            SadConsole.Game.Create(_sadConsoleConfig.Font,
                (_emulatorScreenConfig.Cols + (_emulatorScreenConfig.BorderCols*2)) * _sadConsoleConfig.FontScale, 
                (_emulatorScreenConfig.Rows + (_emulatorScreenConfig.BorderRows*2)) * _sadConsoleConfig.FontScale);

            // Hook the start event so we can add consoles to the system.
            SadConsole.Game.OnInitialize = InitSadConsole;

            // Hook the update event that happens each frame
            SadConsole.Game.OnUpdate = UpdateSadConsole;

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
        private void UpdateSadConsole(GameTime gameTime)
        {
            _sadConsoleEmulatorLoop.SadConsoleUpdate(gameTime);
        }

        /// <summary>
        /// Runs when SadConsole engine starts up
        /// </summary>
        private void InitSadConsole()
        {
            // TODO: Better way to map numeric scale value to SadConsole.Font.FontSizes enum?
            var fontSize = _sadConsoleConfig.FontScale switch
            {
                1 => SadConsole.Font.FontSizes.One,
                2 => SadConsole.Font.FontSizes.Two,
                3 => SadConsole.Font.FontSizes.Three,
                _ => SadConsole.Font.FontSizes.One,
            };
            SadConsole.Global.FontDefault = SadConsole.Global.FontDefault.Master.GetFont(fontSize);

            // Create a SadConsole screen
            _sadConsoleScreen = new SadConsoleScreen(_emulatorScreenConfig);

            // Set SadConsole engine current screen to our screen
            SadConsole.Global.CurrentScreen = _sadConsoleScreen;

            // Start with focus on screen console
            SadConsole.Global.FocusedConsoles.Set(_sadConsoleScreen.ScreenConsole);

            // Set main window title
            SadConsole.Game.Instance.Window.Title = _sadConsoleConfig.WindowTitle;
        }
    }
}