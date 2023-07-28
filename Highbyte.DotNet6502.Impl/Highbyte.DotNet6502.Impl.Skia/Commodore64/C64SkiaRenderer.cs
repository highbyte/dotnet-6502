using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64;

public class C64SkiaRenderer : IRenderer<C64, SkiaRenderContext>, IRenderer
{
    private Func<SKCanvas> _getSkCanvas;
    private Func<GRContext> _getGRContext;

    private const int CHARGEN_IMAGE_CHARACTERS_PER_ROW = 16;

    private SKImage _characterSetCurrent;

    private SKImage _characterSetROMShiftedImage;
    private SKImage _characterSetROMUnshiftedImage;

    private SKRect _drawImageSource = new SKRect();
    private SKRect _drawImageDest = new SKRect();

    private C64SkiaPaint _c64SkiaPaint;

    public void Init(C64 c64, SkiaRenderContext skiaRenderContext)
    {
        _getSkCanvas = skiaRenderContext.GetCanvas;
        _getGRContext = skiaRenderContext.GetGRContext;

        _c64SkiaPaint = new C64SkiaPaint(c64.ColorMapName);

        InitCharset(c64, _getGRContext());
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SkiaRenderContext)renderContext);
    }

    public void Draw(C64 c64)
    {
        RenderBackgroundAndBorder(c64);
        RenderMainScreen(c64);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    private void InitCharset(C64 c64, GRContext grContext)
    {
        // Generate and remember images of the Chargen ROM charset.
        GenerateROMChargenImages(c64, grContext);
        // Default to shifted ROM character set
        _characterSetCurrent = _characterSetROMShiftedImage;
        // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        c64.Vic2.CharsetAddressChanged += (s, e) => GenerateCurrentChargenImage(c64, grContext);
    }

    private void GenerateROMChargenImages(C64 c64, GRContext grContext)
    {
        // Get the two character sets (shifted & unshifted) from VIC2 view of memory (considering selected 16KB bank and charset start offset)

        var characterSets = c64.ROMData[C64Config.CHARGEN_ROM_NAME];

        // Chargen ROM data contains two character sets (1024 bytes each).
        var characterSetShifted = characterSets.Take(Vic2.CHARACTERSET_SIZE).ToArray();
        var characterSetUnShifted = characterSets.Skip(Vic2.CHARACTERSET_SIZE).Take(Vic2.CHARACTERSET_SIZE).ToArray();

        var chargen = new Chargen();
        // Generate and save the images for the two Chargen ROM character sets
        _characterSetROMShiftedImage = chargen.GenerateChargenImage(grContext, characterSetShifted, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);
        _characterSetROMUnshiftedImage = chargen.GenerateChargenImage(grContext, characterSetUnShifted, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);

#if DEBUG
        chargen.DumpChargenFileToImageFile(_characterSetROMShiftedImage, $"{Path.GetTempPath()}/c64_chargen_shifted_dump.png");
        chargen.DumpChargenFileToImageFile(_characterSetROMUnshiftedImage, $"{Path.GetTempPath()}/c64_chargen_unshifted_dump.png");
#endif
    }

    // TODO: Vic2 class should generate event when VIC2 bank (in 0xdd00) or VIC2 character set offset (in 0xd018) is changed, so we can generate new character set image.
    //       Detect if the VIC2 address is a Chargen ROM shadow location (bank 0 and 2, offset 0x1000 or 0x1800), if so we don't need to generate new image, instead use pre-generated images we did on Init()
    private void GenerateCurrentChargenImage(C64 c64, GRContext grContext)
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
        _characterSetCurrent = chargen.GenerateChargenImage(grContext, characterSet, charactersPerRow: CHARGEN_IMAGE_CHARACTERS_PER_ROW);
    }

    private void RenderMainScreen(C64 c64)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        var firstVisibleScreenXPos = vic2Screen.BorderWidth;
        var firstVisibleScreenYPos = vic2Screen.FirstScreenLineOfMainScreen - vic2Screen.FirstVisibleScreenLineOfMainScreen;
        var lastVisibleScreenXPos = firstVisibleScreenXPos + vic2Screen.Width;
        var lastVisibleScreenYPos = firstVisibleScreenYPos + vic2Screen.Height;

        // Offset based on horizontal and vertical scrolling settings
        var scrollX = c64.Vic2.FineScrollXValue;
        var scrollY = c64.Vic2.FineScrollYValue - 3;// Note: VIC2 Y scroll value is by default 3 (=no offset)

        // Clip main screen area if 38 column mode (default 40) or 24 (default 25) row mode is enabled
        var clippedFirstVisibleScreenXPos = firstVisibleScreenXPos;
        var clippedFirstVisibleScreenYPos = firstVisibleScreenYPos;
        var clippedLastVisibleScreenXPos = lastVisibleScreenXPos;
        var clippedLastVisibleScreenYPos = lastVisibleScreenYPos;
        if (c64.Vic2.Is38ColumnDisplayEnabled)
        {
            clippedFirstVisibleScreenXPos += 8;
            clippedLastVisibleScreenXPos -= 8;

            scrollX += 1;   // Note: In 38 column mode, the screen is shifted 1 pixel to the right (at least as it's shown in VICE emulator)
        }

        // Remember original canvas adjustments
        var canvas = _getSkCanvas();
        canvas.Save();
        // Clip to the visible character screen area
        canvas.ClipRect(new SKRect(clippedFirstVisibleScreenXPos, clippedFirstVisibleScreenYPos, clippedLastVisibleScreenXPos, clippedLastVisibleScreenYPos), SKClipOperation.Intersect);
        canvas.Translate(scrollX, scrollY);

        // Build screen data characters based on emulator memory contents (byte)
        ushort currentScreenAddress = Vic2Addr.SCREEN_RAM_START;
        ushort currentColorAddress = Vic2Addr.COLOR_RAM_START;
        for (int row = 0; row < vic2Screen.Rows; row++)
        {
            for (int col = 0; col < vic2Screen.Cols; col++)
            {
                byte charByte = emulatorMem[currentScreenAddress++];
                byte colorByte = emulatorMem[currentColorAddress++];
                DrawEmulatorCharacterOnScreen(
                    canvas,
                    firstVisibleScreenXPos,
                    firstVisibleScreenYPos,
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

    private void RenderBackgroundAndBorder(C64 c64)
    {
        var emulatorMem = c64.Mem;
        var canvas = _getSkCanvas();

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
        var firstVisibleScreenLineOfMainScreen = vic2Screen.FirstVisibleScreenLineOfMainScreen;
        var lastVisibleScreenLineOfMainScreen = vic2Screen.LastVisibleScreenLineOfMainScreen;

        foreach (var c64ScreenLine in c64.Vic2.ScreenLineBorderColor.Keys)
        {
            if (c64ScreenLine < firstVisibleScreenLineOfMainScreen || c64ScreenLine > lastVisibleScreenLineOfMainScreen)
                continue;
            var borderColor = c64.Vic2.ScreenLineBorderColor[c64ScreenLine];
            ushort canvasLine = (ushort)(c64ScreenLine - vic2Screen.FirstVisibleScreenLineOfMainScreen);
            canvas.DrawRect(0, canvasLine, vic2Screen.VisibleWidth, 1, _c64SkiaPaint.GetFillPaint(borderColor));
        }
    }

    // Draw background per line.
    // Slower, but more accurate (though not completley, becasuse background color changes within a line is not accounted for).
    private void DrawRasterLinesBackground(C64 c64, SKCanvas canvas)
    {
        var vic2Screen = c64.Vic2.Vic2Screen;
        var firstVisibleScreenXPos = vic2Screen.BorderWidth;
        var screenWidth = vic2Screen.Width;

        if (c64.Vic2.Is38ColumnDisplayEnabled)
        {
            firstVisibleScreenXPos += 8;
            screenWidth -= 16;
        }

        foreach (var c64ScreenLine in c64.Vic2.ScreenLineBackgroundColor.Keys)
        {
            if (c64ScreenLine < vic2Screen.FirstScreenLineOfMainScreen || c64ScreenLine > vic2Screen.LastScreenLineOfMainScreen)
                continue;
            var backgroundColor = c64.Vic2.ScreenLineBackgroundColor[c64ScreenLine];
            ushort canvasLine = (ushort)(c64ScreenLine - vic2Screen.FirstVisibleScreenLineOfMainScreen);
            canvas.DrawRect(firstVisibleScreenXPos, canvasLine, screenWidth, 1, _c64SkiaPaint.GetFillPaint(backgroundColor));
        }
    }

    // Simple approximation, draw 4 rectangles for border. Fast, but does not handle changes in border color per raster line.
    private void DrawSimpleBorder(C64 c64, SKCanvas canvas)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        byte borderColor = emulatorMem[Vic2Addr.BORDER_COLOR];
        SKPaint borderPaint = _c64SkiaPaint.GetFillPaint(borderColor);

        canvas.DrawRect(0, 0, vic2Screen.VisibleWidth, vic2Screen.BorderHeight, borderPaint);
        canvas.DrawRect(0, (vic2Screen.BorderHeight + vic2Screen.Height), vic2Screen.VisibleWidth, vic2Screen.BorderHeight, borderPaint);
        canvas.DrawRect(0, vic2Screen.BorderHeight, vic2Screen.BorderWidth, vic2Screen.Height, borderPaint);
        canvas.DrawRect(vic2Screen.BorderWidth + vic2Screen.Width, vic2Screen.BorderHeight, vic2Screen.BorderWidth, vic2Screen.Height, borderPaint);
    }

    // Simple approximation, draw 1 rectangle for border. Fast, but does not handle changes in background color per raster line.
    private void DrawSimpleBackground(C64 c64, SKCanvas canvas)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        // Draw 1 rectangle for background
        byte backgroundColor = emulatorMem[Vic2Addr.BACKGROUND_COLOR];
        SKPaint bgPaint = _c64SkiaPaint.GetFillPaint(backgroundColor);

        canvas.DrawRect(vic2Screen.BorderWidth, vic2Screen.BorderHeight, vic2Screen.Width, vic2Screen.Height, bgPaint);
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

        int pixelPosX = col * vic2Screen.CharacterWidth;
        int pixelPosY = row * vic2Screen.CharacterHeight;

        // Adjust for left border
        pixelPosX += firstVisibleScreenXPos;

        // Adjust for top border
        pixelPosY += firstVisibleScreenYPos;
        //pixelPosY += vic2Screen.BorderHeight;

        // Draw character image from chargen ROM to a Skia surface
        // The chargen ROM has been loaded to a SKImage with 16 characters per row (each character 8 x 8 pixels).
        int romImageX = (character % CHARGEN_IMAGE_CHARACTERS_PER_ROW) * 8;
        int romImageY = (character / CHARGEN_IMAGE_CHARACTERS_PER_ROW) * 8;

        _drawImageSource.Left = romImageX;
        _drawImageSource.Top = romImageY;
        _drawImageSource.Right = romImageX + 8;
        _drawImageSource.Bottom = romImageY + 8;

        _drawImageDest.Left = pixelPosX;
        _drawImageDest.Top = pixelPosY;
        _drawImageDest.Right = pixelPosX + 8;
        _drawImageDest.Bottom = pixelPosY + 8;

        var paint = _c64SkiaPaint.C64ToDrawChargenCharacterMap[characterColor];
        canvas.DrawImage(_characterSetCurrent,
            //source: new SKRect(romImageX, romImageY, romImageX + 8, romImageY + 8),
            //dest: new SKRect(pixelPosX, pixelPosY, pixelPosX + 8, pixelPosY + 8),
            source: _drawImageSource,
            dest: _drawImageDest,
            paint
            );
    }
}
