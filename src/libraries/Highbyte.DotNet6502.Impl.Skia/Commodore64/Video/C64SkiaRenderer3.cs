using System.Numerics;
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

    // Colors to draw border and background colors with on the bitmap. These colors will be replaced by the shader.
    // Could be any color, but must be different from normal C64 colors (used when drawing foreground colors).
    SKColor _borderDrawColor = SKColors.DarkKhaki;
    SKColor _bg0DrawColor = SKColors.DarkOrchid;
    SKColor _bg1DrawColor = SKColors.DarkGoldenrod;
    SKColor _bg2DrawColor = SKColors.DarkMagenta;
    SKColor _bg3DrawColor = SKColors.DarkOrange;

    // Lookup table for mapping C64 colors to shader colors
    Dictionary<uint, float[]> _sKColorToShaderColorMap = new Dictionary<uint, float[]>();

    C64SkiaColors _c64SkiaColors;

    private bool _changedAllCharsetCodes = false;
    private SKRuntimeEffect _sKRuntimeEffect; // Shader source

    private uint[] _lineColorsPixelArray;
    private const int _lineColorsPixelArrayWidth = 5; // bg0, bg1, bg2, bg3, and border on each line;
    private SKBitmap _lineColorsBitmap;

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
// The bitmap that was drawn.
uniform shader bitmap_texture;

// The actual color to display as border and background colors for each line. Used to replace the colors in the bitmap.
uniform shader line_color_map;

// The color used to draw border and background colors
uniform half4 borderColor;
uniform half4 bg0Color;
uniform half4 bg1Color;
uniform half4 bg2Color;
uniform half4 bg3Color;

half4 map_color(half4 texColor, float line) {

    // For images, Skia GSL use the common convention that the centers are at half-pixel offsets. 
    // So to sample the top-left pixel in an image shader, you'd want to pass (0.5, 0.5) as coords.
    // The next (to the right) pixel would be (1.5, 0.5).
    // The next (to the below ) pixel would be (0.5, 1.5).

    // Assume image in line_color_map is 5 pixel wide (bg0, bg1, bg2, bg3, border), and y (number of main screen lines) pixels high.

    if(line < #MAIN_SCREEN_START || line > #MAIN_SCREEN_END) {

        half4 useColor;
        float2 lineCoord;

        // Only border colors can be used outside main screen area, no need to check for replacement of other colors here
        if(texColor == borderColor) {
            lineCoord = float2(0.5 + 4, 0.5 + line);
            useColor = line_color_map.eval(lineCoord);
        }
        else {
            useColor = texColor;    // Not color that should be transformed (i.e. foreground color)
        }
        return useColor;
    }
    else {
        half4 useColor;
        float2 lineCoord;

        // Main screen area + side borders
        if(texColor == borderColor) {
            lineCoord = float2(0.5 + 4, 0.5 + line); 
            useColor = line_color_map.eval(lineCoord);
        }
        else if(texColor == bg0Color) {
            lineCoord = float2(0.5 + 0, 0.5 + line); 
            useColor = line_color_map.eval(lineCoord);
        }
        else if(texColor == bg1Color) {
            lineCoord = float2(0.5 + 1, 0.5 + line); 
            useColor = line_color_map.eval(lineCoord);
        }
        else if(texColor == bg2Color) {
            lineCoord = float2(0.5 + 2, 0.5 + line); 
            useColor = line_color_map.eval(lineCoord);
        }
        else if(texColor == bg3Color) {
            lineCoord = float2(0.5 + 3, 0.5 + line); 
            useColor = line_color_map.eval(lineCoord);
        }
        else {
            useColor = texColor;    // Not color that should be transformed (i.e. foreground color)
        }
        return useColor;
    }
}

