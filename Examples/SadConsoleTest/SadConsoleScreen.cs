using Microsoft.Xna.Framework;
using SadConsole;
using SadConsole.Effects;
using System.Collections.Generic;
using System.Linq;

namespace SadConsoleTest
{
    public class SadConsoleScreen: ContainerConsole
    {
        public readonly int BorderWidth;
        public readonly int BorderHeight;
        public readonly int ScreenWidth;
        public readonly int ScreenHeight;
        public readonly Microsoft.Xna.Framework.Color DefaultFgColor;
        public readonly Microsoft.Xna.Framework.Color DefaultBgColor;

        public Console ScreenConsole { get ;}
              
        public SadConsoleScreen(
            int screenWidth, 
            int screenHeight, 
            int borderWidth, 
            int borderHeight,
            Microsoft.Xna.Framework.Color defaultFgColor,
            Microsoft.Xna.Framework.Color defaultBgColor
            )
        {
            ScreenHeight = screenHeight;
            ScreenWidth = screenWidth;
            BorderHeight = borderHeight;
            BorderWidth = borderWidth;
            DefaultFgColor = defaultFgColor;
            DefaultBgColor = defaultBgColor;

            ScreenConsole = CreateScreenConsole();
        }

        private Console CreateScreenConsole()
        {
            // Setup screen
            // TODO: Implement border
            // var screen = new Console(ScreenWidth + (BorderWidth * 2), ScreenHeight + (BorderHeight * 2));
            // screen.Position = new Point(BorderWidth, BorderHeight);
            var screen = new Console(ScreenWidth, ScreenHeight);

            screen.DefaultForeground = DefaultFgColor;
            screen.DefaultBackground = DefaultBgColor;
            screen.Clear();
            screen.Cursor.IsEnabled = false;
            screen.Cursor.IsVisible = false;

            // TODO: Border around edges of map
            // screen.DrawBox(new Rectangle(0, 0, screen.Width - BorderWidth, screen.Height - BorderHeight)
            //                ,new Cell(SadConsoleRendererHelper.ColorToXNAColor(fgColor), SadConsoleRendererHelper.ColorToXNAColor(borderBgColor), 0));           

            screen.Parent = this;            
            return screen;
        }

        public void DrawCharacter(int x, int y, int sadConsoleCharCode, Microsoft.Xna.Framework.Color fgColor, Microsoft.Xna.Framework.Color bgColor)
        {
            ScreenConsole.SetGlyph(x, y, sadConsoleCharCode, fgColor, bgColor);
        }
    }
}
