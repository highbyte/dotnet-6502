using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.SadConsole.Commodore64.Video;

public class C64SadConsoleRenderer : IRenderer<C64, SadConsoleRenderContext>
{
    private SadConsoleRenderContext _sadConsoleRenderContext = default!;
    private C64SadConsoleColors _c64SadConsoleColors = default!;

    // Stats
    public Instrumentations Stats { get; } = new();

    public C64SadConsoleRenderer()
    {
    }

    public void Init(C64 c64, SadConsoleRenderContext sadConsoleRenderContext)
    {
        _sadConsoleRenderContext = sadConsoleRenderContext;
        _c64SadConsoleColors = new C64SadConsoleColors(c64.ColorMapName);
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SadConsoleRenderContext)renderContext);
    }

    public void Draw(C64 c64)
    {
        RenderMainScreen(c64);
        RenderBorder(c64);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
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
        sadConsoleCharacter = TranslateC64ScreenCodeToSadConsoleC64Font(emulatorCharacter);

        _sadConsoleRenderContext.Screen.DrawCharacter(
            x,
            y,
            sadConsoleCharacter,
            _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorFgColor, c64.ColorMapName)),
            _c64SadConsoleColors.GetSadConsoleColor(ColorMaps.GetSystemColor(emulatorBgColor, c64.ColorMapName))
            );
    }

    private byte TranslateC64ScreenCodeToSadConsoleC64Font(byte sourceByte)
    {
        switch (sourceByte & 0xff)
        {
            case 0xa0:  //160, C64 inverted space
                return 219; // Inverted square in SadConsole C64 font
            case 0xe0:  //224, Also C64 inverted space?
                return 219; // Inverted square in SadConsole C64 font
            default:

                // Convert C64 screen code to PETSCII
                var sadConsoleCharacter = Petscii.C64ScreenCodeToPetscII(sourceByte);
                // TODO: Also convert to ASCII?  Would depend on the font being used?
                //sadConsoleCharacter = CharacterMaps.PETSCIICodeToASCII(sadConsoleCharacter);

                return sadConsoleCharacter;
        }
    }

    private int GetBorderCols(C64 c64)
    {
        return c64.Vic2.Vic2Screen.VisibleLeftRightBorderWidth / c64.Vic2.Vic2Screen.CharacterWidth;
    }
    private int GetBorderRows(C64 c64)
    {
        return c64.Vic2.Vic2Screen.VisibleTopBottomBorderHeight / c64.Vic2.Vic2Screen.CharacterHeight;
    }
}
