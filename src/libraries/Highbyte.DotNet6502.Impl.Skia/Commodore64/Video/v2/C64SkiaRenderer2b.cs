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

public class C64SkiaRenderer2b : IRenderer<C64, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas = default!;

    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_BackgroundAndBorder;
    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_Foreground;
    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_LineData;

    private SKRuntimeEffect _sKRuntimeEffect; // Shader
    private SKRuntimeEffectUniforms _sKRuntimeEffectUniforms; // Shader uniforms
    private SKRuntimeEffectChildren _sKRuntimeEffectChildren; // Shader children (textures)
    private SKPaint _shaderPaint;

    // Lookup table for mapping C64 colors to shader colors
    private readonly Dictionary<uint, float[]> _sKColorToShaderColorMap = new Dictionary<uint, float[]>();
    private C64SkiaColors _c64SkiaColors;

    // Pre-calculated pixel arrays
    private Dictionary<byte, uint[]> _oneLineSameColorPixels; // pixelArray


    // Text standard mode: 8-bit patterns mapped to 8 pixels (1 pixel = 1 uint rgba).
    // 1 maps to the color in the lookup table, and 0 maps to a predefined "background" color that will be replaced in shader.
    private Dictionary<(byte eightPixels, byte color1), uint[]> _eightPixelsOneColorAndBackground;

    // Text extended and bitmap "Standard" (HiRes) mode: 8-bit patterns mapped to 8 pixels (1 pixel = 1 uint rgba).
    // 1 and 0 maps to the two colors in the lookup table.
    private Dictionary<(byte eightPixels, byte color0, byte color1), uint[]> _eightPixelsTwoColors;

    // For text and bitmap mode "Multicolor": 8-bit patterns mapped to 4 width 2 pixels (1 pixel = 1 uint rgba).
    // 01, 10, and 11 maps to the colors in the lookup table, and 00 maps to a predefined "background" color that will be replaced in shader.
    private Dictionary<(byte eightPixels, byte color1, byte color2, byte color3), uint[]> _eightPixelsThreeColorsAndBackground;

    private readonly SKColor _transparentColor = SKColors.DarkMagenta; // SKColors.Black.WithAlpha(0);

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
    private ElapsedMillisecondsTimedStatSystem _spritesStat;
    private ElapsedMillisecondsTimedStatSystem _lineDataImageStat;
    private ElapsedMillisecondsTimedStatSystem _drawCanvasWithShader;


    // Keep track of C64 data that should update each new line
    private int _lastScreenLineDataUpdate = -1;
    private ulong _lastCyclesConsumedCurrentVblank;


    // Copies of C64 screen values that should'nt change
    private int _screenLayoutInclNonVisibleScreenStartX;
    private int _screenLayoutInclNonVisibleScreenStartY;
    private int _screenLayoutInclNonVisibleScreenEndX;
    private int _screenLayoutInclNonVisibleScreenEndY;
    private int _vic2ScreenTextCols;

    private int _screenStartY;
    private int _screenStartX;

    private int _topBorderStartX;
    private int _topBorderStartY;
    private int _topBorderEndX;
    private int _topBorderEndY;

    private int _bottomBorderStartX;
    private int _bottomBorderStartY;
    private int _bottomBorderEndX;
    private int _bottomBorderEndY;

    private int _leftBorderStartX;
    private int _leftBorderStartY;
    private int _leftBorderEndX;
    private int _leftBorderEndY;

    private int _rightBorderStartX;
    private int _rightBorderStartY;
    private int _rightBorderEndX;
    private int _rightBorderEndY;

    private int _vic2ScreenCharacterHeight;
    private int _width;
    private int _height;
    private int _drawableAreaHeight;
    private int _drawableAreaWidth;
    private ulong _cyclesPerLine;
    private ushort _vic2VideoMatrixBaseAddress;
    private ushort _vic2BitmapBaseAddress;
    private ushort _vic2CharacterSetAddressInVIC2Bank;
    private bool _isTextMode;
    private CharMode _characterMode;
    private BitmMode _bitmapMode;
    private int _scrollX;
    private int _scrollY;

    private byte _borderColor;
    private byte _backgroundColor0;
    private byte _backgroundColor1;
    private byte _backgroundColor2;
    private byte _backgroundColor3;

    private bool _is38ColModeEnabled;
    private bool _is24RowModeEnabled;

    private int _leftBorderEndXAdjusted;
    private int _leftBorderLengthAdjusted;
    private int _rightBorderStartXAdjusted;
    private int _rightBorderLengthAdjusted;
    private int _topBorderEndYAdjusted;
    private int _bottomBorderStartYAdjusted;
    private int _screenStartXAdjusted;

    private int _screenLayoutInclNonVisibleTopBorderStartY;
    private int _screenLayoutInclNonVisibleBottomBorderEndY;
    private int _screenLayoutInclNonVisibleLeftBorderStartX;
    private int _screenLayoutInclNonVisibleRightBorderEndX;

    public C64SkiaRenderer2b()
    {
    }

    public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
    {
        c64.SetAfterInstructionCallback(AfterInstructionExecuted);
        c64.RememberVic2RegistersPerRasterLine = true; // Set to false if/when sprites are drawn directly to the screen bitmap in the "after instruction" callback here.

        // Init class variables with C64 screen values that should'nt change

        // Entire screen area, including non-visible parts. Without consideration to 38 column mode or 24 row mode.
        var screenLayoutInclNonVisible = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.Visible, for24RowMode: false, for38ColMode: false); // Full area of raster lines, including non-visible. Borders don't start at 0,0

        _screenLayoutInclNonVisibleTopBorderStartY = screenLayoutInclNonVisible.TopBorder.Start.Y;
        _screenLayoutInclNonVisibleBottomBorderEndY = screenLayoutInclNonVisible.BottomBorder.End.Y;
        _screenLayoutInclNonVisibleLeftBorderStartX = screenLayoutInclNonVisible.LeftBorder.Start.X;
        _screenLayoutInclNonVisibleRightBorderEndX = screenLayoutInclNonVisible.RightBorder.End.X;

        _screenLayoutInclNonVisibleScreenStartX = screenLayoutInclNonVisible.Screen.Start.X;
        _screenLayoutInclNonVisibleScreenStartY = screenLayoutInclNonVisible.Screen.Start.Y;
        _screenLayoutInclNonVisibleScreenEndX = screenLayoutInclNonVisible.Screen.End.X;
        _screenLayoutInclNonVisibleScreenEndY = screenLayoutInclNonVisible.Screen.End.Y;

        // Entire screen area with only visible parts (borders, screen). Without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalized = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // Not considering 24 row mode or 38 col mode or fine scroll 
        _screenStartX = visibleMainScreenAreaNormalized.Screen.Start.X;
        _screenStartY = visibleMainScreenAreaNormalized.Screen.Start.Y;

        _topBorderStartX = visibleMainScreenAreaNormalized.TopBorder.Start.X;
        _topBorderStartY = visibleMainScreenAreaNormalized.TopBorder.Start.Y;
        _topBorderEndX = visibleMainScreenAreaNormalized.TopBorder.End.X;
        _topBorderEndY = visibleMainScreenAreaNormalized.TopBorder.End.Y;

        _bottomBorderStartX = visibleMainScreenAreaNormalized.BottomBorder.Start.X;
        _bottomBorderStartY = visibleMainScreenAreaNormalized.BottomBorder.Start.Y;
        _bottomBorderEndX = visibleMainScreenAreaNormalized.BottomBorder.End.X;
        _bottomBorderEndY = visibleMainScreenAreaNormalized.BottomBorder.End.Y;

        _leftBorderStartX = visibleMainScreenAreaNormalized.LeftBorder.Start.X;
        _leftBorderStartY = visibleMainScreenAreaNormalized.LeftBorder.Start.Y;
        _leftBorderEndX = visibleMainScreenAreaNormalized.LeftBorder.End.X;
        _leftBorderEndY = visibleMainScreenAreaNormalized.LeftBorder.End.Y;

        _rightBorderStartX = visibleMainScreenAreaNormalized.RightBorder.Start.X;
        _rightBorderStartY = visibleMainScreenAreaNormalized.RightBorder.Start.Y;
        _rightBorderEndX = visibleMainScreenAreaNormalized.RightBorder.End.X;
        _rightBorderEndY = visibleMainScreenAreaNormalized.RightBorder.End.Y;

        _vic2ScreenTextCols = c64.Vic2.Vic2Screen.TextCols;
        _vic2ScreenCharacterHeight = c64.Vic2.Vic2Screen.CharacterHeight;
        _width = c64.Vic2.Vic2Screen.VisibleWidth;
        _height = c64.Vic2.Vic2Screen.VisibleHeight;
        _drawableAreaHeight = c64.Vic2.Vic2Screen.DrawableAreaHeight;
        _drawableAreaWidth = c64.Vic2.Vic2Screen.DrawableAreaWidth;
        _cyclesPerLine = c64.Vic2.Vic2Model.CyclesPerLine;

        _lastScreenLineDataUpdate = -1;

        _getSkCanvas = skiaRenderContext.GetCanvas;
        _c64SkiaColors = new C64SkiaColors(c64.ColorMapName);

        InitBitmaps(c64);
        InitLineDataBitmap(c64);

        InitBitPatternToPixelMaps(c64);

        InitShader(c64);

        Instrumentations.Clear();
        _spritesStat = Instrumentations.Add($"{StatsCategory}-Sprites", new ElapsedMillisecondsTimedStatSystem(c64));
        _lineDataImageStat = Instrumentations.Add($"{StatsCategory}-LineDataImage", new ElapsedMillisecondsTimedStatSystem(c64));
        _drawCanvasWithShader = Instrumentations.Add($"{StatsCategory}-DrawCanvasWithShader", new ElapsedMillisecondsTimedStatSystem(c64));
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SkiaRenderContext)renderContext);
    }

    public void Draw(C64 c64)
    {
        // Draw sprites to background of foreground bitmaps
        DrawSpritesToBitmapBackedByPixelArray(c64, _skiaPixelArrayBitmap_BackgroundAndBorder.PixelArray, _skiaPixelArrayBitmap_Foreground.PixelArray);

        // "Draw" line data (color values of VIC2 registers per raster line) to separate bitmap
        DrawLineDataToBitmapBackedByPixelArray(c64, _skiaPixelArrayBitmap_LineData.PixelArray);

        // Draw to a canvas using a shader with texture info from screen and sprite bitmaps, together with line data bitmap
        WriteBitmapToCanvas(_skiaPixelArrayBitmap_BackgroundAndBorder.Bitmap, _skiaPixelArrayBitmap_Foreground.Bitmap, _skiaPixelArrayBitmap_LineData.Bitmap, _getSkCanvas(), c64);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    public void Cleanup()
    {
        _skiaPixelArrayBitmap_BackgroundAndBorder.Free();
        _skiaPixelArrayBitmap_Foreground.Free();
        _skiaPixelArrayBitmap_LineData.Free();
    }

    private void InitShader(C64 c64)
    {
        // --------------------
        // Load and compile shader.
        // --------------------
        var src = LoadShaderSource("C64_sksl_shader2b.frag");
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
        AddColorToShaderColorMap(_transparentColor);

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

    private void InitBitmaps(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;

        // Init pixel array to associate with a SKBitmap that is written to a SKCanvas
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Array/Bitmap for C64 background and borders
        _skiaPixelArrayBitmap_BackgroundAndBorder = SkiaBitmapBackedByPixelArray.Create(width, height);

        // Array/Bitmap for C64 foreground color from text, bitmaps, and sprites.
        _skiaPixelArrayBitmap_Foreground = SkiaBitmapBackedByPixelArray.Create(width, height);
    }

    private void InitBitPatternToPixelMaps(C64 c64)
    {
        // Create 8 precalculated pixels (with colors to be used in the shader) for each 8 bit pattern suited for C64 normal color or multicolor text/bitmap.
        // 
        // A 0 bit (or 00 bit pair) is the background color, and is set to specific color value to be checked for in the shader.
        // 

        var vic2 = c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;
        var width = vic2Screen.VisibleWidth;

        // A single line of the same color. Used for filling borders with various lengths.
        _oneLineSameColorPixels = new Dictionary<byte, uint[]>();
        for (byte colorCode = 0; colorCode < 16; colorCode++)
        {
            var colorVal = (uint)_c64SkiaColors.C64ToSkColorMap[colorCode];
            uint[] oneLine = new uint[width];
            for (var i = 0; i < oneLine.Length; i++)
                oneLine[i] = colorVal;
            _oneLineSameColorPixels[colorCode] = oneLine;
        }


        // Note: The color for "transparent" (background) will be checked for in shader when combining with background/border colors.
        var transparentColorVal = (uint)_transparentColor;

        // Text (normal) & bitmap (standard "HiRes") mode with one foreground color with a single "transparent" color as background color
        // 8 bits => 8 pixels
        _eightPixelsOneColorAndBackground = new();
        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
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
                        bitmapPixels[pixelPos] = transparentColorVal;
                }
                _eightPixelsOneColorAndBackground.Add(((byte)pixelPattern, bitmapFgColorCode), bitmapPixels);
            }
        }


        // Text extended & bitmap standard "HiRes" mode with one foreground color and a "background" color (non-transparent)
        // 8 bits => 8 pixels
        _eightPixelsTwoColors = new();

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
                    _eightPixelsTwoColors.Add(((byte)pixelPattern, bitmapBgColorCode, bitmapFgColorCode), bitmapPixels);
                }
            }
        }


        // Text multicolor & bitmap multicolor mode with one foreground color, two other colors, with a single "transparent" color as background color
        // 8 bits => 4 pixels (with length 2)
        _eightPixelsThreeColorsAndBackground = new();

        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte color1 = 0; color1 < 16; color1++)
            {
                var color1Val = (uint)_c64SkiaColors.C64ToSkColorMap[color1];

                for (byte color2 = 0; color2 < 16; color2++)
                {
                    var color2Val = (uint)_c64SkiaColors.C64ToSkColorMap[color2];

                    for (byte color3 = 0; color3 < 16; color3++)
                    {
                        var color3Val = (uint)_c64SkiaColors.C64ToSkColorMap[color3];

                        var bitmapMulicolorPixels = new uint[8];

                        // Loop each multi-color pixel pair (4 pixel pairs)
                        var mask = 0b11000000;
                        // Text multicolor pixel patterns
                        //      00 => screen bg color (transparent)
                        //      01 (multi color 1) => backgroundColor1
                        //      10 (multi color 2) => backgroundColor2
                        //      11 (multi color 3) => foreground color from color RAM.

                        // Bitmap multicolor pixel patterns
                        //      00 => screen bg color (transparent)
                        //      01 (multi color 1) => bitmap fg color (from text screen high 4 bits)
                        //      10 (multi color 2) => bitmap bg color (from text screen low 4 bits)
                        //      11 (multi color 3) => color RAM color (for corresponding position in text screen)


                        for (var pixel = 0; pixel < 4; pixel++)
                        {
                            var pixelPair = (pixelPattern & mask) >> 6 - pixel * 2;
                            var pairColorVal = pixelPair switch
                            {
                                0b00 => transparentColorVal,
                                0b01 => color1Val,
                                0b10 => color2Val,
                                0b11 => color3Val,
                                _ => throw new DotNet6502Exception("Invalid pixel pair value.")
                            };
                            mask = mask >> 2;
                            bitmapMulicolorPixels[pixel * 2] = pairColorVal;
                            bitmapMulicolorPixels[pixel * 2 + 1] = pairColorVal;
                        }
                        _eightPixelsThreeColorsAndBackground.Add(((byte)pixelPattern, color1, color2, color3), bitmapMulicolorPixels);
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

    private void WriteBitmapToCanvas(SKBitmap backgroundAndBorderBitmap, SKBitmap foregroundBitmap, SKBitmap lineDataBitmap, SKCanvas canvas, C64 c64)
    {

        _drawCanvasWithShader.Start();
        // shader uniform values
        _sKRuntimeEffectUniforms["transparentColor"] = _sKColorToShaderColorMap[(uint)_transparentColor];

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
        // Convert bitmaps to shader textures
        var backgroundAndBorderTexture = backgroundAndBorderBitmap.ToShader();
        var foregroundTexture = foregroundBitmap.ToShader();

        // Convert "data" bitmaps to shader texture
        var lineDataBitmapShaderTexture = lineDataBitmap.ToShader();

        _sKRuntimeEffectChildren["background_and_border_texture"] = backgroundAndBorderTexture;
        _sKRuntimeEffectChildren["foreground_texture"] = foregroundTexture;
        _sKRuntimeEffectChildren["line_data_map"] = lineDataBitmapShaderTexture;

        using var shader = _sKRuntimeEffect.ToShader(_sKRuntimeEffectUniforms, _sKRuntimeEffectChildren);
        _shaderPaint.Shader = shader;

        canvas.DrawRect(0, 0, backgroundAndBorderBitmap.Width, backgroundAndBorderBitmap.Height, _shaderPaint);

        _drawCanvasWithShader.Stop();
    }


    private void AfterInstructionExecuted(C64 c64, InstructionExecResult instructionExecResult)
    {

        // Loop cycles since last time we processed (each instruction)
        for (var cycleCurrentVblank = _lastCyclesConsumedCurrentVblank; cycleCurrentVblank < c64.Vic2.CyclesConsumedCurrentVblank; cycleCurrentVblank++)
        {
            // For the cycle processed in current loop iteration, get line and x position.
            // Skip if not within visible C64 border/text/bitmap area

            // Line
            var rasterLine = (int)(cycleCurrentVblank / _cyclesPerLine);
            var screenLine = c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
            if (screenLine < _screenLayoutInclNonVisibleTopBorderStartY || screenLine > _screenLayoutInclNonVisibleBottomBorderEndY)
                continue;

            // X position
            var cycleOnScreenLine = cycleCurrentVblank % _cyclesPerLine;
            int posX = ((int)(cycleOnScreenLine * 8)); // 1 cycle = 8 pixels;
            if (posX < _screenLayoutInclNonVisibleLeftBorderStartX || posX > _screenLayoutInclNonVisibleRightBorderEndX)
                continue;

            // On a new line
            if (screenLine != _lastScreenLineDataUpdate)
            {
                // Draw border once per line, after normal screen (to cover up any scrolling?). We take data from previous line.
                if (_lastScreenLineDataUpdate >= 0)
                    DrawBorderPixels(normalizedScreenLine: _lastScreenLineDataUpdate - _screenLayoutInclNonVisibleTopBorderStartY);

                if (screenLine - _screenLayoutInclNonVisibleTopBorderStartY == 0)
                {
                    // First line of screen. Clear foreground bitmap, otherwise it will contain garbage from previous frame if fine scrolling is used.
                    Array.Clear(_skiaPixelArrayBitmap_Foreground.PixelArray);
                }

                // C64 screen data is updated each line. TODO: Is this correct assumption? Can these values update mid-line?
                _vic2VideoMatrixBaseAddress = c64.Vic2.VideoMatrixBaseAddress;
                _vic2BitmapBaseAddress = c64.Vic2.BitmapManager.BitmapAddressInVIC2Bank;
                _vic2CharacterSetAddressInVIC2Bank = c64.Vic2.CharsetManager.CharacterSetAddressInVIC2Bank;

                _isTextMode = c64.Vic2.DisplayMode == DispMode.Text;
                _characterMode = c64.Vic2.CharacterMode;
                _bitmapMode = c64.Vic2.BitmapMode;
                _scrollX = c64.Vic2.GetScrollX();
                _scrollY = c64.Vic2.GetScrollY();


                _borderColor = c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);

                _backgroundColor0 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);
                _backgroundColor1 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1);
                _backgroundColor2 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2);
                _backgroundColor3 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3);

                _is38ColModeEnabled = c64.Vic2.Is38ColumnDisplayEnabled;
                _is24RowModeEnabled = c64.Vic2.Is24RowDisplayEnabled;

                _leftBorderEndXAdjusted = _leftBorderEndX + (_is38ColModeEnabled ? Vic2Screen.COL_38_LEFT_BORDER_END_X_DELTA : 0);
                _leftBorderLengthAdjusted = _leftBorderEndXAdjusted - _leftBorderStartX + 1;
                _rightBorderStartXAdjusted = _rightBorderStartX + (_is38ColModeEnabled ? Vic2Screen.COL_38_RIGHT_BORDER_START_X_DELTA : 0);
                _rightBorderLengthAdjusted = _width - _rightBorderStartXAdjusted;

                _topBorderEndYAdjusted = _topBorderEndY + (_is24RowModeEnabled ? Vic2Screen.ROW_24_TOP_BORDER_END_Y_DELTA : 0);
                _bottomBorderStartYAdjusted = _bottomBorderStartY + (_is24RowModeEnabled ? Vic2Screen.ROW_24_BOTTOM_BORDER_START_Y_DELTA : 0);

                _screenStartXAdjusted = _leftBorderEndXAdjusted + 1;

                _lastScreenLineDataUpdate = screenLine;
            }

            // Only draw main screen area (text/bitmap) if within it
            if (!(screenLine < _screenLayoutInclNonVisibleScreenStartY || screenLine > _screenLayoutInclNonVisibleScreenEndY
                || posX < _screenLayoutInclNonVisibleScreenStartX || posX > _screenLayoutInclNonVisibleScreenEndX))
            {
                DrawTextAndBitmapPixels(c64, drawLine: screenLine - _screenLayoutInclNonVisibleScreenStartY, col: (posX - _screenLayoutInclNonVisibleScreenStartX) / 8);
            }

        } // End for each cycle

        _lastCyclesConsumedCurrentVblank = c64.Vic2.CyclesConsumedCurrentVblank;
    }

    private void DrawBorderPixels(int normalizedScreenLine)
    {
        // Top or bottom border
        if (normalizedScreenLine <= _topBorderEndYAdjusted || normalizedScreenLine >= _bottomBorderStartYAdjusted)
        {
            var topBottomBorderLineStartIndex = normalizedScreenLine * _width;
            Array.Copy(_oneLineSameColorPixels[_borderColor], 0, _skiaPixelArrayBitmap_BackgroundAndBorder.PixelArray, topBottomBorderLineStartIndex, _width);
            return;
        }

        // Left border
        var lineStartIndex = normalizedScreenLine * _width;
        Array.Copy(_oneLineSameColorPixels[_borderColor], 0, _skiaPixelArrayBitmap_BackgroundAndBorder.PixelArray, lineStartIndex, _leftBorderLengthAdjusted);
        // Right border
        lineStartIndex += _rightBorderStartXAdjusted;
        Array.Copy(_oneLineSameColorPixels[_borderColor], _rightBorderStartXAdjusted, _skiaPixelArrayBitmap_BackgroundAndBorder.PixelArray, lineStartIndex, _rightBorderLengthAdjusted);
    }

    private void DrawTextAndBitmapPixels(C64 c64, int drawLine, int col)
    {
        var characterRow = drawLine / 8;
        ushort characterLine = (ushort)(drawLine % 8);

        ushort characterAddress = (ushort)(_vic2VideoMatrixBaseAddress + (characterRow * _vic2ScreenTextCols) + col);
        ushort colorRamAddress = (ushort)(Vic2Addr.COLOR_RAM_START + (characterRow * _vic2ScreenTextCols) + col);
        ushort c64BitMapAddress = (ushort)(_vic2BitmapBaseAddress + (characterRow * _vic2ScreenTextCols * 8) + (col * 8) + characterLine);

        // Determine character code at current position from video matrix
        var characterCode = c64.Vic2.Vic2Mem[characterAddress];
        var colorRamCode = c64.ReadIOStorage(colorRamAddress);

        uint[] eightPixels;
        if (_isTextMode)
        {
            // Determine colors
            var fgColorCode = colorRamCode;
            int bgColorNumber;  // 0-3
            if (_characterMode == CharMode.Standard)
            {
                bgColorNumber = 0;
            }
            else if (_characterMode == CharMode.Extended)
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
                    _characterMode = CharMode.Standard;
                }
                else
                {
                    // If displaying in MultiColor mode, the actual color used from color RAM will be values 0-7.
                    // Thus color values 8-15 are transformed to 0-7
                    fgColorCode = (byte)((fgColorCode & 0b00001111) - 8);
                }
            }

            // Read one line (8 bits/pixels) of character pixel data from character set from the current line of the character code
            var characterSetLineAddress = (ushort)(_vic2CharacterSetAddressInVIC2Bank
                + characterCode * _vic2ScreenCharacterHeight
                + characterLine);
            var lineData = c64.Vic2.Vic2Mem[characterSetLineAddress];

            // Get pre-calculated 8 pixels that should be drawn on the bitmap, with correct colors for foreground and background
            if (_characterMode == CharMode.Standard || _characterMode == CharMode.Extended)
            {
                switch (bgColorNumber)
                {
                    case 0:
                        eightPixels = _eightPixelsOneColorAndBackground[(lineData, fgColorCode)];
                        break;
                    case 1:
                        eightPixels = _eightPixelsTwoColors[(lineData, _backgroundColor1, fgColorCode)];
                        break;
                    case 2:
                        eightPixels = _eightPixelsTwoColors[(lineData, _backgroundColor2, fgColorCode)];
                        break;
                    case 3:
                        eightPixels = _eightPixelsTwoColors[(lineData, _backgroundColor3, fgColorCode)];
                        break;
                    default:
                        throw new DotNet6502Exception("Invalid background color number.");
                }
           }
            else // Asume text multicolor mode
            {
                // Text multicolor mode color usage (8 bits, 4 pixel pairs)
                // Transparent background = the color of pixel-pair 00
                // backgroundColor1       = the color of pixel-pair 01
                // backgroundColor2       = the color of pixel-pair 10
                // fgColorCode            = the color of pixel-pair 11

                // Get the corresponding array of uints representing the 8 pixels of the character
                eightPixels = _eightPixelsThreeColorsAndBackground[(lineData, _backgroundColor1, _backgroundColor2, fgColorCode)];
            }
        }
        else
        {
            // Assume bitmap mode

            // 8 bits of bitmap data for the current line, at the current column
            var bitmapLineData = c64.Vic2.Vic2Mem[c64BitMapAddress];

            // Bg color is picked from text screen, low 4 bits.
            byte bitmapBgColorCode = (byte)(characterCode & 0b00001111);
            // Fg color is picked from text screen, high 4 bits.
            byte bitmapFgColorCode = (byte)((characterCode & 0b11110000) >> 4);

            if (_bitmapMode == BitmMode.Standard)
            {
                // Bitmap Standard (HiRes) mode, 8 bits => 8 pixels
                // ----------
                // Pixel not set (bit = 0) => bitmap bg color (from text screen low 4 bits)
                // Pixel set (bit = 1) => bitmap fg color
                eightPixels = _eightPixelsTwoColors[(bitmapLineData, bitmapBgColorCode, bitmapFgColorCode)];
            }
            else
            {
                // Bitmap Multi color mode, 8 bits => 4 pixels
                // ----------
                // Pixel pattern 00 => screen bg color
                // Pixel pattern 01 (multi color 1) => bitmap fg color (from text screen high 4 bits)
                // Pixel pattern 10 (multi color 2) => bitmap bg color (from text screen low 4 bits)
                // Pixel pattern 11 (multi color 3) => color RAM color (for corresponding position in text screen)
                eightPixels = _eightPixelsThreeColorsAndBackground[(bitmapLineData, bitmapFgColorCode, bitmapBgColorCode, colorRamCode)];
            }
        }

        // Write the background color to the pixel array for background and border
        WriteToPixelArray(_oneLineSameColorPixels[_backgroundColor0], _skiaPixelArrayBitmap_BackgroundAndBorder.PixelArray, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: false, fnAdjustForScrollY: false);

        // Write the character to the pixel array for foreground (adjusted for fine scrolling)
        WriteToPixelArray(eightPixels, _skiaPixelArrayBitmap_Foreground.PixelArray, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true, fnAdjustForScrollY: true);


        void WriteToPixelArray(uint[] fnEightPixels, uint[] fnPixelArray, int fnMainScreenY, int fnMainScreenX, int fnLength, bool fnAdjustForScrollX, bool fnAdjustForScrollY)
        {
            // Draw 8 pixels (or less) of character on the the pixel array part used for the C64 drawable screen (320x200)

            // ----------
            // Y position
            // ----------
            if (fnAdjustForScrollY)
                fnMainScreenY += _scrollY;
            var ypos = _screenStartY + fnMainScreenY;
            if ((ypos <= _topBorderEndYAdjusted) || (ypos >= _bottomBorderStartYAdjusted))
                return;

            // ----------
            // X position
            // ----------
            var sourcePixelStart = 0;
            if (fnAdjustForScrollX)
                fnMainScreenX += _scrollX;
            var xpos = _screenStartX + fnMainScreenX;
            if ((xpos + fnLength <= _screenStartXAdjusted) || (xpos >= _rightBorderStartXAdjusted))
                return;
            if (xpos < _screenStartXAdjusted)
            {
                fnLength = xpos + fnLength - _screenStartXAdjusted;
                xpos = _screenStartXAdjusted;
                sourcePixelStart = 8 - fnLength;
            }
            else if (xpos + fnLength >= _rightBorderStartXAdjusted)
            {
                fnLength = _rightBorderStartXAdjusted - xpos;
            }

            // ----------
            // Copy pixels to correct location in pixel array
            // ----------
            // Calculate the position in the bitmap where the 8 pixels should be drawn
            var lBitmapIndex = (ypos) * _width + xpos;
            // Copy array with Span
            // - Seems to be a bit faster on .NET 8 WASM than Array.Copy and Buffer.BlockCopy.
            // - TODO: Is the extra heap memory allocation of Span objects (which leads to GC pressure) worth the performance gain?
            var source = new ReadOnlySpan<uint>(fnEightPixels, sourcePixelStart, fnLength);
            var target = new Span<uint>(fnPixelArray, lBitmapIndex, fnLength);
            source.CopyTo(target);

            // Or Copy array with Array.Copy
            //Array.Copy(fnEightPixels, 0, fnPixelArray, lBitmapIndex, fnLength);

            // Or Copy array with Buffer.BlockCopy
            //Buffer.BlockCopy(fnEightPixels, 0, fnPixelArray, lBitmapIndex * 4, fnLength * 4);   // Note: Buffer.BlockCopy uses byte size, so multiply by 4 to get uint size
        }
    }

    private void DrawSpritesToBitmapBackedByPixelArray(C64 c64, uint[] backgroundPixelArray,  uint[] foregroundPixelArray)
    {
        // Main screen, copy 8 pixels at a time
        _spritesStat.Start();
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;


        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // Write sprites to a separate bitmap/pixel array
        foreach (var sprite in c64.Vic2.SpriteManager.Sprites.OrderByDescending(s => s.SpriteNumber))
        {
            if (!sprite.Visible)
                continue;

            var spriteScreenPosX = sprite.X + visibleMainScreenArea.Screen.Start.X - vic2.SpriteManager.ScreenOffsetX;
            var spriteScreenPosY = sprite.Y + visibleMainScreenArea.Screen.Start.Y - vic2.SpriteManager.ScreenOffsetY;
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

                //// If pixel to be set is from a low prio sprite, don't overwrite if current pixel is from high prio sprite
                //const uint BLUE_COLOR_MASK = 0x000000ff;
                if (!priorityOverForground)
                {
                    //if ((backgroundPixelArray[bitmapIndex] & BLUE_COLOR_MASK) == HIGH_PRIO_SPRITE_BLUE)
                    //    return;
                    backgroundPixelArray[bitmapIndex] = color;
                }
                else
                {
                    foregroundPixelArray[bitmapIndex] = color;
                }
            }

            sprite.ClearDirty();
        }

        _spritesStat.Stop();
    }

    private void DrawLineDataToBitmapBackedByPixelArray(C64 c64, uint[] lineDataPixelArray)
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

            //lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Border_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BorderColor];

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

            //lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg0_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor0];
            //lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg1_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor1];
            //lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg2_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor2];
            //lineDataPixelArray[pixelArrayIndex + (int)ShaderLineData.Bg3_Color] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor3];
        }

        _lineDataImageStat.Stop();
    }
}
