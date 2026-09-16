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

    /// <summary>
    /// Set by a render provider that derives the sprite-to-background collisions from the pixels it
    /// resolves (the sequencer generator with per-line sprites): the per-line collision pass then
    /// leaves that register to <see cref="AddSpriteToBackgroundCollisions"/>.
    /// </summary>
    public bool BackgroundCollisionsFromRenderer { get; set; }

    /// <summary>
    /// Latches sprite-to-background collisions found on a line (bit n for sprite n) and raises the
    /// collision interrupt when they are new.
    /// </summary>
    public void AddSpriteToBackgroundCollisions(byte mask);

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
    /// Start-of-line snapshot of what the VIC-II displays on a raster line: the sprites whose
    /// display is on (decided in cycle 58 of the line before) and, per sprite, the three bytes its
    /// s-accesses fetch for the line. Kept per line, since the renderer can reach a line after the
    /// VIC-II has already entered the next.
    /// </summary>
    public byte LineSpriteDisplayMask(int rasterLine);
    public ReadOnlySpan<byte> LineSpriteData(int rasterLine, int sprite);
    public byte LineSpriteXExpand(int rasterLine);
    public byte LineSpriteMultiColor(int rasterLine);

    /// <summary>
    /// The output runs of a sprite on a line, derived by the VIC-II when the line ends from its X
    /// compare per pixel: the pixel index within the line where each run starts, the three bytes
    /// of the row it shifts out, how many of its pixels are shown, and how many more repeat the
    /// last shown pixel (a sprite still shifting when its own fetch begins).
    /// </summary>
    public byte LineSpriteRunMask(int rasterLine);
    public int LineSpriteRunCount(int rasterLine, int sprite);
    public int LineSpriteRunStart(int rasterLine, int sprite, int run);
    public uint LineSpriteRunData(int rasterLine, int sprite, int run);
    public int LineSpriteRunLength(int rasterLine, int sprite, int run);
    public int LineSpriteRunStretch(int rasterLine, int sprite, int run);
    /// <summary>
    /// The run's flags (X-expand, multicolour and priority as it started; decoded) and, for a
    /// decoded run, its pixels (see <see cref="Vic2SpriteManager.RunFlagDecoded"/>).
    /// </summary>
    public byte LineSpriteRunFlags(int rasterLine, int sprite, int run);
    public ReadOnlySpan<byte> LineSpriteRunPixels(int rasterLine, int sprite, int run);
    public void AddLineSpriteRun(int rasterLine, int sprite, int run, int startPixel, uint rowBits, int length, int stretch, byte flags);
    public void AddLineSpriteDecodedRun(int rasterLine, int sprite, int run, int startPixel, ReadOnlySpan<byte> pixels);
    public void EndLineSpriteCollisions(int rasterLine);

    /// <summary>
    /// Captures the per-line sprite trigger-input snapshot (enable mask + Y). Called once per raster
    /// line from <see cref="Vic2.AdvanceRaster"/> when per-line sprite processing is active, before
    /// the collision accumulation and before the rasterizer's per-line sprite pass reads it.
    /// </summary>
    public void CaptureLineSpriteSnapshot(int rasterLine);

    /// <summary>
    /// Accumulates sprite-to-background collisions for a single raster line into the collision
    /// store, using the sprites' positions at the line's start. Called once per raster line from
    /// <see cref="Vic2.AdvanceRaster"/> when <see cref="PerLineCollisionEnabled"/>. Sprite-to-sprite
    /// collisions come from the line's output runs instead (<see cref="EndLineSpriteCollisions"/>).
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
