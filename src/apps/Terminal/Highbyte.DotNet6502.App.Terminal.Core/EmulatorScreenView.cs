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
    // Border cells cropped from each edge of the emulated frame so the kept border is no thicker than
    // VerticalBorderRows (top/bottom) / HorizontalBorderColumns (left/right). Set once per run from the
    // system's exact screen geometry via SetBorderThickness — fixed for the run, so the screen never
    // resizes mid-boot.
    private int _topTrim;
    private int _bottomTrim;
    private int _leftTrim;
    private int _rightTrim;

    private static readonly Attribute s_emptyAttribute = new(new Color(0, 0, 0), new Color(0, 0, 0));

    /// <summary>Raised (on the UI thread) when the user presses a key while the screen is focused.</summary>
    public event Action<Key>? EmulatorKeyPressed;

    public EmulatorScreenView()
    {
        CanFocus = true;
        KeyDown += OnScreenKeyDown;
    }

    // The emulated systems draw a thick solid-colour border around their screen (the C64 ~6 cells
    // wide / ~2 tall, the VIC-20 ~5 / ~2), which wastes space in a terminal where every row/column is
    // precious. These two properties cap the border kept on each side: the frame is cropped
    // symmetrically so no more than this many border cells remain (a system whose border is already
    // this thin, or thinner, is left untouched). 0 disables cropping for that axis (full border).

    /// <summary>Border rows to keep on the top and bottom. See EmulatorConfig.VerticalBorderRows.</summary>
    public int VerticalBorderRows { get; set; }

    /// <summary>Border columns to keep on the left and right. See EmulatorConfig.HorizontalBorderColumns.</summary>
    public int HorizontalBorderColumns { get; set; }

    /// <summary>
    /// Cell width of the most recently snapshotted frame, with the side border cropped to
    /// <see cref="HorizontalBorderColumns"/> on each side (0 until the first frame). This is the width
    /// actually painted, so the host sizes the screen box to it.
    /// </summary>
    public int FrameWidth => Math.Max(0, _frameWidth - _leftTrim - _rightTrim);

    /// <summary>
    /// Cell height of the most recently snapshotted frame, with the top/bottom border cropped to
    /// <see cref="VerticalBorderRows"/> on each side (0 until the first frame). This is the height
    /// actually painted, so the host sizes the screen box to it.
    /// </summary>
    public int FrameHeight => Math.Max(0, _frameHeight - _topTrim - _bottomTrim);

    public void SetRenderTarget(TerminalRenderTarget? target)
    {
        _renderTarget = target;
        if (target == null)
        {
            _frameWidth = 0;
            _frameHeight = 0;
            _topTrim = _bottomTrim = _leftTrim = _rightTrim = 0;
        }
    }

    /// <summary>
    /// Set the running system's cosmetic border thickness (in cells) so the view can crop it down to
    /// the configured keep amounts (<see cref="VerticalBorderRows"/> / <see cref="HorizontalBorderColumns"/>).
    /// The host supplies this once at start from the system's own screen geometry, so the trim is exact
    /// and constant for the whole run — the screen never resizes while the system boots. A system with
    /// no border (0, 0) is shown untrimmed.
    /// </summary>
    public void SetBorderThickness(int borderColumns, int borderRows)
    {
        _leftTrim = _rightTrim = Math.Max(0, borderColumns - HorizontalBorderColumns);
        _topTrim = _bottomTrim = Math.Max(0, borderRows - VerticalBorderRows);
        SetNeedsDraw();
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

        // Paint only the cropped region: the border on each edge has been trimmed down to the
        // configured keep amount (_topTrim/_bottomTrim rows, _leftTrim/_rightTrim columns), so a
        // bordered system screen fits a default terminal while still showing a thin border.
        var drawHeight = Math.Min(_frameHeight - _topTrim - _bottomTrim, viewport.Height);
        var drawWidth = Math.Min(_frameWidth - _leftTrim - _rightTrim, viewport.Width);

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
                var cell = _buffer[y + _topTrim, x + _leftTrim];
                SetAttribute(new Attribute(cell.Foreground, cell.Background));
                Move(x, y);
                AddRune(cell.Rune);
            }
        }

        return true; // content fully drawn; skip default
    }
}
