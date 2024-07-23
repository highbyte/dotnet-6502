using SadRogue.Primitives;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class SadConsoleUISettings
{
    public static Color UIConsoleBackgroundColor = new Color(5, 15, 45);
    public static Color UIConsoleForegroundColor = Color.White;

    public const bool UI_USE_CONSOLE_BORDER = true;
    public readonly static ColoredGlyph ConsoleBorderGlyph = new(new Color(90, 90, 90), UIConsoleBackgroundColor);
    public readonly static ShapeParameters ConsoleDrawBoxBorderParameters = new ShapeParameters(
            hasBorder: true,
            borderGlyph: ConsoleBorderGlyph,
            ignoreBorderForeground: false,
            ignoreBorderBackground: false,
            ignoreBorderGlyph: false,
            ignoreBorderMirror: false,
            hasFill: false,
            fillGlyph: null,
            ignoreFillForeground: false,
            ignoreFillBackground: false,
            ignoreFillGlyph: false,
            ignoreFillMirror: false,
            boxBorderStyle: ICellSurface.ConnectedLineThin,
            boxBorderStyleGlyphs: null);
}
