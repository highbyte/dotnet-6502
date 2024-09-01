using static SadConsole.IFont;

namespace Highbyte.DotNet6502.App.SadConsole;
public static class ConsolePositioningHelper
{

    public static float GetFontSizeScaleFactor(this IFont.Sizes fontSize) =>
        fontSize switch
        {
            Sizes.Quarter => 0.25f,
            Sizes.Half => 0.5f,
            Sizes.One => 1,
            Sizes.Two => 2,
            Sizes.Three => 3,
            Sizes.Four => 4,
            _ => 1
        };
}
