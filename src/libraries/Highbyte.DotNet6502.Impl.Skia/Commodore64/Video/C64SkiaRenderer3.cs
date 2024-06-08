using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using SkiaSharp;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class C64SkiaRenderer3 : IRenderer<C64, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas = default!;
    private SKBitmap _bitmap = default!;
    private uint[] _pixelArray;

    // Pre-calculated pixel arrays
    uint[] _oneLineBorderPixels; // pixelArray
    uint[] _sideBorderPixels; // pixelArray

    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg0Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg1Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg2Map;
    Dictionary<(byte eightPixels, byte fgColorCode), uint[]> _bitmapEightPixelsBg3Map;

    SKColor _borderDrawColor = SKColors.DarkKhaki;// new SKColor(3, 2, 1, 255);
    SKColor _bg0DrawColor = SKColors.DarkOrchid; // new SKColor(6, 5, 4, 255);
    SKColor _bg1DrawColor = SKColors.DarkGoldenrod; // new SKColor(9, 8, 7, 255);
    SKColor _bg2DrawColor = SKColors.DarkMagenta; // new SKColor(12, 11, 10, 255);
    SKColor _bg3DrawColor = SKColors.DarkOrange; // new SKColor(15, 14, 13, 255);

    Dictionary<uint, float[]> _sKColorToShaderColorMap = new Dictionary<uint, float[]>();

    C64SkiaColors _c64SkiaColors;

    private bool _changedAllCharsetCodes = false;
    private SKRuntimeEffect _sKRuntimeEffect; // Shader source

    private readonly HashSet<byte> _changedCharsetCodes = new();

    // Sprite drawing variables
    private readonly SKImage[] _spriteImages;

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();
    private const string StatsCategory = "SkiaSharp-Custom";
    private readonly ElapsedMillisecondsTimedStat _borderStat;
    private readonly ElapsedMillisecondsTimedStat _textScreenStat;
    private readonly ElapsedMillisecondsTimedStat _spritesStat;

    public C64SkiaRenderer3()
    {
        _spriteImages = new SKImage[Vic2SpriteManager.NUMBERS_OF_SPRITES];

        _borderStat = Instrumentations.Add<ElapsedMillisecondsTimedStat>($"{StatsCategory}-Border");
        _spritesStat = Instrumentations.Add<ElapsedMillisecondsTimedStat>($"{StatsCategory}-Sprites");
        _textScreenStat = Instrumentations.Add<ElapsedMillisecondsTimedStat>($"{StatsCategory}-TextScreen");
    }

    public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
    {
        _getSkCanvas = skiaRenderContext.GetCanvas;

        _c64SkiaColors = new C64SkiaColors(c64.ColorMapName);

        InitCharset(c64);

        InitBitmap(c64);

        InitShader(c64);

    }

    // Test with shader
    private void InitShader(C64 c64)
    {
        var src = @"
// The bitmap that was drawn
uniform shader color_map;

// The color used to draw a border or bg color
uniform half4 borderColor;
uniform half4 bg0Color;
uniform half4 bg1Color;
uniform half4 bg2Color;
uniform half4 bg3Color;

// The actual color to display as border or bg colors (for each visible line)
// TODO: Max 16K total uniform size.Sum of all arrays below is (if each array has 235 element):  5 * 235 * (4 floats * 4 bytes per float) = 18800 bytes
uniform half4 borderLineColors[#VISIBLE_HEIGHT];
uniform half4 bg0LineColors[#MAIN_SCREEN_HEIGHT];
uniform half4 bg1LineColors[#MAIN_SCREEN_HEIGHT];
uniform half4 bg2LineColors[#MAIN_SCREEN_HEIGHT];
uniform half4 bg3LineColors[#MAIN_SCREEN_HEIGHT];


half4 map_color(half4 texColor, float line) {
    half4 useColor;

    if(line < #MAIN_SCREEN_START || line > #MAIN_SCREEN_END) {
        useColor = texColor == borderColor ? borderLineColors[line]
                : texColor; 
    }
    else {
        int mainScreenLine = line - #MAIN_SCREEN_START;
        useColor = texColor == borderColor ? borderLineColors[line]
                : texColor == bg0Color ? bg0LineColors[mainScreenLine]
                : texColor == bg1Color ? bg1LineColors[mainScreenLine]
                : texColor == bg2Color ? bg2LineColors[mainScreenLine]
                //: texColor == bg3Color ? bg3LineColors[mainScreenLine]  // TODO: Uncomment when bg3 is implemented, fails silently because >16KB total uniform size
                : texColor; 

        //useColor = texColor;
    }
    return useColor;
}

half4 main(float2 fragCoord) {

    half4 texColor = sample(color_map, fragCoord);

    float scaleX = 1;
    float scaleY = 1;
    uint x = uint(fragCoord.x * 1.0/scaleX);
    uint y = uint(fragCoord.y * 1.0/scaleY);

    half4 useColor;

    if(y < #VISIBLE_HEIGHT) {
        useColor = map_color(texColor, y);
    }
    else {
        // Error?
        useColor = half4(1, 0, 0, 1);
    }

    return useColor;
}";
        src = src.Replace("#VISIBLE_HEIGHT", c64.Vic2.Vic2Screen.VisibleHeight.ToString());

        src = src.Replace("#MAIN_SCREEN_HEIGHT", c64.Vic2.Vic2Screen.DrawableAreaHeight.ToString());

        var visibleMainScreenAreaNormalized = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var bitmapMainScreenStartLine = visibleMainScreenAreaNormalized.Screen.Start.Y;

        src = src.Replace("#MAIN_SCREEN_START", bitmapMainScreenStartLine.ToString());
        src = src.Replace("#MAIN_SCREEN_END", (bitmapMainScreenStartLine + c64.Vic2.Vic2Screen.DrawableAreaHeight - 1).ToString());

        _sKRuntimeEffect = SKRuntimeEffect.Create(src, out var error);
        if (!string.IsNullOrEmpty(error))
            throw new DotNet6502Exception($"Shader compilation error: {error}");

        // Init color map (colors used in shader to transform the colors the bitmap was drawn with).
        SKColor color;
        color = _borderDrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _bg0DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _bg1DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _bg2DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });

        color = _bg3DrawColor;
        _sKColorToShaderColorMap.Add((uint)color, new[] { color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f });


        // Init the actual colors used in the shader to draw the border lines
        foreach (var c64Color in Enum.GetValues<C64Colors>())
        {
            var c64ColorValue = (byte)c64Color; // Color 0-15
            var systemColor = GetSystemColor(c64ColorValue, ColorMaps.DEFAULT_COLOR_MAP_NAME); // .NET "Color" type
            var c64SkColor = _c64SkiaColors.SystemToSkColorMap[systemColor];    // Skia "SKColor" type
            _sKColorToShaderColorMap.Add((uint)c64SkColor, new[] { c64SkColor.Red / 255.0f, c64SkColor.Green / 255.0f, c64SkColor.Blue / 255.0f, c64SkColor.Alpha / 255.0f });
        }
    }

    private void InitBitmap(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;

        // Init pixel array to associate with a SKBitmap that is written to a SKCanvas
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;
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

        // Init pre-calculated pixel arrays

        // Borders (new version)
        _oneLineBorderPixels = new uint[width];
        for (var i = 0; i < _oneLineBorderPixels.Length; i++)
            _oneLineBorderPixels[i] = (uint)_borderDrawColor;
        _sideBorderPixels = new uint[vic2Screen.VisibleLeftRightBorderWidth]; // Assume right border is same width as left border
        for (var i = 0; i < _sideBorderPixels.Length; i++)
            _sideBorderPixels[i] = (uint)_borderDrawColor;

        // Main text screen: Pre-calculate the 8 pixels for each combination of bit pixel pattern and foreground color
        _bitmapEightPixelsBg0Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsBg1Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsBg2Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels
        _bitmapEightPixelsBg3Map = new(); // (pixelPattern, fgColorIndex) => bitmapPixels

        uint bg0ColorVal = (uint)_bg0DrawColor; // Note the background color _bg0DrawColor is hardcoded, and will be replaced by shader
        uint bg1ColorVal = (uint)_bg1DrawColor; // Note the background color _bg1DrawColor is hardcoded, and will be replaced by shader
        uint bg2ColorVal = (uint)_bg2DrawColor; // Note the background color _bg2DrawColor is hardcoded, and will be replaced by shader
        uint bg3ColorVal = (uint)_bg3DrawColor; // Note the background color _bg3DrawColor is hardcoded, and will be replaced by shader

        for (int pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte fgColorIndex = 0; fgColorIndex < 16; fgColorIndex++)
            {
                var fgSystemColor = GetSystemColor(fgColorIndex, c64.ColorMapName); // .NET "Color" type
                var fgSkColor = _c64SkiaColors.SystemToSkColorMap[fgSystemColor];    // Skia "SKColor" type
                uint fgColorVal = (uint)fgSkColor;

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
                _bitmapEightPixelsBg0Map.Add(((byte)pixelPattern, fgColorIndex), bitmapPixelsBg0);
                _bitmapEightPixelsBg1Map.Add(((byte)pixelPattern, fgColorIndex), bitmapPixelsBg1);
                _bitmapEightPixelsBg2Map.Add(((byte)pixelPattern, fgColorIndex), bitmapPixelsBg2);
                _bitmapEightPixelsBg3Map.Add(((byte)pixelPattern, fgColorIndex), bitmapPixelsBg3);
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

        DrawBorderAndScreenToBitmapBackedByPixelArray(c64, _pixelArray);

        WriteBitmapToCanvas(_bitmap, canvas, c64);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    private void InitCharset(C64 c64)
    {
        // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        c64.Vic2.CharsetManager.CharsetAddressChanged += (s, e) => CharsetChangedHandler(c64, e);
    }

    private void CharsetChangedHandler(C64 c64, Vic2CharsetManager.CharsetAddressChangedEventArgs e)
    {
        if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetBaseAddress)
        {
            //GenerateCurrentChargenImage(c64);
            _changedAllCharsetCodes = true;
        }
        else if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetCharacter && e.CharCode.HasValue)
        {
            //UpdateChangedCharacterOnCurrentImage(c64, e.CharCode.Value);
            if (!_changedCharsetCodes.Contains(e.CharCode.Value))
                _changedCharsetCodes.Add(e.CharCode.Value);
        }
    }


    private void WriteBitmapToCanvas(SKBitmap bitmap, SKCanvas canvas, C64 c64)
    {
        canvas.Save();

        // Convert bitmap to shader texture
        var shaderTexture = bitmap.ToShader();

        // Build array to send to shader with the actual color that should be used differnt types of colors (border, bg0, bg1, bg2, bg3), dependent on the line number
        var c64ScreenLineIORegisterValues = new Dictionary<int, ScreenLineData>(c64.Vic2.ScreenLineIORegisterValues);
        float[][] borderLineColors = new float[c64.Vic2.Vic2Screen.VisibleHeight][];
        float[][] bg0LineColors = new float[c64.Vic2.Vic2Screen.DrawableAreaHeight][];
        float[][] bg1LineColors = new float[c64.Vic2.Vic2Screen.DrawableAreaHeight][];
        float[][] bg2LineColors = new float[c64.Vic2.Vic2Screen.DrawableAreaHeight][];
        float[][] bg3LineColors = new float[c64.Vic2.Vic2Screen.DrawableAreaHeight][];

        var visibleMainScreenArea = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.Visible);

        foreach (var lineData in c64ScreenLineIORegisterValues)
        {
            // Check if in total visisble area, because c64ScreenLineIORegisterValues includes non-visible lines
            if (lineData.Key < visibleMainScreenArea.TopBorder.Start.Y || lineData.Key > visibleMainScreenArea.BottomBorder.End.Y)
                continue;
            var bitmapLine = lineData.Key - visibleMainScreenArea.TopBorder.Start.Y;

            var borderSystemColor = GetSystemColor(lineData.Value.BorderColor, c64.ColorMapName); // .NET "Color" type
            var borderSkColor = _c64SkiaColors.SystemToSkColorMap[borderSystemColor];             // Skia "SKColor" type
            borderLineColors[bitmapLine] = _sKColorToShaderColorMap[(uint)borderSkColor];

            // Check if line is within main screen area, only there are background colors used.
            if (bitmapLine < visibleMainScreenArea.Screen.Start.Y || bitmapLine >= (visibleMainScreenArea.Screen.Start.Y + c64.Vic2.Vic2Screen.DrawableAreaHeight))
                continue;
            var mainScreenRelativeLine = bitmapLine - visibleMainScreenArea.Screen.Start.Y;

            var bg0SystemColor = GetSystemColor(lineData.Value.BackgroundColor0, c64.ColorMapName); // .NET "Color" type
            var bg0SkColor = _c64SkiaColors.SystemToSkColorMap[bg0SystemColor];             // Skia "SKColor" type
            bg0LineColors[mainScreenRelativeLine] = _sKColorToShaderColorMap[(uint)bg0SkColor];

            var bg1SystemColor = GetSystemColor(lineData.Value.BackgroundColor1, c64.ColorMapName); // .NET "Color" type
            var bg1SkColor = _c64SkiaColors.SystemToSkColorMap[bg1SystemColor];             // Skia "SKColor" type
            bg1LineColors[mainScreenRelativeLine] = _sKColorToShaderColorMap[(uint)bg1SkColor];

            var bg2SystemColor = GetSystemColor(lineData.Value.BackgroundColor2, c64.ColorMapName); // .NET "Color" type
            var bg2SkColor = _c64SkiaColors.SystemToSkColorMap[bg2SystemColor];             // Skia "SKColor" type
            bg2LineColors[mainScreenRelativeLine] = _sKColorToShaderColorMap[(uint)bg2SkColor];

            var bg3SystemColor = GetSystemColor(lineData.Value.BackgroundColor3, c64.ColorMapName); // .NET "Color" type
            var bg3SkColor = _c64SkiaColors.SystemToSkColorMap[bg3SystemColor];             // Skia "SKColor" type
            bg3LineColors[mainScreenRelativeLine] = _sKColorToShaderColorMap[(uint)bg3SkColor];

        }

        // shader uniform values
        var uniforms = new SKRuntimeEffectUniforms(_sKRuntimeEffect)
        {
            ["borderColor"] = _sKColorToShaderColorMap[(uint)_borderDrawColor],
            ["borderLineColors"] = borderLineColors,

            ["bg0Color"] = _sKColorToShaderColorMap[(uint)_bg0DrawColor],
            ["bg0LineColors"] = bg0LineColors,

            ["bg1Color"] = _sKColorToShaderColorMap[(uint)_bg1DrawColor],
            ["bg1LineColors"] = bg1LineColors,

            ["bg2Color"] = _sKColorToShaderColorMap[(uint)_bg2DrawColor],
            ["bg2LineColors"] = bg2LineColors,

            ["bg3Color"] = _sKColorToShaderColorMap[(uint)_bg3DrawColor],
            ["bg3LineColors"] = bg3LineColors,
        };
        // shader uniform texture sampling values
        var children = new SKRuntimeEffectChildren(_sKRuntimeEffect)
        {
            ["color_map"] = shaderTexture
        };

        using var shader = _sKRuntimeEffect.ToShader(true, uniforms, children);
        using var shaderPaint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, _bitmap.Width, _bitmap.Height, shaderPaint);

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

        var startY = 0;
        //var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Copy 8 pixels each time
        // Borders
        using (_borderStat.Measure())
        {
            for (var y = startY; y < (startY + height); y++)
            {
                // Top or bottom border
                if (y <= visibleMainScreenAreaNormalized.TopBorder.End.Y || y >= visibleMainScreenAreaNormalized.BottomBorder.Start.Y)
                {
                    var topBottomBorderLineStartIndex = y * _bitmap.Width;
                    Array.Copy(_oneLineBorderPixels, 0, pixelArray, topBottomBorderLineStartIndex, _bitmap.Width);
                    continue;
                }

                // Left border
                int lineStartIndex = y * _bitmap.Width;
                Array.Copy(_sideBorderPixels, 0, pixelArray, lineStartIndex, _sideBorderPixels.Length);
                // Right border
                lineStartIndex += visibleMainScreenAreaNormalized.RightBorder.Start.X;
                Array.Copy(_sideBorderPixels, 0, pixelArray, lineStartIndex, _sideBorderPixels.Length);
            }
        }

        // Main screen
        using (_textScreenStat.Measure())
        {
            // Copy settings used in loop to local variables to increase performance
            var vic2VideoMatrixBaseAddress = vic2.VideoMatrixBaseAddress;
            var vic2ScreenTextCols = vic2Screen.TextCols;
            var screenStartY = visibleMainScreenAreaNormalized.Screen.Start.Y;
            var screenStartX = visibleMainScreenAreaNormalized.Screen.Start.X;
            var vic2CharacterSetAddressInVIC2Bank = vic2.CharsetManager.CharacterSetAddressInVIC2Bank;
            var vic2ScreenCharacterHeight = vic2.Vic2Screen.CharacterHeight;

            // Loop each row line on main text/gfx screen, starting with line 0.
            for (var drawLine = 0; drawLine < vic2Screen.DrawableAreaHeight; drawLine++)
            {
                var characterRow = drawLine / 8;
                var characterLine = drawLine % 8;
                var characterAddress = (ushort)(vic2VideoMatrixBaseAddress + (characterRow * vic2ScreenTextCols));

                // Calculate the y position in the bitmap where the 8 pixels should be drawn
                var bitmapY = screenStartY + drawLine;

                bool textMode = (vic2.DisplayMode == DispMode.Text); // TODO: Check for display mode more than once per line?
                var characterMode = vic2.CharacterMode; // TODO: Check for display mode more than once per line?

                for (var col = 0; col < vic2ScreenTextCols; col++)
                // Loop each column on main text/gfx screen, starting with column 0.
                {
                    uint[] bitmapEightPixels;
                    if (textMode)
                    {
                        // Determine character code at current position
                        var characterCode = vic2Mem[characterAddress];

                        // Read one line (8 bits/pixels) of character pixel data from character set from the current line of the character code
                        var characterSetLineAddress = (ushort)(vic2CharacterSetAddressInVIC2Bank
                                + (characterCode * vic2ScreenCharacterHeight)
                                + characterLine);
                        byte lineData = vic2Mem[characterSetLineAddress];

                        // Determine colors
                        var fgColorCode = c64.ReadIOStorage((ushort)(Vic2Addr.COLOR_RAM_START + (characterRow * vic2ScreenTextCols) + col));
                        if (characterMode == CharMode.Standard)
                        {
                            // Get the corresponding array of uints representing the 8 pixels of the character
                            bitmapEightPixels = _bitmapEightPixelsBg0Map[(lineData, fgColorCode)];

                        }
                        else if (characterMode == CharMode.Extended)
                        {
                            var bgColorSelector = characterCode >> 6;   // Bit 6 and 7 of character byte is used to select background color (0-3)

                            // Get the corresponding array of uints representing the 8 pixels of the character
                            bitmapEightPixels = bgColorSelector switch
                            {
                                0 => _bitmapEightPixelsBg0Map[(lineData, fgColorCode)],
                                1 => _bitmapEightPixelsBg1Map[(lineData, fgColorCode)],
                                2 => _bitmapEightPixelsBg2Map[(lineData, fgColorCode)],
                                3 => _bitmapEightPixelsBg3Map[(lineData, fgColorCode)],
                                _ => throw new NotImplementedException($"Background color selector {bgColorSelector} not implemented.")
                            };
                            characterCode = (byte)(characterCode & 0b00111111); // The actual usable character codes are in the lower 6 bits (0-63)

                        }
                        else // Asume multicolor mode
                        {
                            // When in MultiColor mode, a character can still be displayed in Standard mode depending on the value from color RAM.
                            if (characterCode <= 7)
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

                            // Text multicolor mode color usage (8 bits, 4 pixel pairs)
                            // backgroundColor0 = the color of pixel-pair 00
                            // backgroundColor1 = the color of pixel-pair 01
                            // backgroundColor2 = the color of pixel-pair 10
                            // fgColorCode      = the color of pixel-pair 11


                            // Get the corresponding array of uints representing the 8 pixels of the character
                            // TODO
                            //bitmapEightPixels = _bitmapEightPixelsMultiColorMap[(lineData, bgColorCode, fgColorCode)];
                            bitmapEightPixels = new uint[8];
                        }

                    }
                    else
                    {
                        // Assume bitmap mode
                        // TODO
                        bitmapEightPixels = new uint[8];
                    }


                    // Calculate the x position in the bitmap where the 8 pixels should be drawn
                    var bitmapX = screenStartX + (col * 8);
                    int bitmapIndex = (bitmapY * _bitmap.Width + bitmapX);
                    // Draw 8 pixels on the bitmap
                    Array.Copy(bitmapEightPixels, 0, pixelArray, bitmapIndex, bitmapEightPixels.Length);

                    characterAddress++;
                }
            }
        }
    }
}
