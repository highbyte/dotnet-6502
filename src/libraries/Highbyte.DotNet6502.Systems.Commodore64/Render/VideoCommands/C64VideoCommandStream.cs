using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoCommands;
using Highbyte.DotNet6502.Systems.Utils;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.VideoCommands;

[DisplayName("Video Commands")]
[HelpText("Generates a stream of video commands for each frame,\nbased on the C64 screen memory and color RAM.")]
public class C64VideoCommandStream : IRenderProvider, IVideoCommandStream
{
    public string Name => "C64VideoCommandStream";

    private readonly C64 _c64;

    private readonly Queue<IVideoCommand> _commands = new();

    private readonly Dictionary<byte, uint> _c64ToRenderColorMap;


    public C64VideoCommandStream(C64 c64)
    {
        _c64 = c64;

        _c64ToRenderColorMap = new();
        foreach (byte c64Color in Enum.GetValues<C64Colors>())
        {
            var systemColor = GetSystemColor(c64Color, _c64.ColorMapName).ToArgb();
            _c64ToRenderColorMap.Add(c64Color, (uint)systemColor);
        }
    }

    public event EventHandler? FrameCompleted;

    public IEnumerable<IVideoCommand> DequeueAll()
    {
        while (_commands.Count > 0)
            yield return _commands.Dequeue();
    }

    public void OnAfterInstruction()
    {
    }

    public void OnEndFrame()
    {
        GenerateCommands();
        FrameCompleted?.Invoke(this, EventArgs.Empty);
    }

    // Called only from legacy render pipeline
    public void GenerateCommands()
    {
        GenerateConfig(_c64);
        RenderBorder(_c64);
        RenderMainScreen(_c64);
    }

    private void GenerateConfig(C64 c64)
    {
        var configCommand = new SetConfig(
            GlyphToUnicodeConverter: FromC64ScreenCodeToUnicode);

        _commands.Enqueue(configCommand);
    }

    private string FromC64ScreenCodeToUnicode(byte c64ScreenCode)
    {
        // Convert C64 screen code to PETSCII, then to ASCII, then to string
        try
        {
            string representAsString;
            switch (c64ScreenCode)
            {
                case 0xa0:  //160, C64 inverted space
                case 0xe0:  //224, Also C64 inverted space?
                    // Unicode for Inverted square in https://style64.org/c64-truetype font
                    representAsString = ((char)0x2588).ToString();
                    break;

                default:
                    var petsciiCode = Petscii.C64ScreenCodeToPetscII(c64ScreenCode);
                    var asciiCode = Petscii.PetscIIToAscII(petsciiCode);

                    switch (asciiCode)
                    {
                        case 0x00:  // Uninitialized
                            representAsString = " "; // Replace with space
                            break;

                        default:
                            // Check if asciiCode is a letter
                            if (asciiCode >= 0x61 && asciiCode <= 0x7A) // a-z
                            {
                                // Even though both upper and lowercase characters are used in the 6502 program (and in the font), show all as uppercase for C64 look.
                                // Convert to uppercase
                                //asciiCode = asciiCode - 0x20;
                                representAsString = Convert.ToString((char)asciiCode).ToUpper();
                            }
                            else
                            {
                                representAsString = Convert.ToString((char)asciiCode);
                            }
                            break;
                    }
                    break;
            }

            return representAsString;
        }
        catch
        {
            // Fallback for any conversion errors
            return " ";
        }
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

        uint fgColor = _c64ToRenderColorMap[emulatorFgColor];
        uint bgColor = _c64ToRenderColorMap[emulatorBgColor];

        var drawGlyphCommand = GenerateDrawGlyphCommand(
            x,
            y,
            emulatorCharacter,
            fgColor,
            bgColor);

        _commands.Enqueue(drawGlyphCommand);
    }

    private int GetBorderCols(C64 c64)
    {
        return c64.Vic2.Vic2Screen.VisibleLeftRightBorderWidth / c64.Vic2.Vic2Screen.CharacterWidth;
    }
    private int GetBorderRows(C64 c64)
    {
        return c64.Vic2.Vic2Screen.VisibleTopBottomBorderHeight / c64.Vic2.Vic2Screen.CharacterHeight;
    }

    private DrawGlyphArgb GenerateDrawGlyphCommand(int x, int y, int sadConsoleCharCode, uint fgColorArgb, uint bgColorArgb)
    {
        return new DrawGlyphArgb(x, y, sadConsoleCharCode, fgColorArgb, bgColorArgb);
    }
}
