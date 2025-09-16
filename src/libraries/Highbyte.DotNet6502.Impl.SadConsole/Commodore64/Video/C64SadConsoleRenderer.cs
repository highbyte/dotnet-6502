//using Highbyte.DotNet6502.Systems;
//using Highbyte.DotNet6502.Systems.Commodore64;
//using Highbyte.DotNet6502.Systems.Commodore64.Video;
//using Highbyte.DotNet6502.Systems.Instrumentation;

//namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;

//// TODO: Is this completely replace in new render pipeline with a VideoCommand render target + custom adjustment?
//public class C64SadConsoleRenderer_LEGACY
//{
//    private readonly C64 _c64;
//    public ISystem System => _c64;
//    private readonly SadConsoleRenderContext _sadConsoleRenderContext = default!;
//    private C64SadConsoleColors _c64SadConsoleColors = default!;

//    // Instrumentations
//    public Instrumentations Instrumentations { get; } = new();


//    public C64SadConsoleRenderer_LEGACY(C64 c64, SadConsoleRenderContext sadConsoleRenderContext)
//    {
//        _c64 = c64;
//        _sadConsoleRenderContext = sadConsoleRenderContext;
//    }

//    public void Init()
//    {
//        _c64SadConsoleColors = new C64SadConsoleColors(_c64.ColorMapName);
//    }

//    public void Cleanup()
//    {
//    }

//    public void GenerateFrame()
//    {
//        // All drawing is done in DrawFrame with commands against SadConsole screen, so nothing to do here.
//    }

//    public void DrawFrame()
//    {
//        RenderMainScreen(_c64);
//        RenderBorder(_c64);
//    }

//    private void RenderMainScreen(C64 c64)
//    {
//        var vic2 = c64.Vic2;
//        var vic2Mem = vic2.Vic2Mem;
//        var vic2Screen = vic2.Vic2Screen;

//        // // Top Left
//        // DrawEmulatorCharacterOnScreen(0, 0, 65, 0x01, 0x05);
//        // // Bottom Right
//        // DrawEmulatorCharacterOnScreen(_emulatorMemoryConfig.Cols-1, _emulatorMemoryConfig.Rows-1 , 66, 0x01, 0x05);
//        // return;

//        var bgColor = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);

//        // Build screen data characters based on emulator memory contents (byte)
//        var currentScreenAddress = vic2.VideoMatrixBaseAddress;
//        var currentColorAddress = Vic2Addr.COLOR_RAM_START;
//        for (var row = 0; row < vic2Screen.TextRows; row++)
//        {
//            for (var col = 0; col < vic2Screen.TextCols; col++)
//            {
//                var charByte = vic2Mem[currentScreenAddress++];
//                var colorByte = c64.ReadIOStorage(currentColorAddress++);
//                DrawEmulatorCharacterOnScreen(
//                    col,
//                    row,
//                    charByte,
//                    colorByte,
//                    bgColor,
//                    c64,
//                    adjustForBorder: true
//                    );
//            }
//        }
//    }

//    private void RenderBorder(C64 c64)
//    {
//        var emulatorMem = c64.Mem;
//        var vic2Screen = c64.Vic2.Vic2Screen;

//        byte borderCharacter = 0;    // 0 = no character
//        var borderBgColor = c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);
//        var borderFgColor = borderBgColor;

//        var border_cols = GetBorderCols(c64);
//        var border_rows = GetBorderRows(c64);

//        for (var row = 0; row < vic2Screen.TextRows + border_rows * 2; row++)
//        {
//            for (var col = 0; col < vic2Screen.TextCols + border_cols * 2; col++)
//            {
//                if (row < border_rows || row >= vic2Screen.TextRows + border_rows
//                    || col < border_cols || col >= vic2Screen.TextCols + border_cols)
//                {
//                    DrawEmulatorCharacterOnScreen(
//                        col,
//                        row,
//                        borderCharacter,
//                        borderFgColor,
//                        borderBgColor,
//                        c64,
//                        adjustForBorder: false
//                        );
//                }
//            }
//        }
//    }

//    /// <summary>
//    /// Draw character to screen, with adjusted position for border.
//    /// Colors are translated from emulator to SadConsole.
//    /// </summary>
//    /// <param name="x"></param>
//    /// <param name="y"></param>
//    /// <param name="emulatorCharacter"></param>
//    /// <param name="emulatorFgColor"></param>
//    /// <param name="emulatorBgColor"></param>
//    public void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, C64 c64, bool adjustForBorder)
//    {
//        if (adjustForBorder)
//        {
//            x += GetBorderCols(c64);
//            y += GetBorderRows(c64);
//        }

