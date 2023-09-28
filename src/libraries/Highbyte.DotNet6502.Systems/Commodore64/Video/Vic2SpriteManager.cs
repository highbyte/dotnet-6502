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

            var spriteCollided = CheckCollisionAgainstBackground(sprite);

            if (spriteCollided)
            {
                // Set bit in collision byte for sprite number
                collision |= (byte)(1 << spriteNumber);
            }
        }
        return collision;
    }

    public bool CheckCollisionAgainstBackground(Vic2Sprite sprite)
    {
        // TODO: If sprite is expanded in Y (double height), duplicate the number of vertical lines

        // Loop each sprite line
        int numberOfScreenLines = sprite.DoubleHeight ? DEFAULT_HEIGTH * 2 : DEFAULT_HEIGTH;
        for (int screenLine = 0; screenLine < numberOfScreenLines; screenLine++)
        {
            // Get the pixels in the sprite line (24 pixels/3 bytes, or 48 pixels/ 6 bytes, depending if sprite is expanded vertically or not)
            byte[] spriteLineData = GetSpriteRowLineData(sprite, screenLine);


            // TODO: In GetCharacterRowLineDataMatchingSpritePosition, take into account
            //       horizontal or vertical scroll values to adjust from where from screen memory to read...

            // Get the corresponding character row line data (adjusted to align with byte boundaries for easy comparison)
            byte[] screenLineData = GetCharacterRowLineDataMatchingSpritePosition(sprite, screenLine, spriteLineData.Length);
            // TODO: If multicolor screen mode, make similar modification to pixelData2 as for sprite multicolor mode above?

            // Check collision on line
            bool collisionFound = CheckCollision(spriteLineData, screenLineData);
            if (collisionFound)
                return true;
        }
        return false;
    }

    public bool CheckCollision(byte[] pixelData1, byte[] pixelData2)
    {
        // Check if any same pixel (bit) is set in both set of bytes
        for (int i = 0; i < pixelData1.Length; i++)
        {
            if ((pixelData1[i] & pixelData2[i]) != 0)
            {
                return true;
            }
        }
        return false;
    }

    public byte[] GetSpriteRowLineData(Vic2Sprite sprite, int spriteScreenLine)
    {
        var spriteLine = sprite.DoubleHeight ? spriteScreenLine / 2 : spriteScreenLine;

        var spriteData = sprite.Data;

        // Get the pixels in the sprite line (24 pixels = 3 bytes)
        byte[] spriteLineData = spriteData.Rows[spriteLine].Bytes;

        if (sprite.Multicolor)
        {
            // If multicolor sprite mode, then each pixel is 2 bits, otherwise 1 bit.
            // For collision detection sake, the color does not matter.
            // Make sure if any bit in the pair (01,10,11) in variable pixelData1 is set to 1,
            // then set both bits in pair to 1.
            for (int i = 0; i < spriteLineData.Length; i++)
            {
                spriteLineData[i] = ChangeAnyBitPairsToSet(spriteLineData[i]);
            }
        }

        if (sprite.DoubleWidth)
        {
            var spriteLineDataExpanded = new byte[6];
            for (int i = 0; i < 3; i++)
            {
                var expandedPair = spriteLineData[i].StretchBits();
                spriteLineDataExpanded[i * 2] = expandedPair[0];
                spriteLineDataExpanded[i * 2 + 1] = expandedPair[1];
            }
            spriteLineData = spriteLineDataExpanded;
        }

        return spriteLineData;
    }

    private byte ChangeAnyBitPairsToSet(byte data)
    {
        byte newData = 0;
        byte mask = 0b00000011;
        for (int i = 0; i < 4; i++)
        {
            var bitPair = (byte)(data & mask);
            if (bitPair != 0)
                newData |= mask;
            mask <<= 2;
        }
        return newData;
    }

    public byte[] GetCharacterRowLineDataMatchingSpritePosition(Vic2Sprite sprite, int spriteScreenLine, int spriteBytesWidth)
    {
        // Adjust sprite position to match text screen y pixel position
        var textScreenPosY = sprite.Y + spriteScreenLine - SCREEN_OFFSET_Y;
        if (textScreenPosY < 0 || textScreenPosY > (8 * 25))
            return new byte[spriteBytesWidth]; // Y position is outside of text screen, so fill with 0

        // Adjust sprite position to match text screen x pixel position
        var textScreenPosX = sprite.X - SCREEN_OFFSET_X;

        // A sprite is 24 pixels/3 bytes or 48/6 bytes wide (depending if expanded in X or not). Note that actual defintion of expanded X sprite is till 3 bytes, it's only expanded when drawn on screen (double pixels).
        // Allocate one extra byte if character start x position does not align with sprite start x position (will be aligned after shifting bits afterwards)
        int scrollOffset;
        int numberOfBytesToRead;
        if (textScreenPosX < 0)
        {
            scrollOffset = (textScreenPosX % 8) + 8;
            numberOfBytesToRead = spriteBytesWidth + 1;
        }
        else
        {
            scrollOffset = textScreenPosX % 8;
            numberOfBytesToRead = scrollOffset == 0 ? spriteBytesWidth : spriteBytesWidth + 1;
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
            var allButLastByte = new byte[spriteBytesWidth];
            for (int i = 0; i < spriteBytesWidth; i++)
                allButLastByte[i] = bytes[i];
            return allButLastByte;
        }
        else
        {
            int scrollPixelsRight = 8 - scrollOffset;
            var shiftedBytes = bytes.ShiftRight(scrollPixelsRight, out _);

            var allButFirstByte = new byte[spriteBytesWidth];
            for (int i = 0; i < spriteBytesWidth; i++)
                allButFirstByte[i] = shiftedBytes[i + 1];
            return allButFirstByte;
        }
    }
}
