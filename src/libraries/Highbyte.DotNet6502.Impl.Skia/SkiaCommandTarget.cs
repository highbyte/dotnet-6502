using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Impl.Skia;

/// Skia-backed ICommandTarget.
/// - If useCellCoordinates = true, X/Y are cell coordinates and cellWidth/cellHeight are used.
/// - Otherwise X/Y/W/H are pixels.
/// - Glyphs rendered with configurable Typeface/FontSize; text baseline aligned to cell (or to Y if pixel-space).
[DisplayName("Skia Commands")]
[HelpText("Renders IVideoCommands to a SkiaSharp SKCanvas.\nSupports FillRect and DrawGlyph commands.")]
public sealed class SkiaCommandTarget : ICommandTarget, IDisposable
{
    private readonly Func<SKCanvas?> _canvasAccessor;
    private readonly bool _useCellCoordinates;
    private readonly bool _flush;
    private readonly int _cellWidth;
    private readonly int _cellHeight;

    private readonly SKPaint _fillPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _textPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill, LcdRenderText = true, SubpixelText = true };
    private readonly SKTypeface _typeFace;
    //private readonly SKPaintMaps _skPaintMaps;

    // Color caches to avoid repeated SKColor allocations
    private static readonly ConcurrentDictionary<uint, SKColor> _argbColorCache = new();
    private static readonly ConcurrentDictionary<Color, SKColor> _systemColorCache = new();
    private const int MaxColorCacheItemSize = 1024; // Prevent unlimited memory growth

    public string Name => "SkiaCommandTarget";

    public SkiaCommandTarget(
        Func<SKCanvas?> canvasAccessor,
        bool useCellCoordinates,
        bool flush,
        int cellWidth = 8,
        int cellHeight = 8,
        SKTypeface? typeface = null,
        float fontSize = 8)
    {
        _canvasAccessor = canvasAccessor ?? throw new ArgumentNullException(nameof(canvasAccessor));
        _useCellCoordinates = useCellCoordinates;
        _flush = flush;
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;

        if (_typeFace == null)
            _typeFace = LoadEmbeddedFont("C64_Pro_Mono-STYLE.ttf");
        _textPaint.Typeface = _typeFace;
        _textPaint.TextSize = fontSize;

        //_skPaintMaps = new SKPaintMaps(
        //    textSize: (int)fontSize,
        //    typeFace: typeFace,
        //    SKPaintMaps.ColorMap
        //);
    }

    public void BeginFrame()
    {
        // no-op; clear in your app if needed
    }

    public void Execute(IVideoCommand cmd)
    {
        var canvas = _canvasAccessor();
        if (canvas is null) return;

        switch (cmd)
        {
            case FillRect(var x, var y, var w, var h, var color):
                DrawFillRect(canvas, x, y, w, h, color);
                break;

            case DrawGlyph(var gx, var gy, var glyph, Color foreColor, Color backColor):
                DrawGlyphInternal(canvas, gx, gy,
                    GetDrawTextFromCharacter((byte)glyph),
                    FromColor(foreColor), FromColor(backColor));
                break;

            case DrawGlyphArgb(var gx, var gy, var glyph, var foreColorArgb, var backColorArgb):
                DrawGlyphInternal(canvas, gx, gy,
                    GetDrawTextFromCharacter((byte)glyph),
                    FromArgb(foreColorArgb), FromArgb(backColorArgb));
                break;

        }
    }

    public void EndFrame()
    {
        if (_flush)
        {
            var canvas = _canvasAccessor();
            canvas?.Flush();
        }
    }

    private void DrawFillRect(SKCanvas canvas, int x, int y, int w, int h, uint colorRgba)
    {
        var color = FromArgb(colorRgba);
        _fillPaint.Color = color;

        if (_useCellCoordinates)
        {
            var pixelX = x * _cellWidth;
            var pixelY = y * _cellHeight;
            var pixelW = w * _cellWidth;
            var pixelH = h * _cellHeight;
            canvas.DrawRect(pixelX, pixelY, pixelW, pixelH, _fillPaint);
        }
        else
        {
            canvas.DrawRect(x, y, w, h, _fillPaint);
        }
    }

    private void DrawGlyphInternal(SKCanvas canvas, int x, int y, string text, SKColor fg, SKColor bg)
    {
        // Background fill
        _fillPaint.Color = bg;

        if (_useCellCoordinates)
        {
            var px = x * _cellWidth;
            var py = y * _cellHeight;

            // Foreground glyph
            _textPaint.Color = fg;

            // Make clipping rectangle for the tile we're drawing, to avoid any accidental spill-over to neighboring tiles.
            // Use simple save/restore pattern - safest approach with minimal overhead
            canvas.Save();
            var rect = new SKRect(px, py, px + _cellWidth, py + _cellHeight);
            canvas.ClipRect(rect, SKClipOperation.Intersect);
            // Fill background
            canvas.DrawRect(px, py, _cellWidth, _cellHeight, _fillPaint);
            // Draw character
            //canvas.DrawText(character, x, y + (_cellWidth - 2), _textPaint);
            canvas.DrawText(text, px, py + _cellWidth, _textPaint);
            canvas.Restore();
        }
        else
        {
            // Pixel mode
            // TODO
            throw new NotImplementedException("Pixel mode not implemented in DrawGlyphInternal");
        }
    }

    private static SKColor FromArgb(uint argb)
    {
        // Check cache first
        if (_argbColorCache.TryGetValue(argb, out var cachedColor))
            return cachedColor;

        // Create new color if not in cache
        var newColor = new SKColor(argb);

        // Add to cache if we haven't exceeded the maximum size
        if (_argbColorCache.Count < MaxColorCacheItemSize)
        {
            _argbColorCache.TryAdd(argb, newColor);
        }

        return newColor;
    }

    // Note: Is caching Color -> SKColor conversions worth it? Color is struct, not allocated on heap. And no logic to convert Color to SKColor, just new.
    private static SKColor FromColor(Color c)
    {
        // Check cache first using the Color directly as key
        if (_systemColorCache.TryGetValue(c, out var cachedColor))
            return cachedColor;

        // Create new color if not in cache
        var newColor = new SKColor(c.R, c.G, c.B, c.A);

        // Add to cache if we haven't exceeded the maximum size
        if (_systemColorCache.Count < MaxColorCacheItemSize)
        {
            _systemColorCache.TryAdd(c, newColor);
        }

        return newColor;
    }

    private string GetDrawTextFromCharacter(byte chr)
    {
        string representAsString;
        switch (chr)
        {
            case 0x00:  // Uninitialized
            case 0x0a:  // NewLine/CarrigeReturn
            case 0x0d:  // NewLine/CarrigeReturn
                representAsString = " "; // Replace with space
                break;
            case 0xa0:  //160, C64 inverted space
            case 0xe0:  //224, Also C64 inverted space?
                // Unicode for Inverted square in https://style64.org/c64-truetype font
                representAsString = ((char)0x2588).ToString();
                break;
            default:
                // Even though both upper and lowercase characters are used in the 6502 program (and in the font), show all as uppercase for C64 look.
                representAsString = Convert.ToString((char)chr).ToUpper();
                break;
        }
        return representAsString;
    }

    public void Dispose()
    {
        _fillPaint.Dispose();
        _textPaint.Dispose();
        _typeFace.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }


    private SKTypeface LoadEmbeddedFont(string fullFontName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var resourceName = $"{"Highbyte.DotNet6502.Impl.Skia.Resources.Fonts"}.{fullFontName}";
        using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
        {
            if (resourceStream == null)
                throw new ArgumentException($"Cannot load font from embedded resource. Resource: {resourceName}", nameof(fullFontName));

            var typeFace = SKTypeface.FromStream(resourceStream) ?? throw new ArgumentException($"Cannot load font as a Skia TypeFace from embedded resource. Resource: {resourceName}", nameof(fullFontName));
            return typeFace;
        }
    }
}
