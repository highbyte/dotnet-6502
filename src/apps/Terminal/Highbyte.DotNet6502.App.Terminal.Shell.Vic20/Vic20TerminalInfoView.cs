using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Highbyte.DotNet6502.App.Terminal.Shell.Vic20;

/// <summary>
/// VIC-20-specific info panel shown in the terminal host's Info tab. Contributed by
/// <see cref="Vic20TerminalShellPlugin"/>. Lists the VIC-20 keyboard mapping for the terminal host
/// (which PC/Mac key maps to which VIC-20 key) in a scrollable list, suited to the narrow tabbed pane.
/// </summary>
public sealed class Vic20TerminalInfoView : View, ITerminalInfoContribution
{
    private static readonly string[] s_lines =
    {
        "VIC-20 keyboard mapping",
        "(PC/Mac  ->  VIC-20)",
        "",
        "Esc         Run/Stop",
        "            (stop Basic)",
        "Arrows      Cursor keys",
        "Ctrl,1-8    Ctrl+1-8 colors",
        "Backspace   Del",
        "Del         Del",
        "F1 F3 F5 F7 F1 F3 F5 F7",
        "",
        "Docs:",
        "highbyte.github.io/dotnet-6502",
        "  (systems/vic20/keyboard)",
    };

    public View View => this;

    public Vic20TerminalInfoView()
    {
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.HorizontalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.SetSource(new ObservableCollection<string>(s_lines));
        Add(list);
    }
}
