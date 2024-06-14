using System.Globalization;
using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Utils;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class C64SkiaRenderer3 : IRenderer<C64, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas = default!;

    private uint[] _pixelArray;
    private SKBitmap _bitmap = default!;

    private uint[] _spritesPixelArray;
    private SKBitmap _spritesBitmap;

    // Pre-calculated pixel arrays
    uint[] _oneLineBorderPixels; // pixelArray
    uint[] _oneCharLineBg0Pixels; // pixelArray

    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg0Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg1Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg2Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg3Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsMultiColorMap;

    // Colors to draw border and background colors with on the bitmap. These colors will be replaced by the shader.
    // Could be any color, but must be different from normal C64 colors (used when drawing foreground colors).

    SKColor _bg0DrawColorActual = SKColors.Orchid;
    SKColor _bg0DrawColor = SKColors.DarkOrchid.WithAlpha(0);   // Any color with alpha 0, will make sure _bg0DrawColorActual is used as background color (replace in shader with _bg0DrawColorActual)
    SKColor _bg1DrawColor = SKColors.DarkOliveGreen;
    SKColor _bg2DrawColor = SKColors.DarkMagenta;
    SKColor _bg3DrawColor = SKColors.DarkOrange;

    SKColor _borderDrawColor = SKColors.DarkKhaki;

    const byte LOW_PRIO_SPRITE_BLUE = 51;   // 51 translates to exactly 0.2 in this texture shader (51/255 = 0.2)
    const float LOW_PRIO_SPRITE_BLUE_SHADER = LOW_PRIO_SPRITE_BLUE / 255.0f; // Shader uses 0-1 float values
    const byte HIGH_PRIO_SPRITE_BLUE = 255; // 255 translates to exactly 1.0 in this texture shader (255/255 = 1.0)
    const float HIGH_PRIO_SPRITE_BLUE_SHADER = HIGH_PRIO_SPRITE_BLUE / 255.0f;  // Shader uses 0-1 float values

    SKColor _spriteLowPrioMultiColor0 = new SKColor(red: 200, green: 200, blue: LOW_PRIO_SPRITE_BLUE);
    SKColor _spriteLowPrioMultiColor1 = new SKColor(red: 210, green: 210, blue: LOW_PRIO_SPRITE_BLUE);
    SKColor _spriteHighPrioMultiColor0 = new SKColor(red: 200, green: 200, blue: HIGH_PRIO_SPRITE_BLUE);
    SKColor _spriteHighPrioMultiColor1 = new SKColor(red: 210, green: 210, blue: HIGH_PRIO_SPRITE_BLUE);

    // Sprite 0 - 7. Low prio colors have Blue value of 51. Rest of the colors is used to distinguish the sprite.
    SKColor[] _spriteLowPrioColors = new SKColor[]
    {
        new SKColor(red: 0,  green: 0,  blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 10, green: 10, blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 20, green: 20, blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 30, green: 30, blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 40, green: 40, blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 50, green: 50, blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 60, green: 60, blue: LOW_PRIO_SPRITE_BLUE),
        new SKColor(red: 70, green: 70, blue: LOW_PRIO_SPRITE_BLUE),
    };
    // Sprite 0 - 7. High prio colors have Blue value of 255. Rest of the colors is used to distinguish the sprite.
    SKColor[] _spriteHighPrioColors = new SKColor[]
    {
        new SKColor(red: 0,  green: 0,  blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 10, green: 10, blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 20, green: 20, blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 30, green: 30, blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 40, green: 40, blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 50, green: 50, blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 60, green: 60, blue: HIGH_PRIO_SPRITE_BLUE),
        new SKColor(red: 70, green: 70, blue: HIGH_PRIO_SPRITE_BLUE),
    };

    // Lookup table for mapping C64 colors to shader colors
    Dictionary<uint, float[]> _sKColorToShaderColorMap = new Dictionary<uint, float[]>();

    C64SkiaColors _c64SkiaColors;

    private SKRuntimeEffect _sKRuntimeEffect; // Shader source

    private const int LineDataIndex_Bg0_Color = 0;
    private const int LineDataIndex_Bg1_Color = 1;
    private const int LineDataIndex_Bg2_Color = 2;
    private const int LineDataIndex_Bg3_Color = 3;
    private const int LineDataIndex_Border_Color = 4;
    private const int LineDataIndex_SpriteMultiColor0 = 5;
    private const int LineDataIndex_SpriteMultiColor1 = 6;
    private const int LineDataIndex_Sprite0_Color = 7;
    private const int LineDataIndex_Sprite1_Color = 8;
    private const int LineDataIndex_Sprite2_Color = 9;
    private const int LineDataIndex_Sprite3_Color = 10;
    private const int LineDataIndex_Sprite4_Color = 11;
    private const int LineDataIndex_Sprite5_Color = 12;
    private const int LineDataIndex_Sprite6_Color = 13;
    private const int LineDataIndex_Sprite7_Color = 14;

    private uint[] _lineDataPixelArray;
    private const int _lineDataPixelArrayWidth = 5 + 2 + 8; // bg0, bg1, bg2, bg3, border,   spriteMultiColor0, spriteMultiColor1, and  sprite color 0-7, on each line;
    private SKBitmap _lineDataBitmap;

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();
    private const string StatsCategory = "SkiaSharp-Custom";
    private readonly ElapsedMillisecondsTimedStat _borderStat;
    private readonly ElapsedMillisecondsTimedStat _textScreenStat;
    private readonly ElapsedMillisecondsTimedStat _spritesStat;

    public C64SkiaRenderer3()
    {
        _borderStat = Instrumentations.Add<ElapsedMillisecondsTimedStat>($"{StatsCategory}-Border");
        _spritesStat = Instrumentations.Add<ElapsedMillisecondsTimedStat>($"{StatsCategory}-Sprites");
        _textScreenStat = Instrumentations.Add<ElapsedMillisecondsTimedStat>($"{StatsCategory}-TextScreen");
    }

    public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
    {
        _getSkCanvas = skiaRenderContext.GetCanvas;

        _c64SkiaColors = new C64SkiaColors(c64.ColorMapName);

        InitBitmap(c64);
        InitShader(c64);
    }

    // Test with shader
    private void InitShader(C64 c64)
    {
        var src = @"
// The bitmap that was drawn.
uniform shader bitmap_texture;

// Sprites textures
uniform shader sprites_texture;

// The actual color to display as border and background colors for each line. Used to replace the colors in the bitmap.
uniform shader line_data_map;

// The color used to draw border and background colors
uniform half4 bg0Color;
uniform half4 bg1Color;
uniform half4 bg2Color;
uniform half4 bg3Color;

uniform half4 borderColor;

uniform half4 spriteLowPrioMultiColor0;
uniform half4 spriteLowPrioMultiColor1;

uniform half4 spriteHighPrioMultiColor0;
uniform half4 spriteHighPrioMultiColor1;

uniform half4 sprite0LowPrioColor;
uniform half4 sprite1LowPrioColor;
uniform half4 sprite2LowPrioColor;
uniform half4 sprite3LowPrioColor;
uniform half4 sprite4LowPrioColor;
uniform half4 sprite5LowPrioColor;
uniform half4 sprite6LowPrioColor;
uniform half4 sprite7LowPrioColor;

uniform half4 sprite0HighPrioColor;
uniform half4 sprite1HighPrioColor;
uniform half4 sprite2HighPrioColor;
uniform half4 sprite3HighPrioColor;
uniform half4 sprite4HighPrioColor;
uniform half4 sprite5HighPrioColor;
uniform half4 sprite6HighPrioColor;
uniform half4 sprite7HighPrioColor;

half4 get_line_data(float lineIndex, float line) {
    // Assume image in line_data_map is x pixel wide (bg0, bg1, bg2, bg3, border colors, etc.), and y (number of main screen lines) pixels high.

    // For images, Skia GSL use the common convention that the centers are at half-pixel offsets. 
    // So to sample the top-left pixel in an image shader, you'd want to pass (0.5, 0.5) as coords.
    // The next (to the right) pixel would be (1.5, 0.5).
    // The next (to the below ) pixel would be (0.5, 1.5).

    return line_data_map.eval(float2(0.5 + lineIndex, 0.5 + line));
}

half4 map_sprite_color(half4 spriteColor, float line) {

    half4 useColor;

    if(spriteColor == spriteLowPrioMultiColor0 || spriteColor == spriteHighPrioMultiColor0) {
        useColor = get_line_data(#SPRITE_MULTICOLOR0_INDEX, line);
    }
    else if(spriteColor == spriteLowPrioMultiColor1 || spriteColor == spriteHighPrioMultiColor1) {
        useColor = get_line_data(#SPRITE_MULTICOLOR1_INDEX, line);
    }

    else if(spriteColor == sprite0LowPrioColor || spriteColor == sprite0HighPrioColor) {
        useColor = get_line_data(#SPRITE0_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite1LowPrioColor || spriteColor == sprite1HighPrioColor) {
        useColor = get_line_data(#SPRITE1_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite2LowPrioColor || spriteColor == sprite2HighPrioColor) {
        useColor = get_line_data(#SPRITE2_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite3LowPrioColor || spriteColor == sprite3HighPrioColor) {
        useColor = get_line_data(#SPRITE3_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite4LowPrioColor || spriteColor == sprite4HighPrioColor) {
        useColor = get_line_data(#SPRITE4_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite5LowPrioColor || spriteColor == sprite5HighPrioColor) {
        useColor = get_line_data(#SPRITE5_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite6LowPrioColor || spriteColor == sprite6HighPrioColor) {
        useColor = get_line_data(#SPRITE6_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite7LowPrioColor || spriteColor == sprite7HighPrioColor) {
        useColor = get_line_data(#SPRITE7_COLOR_INDEX, line);
    }

    else {
        // Should not happen, show bright purple color to indicate error.
        useColor = half4(1, 0, 1, 1);
    }

    return useColor;    
}

half4 map_screen_color(half4 textAndBitmapColor, half4 spriteColor, float line) {

    // For images, Skia SKSL lanuage use the common convention that the centers are at half-pixel offsets. 
    // To sample the top-left pixel in an image shader, you'd want to pass (0.5, 0.5) as coords.
    // The next (to the right) pixel would be (1.5, 0.5).
    // The next (to the below ) pixel would be (0.5, 1.5).

    half4 useColor;
    float2 lineCoord;

    if(spriteColor.b == #HIGH_PRIO_SPRITE_BLUE_SHADER) {
        // Sprite pixel with specific Blue color indicates high-prio sprite here, and should be shown instead of background or border color.
        useColor = map_sprite_color(spriteColor, line);
    }
    else if(textAndBitmapColor == borderColor) {
        // Normal border color indicates border color could used.

        // But if a sprite pixel from a low prio sprite is drawn at this position, use the sprite color instead of border color.
        // Sprite pixel with specific Blue color indicates low-prio sprite here, and should be shown instead of border color.
        if(spriteColor.b == #LOW_PRIO_SPRITE_BLUE_SHADER) {
            // Use sprite color
            useColor = map_sprite_color(spriteColor, line);
        }
        else {
            // Use border color
            useColor = get_line_data(#BORDER_COLOR_INDEX, line);
        }
    }
    else if((textAndBitmapColor + bg0Color) == bg0Color) {
        // Normal text/bitmap screen indicates background color could used.

        // But if a sprite pixel from a low prio sprite is drawn at this position, use the sprite color instead of background color.
        // Sprite pixel with specific Blue color indicates low-prio sprite here, and should be shown instead of background color.
        if(spriteColor.b == #LOW_PRIO_SPRITE_BLUE_SHADER) {
            // Use sprite color
            useColor = map_sprite_color(spriteColor, line);
        }
        else {
            // Use background color
            useColor = get_line_data(#BG0_COLOR_INDEX, line);
        }
    }
    else if(textAndBitmapColor == bg1Color) {
        useColor = get_line_data(#BG1_COLOR_INDEX, line);
    }
    else if(textAndBitmapColor == bg2Color) {
        useColor = get_line_data(#BG2_COLOR_INDEX, line);
    }
    else if(textAndBitmapColor == bg3Color) {
        useColor = get_line_data(#BG3_COLOR_INDEX, line);
    }
    else {
        useColor = textAndBitmapColor;    // Not a color that should be transformed (i.e. foreground color of text or bitmap)
    }
    return useColor;
}

half4 main(float2 fragCoord) {

    half4 textAndBitmapColor = bitmap_texture.eval(fragCoord);
    half4 spriteColor = sprites_texture.eval(fragCoord);
    float line = fragCoord.y;

    half4 useColor;

    if(line < #VISIBLE_HEIGHT) {
        useColor = map_screen_color(textAndBitmapColor, spriteColor, line);
    }
    else {
        // Should not happen, show bright red color to indicate error.
        useColor = half4(1, 0, 0, 1);
    }

    return useColor;
}";
        src = src.Replace("#VISIBLE_HEIGHT", c64.Vic2.Vic2Screen.VisibleHeight.ToString());

        var visibleMainScreenAreaNormalized = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var bitmapMainScreenStartLine = visibleMainScreenAreaNormalized.Screen.Start.Y;
        src = src.Replace("#MAIN_SCREEN_START", bitmapMainScreenStartLine.ToString());
        src = src.Replace("#MAIN_SCREEN_END", (bitmapMainScreenStartLine + c64.Vic2.Vic2Screen.DrawableAreaHeight - 1).ToString());

        src = src.Replace("#LOW_PRIO_SPRITE_BLUE_SHADER", LOW_PRIO_SPRITE_BLUE_SHADER.ToString(CultureInfo.InvariantCulture));
        src = src.Replace("#HIGH_PRIO_SPRITE_BLUE_SHADER", HIGH_PRIO_SPRITE_BLUE_SHADER.ToString(CultureInfo.InvariantCulture));

        src = src.Replace("#BG0_COLOR_INDEX", LineDataIndex_Bg0_Color.ToString());
        src = src.Replace("#BG1_COLOR_INDEX", LineDataIndex_Bg1_Color.ToString());
        src = src.Replace("#BG2_COLOR_INDEX", LineDataIndex_Bg2_Color.ToString());
        src = src.Replace("#BG3_COLOR_INDEX", LineDataIndex_Bg3_Color.ToString());

        src = src.Replace("#BORDER_COLOR_INDEX", LineDataIndex_Border_Color.ToString());

        src = src.Replace("#SPRITE_MULTICOLOR0_INDEX", LineDataIndex_SpriteMultiColor0.ToString());
        src = src.Replace("#SPRITE_MULTICOLOR1_INDEX", LineDataIndex_SpriteMultiColor1.ToString());

        src = src.Replace("#SPRITE0_COLOR_INDEX", LineDataIndex_Sprite0_Color.ToString());
        src = src.Replace("#SPRITE1_COLOR_INDEX", LineDataIndex_Sprite1_Color.ToString());
        src = src.Replace("#SPRITE2_COLOR_INDEX", LineDataIndex_Sprite2_Color.ToString());
        src = src.Replace("#SPRITE3_COLOR_INDEX", LineDataIndex_Sprite3_Color.ToString());
        src = src.Replace("#SPRITE4_COLOR_INDEX", LineDataIndex_Sprite4_Color.ToString());
        src = src.Replace("#SPRITE5_COLOR_INDEX", LineDataIndex_Sprite5_Color.ToString());
        src = src.Replace("#SPRITE6_COLOR_INDEX", LineDataIndex_Sprite6_Color.ToString());
        src = src.Replace("#SPRITE7_COLOR_INDEX", LineDataIndex_Sprite7_Color.ToString());

        _sKRuntimeEffect = SKRuntimeEffect.CreateShader(src, out var error);
        if (!string.IsNullOrEmpty(error))
            throw new DotNet6502Exception($"Shader compilation error: {error}");

        // Init color map (colors used in shader to transform the colors the bitmap was drawn with).
        SKColor color;
        color = _bg0DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });
        color = _bg0DrawColorActual;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });
        color = _bg1DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });
        color = _bg2DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });
        color = _bg3DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _borderDrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _spriteLowPrioMultiColor0;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });
        color = _spriteLowPrioMultiColor1;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _spriteHighPrioMultiColor0;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });
        color = _spriteHighPrioMultiColor1;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        foreach (var spriteColor in _spriteLowPrioColors.Union(_spriteHighPrioColors))
        {
            _sKColorToShaderColorMap.Add((uint)spriteColor, new[] { spriteColor.Red / 255.0f, spriteColor.Green / 255.0f, spriteColor.Blue / 255.0f, spriteColor.Alpha / 255.0f });
        }

        // TODO: Is this needed?
        // Init the actual colors used in the shader to draw the border lines
        foreach (byte c64Color in Enum.GetValues<C64Colors>())
        {
            var c64SkColor = _c64SkiaColors.C64ToSkColorMap[c64Color];
            _sKColorToShaderColorMap.Add((uint)c64SkColor, new[] { c64SkColor.Red / 255.0f, c64SkColor.Green / 255.0f, c64SkColor.Blue / 255.0f, c64SkColor.Alpha / 255.0f });
        }

        // Init the bg color map bitmap
        _lineDataPixelArray = new uint[_lineDataPixelArrayWidth * c64.Vic2.Vic2Screen.VisibleHeight]; // bg0, bg1, bg2, and bg3 on each line
        _lineDataBitmap = new();

        // pin the managed pixel array so that the GC doesn't move it
        // (It is essential that the pinned memory be unpinned after usage so that the memory can be freed by the GC.)
        var gcHandle = GCHandle.Alloc(_lineDataPixelArray, GCHandleType.Pinned);

        // install the pixels with the color type of the pixel data
        //var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        var info = new SKImageInfo(_lineDataPixelArrayWidth, c64.Vic2.Vic2Screen.DrawableAreaHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);  // Note: SKColorType.Bgra8888 seems to be needed for Blazor WASM. TODO: Does this affect when running in Blazor on Mac/Linux?
        _lineDataBitmap.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); }, null);
    }

    private void InitBitmap(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;

        // Init pixel array to associate with a SKBitmap that is written to a SKCanvas
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // --------------------
        // Entire screen (border and main screen), excluding sprites
        // --------------------
        // Init pixel array to associate with a SKBitmap that is used in shader
        _pixelArray = new uint[width * height];
        //_bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _bitmap = new();
        // pin the managed pixel array so that the GC doesn't move it
        // (It is essential that the pinned memory be unpinned after usage so that the memory can be freed by the GC.)
        var gcHandle = GCHandle.Alloc(_pixelArray, GCHandleType.Pinned);
        // install the pixels with the color type of the pixel data
        //var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);  // Note: SKColorType.Bgra8888 seems to be needed for Blazor WASM. TODO: Does this affect when running in Blazor on Mac/Linux?
        _bitmap.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); }, null);

        // --------------------
        // Sprites on separate bitmap
        // --------------------
        // Init pixel array to associate with a SKBitmap that is used in shader
        _spritesPixelArray = new uint[width * height];
        //_spritesBitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _spritesBitmap = new();
        // pin the managed pixel array so that the GC doesn't move it
        // (It is essential that the pinned memory be unpinned after usage so that the memory can be freed by the GC.)
        var gcHandleSprites = GCHandle.Alloc(_spritesPixelArray, GCHandleType.Pinned);
        // install the pixels with the color type of the pixel data
        var infoSprites = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);  // Note: SKColorType.Bgra8888 seems to be needed for Blazor WASM. TODO: Does this affect when running in Blazor on Mac/Linux?
        _spritesBitmap.InstallPixels(infoSprites, gcHandleSprites.AddrOfPinnedObject(), infoSprites.RowBytes, delegate { gcHandle.Free(); }, null);


        // --------------------
        // Init pre-calculated pixel arrays
        // --------------------
        // Borders: Pre-calculate entire rows, left/right border only, and one char row of pixels.
        _oneLineBorderPixels = new uint[width];
        for (var i = 0; i < _oneLineBorderPixels.Length; i++)
            _oneLineBorderPixels[i] = (uint)_borderDrawColor;
        _oneCharLineBg0Pixels = new uint[8];
        for (var i = 0; i < _oneCharLineBg0Pixels.Length; i++)
            _oneCharLineBg0Pixels[i] = (uint)_bg0DrawColor;

        // Main text screen: Pre-calculate the 8 pixels for each combination of bit pixel pattern and foreground color
        _bitmapEightPixelsBg0Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsBg1Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsBg2Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsBg3Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsMultiColorMap = new(); // (pixelPattern, fgColorIndex) => bitmapPixels

        uint bg0ColorVal = (uint)_bg0DrawColor; // Note the background color _bg0DrawColor is hardcoded, and will be replaced by shader
        uint bg1ColorVal = (uint)_bg1DrawColor; // Note the background color _bg1DrawColor is hardcoded, and will be replaced by shader
        uint bg2ColorVal = (uint)_bg2DrawColor; // Note the background color _bg2DrawColor is hardcoded, and will be replaced by shader
        uint bg3ColorVal = (uint)_bg3DrawColor; // Note the background color _bg3DrawColor is hardcoded, and will be replaced by shader

        // Standard and Extended mode (8 bits -> 8 pixels)
        for (int pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte fgColorCode = 0; fgColorCode < 16; fgColorCode++)
            {
                uint fgColorVal = (uint)_c64SkiaColors.C64ToSkColorMap[fgColorCode];

                uint[] bitmapPixelsBg0 = new uint[8];
                uint[] bitmapPixelsBg1 = new uint[8];
                uint[] bitmapPixelsBg2 = new uint[8];
                uint[] bitmapPixelsBg3 = new uint[8];

                // Loop each bit in pixelPattern
                for (int pixelPos = 0; pixelPos < 8; pixelPos++)
                {
                    // If bit is set, use foreground color, else use background color
                    bool isBitSet = (pixelPattern & (1 << (7 - pixelPos))) != 0;
                    bitmapPixelsBg0[pixelPos] = isBitSet ? fgColorVal : bg0ColorVal;
                    bitmapPixelsBg1[pixelPos] = isBitSet ? fgColorVal : bg1ColorVal;
                    bitmapPixelsBg2[pixelPos] = isBitSet ? fgColorVal : bg2ColorVal;
                    bitmapPixelsBg3[pixelPos] = isBitSet ? fgColorVal : bg3ColorVal;
                }
                _bitmapEightPixelsBg0Map.Add(((byte)pixelPattern, fgColorCode), bitmapPixelsBg0);
                _bitmapEightPixelsBg1Map.Add(((byte)pixelPattern, fgColorCode), bitmapPixelsBg1);
                _bitmapEightPixelsBg2Map.Add(((byte)pixelPattern, fgColorCode), bitmapPixelsBg2);
                _bitmapEightPixelsBg3Map.Add(((byte)pixelPattern, fgColorCode), bitmapPixelsBg3);
            }
        }

        // Multicolor mode, double pixel color (8 bits -> 4 pixels)
        for (int pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            // Only the lower 3 bits are used for foreground color from Color RAM, so only colors 0-7 are possible.
            for (byte fgColorCode = 0; fgColorCode < 8; fgColorCode++)
            {
                uint fgColorVal = (uint)_c64SkiaColors.C64ToSkColorMap[(byte)(fgColorCode)];

                uint[] bitmapPixelsMultiColor = new uint[8];

                // Loop each multi-color pixel pair (4 pixel pairs)
                var mask = 0b11000000;
                for (var pixel = 0; pixel < 4; pixel++)
                {
                    var pixelPair = (pixelPattern & mask) >> (6 - pixel * 2);
                    uint pairColorVal = pixelPair switch
                    {
                        0b00 => bg0ColorVal,
                        0b01 => bg1ColorVal,
                        0b10 => bg2ColorVal,
                        0b11 => fgColorVal,
                        _ => throw new DotNet6502Exception("Invalid pixel pair value.")
                    };
                    mask = mask >> 2;
                    bitmapPixelsMultiColor[pixel * 2] = pairColorVal;
                    bitmapPixelsMultiColor[pixel * 2 + 1] = pairColorVal;
                }
                _bitmapEightPixelsMultiColorMap.Add(((byte)pixelPattern, fgColorCode), bitmapPixelsMultiColor);
            }
        }
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SkiaRenderContext)renderContext);
    }

    public void Draw(C64 c64)
    {
        var canvas = _getSkCanvas();
        canvas.Clear();

        // Draw border and screen to bitmap
        DrawBorderAndScreenToBitmapBackedByPixelArray(c64, _pixelArray);

        // Draw sprites to separate bitmap
        DrawSpritesToBitmapBackedbackedByPixelArray(c64, _spritesPixelArray);

        // Draw to canvas using shader with texture info from screen sprite bitmaps
        WriteBitmapToCanvas(_bitmap, _spritesBitmap, canvas, c64);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    private void WriteBitmapToCanvas(SKBitmap bitmap, SKBitmap spritesBitmap, SKCanvas canvas, C64 c64)
    {
        canvas.Save();

        // Build array to send to shader with the actual color that should be used differnt types of colors (border, bg0, bg1, bg2, bg3), dependent on the line number
        var c64ScreenLineIORegisterValues = new Dictionary<int, ScreenLineData>(c64.Vic2.ScreenLineIORegisterValues);
        var visibleMainScreenArea = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.Visible);
        var drawableScreenStartY = visibleMainScreenArea.Screen.Start.Y;
        var drawableScreenEndY = drawableScreenStartY + c64.Vic2.Vic2Screen.DrawableAreaHeight;

        foreach (var lineData in c64ScreenLineIORegisterValues)
        {
            // Check if in total visisble area, because c64ScreenLineIORegisterValues includes non-visible lines
            if (lineData.Key < visibleMainScreenArea.TopBorder.Start.Y || lineData.Key > visibleMainScreenArea.BottomBorder.End.Y)
                continue;

            var bitmapLine = lineData.Key - visibleMainScreenArea.TopBorder.Start.Y;

            var pixelArrayIndex = bitmapLine * _lineDataPixelArrayWidth;

            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Border_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BorderColor];

            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_SpriteMultiColor0] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.SpriteMultiColor0];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_SpriteMultiColor1] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.SpriteMultiColor1];

            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite0_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite0Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite1_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite1Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite2_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite2Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite3_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite3Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite4_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite4Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite5_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite5Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite6_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite6Color];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Sprite7_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite7Color];

            // Check if line is within main screen area, only there are background colors used (? is that really true when borders are open??)
            if (lineData.Key < drawableScreenStartY || bitmapLine >= (drawableScreenEndY))
                continue;

            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Bg0_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor0];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Bg1_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor1];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Bg2_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor2];
            _lineDataPixelArray[pixelArrayIndex + LineDataIndex_Bg3_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor3];

        }

        // shader uniform values
        var uniforms = new SKRuntimeEffectUniforms(_sKRuntimeEffect)
        {
            ["bg0Color"] = _sKColorToShaderColorMap[(uint)_bg0DrawColorActual],
            ["bg1Color"] = _sKColorToShaderColorMap[(uint)_bg1DrawColor],
            ["bg2Color"] = _sKColorToShaderColorMap[(uint)_bg2DrawColor],
            ["bg3Color"] = _sKColorToShaderColorMap[(uint)_bg3DrawColor],

            ["borderColor"] = _sKColorToShaderColorMap[(uint)_borderDrawColor],

            ["spriteLowPrioMultiColor0"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioMultiColor0],
            ["spriteLowPrioMultiColor1"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioMultiColor1],

            ["spriteHighPrioMultiColor0"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioMultiColor0],
            ["spriteHighPrioMultiColor1"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioMultiColor1],

            ["sprite0LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[0]],
            ["sprite1LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[1]],
            ["sprite2LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[2]],
            ["sprite3LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[3]],
            ["sprite4LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[4]],
            ["sprite5LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[5]],
            ["sprite6LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[6]],
            ["sprite7LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[7]],

            ["sprite0HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[0]],
            ["sprite1HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[1]],
            ["sprite2HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[2]],
            ["sprite3HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[3]],
            ["sprite4HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[4]],
            ["sprite5HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[5]],
            ["sprite6HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[6]],
            ["sprite7HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[7]],
        };

        // Shader uniform texture sampling values
        // Convert bitmap (that one have written the C64 screen to) to shader texture
        var shaderTexture = bitmap.ToShader();
        // Convert shader bitmap (that one have written the C64 sprites to) to shader texture
        var spritesTexture = spritesBitmap.ToShader();

        // Convert other bitmaps to shader texture
        var lineDataBitmapShaderTexture = _lineDataBitmap.ToShader();

        var children = new SKRuntimeEffectChildren(_sKRuntimeEffect)
        {
            ["bitmap_texture"] = shaderTexture,
            ["sprites_texture"] = spritesTexture,
            ["line_data_map"] = lineDataBitmapShaderTexture,
        };

        using var shader = _sKRuntimeEffect.ToShader(uniforms, children);
        using var shaderPaint = new SKPaint { Shader = shader };

        canvas.DrawRect(0, 0, bitmap.Width, bitmap.Height, shaderPaint);

        canvas.Restore();
    }

    private void DrawBorderAndScreenToBitmapBackedByPixelArray(C64 c64, uint[] pixelArray)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalized = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // Main screen draw area for characters, with consideration to possible 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalizedClipped = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized);

        var startY = 0;
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Main screen, copy 8 pixels at a time
        using (_textScreenStat.Measure())
        {
            // Copy settings used in loop to local variables to increase performance
            var vic2VideoMatrixBaseAddress = vic2.VideoMatrixBaseAddress;
            var vic2ScreenTextCols = vic2Screen.TextCols;
            var screenStartY = visibleMainScreenAreaNormalized.Screen.Start.Y;
            var screenStartX = visibleMainScreenAreaNormalized.Screen.Start.X;
            var vic2CharacterSetAddressInVIC2Bank = vic2.CharsetManager.CharacterSetAddressInVIC2Bank;
            var vic2ScreenCharacterHeight = vic2.Vic2Screen.CharacterHeight;

            var vic2Is38ColumnDisplayEnabled = vic2.Is38ColumnDisplayEnabled;
            var vic2Is24RowDisplayEnabled = vic2.Is24RowDisplayEnabled;
            var vic2LineStart24Rows = visibleMainScreenAreaNormalizedClipped.Screen.Start.Y - visibleMainScreenAreaNormalized.Screen.Start.Y;
            var vic2LineEnd24Rows = visibleMainScreenAreaNormalizedClipped.Screen.End.Y - visibleMainScreenAreaNormalized.Screen.Start.Y;

            var scrollX = vic2.GetScrollX();
            var scrollY = vic2.GetScrollY();

            // Loop each row line on main text/gfx screen, starting with line 0.
            for (var drawLine = 0; drawLine < vic2Screen.DrawableAreaHeight; drawLine++)
            {
                // Calculate the y position in the bitmap where the 8 pixels should be drawn
                var bitmapY = (screenStartY + drawLine);

                var characterRow = drawLine / 8;
                var characterLine = drawLine % 8;
                var characterAddress = (ushort)(vic2VideoMatrixBaseAddress + (characterRow * vic2ScreenTextCols));


                bool textMode = (vic2.DisplayMode == DispMode.Text); // TODO: Check for display mode more than once per line?
                var characterMode = vic2.CharacterMode; // TODO: Check for display mode more than once per line?

                // Loop each column on main text/gfx screen, starting with column 0.
                for (var col = 0; col < vic2ScreenTextCols; col++)
                {
                    // Calculate the x position in the bitmap where the 8 pixels should be drawn
                    var bitmapX = screenStartX + (col * 8);

                    uint[] bitmapEightPixels;
                    if (textMode)
                    {
                        // Determine character code at current position
                        var characterCode = vic2Mem[characterAddress++];

                        // Determine colors
                        var fgColorCode = c64.ReadIOStorage((ushort)(Vic2Addr.COLOR_RAM_START + (characterRow * vic2ScreenTextCols) + col));
                        int bgColorNumber;  // 0-3
                        if (characterMode == CharMode.Standard)
                        {
                            bgColorNumber = 0;
                        }
                        else if (characterMode == CharMode.Extended)
                        {
                            bgColorNumber = characterCode >> 6;   // Bit 6 and 7 of character byte is used to select background color (0-3)
                            characterCode = (byte)(characterCode & 0b00111111); // The actual usable character codes are in the lower 6 bits (0-63)

                        }
                        else // Asume multicolor mode
                        {
                            bgColorNumber = 0;
                            // When in MultiColor mode, a character can still be displayed in Standard mode depending on the value from color RAM.
                            if (fgColorCode <= 7)
                            {
                                // If color RAM value is 0-7, normal Standard mode is used (not multi-color)
                                characterMode = CharMode.Standard;
                            }
                            else
                            {
                                // If displaying in MultiColor mode, the actual color used from color RAM will be values 0-7.
                                // Thus color values 8-15 are transformed to 0-7
                                fgColorCode = (byte)((fgColorCode & 0b00001111) - 8);
                            }
                        }

                        // Read one line (8 bits/pixels) of character pixel data from character set from the current line of the character code
                        var characterSetLineAddress = (ushort)(vic2CharacterSetAddressInVIC2Bank
                            + (characterCode * vic2ScreenCharacterHeight)
                            + characterLine);
                        byte lineData = vic2Mem[characterSetLineAddress];

                        // Get pre-calculated 8 pixels that should be drawn on the bitmap, with correct colors for foreground and background
                        if (characterMode == CharMode.Standard || characterMode == CharMode.Extended)
                        {
                            // Get the corresponding array of uints representing the 8 pixels of the character
                            bitmapEightPixels = bgColorNumber switch
                            {
                                0 => _bitmapEightPixelsBg0Map[(lineData, fgColorCode)],
                                1 => _bitmapEightPixelsBg1Map[(lineData, fgColorCode)],
                                2 => _bitmapEightPixelsBg2Map[(lineData, fgColorCode)],
                                3 => _bitmapEightPixelsBg3Map[(lineData, fgColorCode)],
                                _ => throw new NotImplementedException($"Background color number {bgColorNumber} not implemented.")
                            };

                        }
                        else // Asume multicolor mode
                        {
                            // Text multicolor mode color usage (8 bits, 4 pixel pairs)
                            // backgroundColor0 = the color of pixel-pair 00
                            // backgroundColor1 = the color of pixel-pair 01
                            // backgroundColor2 = the color of pixel-pair 10
                            // fgColorCode      = the color of pixel-pair 11

                            // Get the corresponding array of uints representing the 8 pixels of the character
                            bitmapEightPixels = _bitmapEightPixelsMultiColorMap[(lineData, fgColorCode)];
                        }
                    }
                    else
                    {
                        // Assume bitmap mode
                        // TODO
                        bitmapEightPixels = new uint[8];
                    }


                    // Adjust for horizontal scrolling
                    int length = bitmapEightPixels.Length;

                    // Add additional drawing to compensate for horizontal scrolling
                    if (scrollX > 0 && col == 0)
                    {
                        // Fill start of column 0 with background color (the number of x pixels scrolled)
                        // Array.Copy(_oneCharLineBg0Pixels, 0, pixelArray, bitmapIndex, scrollX);
                        WriteToPixelArray(_oneCharLineBg0Pixels, pixelArray, drawLine, 0, fnLength: scrollX, fnAdjustForScrollX: false, fnAdjustForScrollY: true);
                    }

                    // Add additional drawing to compensate for horizontal scrolling
                    // Note: The actual vic2 vertical scroll has 3 as default value (no scroll), but the scrollY variable is adjusted to be between -3 and + 4 with default 0 when no scrolling.
                    if (scrollY != 0)
                    {
                        // If scrolling occured upwards (scrollY < 0) and we are on last line of screen, fill remaining lines with background color
                        if (scrollY < 0 && drawLine == vic2Screen.DrawableAreaHeight - 1)
                        {
                            for (var i = 0; i < -scrollY; i++)
                            {
                                //Array.Copy(_oneCharLineBg0Pixels, 0, pixelArray, fillBitMapIndex + scrollX, length);
                                WriteToPixelArray(_oneCharLineBg0Pixels, pixelArray, drawLine - i, col * 8, fnLength: 8, fnAdjustForScrollX: true, fnAdjustForScrollY: false);
                            }
                        }
                        // If scrolling occured downards (scrollY > 0) and we are on first line of screen, fill the line above that was scrolled with background color
                        else if (scrollY > 0 && drawLine == 0)
                        {
                            for (var i = 0; i < scrollY; i++)
                            {
                                //Array.Copy(_oneCharLineBg0Pixels, 0, pixelArray, fillBitMapIndex + scrollX, length);
                                WriteToPixelArray(_oneCharLineBg0Pixels, pixelArray, i, col * 8, fnLength: 8, fnAdjustForScrollX: true, fnAdjustForScrollY: false);
                            }
                        }
                    }

                    // Write the character to the pixel array
                    WriteToPixelArray(bitmapEightPixels, pixelArray, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true, fnAdjustForScrollY: true);


                    void WriteToPixelArray(uint[] fnEightPixels, uint[] fnPixelArray, int fnMainScreenY, int fnMainScreenX, int fnLength, bool fnAdjustForScrollX, bool fnAdjustForScrollY)
                    {
                        // Draw 8 pixels (or less) of character on the the pixel array part used for the C64 drawable screen (320x200)
                        var lCol = fnMainScreenX / 8;

                        if (fnAdjustForScrollY)
                        {
                            // Skip draw entirely if y position is outside drawable screen
                            if ((fnMainScreenY + scrollY) < 0 || (fnMainScreenY + scrollY) >= vic2Screen.DrawableAreaHeight)
                                return;

                            // TODO: Adjust for vertical scrolling
                            fnMainScreenY += scrollY;
                        }

                        if (fnAdjustForScrollX)
                        {
                            fnMainScreenX += scrollX;
                            if (lCol == vic2ScreenTextCols - 1) // Adjust drawing of last character on line to clip when it reaches the right border
                                fnLength = 8 - scrollX;
                        }

                        // Calculate the position in the bitmap where the 8 pixels should be drawn
                        int lBitmapIndex = ((screenStartY + fnMainScreenY) * _bitmap.Width) + ((screenStartX + fnMainScreenX));

                        Array.Copy(fnEightPixels, 0, fnPixelArray, lBitmapIndex, fnLength);
                    }
                }
            }
        }


        // Borders.
        // The borders must be drawn last, because it will overwrite parts of the main screen area if 38 column or 24 row modes are enabled (which main screen drawing above does not take in consideration)
        using (_borderStat.Measure())
        {
            // Assumption on visibleMainScreenAreaNormalizedClipped:
            // - Contains dimensions of screen parts with consideration to if 38 column mode or 24 row mode is enabled.
            // - Is normalized to start at 0,0 (i.e. TopBorder.Start.X = and TopBorder.Start.Y = 0)
            var leftBorderStartX = visibleMainScreenAreaNormalizedClipped.LeftBorder.Start.X; // Should be 0?
            var leftBorderLength = (visibleMainScreenAreaNormalizedClipped.LeftBorder.End.X - visibleMainScreenAreaNormalizedClipped.LeftBorder.Start.X) + 1;

            var rightBorderStartX = visibleMainScreenAreaNormalizedClipped.RightBorder.Start.X;
            var rightBorderLength = _bitmap.Width - visibleMainScreenAreaNormalizedClipped.RightBorder.Start.X;

            for (var y = startY; y < (startY + height); y++)
            {
                // Top or bottom border
                if (y <= visibleMainScreenAreaNormalizedClipped.TopBorder.End.Y || y >= visibleMainScreenAreaNormalizedClipped.BottomBorder.Start.Y)
                {
                    var topBottomBorderLineStartIndex = y * _bitmap.Width;
                    Array.Copy(_oneLineBorderPixels, 0, pixelArray, topBottomBorderLineStartIndex, _bitmap.Width);
                    continue;
                }

                // Left border
                int lineStartIndex = y * _bitmap.Width;
                Array.Copy(_oneLineBorderPixels, leftBorderStartX, pixelArray, lineStartIndex, leftBorderLength);
                // Right border
                lineStartIndex += visibleMainScreenAreaNormalizedClipped.RightBorder.Start.X;
                Array.Copy(_oneLineBorderPixels, rightBorderStartX, pixelArray, lineStartIndex, rightBorderLength);
            }
        }
    }

    private void DrawSpritesToBitmapBackedbackedByPixelArray(C64 c64, uint[] spritesPixelArray)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // TODO: Is it faster to track previous frame sprite draw positions, and only clear those pixels instead?
        Array.Clear(spritesPixelArray);

        // Write sprites to a separate bitmap/pixel array
        foreach (var sprite in c64.Vic2.SpriteManager.Sprites.OrderByDescending(s => s.SpriteNumber))
        {

            if (!sprite.Visible)
                continue;

            var spriteScreenPosX = sprite.X + visibleMainScreenArea.Screen.Start.X - Vic2SpriteManager.SCREEN_OFFSET_X;
            var spriteScreenPosY = sprite.Y + visibleMainScreenArea.Screen.Start.Y - Vic2SpriteManager.SCREEN_OFFSET_Y;
            var priorityOverForground = sprite.PriorityOverForeground;
            var isMultiColor = sprite.Multicolor;

            // START TEST
            //if (sprite.SpriteNumber == 0)
            //{
            //    spriteScreenPosX = 50 + visibleMainScreenArea.Screen.Start.X - Vic2SpriteManager.SCREEN_OFFSET_X;
            //    spriteScreenPosY = 60 + visibleMainScreenArea.Screen.Start.Y - Vic2SpriteManager.SCREEN_OFFSET_Y;
            //    priorityOverForground = false;
            //}
            //if (sprite.SpriteNumber == 1)
            //{
            //    spriteScreenPosX = 67 + visibleMainScreenArea.Screen.Start.X - Vic2SpriteManager.SCREEN_OFFSET_X;
            //    spriteScreenPosY = 70 + visibleMainScreenArea.Screen.Start.Y - Vic2SpriteManager.SCREEN_OFFSET_Y;
            //    priorityOverForground = true;
            //}
            // END TEST

            var isDoubleWidth = sprite.DoubleWidth;
            var isDoubleHeight = sprite.DoubleHeight;

            uint spriteForegroundPixelColor;  // One color per sprite
            uint spriteMultiColor0PixelColor; // Shared between all sprites
            uint spriteMultiColor1PixelColor; // Shared between all sprites
            if (priorityOverForground)
            {
                // Top prio sprite pixel
                spriteForegroundPixelColor = (uint)_spriteHighPrioColors[sprite.SpriteNumber];
                spriteMultiColor0PixelColor = (uint)_spriteHighPrioMultiColor0;
                spriteMultiColor1PixelColor = (uint)_spriteHighPrioMultiColor1;
            }
            else
            {
                // Low prio sprite pixel
                spriteForegroundPixelColor = (uint)_spriteLowPrioColors[sprite.SpriteNumber];
                spriteMultiColor0PixelColor = (uint)_spriteLowPrioMultiColor0;
                spriteMultiColor1PixelColor = (uint)_spriteLowPrioMultiColor1;
            }

            // Loop each sprite line (21 lines)
            int y = 0;
            foreach (var spriteRow in sprite.Data.Rows)
            {
                // Loop each 8-bit part of the sprite line (3 bytes, 24 pixels).
                int x = 0;
                foreach (var spriteLinePart in spriteRow.Bytes)
                {
                    if (isMultiColor)
                    {
                        var maskMultiColor0Mask = 0b01000000;
                        var maskSpriteColorMask = 0b10000000;
                        var maskMultiColor1Mask = 0b11000000;

                        uint spriteColor;
                        for (var pixel = 0; pixel < 8; pixel += 2)
                        {
                            spriteColor = spriteLinePart switch
                            {
                                var p when (p & maskMultiColor0Mask) == maskMultiColor0Mask => spriteMultiColor0PixelColor,
                                var p when (p & maskSpriteColorMask) == maskSpriteColorMask => spriteForegroundPixelColor,
                                var p when (p & maskMultiColor1Mask) == maskMultiColor1Mask => spriteMultiColor1PixelColor,
                                _ => 0
                            };

                            if (spriteColor > 0)
                            {
                                WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y, spriteColor, priorityOverForground);
                                WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y, spriteColor, priorityOverForground);

                                if (isDoubleWidth)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 2, spriteScreenPosY + y, spriteColor, priorityOverForground);
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 3, spriteScreenPosY + y, spriteColor, priorityOverForground);
                                }

                                if (isDoubleHeight)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);

                                    if (isDoubleWidth)
                                    {
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 2, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 3, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);
                                    }
                                }
                            }

                            maskMultiColor0Mask = maskMultiColor0Mask >> 2;
                            maskMultiColor1Mask = maskMultiColor1Mask >> 2;
                            maskSpriteColorMask = maskSpriteColorMask >> 2;

                            x += isDoubleHeight ? 4 : 2;
                        }
                    }
                    else
                    {
                        var mask = 0b10000000;
                        for (var pixel = 0; pixel < 8; pixel++)
                        {
                            var pixelSet = (spriteLinePart & mask) == mask;
                            if (pixelSet)
                            {
                                WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y, spriteForegroundPixelColor, priorityOverForground);

                                if (isDoubleWidth)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y, spriteForegroundPixelColor, priorityOverForground);
                                }

                                if (isDoubleHeight)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y + 1, spriteForegroundPixelColor, priorityOverForground);
                                    if (isDoubleWidth)
                                    {
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y + 1, spriteForegroundPixelColor, priorityOverForground);
                                    }
                                }
                            }
                            mask = mask >> 1;

                            x += isDoubleHeight ? 2 : 1;
                        }
                    }
                }
                y += isDoubleHeight ? 2 : 1;
            }


            void WriteSpritePixelWithAlphaPrio(int screenPosX, int screenPosY, uint color, bool priorityOverForground)
            {
                // Check if pixel is outside the visible screen area
                if (screenPosX < 0 || screenPosX >= _spritesBitmap.Width || screenPosY < 0 || screenPosY > _spritesBitmap.Height)
                    return;

                // Check if pixel is within side borders, and if it should be shown there or not.
                // TODO: Detect if side borders are open? How to?
                bool openSideBorders = false;
                if (!openSideBorders && (screenPosX < visibleMainScreenArea.Screen.Start.X || screenPosX > visibleMainScreenArea.Screen.End.X))
                    return;

                // Check if pixel is within top/bottom borders, and if it should be shown there or not.
                // TODO: Detect if top/bottom borders are open? How to?
                bool openTopBottomBorders = false;
                if (!openTopBottomBorders && (screenPosY < visibleMainScreenArea.Screen.Start.Y || screenPosY > visibleMainScreenArea.Screen.End.Y))
                    return;

                // Calculate the position in the bitmap where the pixel should be drawn
                int bitmapIndex = (screenPosY * _spritesBitmap.Width) + screenPosX;

                // If pixel to be set is from a low prio sprite, don't overwrite if current pixel is from high prio sprite
                const uint BLUE_COLOR_MASK = 0x000000ff;
                if (!priorityOverForground)
                {
                    if ((spritesPixelArray[bitmapIndex] & BLUE_COLOR_MASK) == HIGH_PRIO_SPRITE_BLUE)
                        return;
                }

                spritesPixelArray[bitmapIndex] = color;
            }


            sprite.ClearDirty();
#if DEBUG
            //spriteGen.DumpSpriteToImageFile(_spriteImages[sprite.SpriteNumber], $"{Path.GetTempPath()}/c64_sprite_{sprite.SpriteNumber}.png");
#endif
        }
    }
}
