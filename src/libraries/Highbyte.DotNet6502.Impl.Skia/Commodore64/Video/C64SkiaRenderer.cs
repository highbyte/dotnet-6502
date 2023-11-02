using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class C64SkiaRenderer : IRenderer<C64, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas;

    private C64SkiaPaint _c64SkiaPaint;

    private Dictionary<int, SKImage> _characterSetCurrent;
    private Dictionary<int, SKImage> _characterSetMultiColorCurrent;

    private Dictionary<int, SKImage> _characterSetROMShiftedImage;
    private Dictionary<int, SKImage> _characterSetROMShiftedMultiColorImage;
    private Dictionary<int, SKImage> _characterSetROMUnshiftedImage;
    private Dictionary<int, SKImage> _characterSetROMUnshiftedMultiColorImage;

    private bool _changedAllCharsetCodes = false;
    private HashSet<byte> _changedCharsetCodes = new();

    private SKRect _drawImageSource = new SKRect();
    private SKRect _drawImageDest = new SKRect();

    private readonly CharGen _charGen;

    // Sprite drawing variables
    private readonly SKImage[] _spriteImages;

    public bool HasDetailedStats => true;
    public List<string> DetailedStatNames => new List<string>()
    {
        DrawBorderStatName,
        DrawBackgroundStatName,
        DrawTextScreenStatName,
        DrawSpritesStatName,
    };
    private const string DrawBorderStatName = "Border";
    private const string DrawBackgroundStatName = "Background";
    private const string DrawTextScreenStatName = "TextScreen";
    private const string DrawSpritesStatName = "Sprites";
    private Dictionary<string, Stopwatch> _detailedStatStopwatches = new()
    {
        {DrawBorderStatName, new Stopwatch() },
        {DrawBackgroundStatName, new Stopwatch() },
        {DrawTextScreenStatName, new Stopwatch() },
        {DrawSpritesStatName, new Stopwatch() }
    };

    public C64SkiaRenderer()
    {
        _charGen = new CharGen();
        _spriteImages = new SKImage[Vic2SpriteManager.NUMBERS_OF_SPRITES];
    }

    public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
    {
        _getSkCanvas = skiaRenderContext.GetCanvas;

        _c64SkiaPaint = new C64SkiaPaint(c64.ColorMapName);

        InitCharset(c64);
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SkiaRenderContext)renderContext);
    }

    public void Draw(C64 c64, Dictionary<string, double> detailedStats)
    {
        foreach (var detailedStatName in DetailedStatNames)
        {
            detailedStats[detailedStatName] = 0;
        }

        var canvas = _getSkCanvas();
        canvas.Clear();

        var swBorder = _detailedStatStopwatches[DrawBorderStatName];
        swBorder.Restart();
        //DrawSimpleBorder(c64, canvas);
        DrawRasterLinesBorder(c64, canvas);
        swBorder.Stop();
        detailedStats[DrawBorderStatName] = swBorder.Elapsed.TotalMilliseconds;

        var swBg = _detailedStatStopwatches[DrawBackgroundStatName];
        swBg.Restart();
        //DrawSimpleBackground(c64, canvas);
        DrawRasterLinesBackground(c64, canvas);
        swBg.Stop();
        detailedStats[DrawBackgroundStatName] = swBg.Elapsed.TotalMilliseconds;

        var swSprites = _detailedStatStopwatches[DrawSpritesStatName];
        swSprites.Restart();
        RenderSprites(c64, canvas, spritesWithPriorityOverForeground: false);
        swSprites.Stop();
        detailedStats[DrawSpritesStatName] = swBg.Elapsed.TotalMilliseconds;

        var swTextScreen = _detailedStatStopwatches[DrawTextScreenStatName];
        swTextScreen.Restart();
        RenderMainScreen(c64, canvas);
        swTextScreen.Stop();
        detailedStats[DrawTextScreenStatName] = swTextScreen.Elapsed.TotalMilliseconds;

        swSprites.Restart();
        RenderSprites(c64, canvas, spritesWithPriorityOverForeground: true);
        swSprites.Stop();
        detailedStats[DrawBackgroundStatName] += swSprites.Elapsed.TotalMilliseconds;
    }

    public void Draw(ISystem system, Dictionary<string, double> detailedStats)
    {
        Draw((C64)system, detailedStats);
    }

    private void InitCharset(C64 c64)
    {
        // Generate and remember images of the CharGen ROM charset.
        GenerateROMChargenImages(c64);

        // Default to shifted ROM character set
        _characterSetCurrent = _characterSetROMShiftedImage;
        _characterSetMultiColorCurrent = _characterSetROMShiftedMultiColorImage;

        // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        c64.Vic2.CharsetManager.CharsetAddressChanged += (s, e) => CharsetChangedHandler(c64, e);
    }

    private void GenerateROMChargenImages(C64 c64)
    {
        // Get the two character sets (shifted & unshifted) from VIC2 view of memory (considering selected 16KB bank and charset start offset)

        var characterSets = c64.ROMData[C64Config.CHARGEN_ROM_NAME];

        // CharGen ROM data contains two character sets (1024 bytes each).
        var characterSetShifted = characterSets.Take(Vic2CharsetManager.CHARACTERSET_SIZE).ToArray();
        var characterSetUnShifted = characterSets.Skip(Vic2CharsetManager.CHARACTERSET_SIZE).Take(Vic2CharsetManager.CHARACTERSET_SIZE).ToArray();

        // Generate and save the images for the two CharGen ROM character sets
        _characterSetROMShiftedImage = _charGen.GenerateChargenImages(characterSetShifted);
        _characterSetROMUnshiftedImage = _charGen.GenerateChargenImages(characterSetUnShifted);

        // Same for multi-color versions
        _characterSetROMShiftedMultiColorImage = _charGen.GenerateChargenImages(characterSetShifted, multiColor: true);
        _characterSetROMUnshiftedMultiColorImage = _charGen.GenerateChargenImages(characterSetUnShifted, multiColor: true);

#if DEBUG
        _charGen.DumpChargenImagesToOneFile(_characterSetROMShiftedImage, $"{Path.GetTempPath()}/c64_chargen_shifted_dump.png");
        _charGen.DumpChargenImagesToOneFile(_characterSetROMUnshiftedImage, $"{Path.GetTempPath()}/c64_chargen_unshifted_dump.png");

        _charGen.DumpChargenImagesToOneFile(_characterSetROMShiftedMultiColorImage, $"{Path.GetTempPath()}/c64_chargen_shifted_multicolor_dump.png");
        _charGen.DumpChargenImagesToOneFile(_characterSetROMUnshiftedMultiColorImage, $"{Path.GetTempPath()}/c64_chargen_unshifted_multicolor_dump.png");
#endif
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

    private void UpdateChangedCharacterOnCurrentImage(C64 c64, byte charCode)
    {
        var charsetManager = c64.Vic2.CharsetManager;
        var characterSet = c64.Vic2.Vic2Mem.ReadData(charsetManager.CharacterSetAddressInVIC2Bank, Vic2CharsetManager.CHARACTERSET_SIZE);

        _characterSetCurrent[charCode] = _charGen.GenerateChargenImageForOneCharacter(characterSet, charCode, multiColor: false);
        _characterSetMultiColorCurrent[charCode] = _charGen.GenerateChargenImageForOneCharacter(characterSet, charCode, multiColor: true);

        //#if DEBUG
        //        _charGen.DumpChargenImagesToOneFile(_characterSetCurrent, $"{Path.GetTempPath()}/c64_chargen_custom_dump.png");
        //        _charGen.DumpChargenImagesToOneFile(_characterSetMultiColorCurrent, $"{Path.GetTempPath()}/c64_chargen_custom_multicolor_dump.png");
        //#endif

    }

    private void GenerateCurrentChargenImage(C64 c64)
    {
        var charsetManager = c64.Vic2.CharsetManager;

        // If the current address points to a location in where the CharGen ROM character sets are located, we can use pre-rendered images for the character set.
        if (charsetManager.CharacterSetAddressInVIC2BankIsChargenROMUnshifted)
        {
            _characterSetCurrent = _characterSetROMUnshiftedImage;
            _characterSetMultiColorCurrent = _characterSetROMUnshiftedMultiColorImage;
            return;
        }
        else if (charsetManager.CharacterSetAddressInVIC2BankIsChargenROMShifted)
        {
            _characterSetCurrent = _characterSetROMShiftedImage;
            _characterSetMultiColorCurrent = _characterSetROMShiftedMultiColorImage;
            return;
        }
        // Pointing to a location where a custom character set is located. Create a image for it.

        // TODO: Is there a concept of "shifted" and "unshifted" character set for custom ones, or is it only relevant for the ones from CharGen ROM and the switching mechanism for them in Basic?
        var characterSet = c64.Vic2.Vic2Mem.ReadData(charsetManager.CharacterSetAddressInVIC2Bank, Vic2CharsetManager.CHARACTERSET_SIZE);

        _characterSetCurrent = _charGen.GenerateChargenImages(characterSet);
        _characterSetMultiColorCurrent = _charGen.GenerateChargenImages(characterSet, multiColor: true);

#if DEBUG
        _charGen.DumpChargenImagesToOneFile(_characterSetCurrent, $"{Path.GetTempPath()}/c64_chargen_custom_dump.png");
        _charGen.DumpChargenImagesToOneFile(_characterSetMultiColorCurrent, $"{Path.GetTempPath()}/c64_chargen_custom_multicolor_dump.png");
#endif
    }

    private void RenderMainScreen(C64 c64, SKCanvas canvas)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;

        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        // Offset based on horizontal and vertical scrolling settings
        var scrollX = vic2.GetScrollX();
        var scrollY = vic2.GetScrollY();

        // Clip main screen area with consideration to possible 38 column and 24 row mode
        var visibleClippedScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized);

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // Remember original canvas adjustments
        canvas.Save();
        // Clip to the visible character screen area
        canvas.ClipRect(
            new SKRect(
                visibleClippedScreenArea.Screen.Start.X,
                visibleClippedScreenArea.Screen.Start.Y,
                visibleClippedScreenArea.Screen.End.X + 1,
                visibleClippedScreenArea.Screen.End.Y + 1),
            SKClipOperation.Intersect);
        canvas.Translate(scrollX, scrollY);

        // Re-create any changed characters in current charset since last frame
        if (_changedAllCharsetCodes)
        {
            GenerateCurrentChargenImage(c64);
            _changedAllCharsetCodes = false;
            _changedCharsetCodes.Clear();
        }
        else if (_changedCharsetCodes.Count > 0)
        {
            foreach (var charCode in _changedCharsetCodes)
            {
                UpdateChangedCharacterOnCurrentImage(c64, charCode);
            }
            _changedCharsetCodes.Clear();
        }

        // Build screen data characters based on emulator memory contents (byte)
        var currentScreenAddress = vic2.VideoMatrixBaseAddress;
        var currentColorAddress = Vic2Addr.COLOR_RAM_START;

        var characterMode = vic2.CharacterMode;
        byte backgroundColor1 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1); // Background color used for extended character mode
        byte backgroundColor2 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2); // Background color used for extended character mode
        byte backgroundColor3 = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3); // Background color used for extended character mode

        for (var row = 0; row < vic2Screen.TextRows; row++)
        {
            for (var col = 0; col < vic2Screen.TextCols; col++)
            {
                var charByte = vic2Mem[currentScreenAddress++];
                var colorByte = c64.ReadIOStorage(currentColorAddress++);

                DrawEmulatorCharacterOnScreen(
                    canvas,
                    visibleMainScreenArea.Screen.Start.X,
                    visibleMainScreenArea.Screen.Start.Y,
                    col,
                    row,
                    charByte,
                    colorByte,
                    c64,
                    characterMode,
                    backgroundColor1,
                    backgroundColor2,
                    backgroundColor3
                    );
            }
        }

        // Restore canvas adjustments
        canvas.Restore();
    }


    // Draw border per line across screen. Assumes the screen in the middle is drawn afterwards and will overwrite.
    // Slower, but more accurate (though not completley, becasuse border color changes within a line is not accounted for).
    private void DrawRasterLinesBorder(C64 c64, SKCanvas canvas)
    {
        var vic2Screen = c64.Vic2.Vic2Screen;
        var vic2ScreenLayouts = c64.Vic2.ScreenLayouts;

        //var visibileVerticalPositions = vic2Screen.GetVerticalPositions(visible: true, normalizeToVisible: false, for24RowMode: c64.Vic2.Is24RowDisplayEnabled);
        var visibileLayout = vic2ScreenLayouts.GetLayout(LayoutType.Visible);

        var drawWidth = vic2Screen.VisibleWidth;

        foreach (var c64ScreenLine in c64.Vic2.ScreenLineIORegisterValues.Keys)
        {
            if (c64ScreenLine < visibileLayout.TopBorder.Start.Y || c64ScreenLine > visibileLayout.BottomBorder.End.Y)
                continue;
            var canvasYPos = (ushort)(c64ScreenLine - visibileLayout.TopBorder.Start.Y);
            var borderColor = c64.Vic2.ScreenLineIORegisterValues[c64ScreenLine].BorderColor;
            canvas.DrawRect(0, canvasYPos, drawWidth, 1, _c64SkiaPaint.GetFillPaint(borderColor));
        }
    }

    // Draw background per line.
    // Slower, but more accurate (though not completley, becasuse background color changes within a line is not accounted for).
    private void DrawRasterLinesBackground(C64 c64, SKCanvas canvas)
    {
        var vic2ScreenLayouts = c64.Vic2.ScreenLayouts;

        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.Visible);
        var canvasXPosStart = visibleMainScreenArea.Screen.Start.X - visibleMainScreenArea.LeftBorder.Start.X;
        var drawWidth = visibleMainScreenArea.Screen.End.X - visibleMainScreenArea.Screen.Start.X + 1;

        foreach (var c64ScreenLine in c64.Vic2.ScreenLineIORegisterValues.Keys)
        {
            if (c64ScreenLine < visibleMainScreenArea.Screen.Start.Y || c64ScreenLine > visibleMainScreenArea.Screen.End.Y)
                continue;
            var canvasYPos = (ushort)(c64ScreenLine - visibleMainScreenArea.TopBorder.Start.Y);
            var backgroundColor = c64.Vic2.ScreenLineIORegisterValues[c64ScreenLine].BackgroundColor0;
            canvas.DrawRect(canvasXPosStart, canvasYPos, drawWidth, 1, _c64SkiaPaint.GetFillPaint(backgroundColor));
        }
    }

    // Simple approximation, draw 4 rectangles for border. Fast, but does not handle changes in border color per raster line.
    private void DrawSimpleBorder(C64 c64, SKCanvas canvas)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        var borderColor = c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);
        var borderPaint = _c64SkiaPaint.GetFillPaint(borderColor);

        canvas.DrawRect(0, 0, vic2Screen.VisibleWidth, vic2Screen.VisibleTopBottomBorderHeight, borderPaint);
        canvas.DrawRect(0, vic2Screen.VisibleTopBottomBorderHeight + vic2Screen.DrawableAreaHeight, vic2Screen.VisibleWidth, vic2Screen.VisibleTopBottomBorderHeight, borderPaint);
        canvas.DrawRect(0, vic2Screen.VisibleTopBottomBorderHeight, vic2Screen.VisibleLeftRightBorderWidth, vic2Screen.DrawableAreaHeight, borderPaint);
        canvas.DrawRect(vic2Screen.VisibleLeftRightBorderWidth + vic2Screen.DrawableAreaWidth, vic2Screen.VisibleTopBottomBorderHeight, vic2Screen.VisibleLeftRightBorderWidth, vic2Screen.DrawableAreaHeight, borderPaint);
    }

    // Simple approximation, draw 1 rectangle for border. Fast, but does not handle changes in background color per raster line.
    private void DrawSimpleBackground(C64 c64, SKCanvas canvas)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        // Draw 1 rectangle for background
        var backgroundColor = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);
        var bgPaint = _c64SkiaPaint.GetFillPaint(backgroundColor);

        canvas.DrawRect(vic2Screen.VisibleLeftRightBorderWidth, vic2Screen.VisibleTopBottomBorderHeight, vic2Screen.DrawableAreaWidth, vic2Screen.DrawableAreaHeight, bgPaint);
    }

    /// <summary>
    /// Draw character to screen, with adjusted position for border.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="character"></param>
    /// <param name="characterColor"></param>
    public void DrawEmulatorCharacterOnScreen(
        SKCanvas canvas,
        int firstVisibleScreenXPos,
        int firstVisibleScreenYPos,
        int col,
        int row,
        byte character,
        byte characterColor,    // Standard and Extended mode text color. Also in MultiColor mode, the color of pixel-pair 11
        C64 c64,
        CharMode characterMode,
        byte backgroundColor1,  // Extended mode, background color for each character. Also in MultiColor mode, the color of pixel-pair 01
        byte backgroundColor2,  // Extended mode, background color for each character. Also in MultiColor mode, the color of pixel-pair 10
        byte backgroundColor3   // Extended mode, background color for each character
        )
    {
        var vic2Screen = c64.Vic2.Vic2Screen;

        var pixelPosX = col * vic2Screen.CharacterWidth;
        var pixelPosY = row * vic2Screen.CharacterHeight;

        // Adjust for left border
        pixelPosX += firstVisibleScreenXPos;

        // Adjust for top border
        pixelPosY += firstVisibleScreenYPos;
        //pixelPosY += vic2Screen.VisibleTopBottomBorderHeight;

        // Check character mode: affects background color, usable bits in character, usable bits in color
        byte? bgColor = null;
        switch (characterMode)
        {
            case CharMode.Standard:
                break;

            case CharMode.Extended:
                var bgColorSelector = character >> 6;   // Bit 6 and 7 of character byte is used to select background color (0-3)
                bgColor = bgColorSelector switch
                {
                    0 => null,
                    1 => backgroundColor1,
                    2 => backgroundColor2,
                    3 => backgroundColor3,
                    _ => throw new NotImplementedException($"Background color selector {bgColorSelector} not implemented.")
                };
                character = (byte)(character & 0b00111111); // The actual usable character codes are in the lower 6 bits (0-63)
                break;

            case CharMode.MultiColor:
                // When in MultiColor mode, a character can still be displayed in Standard mode depending on the value from color RAM.
                if (characterColor <= 7)
                {
                    // If color RAM value is 0-7, normal Standard mode is used (not multi-color)
                    characterMode = CharMode.Standard;
                }
                else
                {
                    // If displaying in MultiColor mode, the actual color used from color RAM will be values 0-7.
                    // Thus color values 8-15 are transformed to 0-7
                    characterColor = (byte)((characterColor & 0b00001111) - 8);
                }
                break;
            default:
                throw new NotImplementedException($"Character mode {characterMode} not implemented.");
        }

        // Select correct pre-generated characters depending on character mode
        var charImage = characterMode switch
        {
            CharMode.Standard => _characterSetCurrent[character],
            CharMode.Extended => _characterSetCurrent[character],
            CharMode.MultiColor => _characterSetMultiColorCurrent[character],
            _ => throw new NotImplementedException($"Character mode {characterMode} not implemented.")
        };

        // The "paint" transforms fixed colors in the pre-generated image to the ones that should be used on screen.
        SKPaint paint = characterMode switch
        {
            CharMode.Standard => _c64SkiaPaint.GetDrawCharacterPaint(characterColor),
            CharMode.Extended => bgColor == null ? _c64SkiaPaint.GetDrawCharacterPaint(characterColor) : _c64SkiaPaint.GetDrawCharacterPaintWithBackground(characterColor, bgColor.Value),
            CharMode.MultiColor => _c64SkiaPaint.GetDrawCharacterPaintWithMultiColor(characterColor, backgroundColor1, backgroundColor2),
            _ => throw new NotImplementedException($"Character mode {characterMode} not implemented.")
        };

        // Draw character image from chargen ROM to the Skia canvas
        _drawImageDest.Left = pixelPosX;
        _drawImageDest.Top = pixelPosY;
        _drawImageDest.Right = pixelPosX + 8;
        _drawImageDest.Bottom = pixelPosY + 8;

        _drawImageSource.Left = 0;
        _drawImageSource.Top = 0;
        _drawImageSource.Right = 8;
        _drawImageSource.Bottom = 8;

        canvas.DrawImage(charImage,
            source: _drawImageSource,
            dest: _drawImageDest,
            paint
            );
    }

    private void RenderSprites(C64 c64, SKCanvas canvas, bool spritesWithPriorityOverForeground)
    {
        var spriteGen = new SpriteGen(_c64SkiaPaint, c64.Vic2);

        var vic2Screen = c64.Vic2.Vic2Screen;
        var vic2ScreenLayouts = c64.Vic2.ScreenLayouts;

        // Clip main screen area with consideration to possible 38 column and 24 row mode
        var visibleClippedScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized);

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        canvas.Save();

        // Clip to visible area (normal text screen area, or including borders if they are opened)
        var clipRect = GetSpriteClipping(vic2Screen, visibleClippedScreenArea);
        canvas.ClipRect(
            clipRect,
            SKClipOperation.Intersect);

        // Draw sprites in reverse order, so that sprite 0 is drawn last and is on top of other sprites.
        foreach (var sprite in c64.Vic2.SpriteManager.Sprites
            .Where(s => s.PriorityOverForeground == spritesWithPriorityOverForeground)
            .OrderByDescending(s => s.SpriteNumber))
        {
            if (!sprite.Visible)
                continue;

            if (_spriteImages[sprite.SpriteNumber] == null || sprite.IsDirty)
            {
                _spriteImages[sprite.SpriteNumber] = spriteGen.GenerateSpriteImage(sprite);
                sprite.ClearDirty();
#if DEBUG
                //spriteGen.DumpSpriteToImageFile(_spriteImages[sprite.SpriteNumber], $"{Path.GetTempPath()}/c64_sprite_{sprite.SpriteNumber}.png");
#endif
            }
            var spriteImage = _spriteImages[sprite.SpriteNumber];

            var spriteCanvasX = sprite.X + visibleMainScreenArea.Screen.Start.X - Vic2SpriteManager.SCREEN_OFFSET_X;
            var spriteCanvasY = sprite.Y + visibleMainScreenArea.Screen.Start.Y - Vic2SpriteManager.SCREEN_OFFSET_Y;

            var spriteWidth = sprite.DoubleWidth ? Vic2Sprite.DEFAULT_WIDTH * 2 : Vic2Sprite.DEFAULT_WIDTH;
            var spriteHeight = sprite.DoubleHeight ? Vic2Sprite.DEFAULT_HEIGTH * 2 : Vic2Sprite.DEFAULT_HEIGTH;

            // TODO: Optimize by using pre-created SKRect objects with sprite dimensions
            var imageDest = new SKRect(spriteCanvasX, spriteCanvasY, spriteCanvasX + spriteWidth, spriteCanvasY + spriteHeight);

            if (sprite.Multicolor)
            {
                canvas.DrawImage(spriteImage, imageDest);
            }
            else
            {
                var paint = _c64SkiaPaint.GetDrawSpritePaint(sprite.Color);
                canvas.DrawImage(spriteImage, imageDest, paint);
            }
        }

        canvas.Restore();
    }

    private SKRect GetSpriteClipping(Vic2Screen vic2Screen, Vic2ScreenLayout visibileLayout)
    {
        // TODO: Detect if borders are opened
        bool verticalBorderOpened = false;
        bool horizontalBorderOpened = false;
        // Clip to main screen area, or if borders are opened (VIC2 trick) skip clipping
        int clipXStart, clipXEnd, clipYStart, clipYEnd;
        if (verticalBorderOpened)
        {
            clipXStart = 0;
            clipXEnd = vic2Screen.VisibleWidth;
        }
        else
        {
            clipXStart = visibileLayout.Screen.Start.X;
            clipXEnd = visibileLayout.Screen.End.X + 1;
        }
        if (horizontalBorderOpened)
        {
            clipYStart = 0;
            clipYEnd = vic2Screen.VisibleHeight;
        }
        else
        {
            clipYStart = visibileLayout.Screen.Start.Y;
            clipYEnd = visibileLayout.Screen.End.Y + 1;
        }

        return new SKRect(clipXStart, clipYStart, clipXEnd, clipYEnd);
    }
}
