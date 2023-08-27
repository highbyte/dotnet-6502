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

    public void MapSpriteDataReadWrite()
    {
        var c64Mem = Vic2.C64.Mem;

        // Map special handling of sprite pointers and data to flag sprite needs to re-generate image
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            // Map changes to sprite pointer
            // TODO: Mapping is to C64 memory, which address to map depends on which VIC2 bank is selected?
            var spritePointerAddress = (ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber);
            //c64Mem.MapReader(spritePointerAddress, (c64Address) => _vic2Mem[c64Address]);
            c64Mem.MapWriter(spritePointerAddress, SpritePointerStore);
        }
    }

    private void SpritePointerStore(ushort c64Address, byte value)
    {
        var c64Mem = Vic2.C64.Mem;

        // Get which sprite that was changed based on pointer c64Address
        var spriteNumber = c64Address - Vic2.SPRITE_POINTERS_START_ADDRESS;
        if (c64Mem[c64Address] != value)
        {
            // Store new value, use the original mapping to underlying RAM memory to avoid inifite loop
            c64Mem.WriteOriginal(c64Address, value);
            // Mark sprite as dirty which will re-generate image
            Sprites[spriteNumber].SetDirty(true);

            // Map changes of actual sprite data 
            for (int spriteDataAddressOffset = 0; spriteDataAddressOffset < 63; spriteDataAddressOffset++)
            {
                // TODO: Mapping is to C64 memory, which address to map depends on which VIC2 bank is selected?
                var spriteDataAddress = (ushort)((c64Mem[c64Address] * 64) + spriteDataAddressOffset);
                //c64Mem.MapReader(spriteDataAddress, (c64Address) => c64Mem[c64Address]);
                c64Mem.MapWriter(spriteDataAddress, SpriteDataStore);
            }
        }
    }

    private void SpriteDataStore(ushort address, byte value)
    {
        var c64Mem = Vic2.C64.Mem;

        if (c64Mem[address] != value)
        {
            // Store new value, use the original mapping to underlying RAM memory to avoid inifite loop
            c64Mem.WriteOriginal(address, value);

            // Get which sprite(s) that was changed based on which data location was changed (and compare that with what sprite pointers are used that points there).
            for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
            {
                // Map changes to sprite pointer
                var spritePointerAddress = (ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber);
                var spritePointer = c64Mem[spritePointerAddress];
                if (address >= spritePointer && address <= spritePointer + 63)
                {
                    // Mark sprite as dirty which will re-generate image
                    Sprites[spriteNumber].SetDirty(true);
                }
            }
        }
    }
}
