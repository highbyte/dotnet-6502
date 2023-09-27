
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2Sprite;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.Video;

public class Sprite_collitions_test
{
    private readonly ITestOutputHelper _output;
    public Sprite_collitions_test(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public void DebugSpriteToScreenContent()
    {
        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
        };

        var c64 = C64.BuildC64(c64Config, new NullLoggerFactory());
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;

        // Write a 'A' screen code to screen memory at col 0, row 0
        byte characterCode = 1;
        var characterCol = 0;
        var characterRow = 0;
        var characterAddress = (ushort)(Vic2Addr.SCREEN_RAM_START + (characterRow * vic2.Vic2Screen.TextCols) + characterCol);
        vic2Mem[characterAddress] = characterCode;

        // Write the shape of the 'A' character to character rom
        var characterSetLineAddress = (ushort)(vic2.CharacterSetAddressInVIC2Bank + (characterCode * vic2.Vic2Screen.CharacterHeight));
        vic2Mem[characterSetLineAddress++] = 0b00011000;
        vic2Mem[characterSetLineAddress++] = 0b00111100;
        vic2Mem[characterSetLineAddress++] = 0b01100110;
        vic2Mem[characterSetLineAddress++] = 0b01111110;
        vic2Mem[characterSetLineAddress++] = 0b01100110;
        vic2Mem[characterSetLineAddress++] = 0b01100110;
        vic2Mem[characterSetLineAddress++] = 0b01100110;
        vic2Mem[characterSetLineAddress++] = 0b00000000;

        // Set sprite position
        var spriteNumber = 0;
        byte spritePosX = Vic2SpriteManager.SCREEN_OFFSET_X + 0;    // Actual horizontal screen position where sprites starts to be shown is at 24
        byte spritePosY = Vic2SpriteManager.SCREEN_OFFSET_Y + 0;    // Actual vertical screen position where sprites starts to be shown is at 50
        c64.Mem[(ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2)] = spritePosX;
        c64.Mem[(ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2)] = spritePosY;

        // Create sprite shape
        FillSpriteShape(vic2Mem, spriteNumber, CreateTestSingleColorSpriteImage(), spritePointer: 192);

        // Get sprite row line data (24 pixels, 3 bytes)
        var vic2SpriteManager = vic2.SpriteManager;
        var sprite = vic2SpriteManager.Sprites[spriteNumber];


        // Loop each sprite line
        var spriteLines = new List<byte[]>();
        var screenLines = new List<byte[]>();

        for (int spriteLineNumber = 0; spriteLineNumber < DEFAULT_HEIGTH; spriteLineNumber++)
        {
            // Get sprite row line data (24 pixels, 3 bytes)
            spriteLines.Add(sprite.Data.Rows[spriteLineNumber].Bytes);
            // Get character ROM data for the corresponding line in the text screen (will match 24 pixels/3 bytes as the sprite)
            screenLines.Add(vic2SpriteManager.GetCharacterRowLineDataMatchingSpritePosition(spritePosX, spritePosY, spriteLineNumber));
        }

        _output.WriteLine("Sprite row line data");
        foreach (var spriteLine in spriteLines)
            _output.WriteLine($"{string.Join(" ", spriteLine.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");

        _output.WriteLine("");

        _output.WriteLine("Character Rom row line data");
        foreach (var screenLine in screenLines)
            _output.WriteLine($"{string.Join(" ", screenLine.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");

    }

    private void FillSpriteShape(Memory vic2Mem, int spriteNumber, byte[] shape, byte spritePointer)
    {
        vic2Mem[(ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber)] = spritePointer;
        //var spritePointer = vic2Mem[(ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber)];
        var spritePointerAddress = (ushort)(spritePointer * 64);
        for (int i = 0; i < shape.Length; i++)
        {
            vic2Mem[(ushort)(spritePointerAddress + i)] = shape[i];
        }
    }

    private byte[] CreateTestSingleColorSpriteImage()
    {
        // 24 x 21 pixels = 3 * 21 bytes = 63 bytes. 3 bytes per row.
        return new byte[]
        {
            0b11110000, 0b11110000, 0b11110000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,

            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,
            0b00000000, 0b00000000, 0b00000000,

            0b00000000, 0b00000000, 0b00000000,
        };

    }
}
