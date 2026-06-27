using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Utils;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2Sprite;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Manager for all VIC-II sprites.
/// </summary>
public class Vic2SpriteManager : IVic2SpriteManager
{
    public Vic2 Vic2 { get; private set; }

    private Memory _vic2Mem => Vic2.Vic2Mem;

    public int SpritePointerStartAddress => Vic2.VideoMatrixBaseAddress + 0x03f8; // Default value is 0x07f8 (because Vic2.VideoMatrixBaseAddress is 0x0400 by default). 8 sprites, last is 0x07ff.

    private const int NUMBERS_OF_SPRITES = 8;
    private const int SCREEN_OFFSET_X = 24;
    private const int SCREEN_OFFSET_Y = 50;
    public int NumberOfSprites => NUMBERS_OF_SPRITES;
    //The Sprite top/left X position that appears on main screen (not border) position 0.
    public int ScreenOffsetX => SCREEN_OFFSET_X;
    //The Sprite top/left Y position that appears on main screen (not border) position 0.
    public int ScreenOffsetY => SCREEN_OFFSET_Y;
    public Vic2Sprite[] Sprites { get; private set; } = new Vic2Sprite[NUMBERS_OF_SPRITES];

    public byte SpriteToSpriteCollisionStore { get; set; }
    public bool SpriteToSpriteCollisionIRQBlock { get; set; }

    public byte SpriteToBackgroundCollisionStore { get; set; }
    public bool SpriteToBackgroundCollisionIRQBlock { get; set; }

    public bool PerLineCollisionEnabled { get; set; }

