using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// A compact, width-friendly tab strip: all tab titles are shown on a single line, with the active
/// tab bracketed and drawn in the focus/highlight color. Terminal.Gui 2.4.5 has no built-in TabView,
/// and stock Buttons add `[ … ]` chrome that overflows the narrow side column, so this is a small
/// hand-rolled strip that renders plain text and reports its own hit spans for mouse clicks.
///
/// Switching tabs is conflict-proof with the running emulator: the strip only changes the active tab
/// from Left/Right arrows while it (not the emulator screen) has focus, so the emulator never loses a
/// keystroke. A mouse click on a title selects it directly.
/// </summary>
public sealed class TabStripView : View
{
    private readonly string[] _tabs;
    private readonly (int Start, int Length)[] _hitSpans; // updated each draw, used for click hit-testing
    private int _active;

    /// <summary>Raised (UI thread) when the active tab changes via Left/Right or a mouse click.</summary>
    public event Action<int>? TabSelected;

    public TabStripView(params string[] tabs)
    {
        _tabs = tabs;
        _hitSpans = new (int, int)[tabs.Length];
        CanFocus = true;
        Height = 1;
        Width = Dim.Fill();
    }

    public int Active => _active;

    /// <summary>Set the active tab. Raises <see cref="TabSelected"/> only when the index actually changes.</summary>
    public void SetActive(int index)
    {
        if (index < 0 || index >= _tabs.Length || index == _active)
            return;
        _active = index;
        SetNeedsDraw();
        TabSelected?.Invoke(_active);
    }

    protected override bool OnKeyDown(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.CursorRight:
                SetActive((_active + 1) % _tabs.Length);
                return true;
            case KeyCode.CursorLeft:
                SetActive((_active - 1 + _tabs.Length) % _tabs.Length);
                return true;
            default:
                return false;
        }
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.IsSingleClicked || mouse.Position is not { } pos)
            return false;

        for (var i = 0; i < _tabs.Length; i++)
        {
            var (start, length) = _hitSpans[i];
            if (pos.X >= start && pos.X < start + length)
            {
                SetFocus();
                SetActive(i);
                return true;
            }
        }
        return false;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var normal = GetAttributeForRole(VisualRole.Normal);
        var activeAttr = GetAttributeForRole(HasFocus ? VisualRole.Focus : VisualRole.HotNormal);

        var width = Viewport.Width;

        // Clear the line in the normal scheme first.
        SetAttribute(normal);
        Move(0, 0);
        for (var i = 0; i < width; i++)
            AddRune((Rune)' ');

        var x = 1; // small leading indent
        for (var i = 0; i < _tabs.Length; i++)
        {
            var label = i == _active ? $"[{_tabs[i]}]" : _tabs[i];
            _hitSpans[i] = (x, label.Length);

            if (x >= width)
                break;

            SetAttribute(i == _active ? activeAttr : normal);
            Move(x, 0);
            foreach (var rune in label.EnumerateRunes())
                AddRune(rune);

            x += label.Length + 2; // two-space gap between tabs
        }

        return true; // fully drawn; skip default
    }
}
