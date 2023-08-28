using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Manager for all VIC-II sprites.
/// </summary>
public class Vic2SpriteManager
{
    public Vic2 Vic2 { get; private set; }
    private Memory _vic2Mem => Vic2.Vic2Mem;

    public const int NUMBERS_OF_SPRITES = 8;
    //The Sprite top/left X position that appears on main screen (not border) position 0.
    public const int SCREEN_OFFSET_X = 24;
    //The Sprite top/left Y position that appears on main screen (not border) position 0.
    public const int SCREEN_OFFSET_Y = 50;
    public Vic2Sprite[] Sprites { get; private set; } = new Vic2Sprite[NUMBERS_OF_SPRITES];

    private Vic2SpriteManager() { }

    public Vic2SpriteManager(Vic2 vic2)
    {
        Vic2 = vic2;
        for (int i = 0; i < Sprites.Length; i++)
        {
            var sprite = new Vic2Sprite(i, this);
            Sprites[i] = sprite;
        }
    }

    public void DetectChangesToSpriteData(ushort vic2Address, byte value)
    {
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            var spritePointerAddress = (ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber);

            // Detect changes to sprite pointer
            if (vic2Address == spritePointerAddress)
                Sprites[spriteNumber].SetDirty(true);

            // Detect changes to the data the sprite pointer points to
            for (int spriteDataAddressOffset = 0; spriteDataAddressOffset < 63; spriteDataAddressOffset++)
            {
                var spriteDataAddress = (ushort)((_vic2Mem[spritePointerAddress] * 64) + spriteDataAddressOffset);
                //var spritePointer = _vic2Mem[spriteDataAddress];

                if (vic2Address >= spriteDataAddress && vic2Address <= spriteDataAddress + 63)
                    Sprites[spriteNumber].SetDirty(true);
            }
        }
    }
}
