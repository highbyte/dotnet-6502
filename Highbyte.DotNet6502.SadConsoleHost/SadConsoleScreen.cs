using SadConsole;
using SadRogue.Primitives;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    /// <summary>
    /// A SadConsole "container" console that will contain our screen where we render to
    /// </summary>
    public class SadConsoleScreen: ScreenObject
    {
        public Console ScreenConsole { get ;}

        public SadConsoleScreen(EmulatorScreenConfig emulatorScreenConfig, SadConsoleConfig sadConsoleConfig)
        {
            ScreenConsole = CreateScreenConsole(emulatorScreenConfig, sadConsoleConfig);
        }

        private Console CreateScreenConsole(EmulatorScreenConfig emulatorScreenConfig, SadConsoleConfig sadConsoleConfig)
        {
            // Setup screen
            var screen = new Console(emulatorScreenConfig.Cols + (emulatorScreenConfig.BorderCols * 2), emulatorScreenConfig.Rows + (emulatorScreenConfig.BorderRows * 2))
            {
                DefaultForeground = Color.White,
                DefaultBackground = Color.Black
            };
            //screen.Position = new Point(BorderWidth, BorderHeight);

            // TODO: Better way to map numeric scale value to SadConsole.Font.FontSizes enum?
            var fontSize = sadConsoleConfig.FontScale switch
            {
                1 => IFont.Sizes.One,
                2 => IFont.Sizes.Two,
                3 => IFont.Sizes.Three,
                _ => IFont.Sizes.One,
            };
            screen.FontSize = screen.Font.GetFontSize(fontSize);

            screen.Clear();
            screen.Cursor.IsEnabled = false;
            screen.Cursor.IsVisible = false;

            screen.Parent = this;            
            return screen;
        }
        
        public void DrawCharacter(int x, int y, int sadConsoleCharCode, Color fgColor, Color bgColor)
        {
            ScreenConsole.SetGlyph(x, y, sadConsoleCharCode, fgColor, bgColor);
        }
    }
}