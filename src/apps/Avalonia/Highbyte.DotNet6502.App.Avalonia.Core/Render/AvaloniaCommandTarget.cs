using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;
using SystemColor = System.Drawing.Color;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Render;

/// <summary>
/// Avalonia-specific command target that renders video commands to an Avalonia DrawingContext.
/// This target translates Skia-based video commands to Avalonia's drawing operations.
/// </summary>
[DisplayName("Avalonia Commands")]
[HelpText("Renders IVideoCommands to an Avalonia DrawingContext.\nSupports FillRect and DrawGlyph commands.")]
public sealed class AvaloniaCommandTarget : ICommandTarget, IDisposable
{
    private DrawingContext? _currentContext;
    private readonly Dictionary<uint, IBrush> _brushCache = new();
    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly Typeface _typeface;
    private readonly double _fontSize;

    public string Name => "AvaloniaCommandTarget";

    public AvaloniaCommandTarget(
        int cellWidth = 8,
        int cellHeight = 8,
        double fontSize = 12,
        string? customFontPath = null,
        string? customFontFamilyName = null)
    {
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        _fontSize = fontSize;

        if (!string.IsNullOrEmpty(customFontPath) && !string.IsNullOrEmpty(customFontFamilyName))
        {
            _typeface = LoadEmbeddedFont(customFontPath, customFontFamilyName);
        }
        else
        {
            _typeface = LoadEmbeddedFont("C64_Pro_Mono-STYLE.ttf", "C64 Pro Mono");
        }
    }

    /// <summary>
    /// Set the current drawing context for rendering operations.
    /// This is called by the control during its Render method.
    /// </summary>
    public void SetDrawingContext(DrawingContext? context)
    {
        _currentContext = context;
    }

    public void BeginFrame()
    {
        // Clear cached brushes periodically to prevent memory leaks
        if (_brushCache.Count > 100)
        {
            _brushCache.Clear();
        }
    }

    public void Execute(IVideoCommand cmd)
    {
        if (_currentContext == null) return;

        switch (cmd)
        {
            case FillRect(var x, var y, var w, var h, var color):
                DrawFillRect(_currentContext, x, y, w, h, color);
                break;

            case DrawGlyph(var gx, var gy, var glyph, SystemColor foreColor, SystemColor backColor):
                DrawGlyph(_currentContext, gx, gy, glyph, foreColor, backColor);
                break;

            case DrawGlyphArgb(var gx, var gy, var glyph, var foreColorArgb, var backColorArgb):
                DrawGlyph(_currentContext, gx, gy, glyph,
                    SystemColor.FromArgb((int)foreColorArgb),
                    SystemColor.FromArgb((int)backColorArgb));
                break;

            default:
                // Ignore unsupported commands
                break;
        }
    }

    public void EndFrame()
    {
        // No-op for Avalonia
    }

    public ValueTask DisposeAsync()
    {
        _brushCache.Clear();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _brushCache.Clear();
    }

    private void DrawFillRect(DrawingContext context, int x, int y, int w, int h, uint colorRgba)
    {
        var brush = GetBrush(colorRgba);
        var rect = new Rect(x, y, w, h);
        context.FillRectangle(brush, rect);
    }

    private void DrawGlyph(DrawingContext context, int x, int y, int glyph, SystemColor foreColor, SystemColor backColor)
    {
        // Calculate pixel position from cell coordinates
        var pixelX = x * _cellWidth;
        var pixelY = y * _cellHeight;

        // Draw background
        var bgBrush = GetBrush(backColor);
        var bgRect = new Rect(pixelX, pixelY, _cellWidth, _cellHeight);
        context.FillRectangle(bgBrush, bgRect);

        // Draw character if not null or space
        if (glyph != 0 && glyph != 32)
        {
            var fgBrush = GetBrush(foreColor);
            var text = GetDrawTextFromCharacter((byte)glyph);

            // Create formatted text for proper font rendering
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                fgBrush);

            // Position the text within the cell
            // For better positioning, align text to top-left and add small padding
            var textPoint = new Point(pixelX + 1, pixelY + 1);
            context.DrawText(formattedText, textPoint);
        }
    }

    private IBrush GetBrush(uint argbColor)
    {
        if (_brushCache.TryGetValue(argbColor, out var cached))
            return cached;

        var color = Color.FromArgb(
            (byte)((argbColor >> 24) & 0xFF),
            (byte)((argbColor >> 16) & 0xFF),
            (byte)((argbColor >> 8) & 0xFF),
            (byte)(argbColor & 0xFF));

        var brush = new SolidColorBrush(color);
        _brushCache[argbColor] = brush;
        return brush;
    }

    private IBrush GetBrush(SystemColor color)
    {
        uint argb = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        return GetBrush(argb);
    }

    private Typeface LoadEmbeddedFont(string fontFileName, string fontFamilyName)
    {
        try
        {
            // Use the font family name from the TTF file
            var fontFamily = new FontFamily($"avares://Highbyte.DotNet6502.App.Avalonia.Core/Resources/Fonts/{fontFileName}#{fontFamilyName}");
            var typeface = new Typeface(fontFamily);
            return typeface;
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"Failed to load custom font: {ex.Message}");
        }

        // Fall back to Consolas or other monospace font
        try
        {
            return new Typeface("Consolas", FontStyle.Normal, FontWeight.Normal);
        }
        catch
        {
            // Final fallback to default monospace
            return new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
        }
    }

    private string GetDrawTextFromCharacter(byte chr)
    {
        return chr switch
        {
            0x00 or 0x0a or 0x0d => " ", // Uninitialized, NewLine/CarriageReturn
            0xa0 or 0xe0 => "█", // C64 inverted space - Unicode block character
            _ => Convert.ToString((char)chr).ToUpper() // Show all as uppercase for C64 look
        };
    }
}
