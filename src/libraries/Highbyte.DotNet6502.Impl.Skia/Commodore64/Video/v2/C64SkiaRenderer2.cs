using System.Globalization;
using System.Reflection;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v2;

/// <summary>
/// Renders a C64 system to a SkiaSharp canvas.
/// 
/// Overview:
/// - Called once per frame.
/// - Writes image data to a SKBitmap backed by a pixel array, and uses a shader + generated SKBitmap "textures" to do the actual drawing to the SkiaSharp canvas.
/// - Fast enough to be used for native and browser (WASM) hosts.
/// 
/// Supports:
/// - Text mode (Standard, Extended, MultiColor)
/// - Bitmap mode (Standard/HiRes, MultiColor)
/// - Colors per raster line
/// - Fine scroll per frame.
/// - Sprites (Standard, MultiColor)
///   
/// Compared to C64SkiaRenderer:
/// - A bit slower, but supports Bitmap graphics and still able to run in a browser with Blazor WebAssembly.
/// </summary>
public class C64SkiaRenderer2 : IRenderer<C64, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas = default!;

    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_TextAndBitmap;
    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_Sprites;
    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_LineData;

    private SKRuntimeEffect _sKRuntimeEffect; // Shader
    private SKRuntimeEffectUniforms _sKRuntimeEffectUniforms; // Shader uniforms
    private SKRuntimeEffectChildren _sKRuntimeEffectChildren; // Shader children (textures)
    private SKPaint _shaderPaint;

    // Lookup table for mapping C64 colors to shader colors
    private readonly Dictionary<uint, float[]> _sKColorToShaderColorMap = new Dictionary<uint, float[]>();
    private C64SkiaColors _c64SkiaColors;

    // Pre-calculated pixel arrays
    private uint[] _oneLineBorderPixels; // pixelArray
    private uint[] _oneCharLineBg0Pixels; // pixelArray

    // 8-bit patterns mapped to 8 pixels (1 pixel = 1 uint rgba) that will be replaced in shader to actual color per raster line.
    private Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg0Map;
    private Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg1Map;
    private Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg2Map;
    private Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg3Map;
    private Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsMultiColorMap;

    // Bitmap mode "Standard" (HiRes): 8-bit patterns mapped to 8 pixels (1 pixel = 1 uint rgba) that that are the actual colors, not to be replaced in shader.
    private Dictionary<(byte eightPixels, byte bitmapBgColorCode, byte bitmapFgColorCode), uint[]> _bitmapEightHiresPixelsActual;
    // Bitmap mode "Multicolo": 8-bit patterns mapped to 4 width 2 pixels (1 pixel = 1 uint rgba) that that are the actual colors, not to be replaced in shader (except background color)
    private Dictionary<(byte eightPixels, byte bitmapBgColorCode, byte bitmapFgColorCode, byte colorRamCode), uint[]> _bitmapEightMulticolorPixelsActual;


    // Colors to draw border and background colors with on the bitmap. These colors will be replaced by the shader.
    // Could be any color, but must be different from normal C64 colors (used when drawing foreground colors).
    private readonly SKColor _bg0DrawColorActual = SKColors.Orchid;
    private readonly SKColor _bg0DrawColor = SKColors.DarkOrchid.WithAlpha(0);   // Any color with alpha 0, will make sure _bg0DrawColorActual is used as background color (replace in shader with _bg0DrawColorActual)
    private readonly SKColor _bg1DrawColor = SKColors.DarkOliveGreen;
    private readonly SKColor _bg2DrawColor = SKColors.DarkMagenta;
    private readonly SKColor _bg3DrawColor = SKColors.DarkOrange;

    private readonly SKColor _borderDrawColor = SKColors.DarkKhaki;

    private const byte LOW_PRIO_SPRITE_BLUE = 51;   // 51 translates to exactly 0.2 in this texture shader (51/255 = 0.2)
    private const float LOW_PRIO_SPRITE_BLUE_SHADER = LOW_PRIO_SPRITE_BLUE / 255.0f; // Shader uses 0-1 float values
    private const byte HIGH_PRIO_SPRITE_BLUE = 255; // 255 translates to exactly 1.0 in this texture shader (255/255 = 1.0)
    private const float HIGH_PRIO_SPRITE_BLUE_SHADER = HIGH_PRIO_SPRITE_BLUE / 255.0f;  // Shader uses 0-1 float values

    private readonly SKColor _spriteLowPrioMultiColor0 = new SKColor(red: 200, green: 200, blue: LOW_PRIO_SPRITE_BLUE);
    private readonly SKColor _spriteLowPrioMultiColor1 = new SKColor(red: 210, green: 210, blue: LOW_PRIO_SPRITE_BLUE);
    private readonly SKColor _spriteHighPrioMultiColor0 = new SKColor(red: 200, green: 200, blue: HIGH_PRIO_SPRITE_BLUE);
    private readonly SKColor _spriteHighPrioMultiColor1 = new SKColor(red: 210, green: 210, blue: HIGH_PRIO_SPRITE_BLUE);

    // Sprite 0 - 7. Low prio colors have Blue value of 51. Rest of the colors is used to distinguish the sprite.
    private readonly SKColor[] _spriteLowPrioColors = new SKColor[]
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
    private readonly SKColor[] _spriteHighPrioColors = new SKColor[]
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

    // Values per raster line data to send to shader
    private enum ShaderLineData : int
    {
        Bg0_Color,  // Index starts at 0
        Bg1_Color,
        Bg2_Color,
        Bg3_Color,
        Border_Color,
        SpriteMultiColor0,
        SpriteMultiColor1,
        Sprite0_Color,
        Sprite1_Color,
        Sprite2_Color,
        Sprite3_Color,
        Sprite4_Color,
        Sprite5_Color,
        Sprite6_Color,
        Sprite7_Color,
    }

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();
    private const string StatsCategory = "SkiaSharp-Custom";
    private ElapsedMillisecondsTimedStatSystem _borderStat;
    private ElapsedMillisecondsTimedStatSystem _textAndBitmapScreenStat;
    private ElapsedMillisecondsTimedStatSystem _spritesStat;
    private ElapsedMillisecondsTimedStatSystem _lineDataImageStat;
    private ElapsedMillisecondsTimedStatSystem _drawCanvasWithShaderStat;

    public C64SkiaRenderer2()
    {
    }

    public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
    {
        _getSkCanvas = skiaRenderContext.GetCanvas;

        _c64SkiaColors = new C64SkiaColors(c64.ColorMapName);

        InitTextAndSpritesBitmap(c64);
        InitLineDataBitmap(c64);

        InitBitPatternToPixelMapsForTextDisplay(c64);
        InitBitPatternToPixelMapsForBitmapDisplay();

        InitShader(c64);

        Instrumentations.Clear();
        _borderStat = Instrumentations.Add($"{StatsCategory}-Border", new ElapsedMillisecondsTimedStatSystem(c64));
        _textAndBitmapScreenStat = Instrumentations.Add($"{StatsCategory}-Screen", new ElapsedMillisecondsTimedStatSystem(c64));
        _spritesStat = Instrumentations.Add($"{StatsCategory}-Sprites", new ElapsedMillisecondsTimedStatSystem(c64));
        _lineDataImageStat = Instrumentations.Add($"{StatsCategory}-LineDataImage", new ElapsedMillisecondsTimedStatSystem(c64));
        _drawCanvasWithShaderStat = Instrumentations.Add($"{StatsCategory}-DrawCanvasWithShader", new ElapsedMillisecondsTimedStatSystem(c64));
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SkiaRenderContext)renderContext);
    }

    public void Draw(C64 c64)
    {
        // Draw border and screen to bitmap
        DrawBorderAndScreenToBitmapBackedByPixelArray(c64, _skiaPixelArrayBitmap_TextAndBitmap.PixelArray);

        // Draw sprites to separate bitmap
        DrawSpritesToBitmapBackedByPixelArray(c64, _skiaPixelArrayBitmap_Sprites.PixelArray);

        // Write line data (color values of VIC2 registers per raster line) to separate bitmap. To be used later in the shader to determine colors per raster line.
        WriteLineDataToBitmapBackedByPixelArray(c64, _skiaPixelArrayBitmap_LineData.PixelArray);

        // Draw to a canvas using a shader with texture info from screen and sprite bitmaps, together with line data bitmap
        WriteBitmapToCanvas(_skiaPixelArrayBitmap_TextAndBitmap.Bitmap, _skiaPixelArrayBitmap_Sprites.Bitmap, _skiaPixelArrayBitmap_LineData.Bitmap, _getSkCanvas(), c64);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    public void Cleanup()
    {
        _skiaPixelArrayBitmap_TextAndBitmap.Free();
        _skiaPixelArrayBitmap_Sprites.Free();
        _skiaPixelArrayBitmap_LineData.Free();
    }

    private void InitShader(C64 c64)
    {
        // --------------------
        // Load and compile shader.
        // --------------------
        var src = LoadShaderSource("C64_sksl_shader2.frag");
        src = ReplaceShaderPlaceholders(src, c64);
        _sKRuntimeEffect = SKRuntimeEffect.CreateShader(src, out var error);
        if (!string.IsNullOrEmpty(error))
            throw new DotNet6502Exception($"Shader compilation error: {error}");

        _sKRuntimeEffectUniforms = new SKRuntimeEffectUniforms(_sKRuntimeEffect);
        _sKRuntimeEffectChildren = new SKRuntimeEffectChildren(_sKRuntimeEffect);
        _shaderPaint = new SKPaint();

        // --------------------
        // Init lookup dictionary for mapping colors 32 bit unsigned int format (from SKColor) to shader colors (float[4] with RGB and alpha values in 0.0 - 1.0 range)
        // --------------------
        InitShaderColorValueLookup();
    }

    private void InitShaderColorValueLookup()
    {
        // Map 32 bit unsigned int color values (from SKColors drawn on pixelarray/bitmap) to float[4] color values as seen in shader
        AddColorToShaderColorMap(_bg0DrawColor);
        AddColorToShaderColorMap(_bg0DrawColorActual);
        AddColorToShaderColorMap(_bg1DrawColor);
        AddColorToShaderColorMap(_bg2DrawColor);
        AddColorToShaderColorMap(_bg3DrawColor);

        AddColorToShaderColorMap(_borderDrawColor);

        AddColorToShaderColorMap(_spriteLowPrioMultiColor0);
        AddColorToShaderColorMap(_spriteLowPrioMultiColor1);

        AddColorToShaderColorMap(_spriteHighPrioMultiColor0);
        AddColorToShaderColorMap(_spriteHighPrioMultiColor1);

        foreach (var spriteColor in _spriteLowPrioColors.Union(_spriteHighPrioColors))
        {
            AddColorToShaderColorMap(spriteColor);
        }

        void AddColorToShaderColorMap(SKColor color)
        {
            _sKColorToShaderColorMap.Add((uint)color, [color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f]);
        }
    }

    private string ReplaceShaderPlaceholders(string src, C64 c64)
    {
        src = src.Replace("#VISIBLE_HEIGHT", c64.Vic2.Vic2Screen.VisibleHeight.ToString());

        var visibleMainScreenAreaNormalized = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var bitmapMainScreenStartLine = visibleMainScreenAreaNormalized.Screen.Start.Y;
        src = src.Replace("#MAIN_SCREEN_START", bitmapMainScreenStartLine.ToString());
        src = src.Replace("#MAIN_SCREEN_END", (bitmapMainScreenStartLine + c64.Vic2.Vic2Screen.DrawableAreaHeight - 1).ToString());

        src = src.Replace("#LOW_PRIO_SPRITE_BLUE_SHADER", LOW_PRIO_SPRITE_BLUE_SHADER.ToString(CultureInfo.InvariantCulture));
        src = src.Replace("#HIGH_PRIO_SPRITE_BLUE_SHADER", HIGH_PRIO_SPRITE_BLUE_SHADER.ToString(CultureInfo.InvariantCulture));

        src = src.Replace("#BG0_COLOR_INDEX", ((int)ShaderLineData.Bg0_Color).ToString());
        src = src.Replace("#BG1_COLOR_INDEX", ((int)ShaderLineData.Bg1_Color).ToString());
        src = src.Replace("#BG2_COLOR_INDEX", ((int)ShaderLineData.Bg2_Color).ToString());
        src = src.Replace("#BG3_COLOR_INDEX", ((int)ShaderLineData.Bg3_Color).ToString());

        src = src.Replace("#BORDER_COLOR_INDEX", ((int)ShaderLineData.Border_Color).ToString());

        src = src.Replace("#SPRITE_MULTICOLOR0_INDEX", ((int)ShaderLineData.SpriteMultiColor0).ToString());
        src = src.Replace("#SPRITE_MULTICOLOR1_INDEX", ((int)ShaderLineData.SpriteMultiColor1).ToString());

        src = src.Replace("#SPRITE0_COLOR_INDEX", ((int)ShaderLineData.Sprite0_Color).ToString());
        src = src.Replace("#SPRITE1_COLOR_INDEX", ((int)ShaderLineData.Sprite1_Color).ToString());
        src = src.Replace("#SPRITE2_COLOR_INDEX", ((int)ShaderLineData.Sprite2_Color).ToString());
        src = src.Replace("#SPRITE3_COLOR_INDEX", ((int)ShaderLineData.Sprite3_Color).ToString());
        src = src.Replace("#SPRITE4_COLOR_INDEX", ((int)ShaderLineData.Sprite4_Color).ToString());
        src = src.Replace("#SPRITE5_COLOR_INDEX", ((int)ShaderLineData.Sprite5_Color).ToString());
        src = src.Replace("#SPRITE6_COLOR_INDEX", ((int)ShaderLineData.Sprite6_Color).ToString());
        src = src.Replace("#SPRITE7_COLOR_INDEX", ((int)ShaderLineData.Sprite7_Color).ToString());
        return src;
    }

    private string LoadShaderSource(string shaderFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{"Highbyte.DotNet6502.Impl.Skia.Resources.Shaders"}.{shaderFileName}";
        using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
        {
            if (resourceStream == null)
                throw new ArgumentException($"Cannot load shader from embedded resource. Resource: {resourceName}", nameof(shaderFileName));

            // Read contents of stream to string
            using var reader = new StreamReader(resourceStream);
            return reader.ReadToEnd();
        }
    }

    private void InitTextAndSpritesBitmap(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;

        // Init pixel array to associate with a SKBitmap that is written to a SKCanvas
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Array/Bitmap for C64 text and bitmap pixels (border + main screen), excluding sprites
        _skiaPixelArrayBitmap_TextAndBitmap = SkiaBitmapBackedByPixelArray.Create(width, height);

        // Array/Bitmap for C64 sprites
        _skiaPixelArrayBitmap_Sprites = SkiaBitmapBackedByPixelArray.Create(width, height);
    }

    private void InitBitPatternToPixelMapsForTextDisplay(C64 c64)
    {
        // Create 8 precalculated pixels for each 8 bit pattern of text display, per possible background color.
        // 
        // The background colors:
        //   - values precalculated here are just placeholders (not the actual colors).
        //  -  will be replaced in the shader by corresponding C64 colors used per scan line.
        // 
        // The foreground color values here are the actual colors used in the bitmap, will not be replaced in the shader.

        var vic2 = c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;
        var width = vic2Screen.VisibleWidth;

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

        var bg0ColorVal = (uint)_bg0DrawColor; // Note the background color _bg0DrawColor is hardcoded, and will be replaced by shader
        var bg1ColorVal = (uint)_bg1DrawColor; // Note the background color _bg1DrawColor is hardcoded, and will be replaced by shader
        var bg2ColorVal = (uint)_bg2DrawColor; // Note the background color _bg2DrawColor is hardcoded, and will be replaced by shader
        var bg3ColorVal = (uint)_bg3DrawColor; // Note the background color _bg3DrawColor is hardcoded, and will be replaced by shader

        // Text (Standard and Extended mode) (8 bits -> 8 pixels)
        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte fgColorCode = 0; fgColorCode < 16; fgColorCode++)
            {
                var fgColorVal = (uint)_c64SkiaColors.C64ToSkColorMap[fgColorCode];

                var bitmapPixelsBg0 = new uint[8];
                var bitmapPixelsBg1 = new uint[8];
                var bitmapPixelsBg2 = new uint[8];
                var bitmapPixelsBg3 = new uint[8];

                // Loop each bit in pixelPattern
                for (var pixelPos = 0; pixelPos < 8; pixelPos++)
                {
                    // If bit is set, use foreground color, else use background color
                    var isBitSet = (pixelPattern & 1 << 7 - pixelPos) != 0;
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

        // Text Multicolor mode, double pixel color (8 bits -> 4 pixels)
        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            // Only the lower 3 bits are used for foreground color from Color RAM, so only colors 0-7 are possible.
            for (byte fgColorCode = 0; fgColorCode < 8; fgColorCode++)
            {
                var fgColorVal = (uint)_c64SkiaColors.C64ToSkColorMap[fgColorCode];

                var bitmapPixelsMultiColor = new uint[8];

                // Loop each multi-color pixel pair (4 pixel pairs)
                var mask = 0b11000000;
                for (var pixel = 0; pixel < 4; pixel++)
                {
                    var pixelPair = (pixelPattern & mask) >> 6 - pixel * 2;
                    var pairColorVal = pixelPair switch
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

    private void InitBitPatternToPixelMapsForBitmapDisplay()
    {
        // Create 8 precalculated pixels for each 8 bit pattern of bitmap display, per possible background/foreground color combination.
        // None of these colors will NOT be replaced in the shader, because these colors come text and color RAM areas (and not from special VIC2 color registers).
        // Thus the colors precalcualted here will be the actual colors used in the bitmap.


        var bg0ColorVal = (uint)_bg0DrawColor; // Note the background color _bg0DrawColor is hardcoded, and will be replaced by shader

        _bitmapEightHiresPixelsActual = new();
        _bitmapEightMulticolorPixelsActual = new(); 

        // Bitmap Standard (Hires) mode, 8 bits => 8 pixels
        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte bitmapBgColorCode = 0; bitmapBgColorCode < 16; bitmapBgColorCode++)
            {
                var bitmapBgColorVal = (uint)_c64SkiaColors.C64ToSkColorMap[bitmapBgColorCode];

                for (byte bitmapFgColorCode = 0; bitmapFgColorCode < 16; bitmapFgColorCode++)
                {
                    var bitmapFgColorVal = (uint)_c64SkiaColors.C64ToSkColorMap[bitmapFgColorCode];

                    // Standard (Hires) mode, 8 bits => 8 pixels. 2 "foreground" colors (fg color and bg color from text screen). No background color that will be replaced in shader.
                    var bitmapPixels = new uint[8];
                    for (var pixelPos = 0; pixelPos < 8; pixelPos++)
                    {
                        // If bit is set, use foreground color, else use background color
                        var isBitSet = (pixelPattern & 1 << 7 - pixelPos) != 0;
                        if (isBitSet)
                            bitmapPixels[pixelPos] = bitmapFgColorVal;
                        else
                            bitmapPixels[pixelPos] = bitmapBgColorVal;
                    }
                    _bitmapEightHiresPixelsActual.Add(((byte)pixelPattern, bitmapBgColorCode, bitmapFgColorCode), bitmapPixels);


                    // Multicolor mode, 8 bits => 4 pixels. 3 "foreground" colors (bg, fg, and extra from color ram) + normal screen background color (which will be replaced in shader)
                    for (byte colorRamCode = 0; colorRamCode < 16; colorRamCode++)
                    {
                        var colorRamVal = (uint)_c64SkiaColors.C64ToSkColorMap[colorRamCode];

                        var bitmapMulicolorPixels = new uint[8];

                        // Loop each multi-color pixel pair (4 pixel pairs)
                        var mask = 0b11000000;
                        // Pixel pattern 00 => screen bg color
                        // Pixel pattern 01 (multi color 1) => bitmap fg color (from text screen high 4 bits)
                        // Pixel pattern 10 (multi color 2) => bitmap bg color (from text screen low 4 bits)
                        // Pixel pattern 11 (multi color 3) => color RAM color (for corresponding position in text screen)
                        for (var pixel = 0; pixel < 4; pixel++)
                        {
                            var pixelPair = (pixelPattern & mask) >> 6 - pixel * 2;
                            var pairColorVal = pixelPair switch
                            {
                                0b00 => bg0ColorVal,
                                0b01 => bitmapFgColorVal,
                                0b10 => bitmapBgColorVal,
                                0b11 => colorRamVal,
                                _ => throw new DotNet6502Exception("Invalid pixel pair value.")
                            };
                            mask = mask >> 2;
                            bitmapMulicolorPixels[pixel * 2] = pairColorVal;
                            bitmapMulicolorPixels[pixel * 2 + 1] = pairColorVal;
                        }

                        _bitmapEightMulticolorPixelsActual.Add(((byte)pixelPattern, bitmapBgColorCode, bitmapFgColorCode, colorRamCode), bitmapMulicolorPixels);

                    }
                }
            }
        }
    }

    private void InitLineDataBitmap(C64 c64)
    {
        // Line data to send to shader in form of a texture
        _skiaPixelArrayBitmap_LineData = SkiaBitmapBackedByPixelArray.Create(Enum.GetNames(typeof(ShaderLineData)).Length, c64.Vic2.Vic2Screen.VisibleHeight);
    }

    private void WriteBitmapToCanvas(SKBitmap bitmap, SKBitmap spritesBitmap, SKBitmap lineDataBitmap, SKCanvas canvas, C64 c64)
    {

        _drawCanvasWithShaderStat.Start();
        // shader uniform values
        _sKRuntimeEffectUniforms["bg0Color"] = _sKColorToShaderColorMap[(uint)_bg0DrawColorActual];
        _sKRuntimeEffectUniforms["bg1Color"] = _sKColorToShaderColorMap[(uint)_bg1DrawColor];
        _sKRuntimeEffectUniforms["bg2Color"] = _sKColorToShaderColorMap[(uint)_bg2DrawColor];
        _sKRuntimeEffectUniforms["bg3Color"] = _sKColorToShaderColorMap[(uint)_bg3DrawColor];

        _sKRuntimeEffectUniforms["borderColor"] = _sKColorToShaderColorMap[(uint)_borderDrawColor];

        _sKRuntimeEffectUniforms["spriteLowPrioMultiColor0"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioMultiColor0];
        _sKRuntimeEffectUniforms["spriteLowPrioMultiColor1"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioMultiColor1];

        _sKRuntimeEffectUniforms["spriteHighPrioMultiColor0"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioMultiColor0];
        _sKRuntimeEffectUniforms["spriteHighPrioMultiColor1"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioMultiColor1];

        _sKRuntimeEffectUniforms["sprite0LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[0]];
        _sKRuntimeEffectUniforms["sprite1LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[1]];
        _sKRuntimeEffectUniforms["sprite2LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[2]];
        _sKRuntimeEffectUniforms["sprite3LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[3]];
        _sKRuntimeEffectUniforms["sprite4LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[4]];
        _sKRuntimeEffectUniforms["sprite5LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[5]];
        _sKRuntimeEffectUniforms["sprite6LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[6]];
        _sKRuntimeEffectUniforms["sprite7LowPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteLowPrioColors[7]];

        _sKRuntimeEffectUniforms["sprite0HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[0]];
        _sKRuntimeEffectUniforms["sprite1HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[1]];
        _sKRuntimeEffectUniforms["sprite2HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[2]];
        _sKRuntimeEffectUniforms["sprite3HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[3]];
        _sKRuntimeEffectUniforms["sprite4HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[4]];
        _sKRuntimeEffectUniforms["sprite5HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[5]];
        _sKRuntimeEffectUniforms["sprite6HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[6]];
        _sKRuntimeEffectUniforms["sprite7HighPrioColor"] = _sKColorToShaderColorMap[(uint)_spriteHighPrioColors[7]];

        // Shader uniform texture sampling values
        // Convert bitmap (that one have written the C64 screen to) to shader texture
        using var shaderTexture = bitmap.ToShader();
        // Convert shader bitmap (that one have written the C64 sprites to) to shader texture
        using var spritesTexture = spritesBitmap.ToShader();

        // Convert other bitmaps to shader texture
        using var lineDataBitmapShaderTexture = lineDataBitmap.ToShader();

        _sKRuntimeEffectChildren["bitmap_texture"] = shaderTexture;
        _sKRuntimeEffectChildren["sprites_texture"] = spritesTexture;
        _sKRuntimeEffectChildren["line_data_map"] = lineDataBitmapShaderTexture;

        using var shader = _sKRuntimeEffect.ToShader(_sKRuntimeEffectUniforms, _sKRuntimeEffectChildren);
        _shaderPaint.Shader = shader;

        canvas.DrawRect(0, 0, bitmap.Width, bitmap.Height, _shaderPaint);

        _drawCanvasWithShaderStat.Stop();
    }

    private void DrawBorderAndScreenToBitmapBackedByPixelArray(C64 c64, uint[] pixelArray)
    {
        _textAndBitmapScreenStat.Start();

        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;
        var bitmapManager = vic2.BitmapManager;

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalized = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // Main screen draw area for characters, with consideration to possible 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalizedClipped = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized);

        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        var drawableAreaHeight = vic2Screen.DrawableAreaHeight;

        // Main screen, copy 8 pixels at a time
        // Copy settings used in loop to local variables to increase performance
        var vic2VideoMatrixBaseAddress = vic2.VideoMatrixBaseAddress;
        var vic2BitmapBaseAddress = bitmapManager.BitmapAddressInVIC2Bank;

        var vic2ScreenTextCols = vic2Screen.TextCols;
        var screenStartY = visibleMainScreenAreaNormalized.Screen.Start.Y;
        var screenStartX = visibleMainScreenAreaNormalized.Screen.Start.X;
        var vic2CharacterSetAddressInVIC2Bank = vic2.CharsetManager.CharacterSetAddressInVIC2Bank;
        var vic2ScreenCharacterHeight = vic2.Vic2Screen.CharacterHeight;

        var vic2Is38ColumnDisplayEnabled = vic2.Is38ColumnDisplayEnabled;
        var vic2Is24RowDisplayEnabled = vic2.Is24RowDisplayEnabled;
        var vic2LineStart24Rows = visibleMainScreenAreaNormalizedClipped.Screen.Start.Y - visibleMainScreenAreaNormalized.Screen.Start.Y;
        var vic2LineEnd24Rows = visibleMainScreenAreaNormalizedClipped.Screen.End.Y - visibleMainScreenAreaNormalized.Screen.Start.Y;

        var isTextMode = vic2.DisplayMode == DispMode.Text; // TODO: Check for display mode more than once per frame? How?
        var characterMode = vic2.CharacterMode; // TODO: Check for display mode more than once per frame? How?
        var bitmapMode = vic2.BitmapMode; // TODO: Check for bitmap mode more than once per frame? How?
        var scrollX = vic2.GetScrollX(); // TODO: Check fine scroll more than once per frame? How?
        var scrollY = vic2.GetScrollY(); // TODO: Check fine scroll more than once per frame? How?

        // Loop each row line on main text/gfx screen, starting with line 0.
        for (ushort drawLine = 0; drawLine < drawableAreaHeight; drawLine++)
        {
            // Calculate the y position in the bitmap where the 8 pixels should be drawn
            var skiaBitmapY = screenStartY + drawLine;

            var characterRow = drawLine / 8;
            ushort characterLine = (ushort)(drawLine % 8);

            ushort characterAddress = (ushort)(vic2VideoMatrixBaseAddress + (characterRow * vic2ScreenTextCols));
            ushort colorRamAddress = (ushort)(Vic2Addr.COLOR_RAM_START + (characterRow * vic2ScreenTextCols));
            ushort c64BitMapAddress = (ushort)(vic2BitmapBaseAddress + (characterRow * vic2ScreenTextCols * 8) + characterLine);

            // Loop each column on main text/gfx screen, starting with column 0.
            for (var col = 0; col < vic2ScreenTextCols; col++)
            {

                // Determine character code at current position from video matrix
                var characterCode = vic2Mem[characterAddress];
                var colorRamCode = c64.ReadIOStorage(colorRamAddress);

                // Calculate the x position in the bitmap where the 8 pixels should be drawn
                var skiaBitmapX = screenStartX + col * 8;

                uint[] eightPixels;
                if (isTextMode)
                {

                    // Determine colors
                    var fgColorCode = colorRamCode;
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
                        + characterCode * vic2ScreenCharacterHeight
                        + characterLine);
                    var lineData = vic2Mem[characterSetLineAddress];

                    // Get pre-calculated 8 pixels that should be drawn on the bitmap, with correct colors for foreground and background
                    if (characterMode == CharMode.Standard || characterMode == CharMode.Extended)
                    {
                        // Get the corresponding array of uints representing the 8 pixels of the character
                        if (bgColorNumber == 0)
                        {
                            eightPixels = _bitmapEightPixelsBg0Map[(lineData, fgColorCode)];
                        }
                        else if (bgColorNumber == 1)
                        {
                            eightPixels = _bitmapEightPixelsBg1Map[(lineData, fgColorCode)];
                        }
                        else if (bgColorNumber == 2)
                        {
                            eightPixels = _bitmapEightPixelsBg2Map[(lineData, fgColorCode)];
                        }
                        else if (bgColorNumber == 3)
                        {
                            eightPixels = _bitmapEightPixelsBg3Map[(lineData, fgColorCode)];
                        }
                        else
                        {
                            throw new NotImplementedException($"Background color number {bgColorNumber} not implemented.");
                        }
                    }
                    else // Asume text multicolor mode
                    {
                        // Text multicolor mode color usage (8 bits, 4 pixel pairs)
                        // backgroundColor0 = the color of pixel-pair 00
                        // backgroundColor1 = the color of pixel-pair 01
                        // backgroundColor2 = the color of pixel-pair 10
                        // fgColorCode      = the color of pixel-pair 11

                        // Get the corresponding array of uints representing the 8 pixels of the character
                        eightPixels = _bitmapEightPixelsMultiColorMap[(lineData, fgColorCode)];
                    }
                }
                else
                {
                    // Assume bitmap mode

                    // 8 bits of bitmap data for the current line, at the current column
                    var bitmapLineData = vic2Mem[c64BitMapAddress];

                    // Bg color is picked from text screen, low 4 bits.
                    byte bitmapBgColorCode = (byte)(characterCode & 0b00001111);
                    // Fg color is picked from text screen, high 4 bits.
                    byte bitmapFgColorCode = (byte)((characterCode & 0b11110000) >> 4);

                    if (bitmapMode == BitmMode.Standard)
                    {
                        // Bitmap Standard (HiRes) mode, 8 bits => 8 pixels
                        // ----------
                        // Pixel not set (bit = 0) => bitmap bg color (from text screen low 4 bits)
                        // Pixel set (bit = 1) => bitmap fg color
                        eightPixels = _bitmapEightHiresPixelsActual[(bitmapLineData, bitmapBgColorCode, bitmapFgColorCode)];
                    }
                    else
                    {
                        // Bitmap Multi color mode, 8 bits => 4 pixels
                        // ----------
                        // Pixel pattern 00 => screen bg color
                        // Pixel pattern 01 (multi color 1) => bitmap fg color (from text screen high 4 bits)
                        // Pixel pattern 10 (multi color 2) => bitmap bg color (from text screen low 4 bits)
                        // Pixel pattern 11 (multi color 3) => color RAM color (for corresponding position in text screen)
                        eightPixels = _bitmapEightMulticolorPixelsActual[(bitmapLineData, bitmapBgColorCode, bitmapFgColorCode, colorRamCode)];
                    }
                }

                // Add additional drawing to compensate for horizontal scrolling
                if (scrollX > 0 && col == 0)
                    // Fill start of column 0 with background color (the number of x pixels scrolled)
                    // Array.Copy(_oneCharLineBg0Pixels, 0, pixelArray, bitmapIndex, scrollX);
                    WriteToPixelArray(_oneCharLineBg0Pixels, pixelArray, drawLine, 0, fnLength: scrollX, fnAdjustForScrollX: false, fnAdjustForScrollY: true);

                // Add additional drawing to compensate for horizontal scrolling
                // Note: The actual vic2 vertical scroll has 3 as default value (no scroll), but the scrollY variable is adjusted to be between -3 and + 4 with default 0 when no scrolling.
                if (scrollY != 0)
                {
                    // If scrolling occured upwards (scrollY < 0) and we are on last line of screen, fill remaining lines with background color
                    if (scrollY < 0 && drawLine == drawableAreaHeight - 1)
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
                WriteToPixelArray(eightPixels, pixelArray, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true, fnAdjustForScrollY: true);


                void WriteToPixelArray(uint[] fnEightPixels, uint[] fnPixelArray, int fnMainScreenY, int fnMainScreenX, int fnLength, bool fnAdjustForScrollX, bool fnAdjustForScrollY)
                {
                    // Draw 8 pixels (or less) of character on the the pixel array part used for the C64 drawable screen (320x200)
                    var lCol = fnMainScreenX / 8;

                    if (fnAdjustForScrollY)
                    {
                        // Skip draw entirely if y position is outside drawable screen
                        if (fnMainScreenY + scrollY < 0 || fnMainScreenY + scrollY >= vic2Screen.DrawableAreaHeight)
                            return;

                        fnMainScreenY += scrollY;
                    }

                    if (fnAdjustForScrollX)
                    {
                        fnMainScreenX += scrollX;
                        if (lCol == vic2ScreenTextCols - 1) // Adjust drawing of last character on line to clip when it reaches the right border
                            fnLength = 8 - scrollX;
                    }

                    // Calculate the position in the bitmap where the 8 pixels should be drawn
                    var lBitmapIndex = (screenStartY + fnMainScreenY) * width + screenStartX + fnMainScreenX;


                    // Copy array with Span
                    // - Seems to be a bit faster on .NET 8 WASM than Array.Copy and Buffer.BlockCopy.
                    // - TODO: Is the extra heap memory allocation of Span objects (which leads to GC pressure) worth the performance gain?
                    var source = new ReadOnlySpan<uint>(fnEightPixels, 0, fnLength);
                    var target = new Span<uint>(fnPixelArray, lBitmapIndex, fnLength);
                    source.CopyTo(target);

                    // Or Copy array with Array.Copy
                    //Array.Copy(fnEightPixels, 0, fnPixelArray, lBitmapIndex, fnLength);

                    // Or Copy array with Buffer.BlockCopy
                    //Buffer.BlockCopy(fnEightPixels, 0, fnPixelArray, lBitmapIndex * 4, fnLength * 4);   // Note: Buffer.BlockCopy uses byte size, so multiply by 4 to get uint size
                }

                characterAddress++;
                colorRamAddress++;
                c64BitMapAddress += 8;

            } // Next column
        } // Next line

        _textAndBitmapScreenStat.Stop();


        // Borders.
        _borderStat.Start();
        // The borders must be drawn last, because it will overwrite parts of the main screen area if 38 column or 24 row modes are enabled (which main screen drawing above does not take in consideration)
        var borderStartY = 0;

        // Assumption on visibleMainScreenAreaNormalizedClipped:
        // - Contains dimensions of screen parts with consideration to if 38 column mode or 24 row mode is enabled.
        // - Is normalized to start at 0,0 (i.e. TopBorder.Start.X = and TopBorder.Start.Y = 0)
        var leftBorderStartX = visibleMainScreenAreaNormalizedClipped.LeftBorder.Start.X; // Should be 0?
        var leftBorderLength = visibleMainScreenAreaNormalizedClipped.LeftBorder.End.X - visibleMainScreenAreaNormalizedClipped.LeftBorder.Start.X + 1;

        var rightBorderStartX = visibleMainScreenAreaNormalizedClipped.RightBorder.Start.X;
        var rightBorderLength = width - visibleMainScreenAreaNormalizedClipped.RightBorder.Start.X;

        for (var y = borderStartY; y < borderStartY + height; y++)
        {
            // Top or bottom border
            if (y <= visibleMainScreenAreaNormalizedClipped.TopBorder.End.Y || y >= visibleMainScreenAreaNormalizedClipped.BottomBorder.Start.Y)
            {
                var topBottomBorderLineStartIndex = y * width;
                Array.Copy(_oneLineBorderPixels, 0, pixelArray, topBottomBorderLineStartIndex, width);
                continue;
            }

            // Left border
            var lineStartIndex = y * width;
            Array.Copy(_oneLineBorderPixels, leftBorderStartX, pixelArray, lineStartIndex, leftBorderLength);
            // Right border
            lineStartIndex += visibleMainScreenAreaNormalizedClipped.RightBorder.Start.X;
            Array.Copy(_oneLineBorderPixels, rightBorderStartX, pixelArray, lineStartIndex, rightBorderLength);
        }

        _borderStat.Stop();
    }

    private void DrawSpritesToBitmapBackedByPixelArray(C64 c64, uint[] spritesPixelArray)
    {
        // Main screen, copy 8 pixels at a time
        _spritesStat.Start();

        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;
        var vic2SpriteManager = vic2.SpriteManager;

        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;


        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // TODO: Is it faster to track previous frame sprite draw positions, and only clear those pixels instead?
        Array.Clear(spritesPixelArray);

        // Write sprites to a separate bitmap/pixel array
        foreach (var sprite in c64.Vic2.SpriteManager.Sprites.OrderByDescending(s => s.SpriteNumber))
        {
            if (!sprite.Visible)
                continue;

            var spriteScreenPosX = sprite.X + visibleMainScreenArea.Screen.Start.X - vic2SpriteManager.ScreenOffsetX;
            var spriteScreenPosY = sprite.Y + visibleMainScreenArea.Screen.Start.Y - vic2SpriteManager.ScreenOffsetY;
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
            var y = 0;
            foreach (var spriteRow in sprite.Data.Rows)
            {
                // Loop each 8-bit part of the sprite line (3 bytes, 24 pixels).
                var x = 0;
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
                            if ((spriteLinePart & maskMultiColor1Mask) == maskMultiColor1Mask)
                            {
                                spriteColor = spriteMultiColor1PixelColor;
                            }
                            else if ((spriteLinePart & maskSpriteColorMask) == maskSpriteColorMask)
                            {
                                spriteColor = spriteForegroundPixelColor;
                            }
                            else if ((spriteLinePart & maskMultiColor0Mask) == maskMultiColor0Mask)
                            {
                                spriteColor = spriteMultiColor0PixelColor;
                            }
                            else
                            {
                                spriteColor = 0;
                            }

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
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y, spriteForegroundPixelColor, priorityOverForground);

                                if (isDoubleHeight)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y + 1, spriteForegroundPixelColor, priorityOverForground);
                                    if (isDoubleWidth)
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y + 1, spriteForegroundPixelColor, priorityOverForground);
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
                if (screenPosX < 0 || screenPosX >= width || screenPosY < 0 || screenPosY > height)
                    return;

                // Check if pixel is within side borders, and if it should be shown there or not.
                // TODO: Detect if side borders are open? How to?
                var openSideBorders = false;
                if (!openSideBorders && (screenPosX < visibleMainScreenArea.Screen.Start.X || screenPosX > visibleMainScreenArea.Screen.End.X))
                    return;

                // Check if pixel is within top/bottom borders, and if it should be shown there or not.
                // TODO: Detect if top/bottom borders are open? How to?
                var openTopBottomBorders = false;
                if (!openTopBottomBorders && (screenPosY < visibleMainScreenArea.Screen.Start.Y || screenPosY > visibleMainScreenArea.Screen.End.Y))
                    return;

                // Calculate the position in the bitmap where the pixel should be drawn
                var bitmapIndex = screenPosY * width + screenPosX;

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
        }

        _spritesStat.Stop();
    }

    private void WriteLineDataToBitmapBackedByPixelArray(C64 c64, uint[] lineDataPixelArray)
    {
        // Build array to send to shader with the actual color that should be used differnt types of colors (border, bg0, bg1, bg2, bg3, etc), dependent on the raster line number
        _lineDataImageStat.Start();

        var c64ScreenLineIORegisterValues = c64.Vic2.ScreenLineIORegisterValues;
        var visibleMainScreenArea = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.Visible);
        var drawableScreenStartY = visibleMainScreenArea.Screen.Start.Y;
        var drawableScreenEndY = drawableScreenStartY + c64.Vic2.Vic2Screen.DrawableAreaHeight;

        var shaderLineDataValuePerLine = Enum.GetNames(typeof(ShaderLineData)).Length;
        foreach (var lineData in c64ScreenLineIORegisterValues)
        {
            // Check if in total visisble area, because c64ScreenLineIORegisterValues includes non-visible lines
            if (lineData.Key < visibleMainScreenArea.TopBorder.Start.Y || lineData.Key > visibleMainScreenArea.BottomBorder.End.Y)
                continue;

            var bitmapLine = lineData.Key - visibleMainScreenArea.TopBorder.Start.Y;

            var pixelArrayIndex = bitmapLine * shaderLineDataValuePerLine;

            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Border_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BorderColor];

            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.SpriteMultiColor0] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.SpriteMultiColor0];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.SpriteMultiColor1] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.SpriteMultiColor1];

            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite0_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite0Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite1_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite1Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite2_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite2Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite3_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite3Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite4_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite4Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite5_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite5Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite6_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite6Color];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Sprite7_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.Sprite7Color];

            // Check if line is within main screen area, only there are background colors used (? is that really true when borders are open??)
            if (lineData.Key < drawableScreenStartY || bitmapLine >= drawableScreenEndY)
                continue;

            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg0_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor0];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg1_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor1];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg2_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor2];
            lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg3_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor3];
        }

        _lineDataImageStat.Stop();
    }
}
