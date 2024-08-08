using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Benchmarks.Commodore64.Data;

public class C64SpriteGenerator
{
    private readonly C64 _c64;
    private readonly Memory _vic2Mem;
    private readonly IVic2SpriteManager _vic2SpriteManager;

    public C64SpriteGenerator(C64 c64)
    {
        _c64 = c64;
        _vic2Mem = c64.Mem;
        _vic2SpriteManager = c64.Vic2.SpriteManager;
    }

    public Vic2Sprite CreateSprite(int spriteNumber, byte x, byte y, bool doubleWidth, bool doubleHeight, bool multiColor, byte[] spriteShape, byte spritePointer = 192)
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
        _c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_X + spriteNumber * 2), x);
        _c64.WriteIOStorage((ushort)(Vic2Addr.SPRITE_0_Y + spriteNumber * 2), y);

        var spriteXEnable = _c64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE);
        spriteXEnable.ChangeBit(spriteNumber, true);
        _c64.WriteIOStorage(Vic2Addr.SPRITE_ENABLE, spriteXEnable);

        var spriteXExpand = _c64.ReadIOStorage(Vic2Addr.SPRITE_X_EXPAND);
        spriteXExpand.ChangeBit(spriteNumber, doubleWidth);
        _c64.WriteIOStorage(Vic2Addr.SPRITE_X_EXPAND, spriteXExpand);

        var spriteYExpand = _c64.ReadIOStorage(Vic2Addr.SPRITE_Y_EXPAND);
        spriteYExpand.ChangeBit(spriteNumber, doubleHeight);
        _c64.WriteIOStorage(Vic2Addr.SPRITE_Y_EXPAND, spriteYExpand);

        var multiColorEnable = _c64.ReadIOStorage(Vic2Addr.SPRITE_MULTICOLOR_ENABLE);
        multiColorEnable.ChangeBit(spriteNumber, multiColor);
        _c64.WriteIOStorage(Vic2Addr.SPRITE_MULTICOLOR_ENABLE, multiColorEnable);
    }

    private void FillSpriteShape(int spriteNumber, byte[] shape, byte spritePointer)
    {
        _vic2Mem[(ushort)(_vic2SpriteManager.SpritePointerStartAddress + spriteNumber)] = spritePointer;
        //var spritePointer = vic2Mem[(ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber)];
        var spritePointerAddress = (ushort)(spritePointer * 64);
        for (int i = 0; i < shape.Length; i++)
        {
            _vic2Mem[(ushort)(spritePointerAddress + i)] = shape[i];
        }
    }

    public byte[] CreateTestSingleColorSpriteImage()
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

    public byte[] CreateTestSingleColorSpriteImage2()
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

    public byte[] CreateTestMultiColorSpriteImage()
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
