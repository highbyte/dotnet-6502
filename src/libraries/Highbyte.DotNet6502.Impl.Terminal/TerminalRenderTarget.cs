using System.Text;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using SystemDrawingColor = System.Drawing.Color;

namespace Highbyte.DotNet6502.Impl.Terminal;

/// <summary>
/// A render target that consumes the system's <see cref="IVideoCommand"/> stream (the same stream
/// the SadConsole/Avalonia/Skia command targets consume) and turns it into a grid of colored text
/// cells — a (<see cref="System.Text.Rune"/> glyph, foreground <see cref="Color"/>, background
/// <see cref="Color"/>) per cell — that a Terminal.Gui view paints into the real terminal.
///
/// Reuses the system's own <c>SetConfig.GlyphToUnicodeConverter</c> (e.g. the C64 screen-code →
/// PETSCII → Unicode mapping) so no system-specific rendering code lives here; this target is
/// system-agnostic.
///
/// Only text mode is rendered. Bitmap/hi-res mode is not represented (the command stream a text-mode
/// system emits is a glyph grid); see the terminal-ui-host-app design notes.
/// </summary>
public sealed class TerminalRenderTarget : ICommandTarget
{
    public string Name => "TerminalRenderTarget";

    public readonly record struct ScreenCell(Rune Rune, Color Foreground, Color Background);

    // Generous fixed maximum. A C64 text screen incl. visible border is ~48x35 cells; this leaves
    // ample headroom for other text-mode systems without per-frame reallocation.
    private const int MaxCols = 128;
    private const int MaxRows = 96;

    private readonly ScreenCell[,] _cells = new ScreenCell[MaxRows, MaxCols];
    private readonly object _lock = new();

    // Committed size of the most recently completed frame (what the view should draw).
    private int _width;
    private int _height;

    // Bounds being accumulated for the in-progress frame.
    private int _frameMaxX = -1;
    private int _frameMaxY = -1;

    private Func<byte, string>? _glyphToUnicode;

    // Caches to avoid per-cell allocations on the hot path.
    private readonly Dictionary<byte, Rune> _glyphCache = new();
    private readonly Dictionary<uint, Color> _colorCache = new();

    private static readonly Rune SpaceRune = new(' ');

    public TerminalRenderTarget()
    {
        var blank = new ScreenCell(SpaceRune, new Color(0, 0, 0), new Color(0, 0, 0));
        for (var r = 0; r < MaxRows; r++)
            for (var c = 0; c < MaxCols; c++)
                _cells[r, c] = blank;
    }

    public void BeginFrame()
    {
        _frameMaxX = -1;
        _frameMaxY = -1;
    }

    public void Execute(IVideoCommand cmd)
    {
        switch (cmd)
        {
            case SetConfig(var glyphToUnicodeConverter):
                _glyphToUnicode = glyphToUnicodeConverter;
                // The converter's output for a screen code can change at runtime (e.g. the C64
                // switching between the uppercase/graphics and lowercase charsets), so drop the
                // cached glyphs. SetConfig is emitted once per frame, so within a frame repeated
                // screen codes are still cached.
                _glyphCache.Clear();
                break;

            case FillRect(var x, var y, var w, var h, var colorArgb):
                {
                    var bg = GetColor(colorArgb);
                    for (var ry = y; ry < y + h; ry++)
                        for (var rx = x; rx < x + w; rx++)
                            SetCell(rx, ry, SpaceRune, bg, bg);
                    break;
                }

            case DrawGlyphArgb(var gx, var gy, var glyph, var foreArgb, var backArgb):
                SetGlyphCell(gx, gy, glyph, GetColor(foreArgb), GetColor(backArgb));
                break;

            case DrawGlyph(var gx, var gy, var glyph, SystemDrawingColor fore, SystemDrawingColor back):
                SetGlyphCell(gx, gy, glyph, GetColor(fore), GetColor(back));
                break;

            default:
                // Ignore unsupported commands (terminal text mode renders glyph commands only).
                break;
        }
    }

    public void EndFrame()
    {
        lock (_lock)
        {
            _width = _frameMaxX + 1;
            _height = _frameMaxY + 1;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Snapshots the most recently completed frame into the supplied buffer. Returns the committed
    /// width/height. The buffer is grown if needed. Thread-safe with respect to frame commits.
    /// </summary>
    public (int width, int height) Snapshot(ref ScreenCell[,] buffer)
    {
        lock (_lock)
        {
            if (buffer.GetLength(0) < _height || buffer.GetLength(1) < _width)
                buffer = new ScreenCell[Math.Max(_height, 1), Math.Max(_width, 1)];

            for (var r = 0; r < _height; r++)
                for (var c = 0; c < _width; c++)
                    buffer[r, c] = _cells[r, c];

            return (_width, _height);
        }
    }

    private void SetCell(int x, int y, Rune rune, Color fg, Color bg)
    {
        if (x < 0 || y < 0 || x >= MaxCols || y >= MaxRows)
            return;

        lock (_lock)
        {
            _cells[y, x] = new ScreenCell(rune, fg, bg);
        }

        if (x > _frameMaxX) _frameMaxX = x;
        if (y > _frameMaxY) _frameMaxY = y;
    }

    private void SetGlyphCell(int x, int y, int glyphId, Color fg, Color bg)
    {
        var id = (byte)glyphId;
        if (IsCommodoreReverseScreenCode(id))
        {
            // The C64/VIC-20 command streams pass raw screen codes as glyph ids. Codes with the
            // high bit set are reverse-video variants. Desktop hosts can render those with a C64
            // font; terminals cannot. Render them as terminal reverse video instead: base glyph,
            // swapped foreground/background. For a reversed space this naturally becomes a solid
            // cursor block.
            SetCell(x, y, GetRune(GetCommodoreBaseScreenCode(id)), bg, fg);
            return;
        }

        SetCell(x, y, GetRune(id), fg, bg);
    }

    private Rune GetRune(int glyphId)
    {
        var id = (byte)glyphId;
        if (_glyphCache.TryGetValue(id, out var cached))
            return cached;

        Rune rune;
        if (_glyphToUnicode != null)
        {
            var s = _glyphToUnicode(id);
            rune = string.IsNullOrEmpty(s) ? SpaceRune : FirstRune(s);
        }
        else
        {
            // No converter supplied: best-effort treat the glyph id as an ASCII code.
            rune = id is 0 or 32 ? SpaceRune : new Rune((char)id);
        }

        _glyphCache[id] = rune;
        return rune;
    }

    private static bool IsCommodoreReverseScreenCode(byte glyphId) => glyphId >= 0x80;

    private static byte GetCommodoreBaseScreenCode(byte glyphId)
        => glyphId is 0xA0 or 0xE0 ? (byte)0x20 : (byte)(glyphId - 0x80);

    private static Rune FirstRune(string s)
    {
        // Take the first Unicode scalar value; fall back to space for malformed input.
        foreach (var rune in s.EnumerateRunes())
            return rune;
        return SpaceRune;
    }

    private Color GetColor(uint argb)
    {
        if (_colorCache.TryGetValue(argb, out var cached))
            return cached;
        var color = new Color(
            (int)((argb >> 16) & 0xFF),
            (int)((argb >> 8) & 0xFF),
            (int)(argb & 0xFF));
        _colorCache[argb] = color;
        return color;
    }

    private Color GetColor(SystemDrawingColor c)
    {
        var argb = (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B);
        return GetColor(argb);
    }
}