    // Pre-calculate all possible sprite combination for collision detection
    // Get all K-Combinations of sprite numbers (2)
    // This will give us all possible combinations of sprite pairs
    // (e.g. 0,1 0,2 0,3 0,4 0,5 0,6 0,7 1,2 1,3 1,4 1,5 1,6 1,7 2,3 2,4 2,5 2,6 2,7 3,4 3,5 3,6 3,7 4,5 4,6 4,7 5,6 5,7 6,7)
    private static readonly List<int[]> s_spriteNumberCombinations = Combinations.CombinationsRosettaWoRecursion(2, NUMBERS_OF_SPRITES).ToList();

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
        SetAllChanged(Vic2SpriteChangeType.All);
    }

    public void SetAllChanged(Vic2SpriteChangeType spriteChangeType)
    {
        foreach (var sprite in Sprites)
        {
            sprite.HasChanged(spriteChangeType);
        }
    }

    public void DetectChangesToSpriteData(ushort vic2Address, byte value)
    {
        // Detect changes in sprite pointers and data
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            var spritePointerAddress = (ushort)(SpritePointerStartAddress + spriteNumber);

            // Detect changes to sprite pointer
            if (vic2Address == spritePointerAddress)
                Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.Data);

            // Detect changes to the data the sprite pointer points to
            var spriteDataAddress = (ushort)(_vic2Mem[spritePointerAddress] * 64);
            if (vic2Address >= spriteDataAddress && vic2Address <= spriteDataAddress + 63)
                Sprites[spriteNumber].HasChanged(Vic2SpriteChangeType.Data);
        }
    }

    public void SetCollitionDetectionStatesAndIRQ()
    {
        // Store currently detected collisions only.
        // Any previous collision that is no longer detected will remain set.
        // It's cleared when reading from the sprite collision IO registers.

        // In per-line mode the stores were already accumulated during the frame (one raster line at a
        // time, from each sprite's per-line/multiplex position) by AccumulatePerLineCollisions, so the
        // end-of-frame single-position recompute is skipped. The IRQ logic below is shared.
        if (!PerLineCollisionEnabled)
        {
            // Sprite-to-sprite collision
            var spriteToSpriteCollision = GetSpriteToSpriteCollision();
            SpriteToSpriteCollisionStore = (byte)(SpriteToSpriteCollisionStore | spriteToSpriteCollision);

            // Sprite-to-background collision
            var spriteToBackgroundCollision = GetSpriteToBackgroundCollision();
            SpriteToBackgroundCollisionStore = (byte)(SpriteToBackgroundCollisionStore | spriteToBackgroundCollision);
        }

        // Raise IRQ if a collision is detected, the corresponding source is enabled, and it's not
        // currently blocked (block is cleared when the game reads the collision IO register).
        //
        // Sprite-to-sprite and sprite-to-background collisions are their own VIC-II interrupt
        // sources ($D019 bits 1 and 2), each enabled independently via $D01A. They must NOT be
        // raised as a raster-compare IRQ ($D019 bit 0): doing so makes a sprite collision look
        // like a raster interrupt, so games that drive raster splits (e.g. Giana Sisters) service
        // the collision as a spurious extra raster IRQ, displacing their split chain.
        if (SpriteToSpriteCollisionStore != 0 && !SpriteToSpriteCollisionIRQBlock
            && Vic2.Vic2IRQ.IsEnabled(IRQSource.SpriteToSpriteCollision)
            && !Vic2.Vic2IRQ.IsTriggered(IRQSource.SpriteToSpriteCollision))
        {
            Vic2.Vic2IRQ.Trigger(IRQSource.SpriteToSpriteCollision, Vic2.C64.CPU);
            SpriteToSpriteCollisionIRQBlock = true;
        }

        if (SpriteToBackgroundCollisionStore != 0 && !SpriteToBackgroundCollisionIRQBlock
            && Vic2.Vic2IRQ.IsEnabled(IRQSource.SpriteToBackgroundCollision)
            && !Vic2.Vic2IRQ.IsTriggered(IRQSource.SpriteToBackgroundCollision))
        {
            Vic2.Vic2IRQ.Trigger(IRQSource.SpriteToBackgroundCollision, Vic2.C64.CPU);
            SpriteToBackgroundCollisionIRQBlock = true;
        }
    }

    /// <summary>
    /// Per-raster-line collision accumulation (multiplex-correct). Evaluated one raster line at a
    /// time using the sprites' live positions, reusing the exact same pixel-overlap helpers as the
    /// end-of-frame path. For a static sprite this OR-accumulates to an identical result; for a
    /// multiplexed sprite each displayed band is evaluated at the position it had on that line.
    ///
    /// A sprite displays raster lines [spriteY, spriteY + heightPixels); its internal row on this
    /// line is (rasterLine - spriteY). That is exactly the spriteScreenLine the helpers expect, so a
    /// static scene reproduces the end-of-frame result line-for-line.
    /// </summary>
    public void AccumulatePerLineCollisions(int rasterLine)
    {
        var enableMask = Vic2.C64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE);
        if (enableMask == 0)
            return;

        // Determine which enabled sprites display (with visible pixels) on this raster line, and
        // their internal row. Cheap reject for the common "nothing here" lines.
        Span<int> rowOf = stackalloc int[NUMBERS_OF_SPRITES];
        int activeMask = 0;
        for (int i = 0; i < NUMBERS_OF_SPRITES; i++)
        {
            if ((enableMask & (1 << i)) == 0)
                continue;
            var sprite = Sprites[i];
            var spriteScreenLine = rasterLine - sprite.Y;
            if (spriteScreenLine < 0 || spriteScreenLine >= sprite.HeightPixels)
                continue;
            if (!sprite.ScreenLineHasVisiblePixels(spriteScreenLine))
                continue;
            rowOf[i] = spriteScreenLine;
            activeMask |= 1 << i;
        }
        if (activeMask == 0)
            return;

        var scrollX = Vic2.GetScrollX();
        var scrollY = Vic2.GetScrollY();

        // Reusable scratch (max sprite width = 6 bytes when X-expanded; background needs +1 for
        // sub-byte alignment). Hoisted out of the loops to avoid per-iteration stackalloc.
        Span<byte> spriteRow = stackalloc byte[DEFAULT_WIDTH / 8 * 2];
        Span<byte> otherRow = stackalloc byte[DEFAULT_WIDTH / 8 * 2];
        Span<byte> bgRow = stackalloc byte[DEFAULT_WIDTH / 8 * 2 + 1];

        // Sprite-to-background, per active sprite on this line.
        for (int i = 0; i < NUMBERS_OF_SPRITES; i++)
        {
            if ((activeMask & (1 << i)) == 0)
                continue;
            // Once a sprite has flagged a background collision this frame, no need to re-check it
            // every subsequent line (the store stays set until the game reads the register).
            if ((SpriteToBackgroundCollisionStore & (1 << i)) != 0)
                continue;

            var sprite = Sprites[i];
            var spriteLineData = spriteRow.Slice(0, sprite.WidthBytes);
            GetSpriteRowLineData(sprite, rowOf[i], ref spriteLineData);
            var screenLineData = bgRow.Slice(0, sprite.WidthBytes + 1);
            GetCharacterRowLineDataMatchingSpritePosition(sprite, rowOf[i], spriteLineData.Length, scrollX, scrollY, ref screenLineData);
            if (CheckCollision(spriteLineData, screenLineData))
                SpriteToBackgroundCollisionStore |= (byte)(1 << i);
        }

        // Sprite-to-sprite, per active pair on this line.
        for (int a = 0; a < NUMBERS_OF_SPRITES; a++)
        {
            if ((activeMask & (1 << a)) == 0)
                continue;
            var spriteA = Sprites[a];
            for (int b = a + 1; b < NUMBERS_OF_SPRITES; b++)
            {
                if ((activeMask & (1 << b)) == 0)
                    continue;
                // Both already flagged this frame -> nothing to add.
                if ((SpriteToSpriteCollisionStore & (1 << a)) != 0 && (SpriteToSpriteCollisionStore & (1 << b)) != 0)
                    continue;
                var spriteB = Sprites[b];
                if (!SpriteBoundsOverlap(spriteA, spriteB))
                    continue;

                var aData = spriteRow.Slice(0, spriteA.WidthBytes);
                GetSpriteRowLineData(spriteA, rowOf[a], ref aData);
                var bData = otherRow.Slice(0, aData.Length);
                GetSpriteRowLineDataMatchingOtherSpritePosition(spriteA, spriteB, rowOf[a], ref bData);
                if (CheckCollision(aData, bData))
                {
                    SpriteToSpriteCollisionStore |= (byte)(1 << a);
                    SpriteToSpriteCollisionStore |= (byte)(1 << b);
                }
            }
        }
    }

    public byte GetSpriteToSpriteCollision()
    {
        byte collision = 0;
        // Loop sprite combinations
        foreach (var spriteNumberCombination in s_spriteNumberCombinations)
        {
            var s0 = spriteNumberCombination[0];
            var s1 = spriteNumberCombination[1];
            var sprite = Sprites[s0];
            var otherSprite = Sprites[s1];

            // If any of the sprite in the combination isn't visible, then no need to check collision
            if (!sprite.Visible || !otherSprite.Visible)
                continue;

            if (!SpriteBoundsOverlap(sprite, otherSprite))
                continue;

            // Loop each sprite line
            for (int screenLine = 0; screenLine < sprite.HeightPixels; screenLine++)
            {
                if (!sprite.ScreenLineHasVisiblePixels(screenLine))
                    continue;

                // Get the pixels in the sprite line (24 pixels/3 bytes, or 48 pixels/ 6 bytes, depending if sprite is expanded horizontally or not)
#pragma warning disable CA2014 // Do not use stackalloc in loops (24 or 48 times = height of sprite, should be fine)
                Span<byte> spriteLineData = stackalloc byte[sprite.WidthBytes];
#pragma warning restore CA2014 // Do not use stackalloc in loops
                GetSpriteRowLineData(sprite, screenLine, ref spriteLineData);

                // Get the corresponding character row line data (adjusted to align with byte boundaries for easy comparison)
#pragma warning disable CA2014 // Do not use stackalloc in loops (24 or 48 times = height of sprite, should be fine)
                Span<byte> otherSpriteLineData = stackalloc byte[spriteLineData.Length];
#pragma warning restore CA2014 // Do not use stackalloc in loops
                GetSpriteRowLineDataMatchingOtherSpritePosition(sprite, otherSprite, screenLine, ref otherSpriteLineData);

                // Check collision on line
                bool collisionFound = CheckCollision(spriteLineData, otherSpriteLineData);

                if (collisionFound)
                {
                    // Set bit in collision byte for both sprites
                    collision |= (byte)(1 << sprite.SpriteNumber);
                    collision |= (byte)(1 << otherSprite.SpriteNumber);
                    break;
                }
            }
        }
        return collision;
    }

    public byte GetSpriteToBackgroundCollision()
    {
        // Offset based on screen horizontal and vertical scrolling settings (affects text and graphics mode, but not sprites)
        var scrollX = Vic2.GetScrollX();
        var scrollY = Vic2.GetScrollY();

        byte collision = 0;
        for (int spriteNumber = 0; spriteNumber < NUMBERS_OF_SPRITES; spriteNumber++)
        {
            var sprite = Sprites[spriteNumber];
            if (!sprite.Visible)
                continue;

            var spriteCollided = CheckCollisionAgainstBackground(sprite, scrollX, scrollY);

            if (spriteCollided)
            {
                // Set bit in collision byte for sprite number
                collision |= (byte)(1 << spriteNumber);
            }
        }
        return collision;
    }

    public bool CheckCollisionAgainstBackground(Vic2Sprite sprite, int scrollX, int scrollY)
    {
        // Loop each sprite line
        for (int screenLine = 0; screenLine < sprite.HeightPixels; screenLine++)
        {
            if (!sprite.ScreenLineHasVisiblePixels(screenLine))
                continue;

            // Get the pixels in the sprite line (24 pixels/3 bytes, or 48 pixels/ 6 bytes, depending if sprite is expanded vertically or not)
#pragma warning disable CA2014 // Do not use stackalloc in loops (24 or 48 times = height of sprite, should be fine)
            Span<byte> spriteLineData = stackalloc byte[sprite.WidthBytes];
#pragma warning restore CA2014 // Do not use stackalloc in loops

            GetSpriteRowLineData(sprite, screenLine, ref spriteLineData);

            // Get the corresponding character row line data (adjusted to align with byte boundaries for easy comparison)
            //byte[] screenLineData = GetCharacterRowLineDataMatchingSpritePosition(sprite, screenLine, spriteLineData.Length, scrollX, scrollY);
#pragma warning disable CA2014 // Do not use stackalloc in loops (24 or 48 times = height of sprite, should be fine)
            Span<byte> screenLineData = stackalloc byte[sprite.WidthBytes + 1];
#pragma warning restore CA2014 // Do not use stackalloc in loops
            GetCharacterRowLineDataMatchingSpritePosition(sprite, screenLine, spriteLineData.Length, scrollX, scrollY, ref screenLineData);

            // Check collision on line
            bool collisionFound = CheckCollision(spriteLineData, screenLineData);
            if (collisionFound)
                return true;
        }
        return false;
    }

    public bool CheckCollision(ReadOnlySpan<byte> pixelData1, ReadOnlySpan<byte> pixelData2)
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

    private static bool SpriteBoundsOverlap(Vic2Sprite sprite, Vic2Sprite otherSprite)
    {
        return sprite.X < otherSprite.X + otherSprite.WidthPixels
            && otherSprite.X < sprite.X + sprite.WidthPixels
            && sprite.Y < otherSprite.Y + otherSprite.HeightPixels
            && otherSprite.Y < sprite.Y + sprite.HeightPixels;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sprite"></param>
    /// <param name="spriteScreenLine"></param>
    /// <param name="spriteLineData">A Span with the length of sprite.WidthBytes. Will be filled with data.</param>
    public void GetSpriteRowLineData(Vic2Sprite sprite, int spriteScreenLine, ref Span<byte> spriteLineData)
    {
        var spriteLine = sprite.DoubleHeight ? spriteScreenLine / 2 : spriteScreenLine;

        // Get the pixels in the sprite line (24 pixels = 3 bytes)
        byte[] originalSpriteLineData = sprite.Data.Rows[spriteLine].Bytes;

        AdjustSpriteLineDataForCollisionDetection(sprite, originalSpriteLineData, ref spriteLineData);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sprite"></param>
    /// <param name="originalSpriteLineData"></param>
    /// <param name="spriteLineData">A Span with the length of sprite.WidthBytes. Will be filled with data.</param>
    private void AdjustSpriteLineDataForCollisionDetection(Vic2Sprite sprite, byte[] originalSpriteLineData, ref Span<byte> spriteLineData)
    {
        for (int i = 0; i < originalSpriteLineData.Length; i++)
        {
            spriteLineData[i] = originalSpriteLineData[i];

            // If multicolor sprite mode, then each pixel is 2 bits, otherwise 1 bit.
            // For collision detection sake, the color does not matter.
            // Make sure if any bit in the pair (01,10,11) in variable pixelData1 is set to 1,
            // then set both bits in pair to 1.
            if (sprite.Multicolor)
                spriteLineData[i] = ChangeAnyBitPairsToSet(originalSpriteLineData[i]);

            // If double with sprite, then expand each byte to two bytes (each pixel is 2 bits instead of 1)
            if (sprite.DoubleWidth)
            {
#pragma warning disable CA2014 // Do not use stackalloc in loops (max 3 times, should be fine)
                Span<byte> expandedPair = stackalloc byte[2];
#pragma warning restore CA2014 // Do not use stackalloc in loops
                originalSpriteLineData[i].StretchBits(ref expandedPair);
                spriteLineData[i * 2] = expandedPair[0];
                spriteLineData[i * 2 + 1] = expandedPair[1];
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sprite0"></param>
    /// <param name="sprite1"></param>
    /// <param name="sprite0ScreenLine"></param>
    /// <param name="bytes">A Span with the length of sprite0.WidthBytes. Will be filled with data.</param>
    /// 
    public void GetSpriteRowLineDataMatchingOtherSpritePosition(Vic2Sprite sprite0, Vic2Sprite sprite1, int sprite0ScreenLine, ref Span<byte> bytes)
    {
        // Loop for each byte (8 pixels) in the sprite
        var spriteScreenPosX = sprite0.X;
        var spriteScreenPosY = sprite0.Y + sprite0ScreenLine;
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = GetSpritePixelByteForCoordinate(sprite1, spriteScreenPosX, spriteScreenPosY);
            spriteScreenPosX += 8;
        }
    }

    private byte GetSpritePixelByteForCoordinate(Vic2Sprite sprite, int x, int y)
    {
        // Check if the coordinate is outside of the sprite (byte boundary for x)
        if (x < 0 || x >= (sprite.X + sprite.WidthPixels) || (x + 8) <= sprite.X
            || y < 0 || y >= (sprite.Y + sprite.HeightPixels) || y < sprite.Y)
        {
            return 0;
        }

        var spriteScreenLine = y - sprite.Y;
        var deltaX = (x - sprite.X);
        var spriteRowByteIndex = deltaX / 8;

        Span<byte> rowBytes = stackalloc byte[sprite.WidthBytes];
        GetSpriteRowLineData(sprite, spriteScreenLine, ref rowBytes);

        var bitPositionAdjustOffset = deltaX % 8;
        if (bitPositionAdjustOffset == 0)
        {
            var rowByte = rowBytes[spriteRowByteIndex];
            return rowByte;
        }

        Span<byte> bytes = stackalloc byte[3];
        bytes[0] = spriteRowByteIndex == 0 ? (byte)0 : rowBytes[spriteRowByteIndex - 1];
        bytes[1] = rowBytes[spriteRowByteIndex];
        bytes[2] = (spriteRowByteIndex + 1) >= rowBytes.Length ? (byte)0 : rowBytes[spriteRowByteIndex + 1];

        Span<byte> shiftedBytes = stackalloc byte[3];
        bytes.ShiftRight(ref shiftedBytes, 8 - bitPositionAdjustOffset, out _);

        var result = shiftedBytes[2];
        return result;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sprite"></param>
    /// <param name="spriteScreenLine"></param>
    /// <param name="spriteBytesWidth"></param>
    /// <param name="scrollX"></param>
    /// <param name="scrollY"></param>
    /// <param name="bytes">A Span with the length of spriteBytesWidth. Will be filled with data.</param>
    public void GetCharacterRowLineDataMatchingSpritePosition(Vic2Sprite sprite, int spriteScreenLine, int spriteBytesWidth, int scrollX, int scrollY, ref Span<byte> bytes)
    {
        // Find out which corresponding text screen Y coordinate of the sprite line
        var textScreenPosY = sprite.Y + spriteScreenLine - SCREEN_OFFSET_Y - scrollY;
        if (textScreenPosY < 0 || textScreenPosY > (8 * 25))
        {
            bytes = bytes.Slice(0, spriteBytesWidth);
            bytes.Clear();
            return;
        }

        // Find out which corresponding text screen X coordinate the sprite starts at
        var textScreenPosX = sprite.X - SCREEN_OFFSET_X - scrollX;

        // A sprite is 24 pixels/3 bytes or 48/6 bytes wide (depending if expanded in X or not). Note that actual defintion of expanded X sprite is still 3 bytes, it's only expanded when drawn on screen (double pixels).
        // Allocate one extra byte if character start x position does not align with sprite start x position (will be aligned after shifting bits afterwards)
        int bitPositionAdjustOffset;
        int numberOfBytesToRead;
        if (textScreenPosX < 0)
        {
            bitPositionAdjustOffset = (textScreenPosX % 8) + 8;
            numberOfBytesToRead = spriteBytesWidth + 1;
        }
        else
        {
            bitPositionAdjustOffset = textScreenPosX % 8;
            numberOfBytesToRead = bitPositionAdjustOffset == 0 ? spriteBytesWidth : spriteBytesWidth + 1;
        }

        bytes = bytes.Slice(0, numberOfBytesToRead);

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
                if (Vic2.DisplayMode == DispMode.Text)
                {
                    bytes[i] = Vic2.CharsetManager.GetTextModeCharacterLine(characterCol, characterRow, characterLine);
                    if (Vic2.CharacterMode == CharMode.MultiColor)
                        bytes[i] = ChangeAnyBitPairsToSet(bytes[i]);
                }
                else // Assume bitmap mode 
                {
                    bytes[i] = Vic2.BitmapManager.GetBitmapCharacterLine(characterCol, characterRow, characterLine);
                    // Note from https://github.com/mist64/c64ref/blob/master/Source/c64io/c64io_mapc64.txt:
                    // "The only exception to this rule is the 01 bit - pair of multicolor graphics data.
                    // This bit-pair is considered part of the background, and the dot it displays can never be involved in a collision."
                    if (Vic2.BitmapMode == BitmMode.MultiColor)
                        bytes[i] = ChangeAnyBitPairsToSet(bytes[i], treat_pattern_01_as_background: true);
                }
            }
            textScreenPosX += 8;
        }

        if (bitPositionAdjustOffset == 0)
        {
            // All but last byte
            bytes = bytes.Slice(0, spriteBytesWidth);
            return;
        }

        int scrollPixelsRight = 8 - bitPositionAdjustOffset;
        Span<byte> shiftedBytes = stackalloc byte[bytes.Length];
        bytes.ShiftRight(ref shiftedBytes, scrollPixelsRight, out _);

        // All but first byte
        shiftedBytes.CopyTo(bytes);
        bytes = bytes.Slice(1, spriteBytesWidth);
    }

    private byte ChangeAnyBitPairsToSet(byte data, bool treat_pattern_01_as_background = false)
    {
        byte newData = 0;
        byte maskCheck = (byte)(treat_pattern_01_as_background ? 0b00000010 : 0b00000011);
        byte maskSet = 0b00000011;
        for (int i = 0; i < 4; i++)
        {
            var bitPair = (byte)(data & maskCheck);
            if (bitPair != 0)
                newData |= maskSet;
            maskCheck <<= 2;
            maskSet <<= 2;
        }
        return newData;
    }
}
