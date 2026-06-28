namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public interface IVic2SpriteManager
{
    public int SpritePointerStartAddress { get; }
    public int NumberOfSprites { get; }
    public int ScreenOffsetX { get; }
    public int ScreenOffsetY { get; }

    public Vic2Sprite[] Sprites { get; }

    public byte SpriteToSpriteCollisionStore { get; set; }
    public bool SpriteToSpriteCollisionIRQBlock { get; set; }

    public byte SpriteToBackgroundCollisionStore { get; set; }
    public bool SpriteToBackgroundCollisionIRQBlock { get; set; }

    public Vic2 Vic2 { get; }

    /// <summary>
    /// When true, sprite collision is accumulated per raster line during the frame (so multiplexed
    /// sprites register collisions for each displayed band), instead of once at end-of-frame on each
    /// sprite's final position. Gated by C64Config.Vic2RasterizerPerLineSprites.
    /// </summary>
    public bool PerLineCollisionEnabled { get; set; }

    public void SetAllDirty();
    public void SetAllChanged(Vic2Sprite.Vic2SpriteChangeType spriteChangeType);

    public void DetectChangesToSpriteData(ushort vic2Address, byte value);
    public void SetCollitionDetectionStatesAndIRQ();

    /// <summary>
    /// Start-of-line snapshot of the sprite enable register ($D015), captured once per raster line by
    /// <see cref="CaptureLineSpriteSnapshot"/>. Single source of truth shared by per-line sprite
    /// rendering and per-line collision (avoids both re-reading the registers).
    /// </summary>
    public byte LineSpriteEnableMask { get; }

    /// <summary>
    /// Start-of-line snapshot of each sprite's Y register, captured once per raster line. Only valid
    /// for sprites whose bit is set in <see cref="LineSpriteEnableMask"/>.
    /// </summary>
    public int[] LineSpriteY { get; }

    /// <summary>
    /// Captures the per-line sprite trigger-input snapshot (enable mask + Y). Called once per raster
    /// line from <see cref="Vic2.AdvanceRaster"/> when per-line sprite processing is active, before
    /// the collision accumulation and before the rasterizer's per-line sprite pass reads it.
    /// </summary>
    public void CaptureLineSpriteSnapshot();

    /// <summary>
    /// Accumulates sprite-to-sprite and sprite-to-background collisions for a single raster line into
    /// the collision stores, using the sprites' current (per-line / multiplex) positions. Called once
    /// per raster line from <see cref="Vic2.AdvanceRaster"/> when <see cref="PerLineCollisionEnabled"/>.
    /// </summary>
    public void AccumulatePerLineCollisions(int rasterLine);

    public byte GetSpriteToSpriteCollision();
    public byte GetSpriteToBackgroundCollision();

    public bool CheckCollisionAgainstBackground(Vic2Sprite sprite, int scrollX, int scrollY);

    public bool CheckCollision(ReadOnlySpan<byte> pixelData1, ReadOnlySpan<byte> pixelData2);

    public void GetSpriteRowLineData(Vic2Sprite sprite, int spriteScreenLine, ref Span<byte> spriteLineData);

    public void GetSpriteRowLineDataMatchingOtherSpritePosition(Vic2Sprite sprite0, Vic2Sprite sprite1, int sprite0ScreenLine, ref Span<byte> bytes);
    public void GetCharacterRowLineDataMatchingSpritePosition(Vic2Sprite sprite, int spriteScreenLine, int spriteBytesWidth, int scrollX, int scrollY, ref Span<byte> bytes);
}