//        byte sadConsoleCharacter;
//        sadConsoleCharacter = TranslateC64ScreenCodeToSadConsoleC64ROMFontIndex(emulatorCharacter, out bool inverted);

//        Color fgColorTemp = _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorFgColor, c64.ColorMapName));
//        Color bgColorTemp = _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorBgColor, c64.ColorMapName));

//        Color fgColor;
//        Color bgColor;
//        if (inverted)
//        {
//            fgColor = bgColorTemp;
//            bgColor = fgColorTemp;
//        }
//        else
//        {
//            fgColor = fgColorTemp;
//            bgColor = bgColorTemp;
//        }
//        ;

//        DrawCharacter(
//            x,
//            y,
//            sadConsoleCharacter,
//            fgColor,
//            bgColor);
//    }

//    private byte TranslateC64ScreenCodeToSadConsoleC64ROMFontIndex(byte sourceByte, out bool inverted)
//    {
//        // The custom SadConsole font C64_ROM.font:
//        // - is a combination of the two built-in C64 ROM fonts, the non-inverted parts of "shifted" and "unshifted" ROM fonts.
//        // - the first 128 characters from the shifted font (non-inverted) are index 0-127.
//        // - the first 128 characters from the unshufter font (non-inverted) are index 1-255.
//        // - Other changes:
//        //   - A sad console font must have index 0 as empty (only transparent background). As the C64 font has a @ sign in this position, it has been removed.
//        //   - A sad console font must have one index as a solid block (only forground). As the C64 non-inverted parts of the C64 ROM fons does not contain a solid block, a duplicate of an empty block (160) has been changed to contain a solid block, one of the duplicate empty characters has been replace with a solid block.
//        //   - These differences, as well as detecting inverted characters (and swapping foreground/background color when drawing) are mapped via code.


//        // ----------
//        // Mapping general C64 font -> SadConsole C64_ROM.font font translation
//        // ----------
//        byte sadConsoleGlyphIndex;
//        bool isUnshiftedC64CharRom = _c64.Vic2.CharsetManager.CharacterSetAddressInVIC2BankIsChargenROMUnshifted;
//        if (isUnshiftedC64CharRom)
//        {
//            if (sourceByte < 128)
//                sadConsoleGlyphIndex = (byte)(sourceByte + 128);
//            else
//                sadConsoleGlyphIndex = sourceByte;
//        }
//        else
//        {
//            if (sourceByte >= 128)
//                sadConsoleGlyphIndex = (byte)(sourceByte - 128);
//            else
//                sadConsoleGlyphIndex = sourceByte;
//        }

//        // ----------
//        // Mapping exceptions between C64 font -> SadConsole C64_ROM.font font
//        // ----------
//        // The first glyph in a SadConsole font must be empty (where the C64 ROM font has a @ sign).
//        // The @ sign has been removed from the C64 ROM font. The @ sign has a duplicate in position 128.
//        if (sourceByte == 0)
//        {
//            sadConsoleGlyphIndex = 128;
//            inverted = false;
//        }
//        // One glyph in SadConsole font must contain a solid block. As the combined font from C64 shifted and unshifted (with both inverted removed),
//        // a duplicate of an empty block (160) has been changed to contain a solid block.
//        // To avoid Space (32) when using an unshifted C64 Char ROM (that would add 128 to the index of the combined SadConsole font) to
//        // be the solid block (see below), always use Space (32).
//        else if (sourceByte == 32)
//        {
//            sadConsoleGlyphIndex = 32;
//            inverted = false;
//        }
//        // One glyph in SadConsole font must contain a solid block. As the combined font from C64 shifted and unshifted (with both inverted removed),
//        // a duplicate of an empty block (160) has been changed to contain a solid block.
//        // In the original C64 font (both shifted and unshifted) this position was a solid block. So use it, and tell not to invert it.
//        else if (sourceByte == 160)
//        {
//            sadConsoleGlyphIndex = 160;
//            inverted = false;
//        }
//        else
//        {
//            inverted = sourceByte >= 128;
//        }

//        return sadConsoleGlyphIndex;
//    }

//    private int GetBorderCols(C64 c64)
//    {
//        return c64.Vic2.Vic2Screen.VisibleLeftRightBorderWidth / c64.Vic2.Vic2Screen.CharacterWidth;
//    }
//    private int GetBorderRows(C64 c64)
//    {
//        return c64.Vic2.Vic2Screen.VisibleTopBottomBorderHeight / c64.Vic2.Vic2Screen.CharacterHeight;
//    }

//    private void DrawCharacter(int x, int y, int sadConsoleCharCode, Color fgColor, Color bgColor)
//    {
//        _sadConsoleRenderContext.Console.SetGlyph(x, y, sadConsoleCharCode, fgColor, bgColor);
//    }
//}
