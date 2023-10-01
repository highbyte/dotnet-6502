using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class C64SkiaRenderer : IRenderer<C64, SkiaRenderContext>, IRenderer
{
    private Func<SKCanvas> _getSkCanvas;

    private C64SkiaPaint _c64SkiaPaint;

    // Character drawing variables
    private const int CHARGEN_IMAGE_CHARACTERS_PER_ROW = 16;
    private SKImage _characterSetCurrent;
    private SKImage _characterSetROMShiftedImage;
    private SKImage _characterSetROMUnshiftedImage;
    private SKRect _drawImageSource = new SKRect();
    private SKRect _drawImageDest = new SKRect();

    // Sprite drawing variables
    private readonly SKImage[] _spriteImages = new SKImage[Vic2SpriteManager.NUMBERS_OF_SPRITES];

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

    public void Draw(C64 c64)
    {
        var canvas = _getSkCanvas();
        canvas.Clear();

        RenderBackgroundAndBorder(c64, canvas);

        RenderSprites(c64, canvas, spritesWithPriorityOverForeground: false);

        RenderMainScreen(c64, canvas);

        RenderSprites(c64, canvas, spritesWithPriorityOverForeground: true);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    private void InitCharset(C64 c64)
    {
        // Generate and remember images of the Chargen ROM charset.
        GenerateROMChargenImages(c64);
        // Default to shifted ROM character set
        _characterSetCurrent = _characterSetROMShiftedImage;
        // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        c64.Vic2.CharsetAddressChanged += (s, e) => GenerateCurrentChargenImage(c64);
    }

    private void GenerateROMChargenImages(C64 c64)
    {
        // Get the two character sets (shifted & unshifted) from VIC2 view of memory (considering selected 16KB bank and charset start offset)

        var characterSets = c64.ROMData[C64Config.CHARGEN_ROM_NAME];

        // Chargen ROM data contains two character sets (1024 bytes each).
        var characterSetShifted = characterSets.Take(Vic2.CHARACTERSET_SIZE).ToArray();
        var characterSetUnShifted = characterSets.Skip(Vic2.CHARACTERSET_SIZE).Take(Vic2.CHARACTERSET_SIZE).ToArray();

        var chargen = new Chargen();
        // Generate and save the images for the two Chargen ROM character sets
        _characterSetROMShiftedImage = chargen.GenerateChargenImage(characterSetShifted, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);
        _characterSetROMUnshiftedImage = chargen.GenerateChargenImage(characterSetUnShifted, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);

#if DEBUG
        chargen.DumpChargenFileToImageFile(_characterSetROMShiftedImage, $"{Path.GetTempPath()}/c64_chargen_shifted_dump.png");
        chargen.DumpChargenFileToImageFile(_characterSetROMUnshiftedImage, $"{Path.GetTempPath()}/c64_chargen_unshifted_dump.png");
#endif
    }

    // TODO: Vic2 class should generate event when VIC2 bank (in 0xdd00) or VIC2 character set offset (in 0x
    // d018) is changed, so we can generate new character set image.
    //       Detect if the VIC2 address is a Chargen ROM shadow location (bank 0 and 2, offset 0x1000 or 0x1800), if so we don't need to generate new image, instead use pre-generated images we did on Init()
    private void GenerateCurrentChargenImage(C64 c64)
    {
        // If the current address points to a location in where the Chargen ROM character sets are located, we can use pre-rendered images for the character set.
        if (c64.Vic2.CharacterSetAddressInVIC2BankIsChargenROMUnshifted)
        {
            _characterSetCurrent = _characterSetROMUnshiftedImage;
            return;
        }
        else if (c64.Vic2.CharacterSetAddressInVIC2BankIsChargenROMShifted)
        {
            _characterSetCurrent = _characterSetROMShiftedImage;
            return;
        }
        // Pointing to a location where a custom character set is located. Create a image for it.
        var characterSet = c64.Vic2.Vic2Mem.ReadData(c64.Vic2.CharacterSetAddressInVIC2Bank, Vic2.CHARACTERSET_SIZE);
        var chargen = new Chargen();
        _characterSetCurrent = chargen.GenerateChargenImage(characterSet, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);
    }

    private void RenderMainScreen(C64 c64, SKCanvas canvas)
    {
        var vic2Mem = c64.Vic2.Vic2Mem;
        var vic2IOStorage = c64.Vic2.Vic2IOStorage;

        var vic2Screen = c64.Vic2.Vic2Screen;
        var vic2ScreenLayouts = c64.Vic2.ScreenLayouts;

        // Offset based on horizontal and vertical scrolling settings
        var scrollX = c64.Vic2.GetScrollX();
        var scrollY = c64.Vic2.GetScrollY();

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

        // Build screen data characters based on emulator memory contents (byte)
        var currentScreenAddress = Vic2Addr.SCREEN_RAM_START;   // TODO: Screen RAM start should be calculated based on current VIC2 bank and screen offset
        var currentColorAddress = Vic2Addr.COLOR_RAM_START;
        for (var row = 0; row < vic2Screen.TextRows; row++)
        {
            for (var col = 0; col < vic2Screen.TextCols; col++)
            {
                var charByte = vic2Mem[currentScreenAddress++];
                var colorByte = vic2IOStorage[currentColorAddress++];  // Note: Color RAM is always at fixed CPU location in CPU ram (not withing the 16K area mapped to the VIC2)
                DrawEmulatorCharacterOnScreen(
                    canvas,
                    visibleMainScreenArea.Screen.Start.X,
                    visibleMainScreenArea.Screen.Start.Y,
                    col,
                    row,
                    charByte,
                    colorByte,
                    c64
                    );
            }
        }

        // Restore canvas adjustments
        canvas.Restore();
    }

    private void RenderBackgroundAndBorder(C64 c64, SKCanvas canvas)
    {
        //DrawSimpleBorder(c64, canvas);
        DrawRasterLinesBorder(c64, canvas);

        //DrawSimpleBackground(c64, canvas);
        DrawRasterLinesBackground(c64, canvas);

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

        foreach (var c64ScreenLine in c64.Vic2.ScreenLineBorderColor.Keys)
        {
            if (c64ScreenLine < visibileLayout.TopBorder.Start.Y || c64ScreenLine > visibileLayout.BottomBorder.End.Y)
                continue;
            var borderColor = c64.Vic2.ScreenLineBorderColor[c64ScreenLine];
            var canvasYPos = (ushort)(c64ScreenLine - visibileLayout.TopBorder.Start.Y);
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

        foreach (var c64ScreenLine in c64.Vic2.ScreenLineBackgroundColor.Keys)
        {
            if (c64ScreenLine < visibleMainScreenArea.Screen.Start.Y || c64ScreenLine > visibleMainScreenArea.Screen.End.Y)
                continue;
            var backgroundColor = c64.Vic2.ScreenLineBackgroundColor[c64ScreenLine];
            var canvasYPos = (ushort)(c64ScreenLine - visibleMainScreenArea.TopBorder.Start.Y);
            canvas.DrawRect(canvasXPosStart, canvasYPos, drawWidth, 1, _c64SkiaPaint.GetFillPaint(backgroundColor));
        }
    }

    // Simple approximation, draw 4 rectangles for border. Fast, but does not handle changes in border color per raster line.
    private void DrawSimpleBorder(C64 c64, SKCanvas canvas)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        var borderColor = emulatorMem[Vic2Addr.BORDER_COLOR];
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
        var backgroundColor = emulatorMem[Vic2Addr.BACKGROUND_COLOR];
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
        byte characterColor,
        C64 c64)
    {
        var vic2Screen = c64.Vic2.Vic2Screen;

        var pixelPosX = col * vic2Screen.CharacterWidth;
        var pixelPosY = row * vic2Screen.CharacterHeight;

        // Adjust for left border
        pixelPosX += firstVisibleScreenXPos;

        // Adjust for top border
        pixelPosY += firstVisibleScreenYPos;
        //pixelPosY += vic2Screen.VisibleTopBottomBorderHeight;

        // Draw character image from chargen ROM to a Skia surface
        // The chargen ROM has been loaded to a SKImage with 16 characters per row (each character 8 x 8 pixels).
        var romImageX = character % CHARGEN_IMAGE_CHARACTERS_PER_ROW * 8;
        var romImageY = character / CHARGEN_IMAGE_CHARACTERS_PER_ROW * 8;

        _drawImageSource.Left = romImageX;
        _drawImageSource.Top = romImageY;
        _drawImageSource.Right = romImageX + 8;
        _drawImageSource.Bottom = romImageY + 8;

        _drawImageDest.Left = pixelPosX;
        _drawImageDest.Top = pixelPosY;
        _drawImageDest.Right = pixelPosX + 8;
        _drawImageDest.Bottom = pixelPosY + 8;

        var paint = _c64SkiaPaint.GetDrawCharacterPaint(characterColor);
        canvas.DrawImage(_characterSetCurrent,
            //source: new SKRect(romImageX, romImageY, romImageX + 8, romImageY + 8),
            //dest: new SKRect(pixelPosX, pixelPosY, pixelPosX + 8, pixelPosY + 8),
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
                spriteGen.DumpSpriteToImageFile(_spriteImages[sprite.SpriteNumber], $"{Path.GetTempPath()}/c64_sprite_{sprite.SpriteNumber}.png");
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
