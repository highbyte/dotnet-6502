using System.Diagnostics;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2Sprite;

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

    public void SetAllDirty()
    {
        foreach (var sprite in Sprites)
        {
            sprite.HasChanged(Vic2SpriteChangeType.All);
        }
    }

    public void DetectChangesToSpriteData(ushort vic2Address, byte value)
    {
        // Detect changes in sprite pointers and data
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            var spritePointerAddress = (ushort)(Vic2.SPRITE_POINTERS_START_ADDRESS + spriteNumber);

            // Detect changes to sprite pointer
            if (vic2Address == spritePointerAddress)
                Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.Data);

            // Detect changes to the data the sprite pointer points to
            var spriteDataAddress = (ushort)(_vic2Mem[spritePointerAddress] * 64);
            if (vic2Address >= spriteDataAddress && vic2Address <= spriteDataAddress + 63)
                Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.Data);
        }
    }

    public byte GetSpriteToSpriteCollision()
    {
        byte collision = 0;
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            var sprite = Sprites[spriteNumber];
            if (!sprite.Visible)
                continue;
            // TODO
        }
        return collision;
    }

    public byte GetSpriteToBackgroundCollision()
    {
        byte collision = 0;
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            var sprite = Sprites[spriteNumber];
            if (!sprite.Visible)
                continue;

            var spriteData = sprite.Data;
            bool collisionFound = false;

            // TODO: If sprite is expanded in Y (double height), duplicate the number of vertical lines

            // Loop each sprite line
            for (int spriteLine = 0; spriteLine < DEFAULT_HEIGTH; spriteLine++)
            {
                // TODO: If sprite is expanded in X (double width), duplicate the number of horizontal pixels

                // Get the pixels in the sprite line (24 pixels = 3 bytes)
                byte[] spriteLineData = spriteData.Rows[spriteLine].Bytes;
                // TODO: If multicolor sprite mode, then each pixel is 2 bits, otherwise 1 bit. Make sure if any bit in the pair (01,10,11) in variable spriteLineData is set to 1, then set both bits in pair to 1.

                // Get the corresponding character row line data (adjusted to align with byte boundaries for easy comparison)
                byte[] screenLineData = GetCharacterRowLineDataMatchingSpritePosition(sprite.X, sprite.Y, spriteLine);
                // TODO: If multicolor screen mode, make similar modification to screenLineData as for sprite multicolor mode above?

                // Check if any pixel in sprite line collides with any pixel in screen line (the similar bit in both places are 1)
                for (int i = 0; i < 3; i++)
                {
                    if ((spriteLineData[i] & screenLineData[i]) != 0)
                    {
                        collision |= (byte)(1 << spriteNumber);
                        collisionFound = true;
                        break;
                    }
                }

                if (collisionFound)
                    break;
            }
        }
        return collision;
    }

    public byte[] GetCharacterRowLineDataMatchingSpritePosition(int spritePosX, int spritePosY, int spriteLine)
    {
        // Adjust sprite position to match text screen y pixel position
        var textScreenPosY = spritePosY + spriteLine - SCREEN_OFFSET_Y;
        if (textScreenPosY < 0 || textScreenPosY > (8 * 25))
            return new byte[3]; // Y position is outside of text screen, so fill with 0

        // Adjust sprite position to match text screen x pixel position
        var textScreenPosX = spritePosX - SCREEN_OFFSET_X;

        // A sprite is 24 pixels (3 bytes) wide.
        // Allocate one extra byte if character start x position does not align with sprite start x position (will be aligned after shifting bits afterwards)
        int scrollOffset;
        int numberOfBytesToRead;
        if (textScreenPosX < 0)
        {
            scrollOffset = (textScreenPosX % 8) + 8;
            numberOfBytesToRead = 4;
        }
        else
        {
            scrollOffset = textScreenPosX % 8;
            numberOfBytesToRead = scrollOffset == 0 ? 3 : 4;
        }

        var bytes = new byte[numberOfBytesToRead];

        // Loop for each byte (8 pixels) in the sprite
        for (int i = 0; i < numberOfBytesToRead; i++)
        {
            if (textScreenPosX < 0 || textScreenPosX >= (8 * 40))
            {
                // X position indicating start of 1 byte (8 pixels) is completley outside of text screen, fill with 0
                bytes[i] = 0;
            }
            else
            {
                var characterCol = textScreenPosX / 8;
                var characterRow = textScreenPosY / 8;
                var characterLine = textScreenPosY % 8;
                bytes[i] = Vic2.GetTextModeCharacterLine(characterCol, characterRow, characterLine);
            }
            textScreenPosX += 8;
        }

        if (scrollOffset == 0)
        {
            var firstThreeBytes = new byte[3];
            for (int i = 0; i < 3; i++)
                firstThreeBytes[i] = bytes[i];
            return firstThreeBytes;
        }
        else
        {
            int scrollPixelsRight = 8 - scrollOffset;
            var shiftedBytes = bytes.ShiftRight(scrollPixelsRight, out _);

            var lastThreeBytes = new byte[3];
            for (int i = 0; i < 3; i++)
                lastThreeBytes[i] = shiftedBytes[i + 1];
            return lastThreeBytes;
        }
    }
}
