using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;

public class C64SadConsoleRenderer : IRenderer
{
    private readonly C64 _c64;
    public ISystem System => _c64;
    private readonly SadConsoleRenderContext _sadConsoleRenderContext = default!;
    private C64SadConsoleColors _c64SadConsoleColors = default!;

    // Instrumentations
    public Instrumentations Instrumentations { get; } = new();


    public C64SadConsoleRenderer(C64 c64, SadConsoleRenderContext sadConsoleRenderContext)
    {
        _c64 = c64;
        _sadConsoleRenderContext = sadConsoleRenderContext;
    }

    public void Init()
    {
        _c64SadConsoleColors = new C64SadConsoleColors(_c64.ColorMapName);
    }

    public void Cleanup()
    {
    }

    public void DrawFrame()
    {
        RenderMainScreen(_c64);
        RenderBorder(_c64);
    }

    private void RenderMainScreen(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;

        // // Top Left
        // DrawEmulatorCharacterOnScreen(0, 0, 65, 0x01, 0x05);
        // // Bottom Right
        // DrawEmulatorCharacterOnScreen(_emulatorMemoryConfig.Cols-1, _emulatorMemoryConfig.Rows-1 , 66, 0x01, 0x05);
        // return;

        var bgColor = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);

        // Build screen data characters based on emulator memory contents (byte)
        var currentScreenAddress = vic2.VideoMatrixBaseAddress;
        var currentColorAddress = Vic2Addr.COLOR_RAM_START;
        for (var row = 0; row < vic2Screen.TextRows; row++)
        {
            for (var col = 0; col < vic2Screen.TextCols; col++)
            {
                var charByte = vic2Mem[currentScreenAddress++];
                var colorByte = c64.ReadIOStorage(currentColorAddress++);
                DrawEmulatorCharacterOnScreen(
                    col,
                    row,
                    charByte,
                    colorByte,
                    bgColor,
                    c64,
                    adjustForBorder: true
                    );
            }
        }
    }

    private void RenderBorder(C64 c64)
    {
        var emulatorMem = c64.Mem;
        var vic2Screen = c64.Vic2.Vic2Screen;

        byte borderCharacter = 0;    // 0 = no character
        var borderBgColor = c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);
        var borderFgColor = borderBgColor;

        var border_cols = GetBorderCols(c64);
        var border_rows = GetBorderRows(c64);

        for (var row = 0; row < vic2Screen.TextRows + border_rows * 2; row++)
        {
            for (var col = 0; col < vic2Screen.TextCols + border_cols * 2; col++)
            {
                if (row < border_rows || row >= vic2Screen.TextRows + border_rows
                    || col < border_cols || col >= vic2Screen.TextCols + border_cols)
                {
                    DrawEmulatorCharacterOnScreen(
                        col,
                        row,
                        borderCharacter,
                        borderFgColor,
                        borderBgColor,
                        c64,
                        adjustForBorder: false
                        );
                }
            }
        }
    }

    /// <summary>
    /// Draw character to screen, with adjusted position for border.
    /// Colors are translated from emulator to SadConsole.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="emulatorCharacter"></param>
    /// <param name="emulatorFgColor"></param>
    /// <param name="emulatorBgColor"></param>
    public void DrawEmulatorCharacterOnScreen(int x, int y, byte emulatorCharacter, byte emulatorFgColor, byte emulatorBgColor, C64 c64, bool adjustForBorder)
    {
        if (adjustForBorder)
        {
            x += GetBorderCols(c64);
            y += GetBorderRows(c64);
        }

        byte sadConsoleCharacter;
        // Default to C64 screen codes as source
        sadConsoleCharacter = TranslateC64ScreenCodeToSadConsoleC64Font(emulatorCharacter, out bool inverted);

        Color fgColor;
        Color bgColor;
        if (inverted)
        {
            fgColor = _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorBgColor, c64.ColorMapName));
            bgColor = _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorFgColor, c64.ColorMapName)); ;
        }
        else
        {
            fgColor = _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorFgColor, c64.ColorMapName));
            bgColor = _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorBgColor, c64.ColorMapName));
        }

        DrawCharacter(
            x,
            y,
            sadConsoleCharacter,
            fgColor,
            bgColor);
    }

    private byte TranslateC64ScreenCodeToSadConsoleC64Font(byte sourceByte, out bool inverted)
    {
        // Assumption: The C64 program running is using the built-in font in ROM, otherwise the output will be unpredictable...

        // In the built-in C64 fonts (both shifted and unshifted versions) screen codes >= 128 are inverted.
        inverted = sourceByte >= 128;


        // Because there is only one Font in SadConsole, adjust capital letters in un-shifted C64 Char COM to letters in the shifted C64 Char ROM
        bool lowerCase;
        bool unshiftedC64CharRom = _c64.Vic2.CharsetManager.CharacterSetAddressInVIC2BankIsChargenROMUnshifted;
        if (unshiftedC64CharRom)
        {
            if (sourceByte >= 65 && sourceByte <= 91)
            {
                lowerCase = false;
                sourceByte -= 64;
            }
            else if (sourceByte >= 193 && sourceByte <= 219)
            {
                lowerCase = false;
                sourceByte -= 192;
            }
            else
            {
                lowerCase = true;
            }
        }
        else
        {
            lowerCase = false;
        }

        // Last 128 characters in C64 font are the same as the first 128 characters, but inverted.
        // SadConsole C64 font does not generally have inverted characters, so display the non-inverted characters instead.
        if (sourceByte >= 128)
            sourceByte -= 128;

        var sadConsoleGlyphIndex = _c64ScreenCodeToSadConsoleFontIndex[sourceByte];

        // Check if we mapped to a SadConsole C64 font index that is a character.
        // If so, and the C64 character was lowercase, adjust the SadConsole C64 font index to lower case character.
        if (sadConsoleGlyphIndex >= 65 && sadConsoleGlyphIndex <= 90 && lowerCase)
        {
            // Adjust SadConsolt C64 font index to lower case (for A-Z)
            sadConsoleGlyphIndex += 32;
        }

        return sadConsoleGlyphIndex;
    }

    // Dictionary to translate C64 screen code to SadConsole C64 font (Yayo_c64.png) index
    private static readonly Dictionary<byte, byte> _c64ScreenCodeToSadConsoleFontIndex = new()
    {
        { 0x00, 0x40 }, // @
        { 0x01, 0x41 }, // A
        { 0x02, 0x42 }, // B
        { 0x03, 0x43 }, // C
        { 0x04, 0x44 }, // D
        { 0x05, 0x45 }, // E
        { 0x06, 0x46 }, // F
        { 0x07, 0x47 }, // G
        { 0x08, 0x48 }, // H
        { 0x09, 0x49 }, // I
        { 0x0a, 0x4a }, // J
        { 0x0b, 0x4b }, // K
        { 0x0c, 0x4c }, // L
        { 0x0d, 0x4d }, // M
        { 0x0e, 0x4e }, // N
        { 0x0f, 0x4f }, // O

        { 0x10, 0x50 }, // P
        { 0x11, 0x51 }, // Q
        { 0x12, 0x52 }, // R
        { 0x13, 0x53 }, // S
        { 0x14, 0x54 }, // T
        { 0x15, 0x55 }, // U
        { 0x16, 0x56 }, // V
        { 0x17, 0x57 }, // W
        { 0x18, 0x58 }, // X
        { 0x19, 0x59 }, // Y
        { 0x1a, 0x5a }, // Z
        { 0x1b, 0x5b }, // [
        { 0x1c, 0x9c }, // Pound
        { 0x1d, 0x5d }, // ]
        { 0x1e, 0x18 }, // Arrow up
        { 0x1f, 0x1b }, // Arrow left

        { 0x20, 0x20 }, // space
        { 0x21, 0x21 }, // !
        { 0x22, 0x22 }, // "
        { 0x23, 0x23 }, // #
        { 0x24, 0x24 }, // $
        { 0x25, 0x25 }, // %
        { 0x26, 0x26 }, // &
        { 0x27, 0x27 }, // '
        { 0x28, 0x28 }, // (
        { 0x29, 0x29 }, // )
        { 0x2a, 0x2a }, // *
        { 0x2b, 0x2b }, // +
        { 0x2c, 0x2c }, // ,
        { 0x2d, 0x2d }, // -
        { 0x2e, 0x2e }, // .
        { 0x2f, 0x2f }, // /

        { 0x30, 0x30 }, // 0
        { 0x31, 0x31 }, // 1
        { 0x32, 0x32 }, // 2
        { 0x33, 0x33 }, // 3
        { 0x34, 0x34 }, // 4
        { 0x35, 0x35 }, // 5
        { 0x36, 0x36 }, // 6
        { 0x37, 0x37 }, // 7
        { 0x38, 0x38 }, // 8
        { 0x39, 0x39 }, // 9
        { 0x3a, 0x3a }, // :
        { 0x3b, 0x3b }, // ;
        { 0x3c, 0x3c }, // <
        { 0x3d, 0x3d }, // =
        { 0x3e, 0x3e }, // >
        { 0x3f, 0x3f }, // ?

        { 0x40, 0xc4 }, // Horizontal bar middle
        { 0x41, 0x06 }, // Heart
        { 0x42, 0xb3 }, // Vertical bar middle
        { 0x43, 0xc4 }, // Horizontal bar middle (same as 0x40?)
        { 0x44, 0xc4 }, // Horizontal bar middle/up (no exact match, approximate)
        { 0x45, 0xc4 }, // Horizontal bar up (no exact match, approximate)
        { 0x46, 0xc4 }, // Horizontal bar middle/down (no exact match, approximate)
        { 0x47, 0xb3 }, // Vertical bar middle/left (no exact match, approximate)
        { 0x48, 0xb3 }, // Vertical bar middle/right (no exact match, approximate)
        { 0x49, 0xbf }, // Curve, bottom/left (no exact match, approximate)
        { 0x4a, 0xc0 }, // Curve, top/right (no exact match, approximate)
        { 0x4b, 0xd9 }, // Curve, top/left (no exact match, approximate)
        { 0x4c, 0xc0 }, // Bar, left & bottom (no exact match, approximate)
        { 0x4d, 0x5c }, // Diagonal top/left to bottom/right (no exact match, approximate)
        { 0x4e, 0x2f }, // Diagonal bottom/left to top/right (no exact match, approximate)
        { 0x4f, 0xda }, // Bar, left & top (no exact match, approximate)

        { 0x50, 0xbf }, // Bar, right & top (no exact match, approximate)
        { 0x51, 0x07 }, // Filled circle
        { 0x52, 0xc4 }, // Horizontal bar middle/down/down (no exact match, approximate)
        { 0x53, 0x06 }, // Heart
        { 0x54, 0xb3 }, // Vertical bar middle/left/left (no exact match, approximate)
        { 0x55, 0xbf }, // Curve, bottom/right (no exact match, approximate)
        { 0x56, 0x58 }, // Big X (no exact match, approximate)
        { 0x57, 0x09 }, // Hollow circle
        { 0x58, 0x05 }, // Clubs (card)
        { 0x59, 0xb3 }, // Vertical bar right (no exact match, approximate)
        { 0x5a, 0x04 }, // Diamonds (card)
        { 0x5b, 0xc4 }, // Big cross
        { 0x5c, 0xb1 }, // Pattern vertical/left (no exact match, approximate)
        { 0x5d, 0xd3 }, // Vertical bar middle 
        { 0x5e, 0xfc }, // Symbol that looks like n but no exactly
        { 0x5f, 0x1e }, // Triangle top/left - top/right - bottom/right (no exact match, approximate)

        { 0x60, 0x20 }, // Blank
        { 0x61, 0xdd }, // Vertical bar left large 
        { 0x62, 0xdc }, // Vertical bar bottom large 
        { 0x63, 0xc4 }, // Horizontal bar top thin (no exact match, approximate)
        { 0x64, 0xc4 }, // Horizontal bar bottom thin (no exact match, approximate)
        { 0x65, 0xb3 }, // Vertical bar left (no exact match, approximate)
        { 0x66, 0xb1 }, // Pattern all
        { 0x67, 0xb3 }, // Vertical bar right (no exact match, approximate)
        { 0x68, 0xb1 }, // Pattern bottom (no exact match, approximate)
        { 0x69, 0x11 }, // Triangle top/left - top/right - bottom/left
        { 0x6a, 0xb3 }, // Vertical bar right (no exact match, approximate)
        { 0x6b, 0xc3 }, // T-junction vertical and right
        { 0x6c, 0xda }, // Quarter filled bottom/right (no exact match, approximate)
        { 0x6d, 0xc0 }, // Connector, top to right
        { 0x6e, 0xbf }, // Connector, left to bottom
        { 0x6f, 0xc4 }, // Vertical bar bottom (no exact match, approximate)

        { 0x70, 0xda }, // Connector, bottom to right
        { 0x71, 0xc1 }, // T-junction horizontal and top
        { 0x72, 0xc2 }, // T-junction horizontal and bottom
        { 0x73, 0xb4 }, // T-junction vertical and left
        { 0x74, 0xb3 }, // Vertical bar left (no exact match, approximate)
        { 0x75, 0xb3 }, // Vertical bar left (no exact match, approximate)
        { 0x76, 0xb3 }, // Vertical bar right (no exact match, approximate)
        { 0x77, 0xc4 }, // Horizontal bar top (no exact match, approximate)
        { 0x78, 0xc4 }, // Horizontal bar top (no exact match, approximate)
        { 0x79, 0xc4 }, // Horizontal bar top (no exact match, approximate)
        { 0x7a, 0xd9 }, // Bar, bottom & right (no exact match, approximate)
        { 0x7b, 0xf9 }, // Quarter block, bottom left (no exact match, approximate)
        { 0x7c, 0xf9 }, // Quarter block, top right (no exact match, approximate)
        { 0x7d, 0xd9 }, // Connector, left to top
        { 0x7e, 0xf9 }, // Quarter block, top left (no exact match, approximate)
        { 0x7f, 0xb2 }, // 2 quarter blocks, top left and bottom right 

        // Rest is the same but inverted, no match in SadConsole C64 font
    };

    private int GetBorderCols(C64 c64)
    {
        return c64.Vic2.Vic2Screen.VisibleLeftRightBorderWidth / c64.Vic2.Vic2Screen.CharacterWidth;
    }
    private int GetBorderRows(C64 c64)
    {
        return c64.Vic2.Vic2Screen.VisibleTopBottomBorderHeight / c64.Vic2.Vic2Screen.CharacterHeight;
    }

    private void DrawCharacter(int x, int y, int sadConsoleCharCode, Color fgColor, Color bgColor)
    {
        _sadConsoleRenderContext.Console.SetGlyph(x, y, sadConsoleCharCode, fgColor, bgColor);
    }
}
