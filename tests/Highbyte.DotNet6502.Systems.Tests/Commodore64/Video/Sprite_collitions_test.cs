
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

        // Write a 'A' screen code to some text screen memory locations
        byte characterCode = 1; // 1 = 'A'
        WriteToTextScreen(vic2, characterCode, col: 0, row: 0);
        WriteToTextScreen(vic2, characterCode, col: 2, row: 0);
        WriteToTextScreen(vic2, characterCode, col: 4, row: 0);

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


        // Set sprite position and size
        var spriteNumber = 0;
        var spriteScreenXOffset = 0;    // X positon from start of visible text screen area. Can be negative if starting before border ends.
        var spriteScreenYOffset = 0;    // Y positon from start of visible text screen area. Can be negative if starting before border ends.
        bool doubleWidth = false;
        bool doubleHeight = false;
        bool multiColor = true;
        SetSpriteProperties(
            c64,
            spriteNumber,
            (byte)(Vic2SpriteManager.SCREEN_OFFSET_X + spriteScreenXOffset),
            (byte)(Vic2SpriteManager.SCREEN_OFFSET_Y + spriteScreenYOffset),
            doubleWidth: doubleWidth,
            doubleHeight: doubleHeight,
            multiColor: multiColor);

        // Create sprite shape
        var spriteShape = multiColor ? CreateTestMultiColorSpriteImage() : CreateTestSingleColorSpriteImage();
        FillSpriteShape(vic2Mem, spriteNumber, spriteShape, spritePointer: 192);

        // Get sprite row line data (24 pixels/3 bytes or 48 pisels/6 bytes)
        var vic2SpriteManager = vic2.SpriteManager;
        var sprite = vic2SpriteManager.Sprites[spriteNumber];

        // Loop each sprite line
        var spriteLines = new List<byte[]>();
        var screenLines = new List<byte[]>();
        int? collisionFoundSpriteScreenLine = null;


        int numberOfSpriteScreenLines = sprite.DoubleHeight ? DEFAULT_HEIGTH * 2 : DEFAULT_HEIGTH;
        for (int spriteScreenLine = 0; spriteScreenLine < numberOfSpriteScreenLines; spriteScreenLine++)
        {
            var spriteLineData = vic2SpriteManager.GetSpriteRowLineData(sprite, spriteScreenLine);
            spriteLines.Add(spriteLineData);

            byte[] screenLineData = vic2SpriteManager.GetCharacterRowLineDataMatchingSpritePosition(sprite, spriteScreenLine, spriteLineData.Length);
            screenLines.Add(screenLineData);

            // Check collision on line
            bool collisionFound = vic2SpriteManager.CheckCollision(spriteLineData, screenLineData);
            if (collisionFound)
                collisionFoundSpriteScreenLine = spriteScreenLine;
        }

        if(collisionFoundSpriteScreenLine.HasValue)
            _output.WriteLine($"Collision found at line: {collisionFoundSpriteScreenLine}");
        else
            _output.WriteLine("No collision");

        _output.WriteLine("");

        _output.WriteLine("Sprite row line data");
        foreach (var spriteLine in spriteLines)
            _output.WriteLine($"{string.Join(" ", spriteLine.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");

        _output.WriteLine("");

        _output.WriteLine("Character Rom row line data");
        foreach (var screenLine in screenLines)
            _output.WriteLine($"{string.Join(" ", screenLine.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");
    }

    private void WriteToTextScreen(Vic2 vic2, byte characterCode, int col, int row)
    {
        var characterAddress = (ushort)(Vic2Addr.SCREEN_RAM_START + (row * vic2.Vic2Screen.TextCols) + col);
        vic2.Vic2Mem[characterAddress] = characterCode;
    }

    private void SetSpriteProperties(C64 c64, int spriteNumber, byte x, byte y, bool doubleWidth, bool doubleHeight, bool multiColor)
    {
        c64.Mem[(ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2)] = x;
        c64.Mem[(ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2)] = y;

        var spriteXExpand = c64.Mem[Vic2Addr.SPRITE_X_EXPAND];
        spriteXExpand.ChangeBit(spriteNumber, doubleWidth);
        c64.Mem[Vic2Addr.SPRITE_X_EXPAND] = spriteXExpand;

        var spriteYExpand = c64.Mem[Vic2Addr.SPRITE_Y_EXPAND];
        spriteYExpand.ChangeBit(spriteNumber, doubleHeight);
        c64.Mem[Vic2Addr.SPRITE_Y_EXPAND] = spriteYExpand;

        var multiColorEnable = c64.Mem[Vic2Addr.SPRITE_MULTICOLOR_ENABLE];
        multiColorEnable.ChangeBit(spriteNumber, multiColor);
        c64.Mem[Vic2Addr.SPRITE_MULTICOLOR_ENABLE] = multiColorEnable;

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

    private byte[] CreateTestMultiColorSpriteImage()
    {
        // 24 x 21 pixels = 3 * 21 bytes = 63 bytes. 3 bytes per row.
        // Each byte contains 4 pixels.
        // Bit pairs 01,10, and 11 are used to 3 different select color.
        // Bit pair 00 is used to select background color.
        return new byte[]
        {
            0b01010000, 0b10100000, 0b11110000,
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
