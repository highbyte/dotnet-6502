using Highbyte.DotNet6502.Systems;
using SadConsole.Host;
using static SadConsole.IFont;
using Console = SadConsole.Console;

namespace Highbyte.DotNet6502.Impl.SadConsole;
public class EmulatorConsole : Console
{
    private const bool USE_CONSOLE_BORDER = true;

    private EmulatorConsole(int width, int height) : base(width, height)
    {
    }

    public static EmulatorConsole Create(ISystem system, IFont font, Sizes fontSize, ShapeParameters consoleDrawBoxBorderParameters)
    {
        var screen = system.Screen;
        var textMode = (ITextMode)system.Screen;

        int totalCols = textMode.TextCols + (USE_CONSOLE_BORDER ? 2 : 0);
        int totalRows = textMode.TextRows + (USE_CONSOLE_BORDER ? 2 : 0);
        if (screen.HasBorder)
        {
            totalCols += (screen.VisibleLeftRightBorderWidth / textMode.CharacterWidth) * 2;
            totalRows += (screen.VisibleTopBottomBorderHeight / textMode.CharacterHeight) * 2;
        }

        var console = new EmulatorConsole(totalCols, totalRows);

        console.Font = font;
        console.FontSize = console.Font.GetFontSize(fontSize);

        console.Surface.DefaultForeground = Color.White;
        console.Surface.DefaultBackground = Color.Black;
        console.Clear();

        console.UseMouse = false;
        console.UseKeyboard = true;

        if (USE_CONSOLE_BORDER)
            console.Surface.DrawBox(new Rectangle(0, 0, console.Width, console.Height), consoleDrawBoxBorderParameters);
        return console;
    }

    public void SetGlyph(int x, int y, int sadConsoleCharCode, Color fgColor, Color bgColor)
    {
        CellSurfaceEditor.SetGlyph(this, x + (USE_CONSOLE_BORDER ? 1 : 0), y + (USE_CONSOLE_BORDER ? 1 : 0), sadConsoleCharCode, fgColor, bgColor);
    }
}
