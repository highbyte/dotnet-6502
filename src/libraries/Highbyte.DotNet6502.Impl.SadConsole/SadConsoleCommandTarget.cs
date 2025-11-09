using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Impl.SadConsole;

[DisplayName("SadConsole Commands")]
[HelpText("Renders IVideoCommands to a SadConsole ScreenSurface.\nSupports FillRect and DrawGlyph commands.")]
public sealed class SadConsoleCommandTarget : ICommandTarget
{
    private readonly ScreenSurface _screenSurface;
    private readonly int _offsetX;
    private readonly int _offsetY;
    private readonly Dictionary<uint, Color> _colorArgbCache = new();
    private readonly Dictionary<System.Drawing.Color, Color> _colorCache = new();

    private readonly Func<int, Color, Color, (int tranformedCharacter, Color transformedFgColor, Color transformedBgColor)>? _transformCharacterAndColor;

    public string Name => "SadConsoleCommandTarget";

    public SadConsoleCommandTarget(
        ScreenSurface screenSurface,
        int offsetX = 0,
        int offsetY = 0,
        Func<int, Color, Color, (int tranformedCharacter, Color transformedFgColor, Color transformedBgColor)>? transformCharacterAndColor = null)
    {
        _screenSurface = screenSurface ?? throw new ArgumentNullException(nameof(screenSurface));
        _offsetX = offsetX;
        _offsetY = offsetY;
        _transformCharacterAndColor = transformCharacterAndColor;
    }

    public void BeginFrame()
    {
        // Optional: clear or prep before drawing
        //_emulatorConsole?.Surface?.Clear();
    }

    public void Execute(IVideoCommand cmd)
    {
        switch (cmd)
        {
            case SetConfig(var glyphToUnicodeConverter):
                // Note: glyphToUnicodeConverter not used in SadConsole implementation, see _transformCharacterAndColor instead (converts to SadConsole-specific character index and colors)
                break;

            case FillRect(var x, var y, var w, var h, var color):
                _screenSurface.Surface.Fill(
                    new Rectangle(x + _offsetX, y + _offsetY, w, h),
                    foreground: GetSadConsoleColor(color),
                    background: _screenSurface.Surface.DefaultBackground,
                    glyph: 0); // 0 = empty glyph
                break;

            case DrawGlyph(var gx, var gy, var glyph, System.Drawing.Color foreColor, System.Drawing.Color backColor):
                SetEmulatorCharacter(gx, gy, glyph, GetSadConsoleColor(foreColor), GetSadConsoleColor(backColor));
                break;

            case DrawGlyphArgb(var gx, var gy, var glyph, var foreColorArgb, var backColorArgb):
                SetEmulatorCharacter(gx, gy, glyph, GetSadConsoleColor(foreColorArgb), GetSadConsoleColor(backColorArgb));
                break;

            default:
                throw new NotSupportedException($"Unsupported command {cmd.GetType().Name}");
        }
    }

    public void EndFrame()
    {
        // Tell SadConsole this surface has changed so it will redraw
        if (_screenSurface.Surface is not null)
            _screenSurface.IsDirty = true;
    }
    public async ValueTask DisposeAsync()
    {
    }

    private Color GetSadConsoleColor(uint argbColor)
    {
        if (_colorArgbCache.TryGetValue(argbColor, out var cached))
            return cached;

        var created = ArgbColorToSadConsole(argbColor);
        _colorArgbCache[argbColor] = created;
        return created;
    }

    private Color GetSadConsoleColor(System.Drawing.Color color)
    {
        if (_colorCache.TryGetValue(color, out var cached))
            return cached;

        var created = SystemColorToSadConsole(color);
        _colorCache[color] = created;
        return created;
    }

    // SadConsole packed uint format is 0xAABBGGRR (R is least significant byte, A is most significant byte)
    private static Color ArgbColorToSadConsole(uint argb) => new Color(ArgbToAbgr(argb));
    private static uint ArgbToAbgr(uint argb)
    {
        return ((argb & 0xFF000000)) |       // A (keep)
               ((argb & 0x00FF0000) >> 16) | // R (shift to B position)
               ((argb & 0x0000FF00)) |       // G (keep)
               ((argb & 0x000000FF) << 16);  // B (shift to R position)
    }

    private static Color SystemColorToSadConsole(System.Drawing.Color color) => new Color(color.R, color.G, color.B, color.A);

    private void SetEmulatorCharacter(int x, int y, int emulatorCharacter, Color fgColor, Color bgColor)
    {
        if (_transformCharacterAndColor != null)
        {
            var (tranformedCharacter, transformedFgColor, transformedBgColor) = _transformCharacterAndColor(emulatorCharacter, fgColor, bgColor);
            emulatorCharacter = tranformedCharacter;
            fgColor = transformedFgColor;
            bgColor = transformedBgColor;
        }
        _screenSurface.Surface.SetGlyph(x + _offsetX, y + _offsetY, emulatorCharacter, fgColor, bgColor);
    }
}
