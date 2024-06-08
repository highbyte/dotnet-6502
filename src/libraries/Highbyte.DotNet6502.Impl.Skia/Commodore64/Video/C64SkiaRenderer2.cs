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

public class C64SkiaRenderer2 : IRenderer<C64, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas = default!;
    private SKBitmap _bitmap = default!;
    private uint[] _pixelArray;

    // Pre-calculated pixel arrays
    Dictionary<byte, uint[]> _oneLinePixelsMap; // colorIndex => pixelArray
    Dictionary<byte, uint[]> _sideBorderPixelsMap; // colorIndex => pixelArray
    Dictionary<(byte eightPixels, byte bgColorCode, byte fgColorCode), uint[]> _bitmapEightPixelsMap;


    C64SkiaColors _c64SkiaColors;

    private bool _changedAllCharsetCodes = false;
    private SKPaint _shaderPaint;
    private readonly HashSet<byte> _changedCharsetCodes = new();

    // Sprite drawing variables
    private readonly SKImage[] _spriteImages;

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();
    private const string StatsCategory = "SkiaSharp-Custom";
    private readonly ElapsedMillisecondsTimedStat _borderStat;
    private readonly ElapsedMillisecondsTimedStat _textScreenStat;
    private readonly ElapsedMillisecondsTimedStat _spritesStat;

    public C64SkiaRenderer2()
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

        //InitShader();

    }

