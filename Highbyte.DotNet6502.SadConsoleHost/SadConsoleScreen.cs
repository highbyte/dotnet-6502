using SadConsole;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    /// <summary>
    /// A SadConsole "container" console that will contain our screen where we render to
    /// </summary>
    public class SadConsoleScreen: ContainerConsole
    {
        public Console ScreenConsole { get ;}

        public SadConsoleScreen(EmulatorScreenConfig emulatorScreenConfig)
        {
            ScreenConsole = CreateScreenConsole(emulatorScreenConfig);
        }

        private Console CreateScreenConsole(EmulatorScreenConfig emulatorScreenConfig)
        {
            // Setup screen
            var screen = new Console(emulatorScreenConfig.Cols + (emulatorScreenConfig.BorderCols * 2), emulatorScreenConfig.Rows + (emulatorScreenConfig.BorderRows * 2))
            {
                DefaultForeground = Microsoft.Xna.Framework.Color.White,
                DefaultBackground = Microsoft.Xna.Framework.Color.Black
            };
            //screen.Position = new Point(BorderWidth, BorderHeight);

            screen.Clear();
            screen.Cursor.IsEnabled = false;
            screen.Cursor.IsVisible = false;

            screen.Parent = this;            
            return screen;
        }
        
        public void DrawCharacter(int x, int y, int sadConsoleCharCode, Microsoft.Xna.Framework.Color fgColor, Microsoft.Xna.Framework.Color bgColor)
        {
            ScreenConsole.SetGlyph(x, y, sadConsoleCharCode, fgColor, bgColor);
        }
    }
}