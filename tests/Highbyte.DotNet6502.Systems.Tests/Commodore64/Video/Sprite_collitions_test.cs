
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
    private readonly C64 _c64;
    private readonly Vic2 _vic2;
    private readonly Memory? _vic2Mem;
    private readonly Vic2SpriteManager? _vic2SpriteManager;

    public Sprite_collitions_test(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;

        var c64Config = new C64Config
        {
            C64Model = "C64NTSC",   // C64NTSC, C64PAL
            Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
            LoadROMs = false
        };

        _c64 = C64.BuildC64(c64Config, new NullLoggerFactory());
        _vic2 = _c64.Vic2;
        _vic2Mem = _vic2.Vic2Mem;
        _vic2SpriteManager = _vic2.SpriteManager;
    }

    [Fact]
    public void DebugSpriteToScreenCollision()
    {
        // Write a 'A' screen code to some text screen memory locations
        byte characterCode = 1; // 1 = 'A'
        WriteToTextScreen(characterCode, col: 0, row: 0);
        WriteToTextScreen(characterCode, col: 2, row: 0);
        WriteToTextScreen(characterCode, col: 4, row: 0);

        // Write the shape of the 'A' character to character rom
        var characterSetLineAddress = (ushort)(_vic2.CharacterSetAddressInVIC2Bank + (characterCode * _vic2.Vic2Screen.CharacterHeight));
        _vic2Mem[characterSetLineAddress++] = 0b00011000;
        _vic2Mem[characterSetLineAddress++] = 0b00111100;
        _vic2Mem[characterSetLineAddress++] = 0b01100110;
        _vic2Mem[characterSetLineAddress++] = 0b01111110;
        _vic2Mem[characterSetLineAddress++] = 0b01100110;
        _vic2Mem[characterSetLineAddress++] = 0b01100110;
        _vic2Mem[characterSetLineAddress++] = 0b01100110;
        _vic2Mem[characterSetLineAddress++] = 0b00000000;

        // Create sprite 
        var sprite = CreateSprite(
            spriteNumber: 0,
            x: Vic2SpriteManager.SCREEN_OFFSET_X + 0,
            y: Vic2SpriteManager.SCREEN_OFFSET_Y + 0,
            doubleWidth: false,
            doubleHeight: false,
            multiColor: true,
            CreateTestMultiColorSpriteImage(),
            spritePointer: 192
            );

        // Get text screen scroll values
        var scrollX = 0;
        var scrollY = 0;

        // Loop each sprite line
        var spriteLines = new List<byte[]>();
        var screenLines = new List<byte[]>();
        int? collisionFoundSpriteScreenLine = null;

        int numberOfSpriteScreenLines = sprite.HeightPixels;
        for (int spriteScreenLine = 0; spriteScreenLine < numberOfSpriteScreenLines; spriteScreenLine++)
        {
            var spriteLineData = _vic2SpriteManager.GetSpriteRowLineData(sprite, spriteScreenLine);
            spriteLines.Add(spriteLineData);

            byte[] screenLineData = _vic2SpriteManager.GetCharacterRowLineDataMatchingSpritePosition(sprite, spriteScreenLine, spriteLineData.Length, scrollX, scrollY);
            screenLines.Add(screenLineData);

            // Check collision on line
            bool collisionFound = _vic2SpriteManager.CheckCollision(spriteLineData, screenLineData);
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

    [Fact]
    public void DebugSpriteToSpriteCollision()
    {
        // Create sprite 0
        var sprite0 = CreateSprite(
            spriteNumber: 0,
            x: 0,
            y: 0,
            doubleWidth: false,
            doubleHeight: true,
            multiColor: false,
            CreateTestSingleColorSpriteImage(),
            spritePointer: 192
            );

        var sprite1 = CreateSprite(
            spriteNumber: 1,
            x: 9,
            y: 0,
            doubleWidth: true,
            doubleHeight: false,
            multiColor: false,
            CreateTestSingleColorSpriteImage2(),
            spritePointer: 193
            );

        // Loop each sprite line
        var sprite0Lines = new List<byte[]>();
        var sprite1Lines = new List<byte[]>();
        int? collisionFoundSpriteScreenLine = null;

        int numberOfSpriteScreenLines = sprite0.HeightPixels;
        for (int spriteScreenLine = 0; spriteScreenLine < numberOfSpriteScreenLines; spriteScreenLine++)
        {
            var sprite0LineData = _vic2SpriteManager.GetSpriteRowLineData(sprite0, spriteScreenLine);
            sprite0Lines.Add(sprite0LineData);

            byte[] sprite1LineData = _vic2SpriteManager.GetSpriteRowLineDataMatchingOtherSpritePosition(sprite0, sprite1, spriteScreenLine, sprite0LineData.Length);
            sprite1Lines.Add(sprite1LineData);

            // Check collision on line
            bool collisionFound = _vic2SpriteManager.CheckCollision(sprite0LineData, sprite1LineData);
            if (collisionFound)
                collisionFoundSpriteScreenLine = spriteScreenLine;
        }

        if (collisionFoundSpriteScreenLine.HasValue)
            _output.WriteLine($"Collision found at line: {collisionFoundSpriteScreenLine}");
        else
            _output.WriteLine("No collision");

        _output.WriteLine("");

        _output.WriteLine("Sprite 0 row line data");
        foreach (var spriteLine in sprite0Lines)
            _output.WriteLine($"{string.Join(" ", spriteLine.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");

        _output.WriteLine("");

        _output.WriteLine("Sprite 1 row line data");
        foreach (var spriteLine in sprite1Lines)
            _output.WriteLine($"{string.Join(" ", spriteLine.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");
    }


    private void WriteToTextScreen(byte characterCode, int col, int row)
    {
        var characterAddress = (ushort)(Vic2Addr.SCREEN_RAM_START + (row * _vic2.Vic2Screen.TextCols) + col);
        _vic2Mem[characterAddress] = characterCode;
    }


    private Vic2Sprite CreateSprite(int spriteNumber, byte x, byte y, bool doubleWidth, bool doubleHeight, bool multiColor, byte[] spriteShape, byte spritePointer = 192)
    {
        SetSpriteProperties(
            spriteNumber,
            x,
            y,
            doubleWidth: doubleWidth,
            doubleHeight: doubleHeight,
            multiColor: multiColor);

        // Create sprite shape
        FillSpriteShape(spriteNumber, spriteShape, spritePointer);

        return _vic2SpriteManager.Sprites[spriteNumber];
    }

    private void SetSpriteProperties(int spriteNumber, byte x, byte y, bool doubleWidth, bool doubleHeight, bool multiColor)
    {
        _c64.Mem[(ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2)] = x;
        _c64.Mem[(ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2)] = y;

        var spriteXExpand = _c64.Mem[Vic2Addr.SPRITE_X_EXPAND];
        spriteXExpand.ChangeBit(spriteNumber, doubleWidth);
        _c64.Mem[Vic2Addr.SPRITE_X_EXPAND] = spriteXExpand;

        var spriteYExpand = _c64.Mem[Vic2Addr.SPRITE_Y_EXPAND];
        spriteYExpand.ChangeBit(spriteNumber, doubleHeight);
        _c64.Mem[Vic2Addr.SPRITE_Y_EXPAND] = spriteYExpand;

        var multiColorEnable = _c64.Mem[Vic2Addr.SPRITE_MULTICOLOR_ENABLE];
        multiColorEnable.ChangeBit(spriteNumber, multiColor);
        _c64.Mem[Vic2Addr.SPRITE_MULTICOLOR_ENABLE] = multiColorEnable;

    }

    private void FillSpriteShape(int spriteNumber, byte[] shape, byte spritePointer)
    {
        _vic2Mem[(ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber)] = spritePointer;
        //var spritePointer = vic2Mem[(ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber)];
        var spritePointerAddress = (ushort)(spritePointer * 64);
        for (int i = 0; i < shape.Length; i++)
        {
            _vic2Mem[(ushort)(spritePointerAddress + i)] = shape[i];
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

    private byte[] CreateTestSingleColorSpriteImage2()
    {
        // 24 x 21 pixels = 3 * 21 bytes = 63 bytes. 3 bytes per row.
        return new byte[]
        {
            0b00011000, 0b00011000, 0b00011000,
            0b00111100, 0b00111100, 0b00111100,
            0b01111110, 0b01111110, 0b01111110,
            0b11111111, 0b11111111, 0b11111111,
            0b01111110, 0b01111110, 0b01111110,
            0b00111100, 0b00111100, 0b00111100,
            0b00011000, 0b00011000, 0b00011000,
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