half4 main(float2 fragCoord) {

    half4 texColor = bitmap_texture.eval(fragCoord);

    float scaleX = 1;
    float scaleY = 1;
    int x2 = int(fragCoord.x * 1.0/scaleX);
    int y2 = int(fragCoord.y * 1.0/scaleY);
    float x = float(x2);
    float y = float(y2);

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

        var visibleMainScreenAreaNormalized = c64.Vic2.ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var bitmapMainScreenStartLine = visibleMainScreenAreaNormalized.Screen.Start.Y;
        src = src.Replace("#MAIN_SCREEN_START", bitmapMainScreenStartLine.ToString());
        src = src.Replace("#MAIN_SCREEN_END", (bitmapMainScreenStartLine + c64.Vic2.Vic2Screen.DrawableAreaHeight - 1).ToString());

        _sKRuntimeEffect = SKRuntimeEffect.CreateShader(src, out var error);
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
        foreach (byte c64Color in Enum.GetValues<C64Colors>())
        {
            var c64SkColor = _c64SkiaColors.C64ToSkColorMap[c64Color];
            _sKColorToShaderColorMap.Add((uint)c64SkColor, new[] { c64SkColor.Red / 255.0f, c64SkColor.Green / 255.0f, c64SkColor.Blue / 255.0f, c64SkColor.Alpha / 255.0f });
        }

        // Init the bg color map bitmap
        _lineColorsPixelArray = new uint[_lineColorsPixelArrayWidth * c64.Vic2.Vic2Screen.VisibleHeight]; // bg0, bg1, bg2, and bg3 on each line
        _lineColorsBitmap = new();

        // pin the managed pixel array so that the GC doesn't move it
        // (It is essential that the pinned memory be unpinned after usage so that the memory can be freed by the GC.)
        var gcHandle = GCHandle.Alloc(_lineColorsPixelArray, GCHandleType.Pinned);

        // install the pixels with the color type of the pixel data
        //var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        var info = new SKImageInfo(_lineColorsPixelArrayWidth, c64.Vic2.Vic2Screen.DrawableAreaHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);  // Note: SKColorType.Bgra8888 seems to be needed for Blazor WASM. TODO: Does this affect when running in Blazor on Mac/Linux?
        _lineColorsBitmap.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); }, null);
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

            _lineColorsPixelArray[bitmapLine * _lineColorsPixelArrayWidth + 4] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BorderColor];

            // Check if line is within main screen area, only there are background colors used.
            if (lineData.Key < drawableScreenStartY || bitmapLine >= (drawableScreenEndY))
                continue;
 
            _lineColorsPixelArray[bitmapLine * _lineColorsPixelArrayWidth + 0] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor0];
            _lineColorsPixelArray[bitmapLine * _lineColorsPixelArrayWidth + 1] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor1];
            _lineColorsPixelArray[bitmapLine * _lineColorsPixelArrayWidth + 2] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor2];
            _lineColorsPixelArray[bitmapLine * _lineColorsPixelArrayWidth + 3] = (uint)_c64SkiaColors.C64ToSkColorMap[lineData.Value.BackgroundColor3];
        }

        // shader uniform values
        var uniforms = new SKRuntimeEffectUniforms(_sKRuntimeEffect)
        {
            ["borderColor"] = _sKColorToShaderColorMap[(uint)_borderDrawColor],
            ["bg0Color"] = _sKColorToShaderColorMap[(uint)_bg0DrawColor],
            ["bg1Color"] = _sKColorToShaderColorMap[(uint)_bg1DrawColor],
            ["bg2Color"] = _sKColorToShaderColorMap[(uint)_bg2DrawColor],
            ["bg3Color"] = _sKColorToShaderColorMap[(uint)_bg3DrawColor],
        };

        // Shader uniform texture sampling values
        // Convert bitmap (that one have written the C64 screen to) to shader texture
        var shaderTexture = bitmap.ToShader();

        // Convert other bitmaps to shader texture
        var lineColorsBitmapShaderTexture = _lineColorsBitmap.ToShader();

        var children = new SKRuntimeEffectChildren(_sKRuntimeEffect)
        {
            ["bitmap_texture"] = shaderTexture,
            ["line_color_map"] = lineColorsBitmapShaderTexture
        };

        using var shader = _sKRuntimeEffect.ToShader(uniforms, children);
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
