using Highbyte.DotNet6502.Impl.Terminal;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// The custom Terminal.Gui view that paints the emulator screen as a grid of colored text cells.
/// It snapshots the latest completed frame from the <see cref="TerminalRenderTarget"/> and emits
/// one rune per cell with TrueColor foreground/background — the only non-stock-widget part of the
/// TUI.
///
/// While this view is focused, key presses are forwarded to the host (for the emulator's keyboard),
/// so the emulated system receives all keys including F1–F8 (which the C64 uses). Host hotkeys are
/// handled globally (F9–F12) at the application level, not here, so they never steal emulator keys.
/// Tab is forwarded too because the C64 uses it as Ctrl for color-key chords (Tab+1..8).
/// </summary>
public sealed class EmulatorScreenView : View
{
    private TerminalRenderTarget? _renderTarget;
    private TerminalRenderTarget.ScreenCell[,] _buffer = new TerminalRenderTarget.ScreenCell[1, 1];
    private int _frameWidth;
    private int _frameHeight;

    private static readonly Attribute s_emptyAttribute = new(new Color(0, 0, 0), new Color(0, 0, 0));

    /// <summary>Raised (on the UI thread) when the user presses a key while the screen is focused.</summary>
    public event Action<Key>? EmulatorKeyPressed;

    public EmulatorScreenView()
    {
        CanFocus = true;
        KeyDown += OnScreenKeyDown;
    }

    /// <summary>
    /// Number of cosmetic border cells to crop from the top and bottom of the emulated frame when
    /// painting (and when reporting <see cref="FrameHeight"/>). Lets the bordered C64/VIC-20 screen
    /// fit a default terminal without losing the border entirely. See EmulatorConfig.ScreenBorderTrim.
    /// </summary>
    public int VerticalBorderTrim { get; set; }

    /// <summary>Cell width of the most recently snapshotted frame (0 until the first frame).</summary>
    public int FrameWidth => _frameWidth;

    /// <summary>
    /// Cell height of the most recently snapshotted frame, with <see cref="VerticalBorderTrim"/>
    /// removed from each side (0 until the first frame). This is the height actually painted, so the
    /// host sizes the screen box to it.
    /// </summary>
    public int FrameHeight => Math.Max(0, _frameHeight - EffectiveTrim * 2);

    /// <summary>The trim actually applied, clamped so it never crops away the whole frame.</summary>
    private int EffectiveTrim => _frameHeight > 2 ? Math.Min(VerticalBorderTrim, (_frameHeight - 1) / 2) : 0;

    public void SetRenderTarget(TerminalRenderTarget? target)
    {
        _renderTarget = target;
        if (target == null)
        {
            _frameWidth = 0;
            _frameHeight = 0;
        }
    }

    /// <summary>
    /// Pulls the latest completed frame from the render target and marks the view for repaint.
    /// Called from the host's throttled display timer (UI thread).
    /// </summary>
    public void RefreshFromRenderTarget()
    {
        if (_renderTarget == null)
            return;
        (_frameWidth, _frameHeight) = _renderTarget.Snapshot(ref _buffer);
        SetNeedsDraw();
    }

    private void OnScreenKeyDown(object? sender, Key key)
    {
        EmulatorKeyPressed?.Invoke(key);
        key.Handled = true; // consume so the emulator receives it (incl. F1–F8) and Terminal.Gui doesn't.
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var viewport = Viewport;

        // Skip the top (and, by reduced height, bottom) cosmetic border cells so a bordered system
        // screen fits a default terminal while still showing a border.
        var trim = EffectiveTrim;
        var drawHeight = Math.Min(_frameHeight - trim * 2, viewport.Height);
        var drawWidth = Math.Min(_frameWidth, viewport.Width);

        // Clear the whole viewport first (in case the emulator frame is smaller than the view).
        SetAttribute(s_emptyAttribute);
        for (var y = 0; y < viewport.Height; y++)
        {
            Move(0, y);
            for (var x = 0; x < viewport.Width; x++)
                AddRune((System.Text.Rune)' ');
        }

        for (var y = 0; y < drawHeight; y++)
        {
            for (var x = 0; x < drawWidth; x++)
            {
                var cell = _buffer[y + trim, x];
                SetAttribute(new Attribute(cell.Foreground, cell.Background));
                Move(x, y);
                AddRune(cell.Rune);
            }
        }

        return true; // content fully drawn; skip default
    }
}
