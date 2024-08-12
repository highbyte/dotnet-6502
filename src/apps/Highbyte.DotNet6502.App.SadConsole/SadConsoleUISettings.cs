using SadConsole.UI;
using SadRogue.Primitives;
using static SadConsole.UI.Colors;

namespace Highbyte.DotNet6502.App.SadConsole;
internal class SadConsoleUISettings
{
    public const bool UI_USE_CONSOLE_BORDER = true;

    public static Colors ThemeColors = CreateDotNet6502Colors(); // Colors.CreateAnsi(); Colors.CreateSadConsoleBlue();

    public static Colors CreateDotNet6502Colors()
    {
        var colors = new Colors()
        {
            White = Color.White,
            Black = Color.Black,
            Gray = new Color(176, 196, 222),
            //GrayDark = new Color(66, 66, 66),
            GrayDark = new Color(49, 63, 83),
            Red = new Color(255, 0, 51),
            Green = new Color(153, 224, 0),
            //Blue = new Color(102, 204, 255),
            Blue = new Color(110, 145, 230),    // new Color(107, 137, 181),
            Purple = new Color(132, 0, 214),
            Yellow = new Color(255, 255, 102),
            Orange = new Color(255, 153, 0),
            Cyan = new Color(82, 242, 234),
            Brown = new Color(100, 59, 15),
            RedDark = new Color(153, 51, 51),
            GreenDark = new Color(110, 166, 23),
            //BlueDark = new Color(51, 102, 153),
            BlueDark = new Color(20, 27, 45),   // new Color(33, 43, 57),
            PurpleDark = new Color(70, 0, 114),
            YellowDark = new Color(255, 207, 15),
            OrangeDark = new Color(255, 102, 0),
            CyanDark = new Color(33, 182, 168),
            BrownDark = new Color(119, 17, 0),
            Gold = new Color(255, 215, 0),
            GoldDark = new Color(127, 107, 0),
            Silver = new Color(192, 192, 192),
            SilverDark = new Color(169, 169, 169),
            Bronze = new Color(205, 127, 50),
            BronzeDark = new Color(144, 89, 35),
        };

        colors.IsLightTheme = true;
        colors.Name = "dotnet6502";

        colors.Title = new AdjustableColor(ColorNames.Orange, "Title", colors);
        colors.Lines = new AdjustableColor(ColorNames.GrayDark, "Lines", colors);

        colors.ControlForegroundNormal = new AdjustableColor(ColorNames.Blue, "Control Foreground Normal", colors);
        colors.ControlForegroundDisabled = new AdjustableColor(ColorNames.GrayDark, "Control Foreground Disabled", colors);
        colors.ControlForegroundMouseOver = new AdjustableColor(ColorNames.Blue, "Control Foreground MouseOver", colors);
        colors.ControlForegroundMouseDown = new AdjustableColor(ColorNames.BlueDark, "Control Foreground MouseDown", colors);
        colors.ControlForegroundSelected = new AdjustableColor(ColorNames.Yellow, "Control Foreground Selected", colors);
        colors.ControlForegroundFocused = new AdjustableColor(ColorNames.Cyan, "Control Foreground Focused", colors);

        colors.ControlBackgroundNormal = new AdjustableColor(ColorNames.BlueDark, "Control Background Normal", colors);
        colors.ControlBackgroundDisabled = new AdjustableColor(ColorNames.BlueDark, "Control Background Disabled", colors);
        colors.ControlBackgroundMouseOver = new AdjustableColor(ColorNames.BlueDark, "Control Background MouseOver", colors) { Brightness = Brightness.Dark };
        colors.ControlBackgroundMouseDown = new AdjustableColor(ColorNames.Blue, "Control Background MouseDown", colors);
        colors.ControlBackgroundSelected = new AdjustableColor(ColorNames.BlueDark, "Control Background Selected", colors);
        colors.ControlBackgroundFocused = new AdjustableColor(ColorNames.BlueDark, "Control Background Focused", colors) { Brightness = Brightness.Dark };

        colors.ControlHostForeground = new AdjustableColor(ColorNames.Blue, "Control Host Foreground", colors);
        colors.ControlHostBackground = new AdjustableColor(ColorNames.BlueDark, "Control Host Background", colors);

        // Rebuild the controls
        colors.RebuildAppearances();

        return colors;
    }


    public readonly static ColoredGlyph ConsoleBorderGlyph = new(ThemeColors.Lines, ThemeColors.ControlHostBackground);

    //public static Color UIConsoleBackgroundColor = new Color(5, 15, 45);
    //public static Color UIConsoleForegroundColor = Color.White;
    //public readonly static ColoredGlyph ConsoleBorderGlyph = new(new Color(90, 90, 90), UIConsoleBackgroundColor);


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
