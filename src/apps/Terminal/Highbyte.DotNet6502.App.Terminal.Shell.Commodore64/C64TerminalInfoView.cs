using System.Collections.ObjectModel;
using Highbyte.DotNet6502.App.Terminal;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Highbyte.DotNet6502.App.Terminal.Shell.Commodore64;

/// <summary>
/// C64-specific info panel shown in the terminal host's Info tab. Contributed by
/// <see cref="C64TerminalShellPlugin"/>. Lists the C64 keyboard mapping for the terminal host (which
/// PC/Mac key maps to which C64 key) in a scrollable list, suited to the narrow tabbed pane.
/// </summary>
public sealed class C64TerminalInfoView : View, ITerminalInfoContribution
{
    private static readonly string[] s_lines =
    {
        "C64 keyboard mapping",
        "(PC/Mac  ->  C64)",
        "",
        "Esc      Run/Stop",
        "Tab,1-8  Ctrl+1-8 colors",
        "Alt+1-8  CBM+1-8 colors",
        "         if terminal",
        "         sends Alt/Meta",
        "Ctrl     Commodore (C=)",
        "Alt      Intl. symbols",
        "Shift    Shift",
        "F1-F8    C64 function keys",
        "",
        "Run/Stop+Restore = soft reset",
        "  (Esc, then PgUp /",
        "   fn+ArrowUp on Mac)",
        "",
        "Docs:",
        "highbyte.github.io/dotnet-6502",
        "  (systems/c64/keyboard)",
    };

    public View View => this;

    public C64TerminalInfoView()
    {
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.HorizontalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        list.SetSource(new ObservableCollection<string>(s_lines));
        Add(list);
    }
}
