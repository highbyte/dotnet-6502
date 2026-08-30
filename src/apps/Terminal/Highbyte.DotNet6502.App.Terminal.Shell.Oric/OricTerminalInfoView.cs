using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Highbyte.DotNet6502.App.Terminal.Shell.Oric;

/// <summary>Oric keyboard mapping and Terminal-host limitations.</summary>
public sealed class OricTerminalInfoView : View, ITerminalInfoContribution
{
    private static readonly string[] s_lines =
    {
        "Oric Atmos keyboard mapping",
        "(PC/Mac  ->  Atmos)",
        "",
        "Enter       RETURN",
        "Backspace   DEL",
        "Alt/Option  FUNCT",
        "Shift       SHIFT",
        "Arrow keys  Cursor keys",
        "Ctrl+C      Stop BASIC/LIST",
        "Ctrl+T      Toggle CAPS",
        "Ctrl+L      Clear screen",
        "Ctrl+X      Abort input line",
        "Ctrl+A      Copy cursor char",
        "",
        "Joystick (when enabled)",
        "WASD        Direction",
        "Space       Fire",
        "",
        "Terminal always displays the",
        "40 x 28 text screen. Hi-res",
        "pixels and custom glyph shapes",
        "are unavailable.",
    };

    public View View => this;

    public OricTerminalInfoView()
    {
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.HorizontalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.SetSource(new ObservableCollection<string>(s_lines));
        Add(list);
    }
}