//    // Test with shader
//    private void InitShader()
//    {
//        var src = @"
//half4 main(float2 fragCoord) {
//  return half4(0.5, 0.5, 0.5, 1);
//}";
//        var effect = SKRuntimeEffect.Create(src, out var error);
//        if (!string.IsNullOrEmpty(error))
//            throw new DotNet6502Exception($"Shader compilation error: {error}");
//        var shader = effect.ToShader(false);
//        _shaderPaint = new SKPaint { Shader = shader };
//    }

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

        // Borders: pre-calculated pixel arrays for each color index, for entire line and sider border line part.
        _oneLinePixelsMap = new();
        _sideBorderPixelsMap = new();
        for (byte borderColorIndex = 0; borderColorIndex < 16; borderColorIndex++)
        {
            var borderSystemColor = GetSystemColor(borderColorIndex, c64.ColorMapName); // .NET "Color" type
            var borderSkColor = _c64SkiaColors.SystemToSkColorMap[borderSystemColor];    // Skia "SKColor" type
            uint borderSkColorVal = (uint)borderSkColor;

            // One line width entire screen width (including borders)
            uint[] oneLine = new uint[width];
            for (var i = 0; i < oneLine.Length; i++)
                oneLine[i] = borderSkColorVal;
            _oneLinePixelsMap.Add(borderColorIndex, oneLine);

            // One line with the length of a side border
            uint[] sideBorderLine = new uint[vic2Screen.VisibleLeftRightBorderWidth]; // Assume right border is same width as left border
            for (var i = 0; i < sideBorderLine.Length; i++)
                sideBorderLine[i] = borderSkColorVal;
            _sideBorderPixelsMap.Add(borderColorIndex, sideBorderLine);
        }

        // Main text screen: Pre-calculate the 8 pixels for each combination of bit pixel pattern, background color and foreground color
        _bitmapEightPixelsMap = new(); // (pixelPattern, bgColorIndex, fgColorIndex) => bitmapPixels
        for (int pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte bgColorIndex = 0; bgColorIndex < 16; bgColorIndex++)
            {
                var bgSystemColor = GetSystemColor(bgColorIndex, c64.ColorMapName); // .NET "Color" type
                var bgSkColor = _c64SkiaColors.SystemToSkColorMap[bgSystemColor];    // Skia "SKColor" type
                uint bgColorVal = (uint)bgSkColor;

                for (byte fgColorIndex = 0; fgColorIndex < 16; fgColorIndex++)
                {
                    var fgSystemColor = GetSystemColor(fgColorIndex, c64.ColorMapName); // .NET "Color" type
                    var fgSkColor = _c64SkiaColors.SystemToSkColorMap[fgSystemColor];    // Skia "SKColor" type
                    uint fgColorVal = (uint)fgSkColor;

                    uint[] bitmapPixels = new uint[8];
                    // Loop each bit in i
                    for (int pixelPos = 0; pixelPos < 8; pixelPos++)
                    {
                        // If bit is set, use foreground color, else use background color
                        bitmapPixels[pixelPos] = (pixelPattern & (1 << (7 - pixelPos))) != 0 ? fgColorVal : bgColorVal;
                    }
                    _bitmapEightPixelsMap.Add(((byte)pixelPattern, bgColorIndex, fgColorIndex), bitmapPixels);

                }
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

        WriteBitmapToCanvas(_bitmap, canvas);
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


    private void WriteBitmapToCanvas(SKBitmap bitmap, SKCanvas canvas)
    {
        canvas.Save();

        //// Clip to the visible character screen area
        //canvas.ClipRect(
        //    new SKRect(
        //        visibleClippedScreenArea.Screen.Start.X,
        //        visibleClippedScreenArea.Screen.Start.Y,
        //        visibleClippedScreenArea.Screen.End.X + 1,
        //        visibleClippedScreenArea.Screen.End.Y + 1),
        //    SKClipOperation.Intersect);
        //canvas.Translate(scrollX, scrollY);

        // Test to change color of the bitmap
        //var c64BlueSkColor = _c64SkiaColors.SystemToSkColorMap[GetSystemColor((byte)C64Colors.Blue, ColorMaps.DEFAULT_COLOR_MAP_NAME)];
        //var c64LightBlueSkColor = _c64SkiaColors.SystemToSkColorMap[GetSystemColor((byte)C64Colors.LightBlue, ColorMaps.DEFAULT_COLOR_MAP_NAME)];
        //var c64BlackSkColor = _c64SkiaColors.SystemToSkColorMap[GetSystemColor((byte)C64Colors.Black, ColorMaps.DEFAULT_COLOR_MAP_NAME)];
        //var darkGreyColor = SKColors.DarkGray;
        //var navyColor = SKColors.Navy;
        //var goldColor = SKColors.Gold;
        //var purpleColor = SKColors.Purple;
        //var blackColor = SKColors.Black;
        ////var colorFilter = CreateReplaceColorFilter(
        ////    newColor: darkGreyColor, originalColor: c64BlueSkColor
        ////    , newColor2: navyColor, originalColor2: c64LightBlueSkColor
        ////    , newColor3: blackColor, originalColor3: c64BlackSkColor
        ////    );
        //var colorFilter = CreateReplaceColorFilter(
        //    newColor: darkGreyColor, originalColor: SKColors.DarkKhaki
        //    , newColor2: navyColor, originalColor2: SKColors.DarkOrchid
        //    , newColor3: purpleColor, originalColor3: SKColors.DarkGoldenrod
        //    );
        //var paint = new SKPaint { ColorFilter = colorFilter, Style = SKPaintStyle.StrokeAndFill };

        // Test with shader
        //canvas.DrawRoundRect(0, 0, 100, 100, 15, 15, _shaderPaint);

        // Draw bitmap to Skia canvas
        SKPaint? paint = null;
        canvas.DrawBitmap(bitmap,
            0,
            0,
            paint: paint
            );

        canvas.Restore();
    }

    /// <summary>
    /// Color filter change the color the original character image was drawn in to a specified color.
    /// </summary>
    /// <param name="newColor"></param>
    /// <param name="originalColor"></param>
    /// <returns></returns>
    private SKColorFilter CreateReplaceColorFilter(
        SKColor newColor,
        SKColor originalColor,
        SKColor? newColor2 = null,
        SKColor? originalColor2 = null,
        SKColor? newColor3 = null,
        SKColor? originalColor3 = null,
        SKColor? newBackgroundColor = null)
    {
        var R = new byte[256];
        var G = new byte[256];
        var B = new byte[256];
        var A = new byte[256];

        // Default map to same color
        for (var i = 0; i < 256; i++)
        {
            R[i] = (byte)i;
            G[i] = (byte)i;
            B[i] = (byte)i;
            A[i] = (byte)i;
        }

        // Changes
        R[originalColor.Red] = newColor.Red;
        G[originalColor.Green] = newColor.Green;
        B[originalColor.Blue] = newColor.Blue;
        A[originalColor.Alpha] = newColor.Alpha;

        if (newColor2 != null && originalColor2 != null)
        {
            A[originalColor2!.Value.Alpha] = newColor2.Value.Alpha;
            R[originalColor2!.Value.Red] = newColor2.Value.Red;
            G[originalColor2!.Value.Green] = newColor2.Value.Green;
            B[originalColor2!.Value.Blue] = newColor2.Value.Blue;
        }
        if (newColor3 != null && originalColor3 != null)
        {
            A[originalColor3!.Value.Alpha] = newColor3.Value.Alpha;
            R[originalColor3!.Value.Red] = newColor3.Value.Red;
            G[originalColor3!.Value.Green] = newColor3.Value.Green;
            B[originalColor3!.Value.Blue] = newColor3.Value.Blue;
        }
        if (newBackgroundColor != null)
        {
            A[0] = newBackgroundColor.Value.Alpha;
            R[0] = newBackgroundColor.Value.Red;
            G[0] = newBackgroundColor.Value.Green;
            B[0] = newBackgroundColor.Value.Blue;
        }

        var colorFilter = SKColorFilter.CreateTable(A, R, G, B);
        //var colorFilter = SKColorFilter.CreateTable(null, R, G, B);
        return colorFilter;
    }

    private void DrawBorderAndScreenToBitmapBackedByPixelArray(C64 c64, uint[] pixelArray)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalized = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.Visible);

        var startY = 0;
        //var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Make a local copy of ScreenLineIORegisterValues to increase performance in loops below 
        var c64ScreenLineIORegisterValues = new Dictionary<int, ScreenLineData>(c64.Vic2.ScreenLineIORegisterValues);

        // Copy 8 pixels each time
        // Borders
        using (_borderStat.Measure())
        {
            //byte borderColor = c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);
            for (var y = startY; y < (startY + height); y++)
            {
                var borderColor = c64ScreenLineIORegisterValues[y + visibleMainScreenArea.TopBorder.Start.Y].BorderColor;

                // Top or bottom border
                if (y <= visibleMainScreenAreaNormalized.TopBorder.End.Y || y >= visibleMainScreenAreaNormalized.BottomBorder.Start.Y)
                {
                    var topBottomBorderLineStartIndex = y * _bitmap.Width;
                    Array.Copy(_oneLinePixelsMap[borderColor], 0, pixelArray, topBottomBorderLineStartIndex, _bitmap.Width);
                    continue;
                }

                // Left border
                int lineStartIndex = y * _bitmap.Width;
                Array.Copy(_sideBorderPixelsMap[borderColor], 0, pixelArray, lineStartIndex, _sideBorderPixelsMap[borderColor].Length);
                // Right border
                lineStartIndex += visibleMainScreenAreaNormalized.RightBorder.Start.X;
                Array.Copy(_sideBorderPixelsMap[borderColor], 0, pixelArray, lineStartIndex, _sideBorderPixelsMap[borderColor].Length);
            }
        }

        // Main screen
        using (_textScreenStat.Measure())
        {
            // Copy settings used in loop to local variables to increase performance
            //byte backgroundColor0 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0); // Background color used for normal and extended character mode
            byte backgroundColor1 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1); // Background color used for extended character mode
            byte backgroundColor2 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2); // Background color used for extended character mode
            byte backgroundColor3 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3); // Background color used for extended character mode
            var vic2VideoMatrixBaseAddress = vic2.VideoMatrixBaseAddress;
            var vic2ScreenTextCols = vic2Screen.TextCols;
            var screenStartY = visibleMainScreenAreaNormalized.Screen.Start.Y;
            var screenStartX = visibleMainScreenAreaNormalized.Screen.Start.X;
            var vic2CharacterSetAddressInVIC2Bank = vic2.CharsetManager.CharacterSetAddressInVIC2Bank;
            var vic2ScreenCharacterHeight = vic2.Vic2Screen.CharacterHeight;

            // Loop each row line on main text/gfx screen, starting with line 0.
            int screenLine = visibleMainScreenArea.Screen.Start.Y;
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
                        byte bgColorCode;
                        if (characterMode == CharMode.Standard)
                        {
                            bgColorCode = c64ScreenLineIORegisterValues[screenLine].BackgroundColor0;

                            // Get the corresponding array of uints representing the 8 pixels of the character
                            bitmapEightPixels = _bitmapEightPixelsMap[(lineData, bgColorCode, fgColorCode)];

                        }
                        else if (characterMode == CharMode.Extended)
                        {
                            var bgColorSelector = characterCode >> 6;   // Bit 6 and 7 of character byte is used to select background color (0-3)
                            bgColorCode = bgColorSelector switch
                            {
                                0 => c64ScreenLineIORegisterValues[screenLine].BackgroundColor0,
                                1 => backgroundColor1,
                                2 => backgroundColor2,
                                3 => backgroundColor3,
                                _ => throw new NotImplementedException($"Background color selector {bgColorSelector} not implemented.")
                            };
                            characterCode = (byte)(characterCode & 0b00111111); // The actual usable character codes are in the lower 6 bits (0-63)

                            // Get the corresponding array of uints representing the 8 pixels of the character
                            bitmapEightPixels = _bitmapEightPixelsMap[(lineData, bgColorCode, fgColorCode)];

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

                screenLine++;
            }
        }
    }
}
