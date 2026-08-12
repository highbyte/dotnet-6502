using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Highbyte.DotNet6502.App.Terminal.Shell.Apple2;

/// <summary>Apple II keyboard mapping and Terminal-host limitations.</summary>
public sealed class Apple2TerminalInfoView : View, ITerminalInfoContribution
{
    private static readonly string[] s_lines =
    {
        "Apple II Plus",
        "40 x 24 text, uppercase",
        "",
        "Keyboard mapping",
        "Enter       Return",
        "Esc         Esc",
        "Backspace   Left arrow",
        "Left/Right  Apple arrows",
        "Up/Down     Line up/down",
        "Ctrl+key    Control code",
        "",
        "Joystick (when enabled)",
        "WASD        Direction",
        "Space       Button 1",
        "Shift       Button 2",
        "",
        "Terminal renders the text",
        "page in graphics modes; hi-res",
        "and lo-res pixels are unavailable.",
    };

    public View View => this;

    public Apple2TerminalInfoView()
    {
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.HorizontalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.SetSource(new ObservableCollection<string>(s_lines));
        Add(list);
    }
}
