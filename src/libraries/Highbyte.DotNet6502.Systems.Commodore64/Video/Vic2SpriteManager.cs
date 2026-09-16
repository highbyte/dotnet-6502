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
    public bool BackgroundCollisionsFromRenderer { get; set; }

    public void AddSpriteToBackgroundCollisions(byte mask)
    {
        if ((SpriteToBackgroundCollisionStore | mask) == SpriteToBackgroundCollisionStore)
            return;
        SpriteToBackgroundCollisionStore |= mask;
        RaiseCollisionIRQsIfNeeded();
    }

    // Single per-line sprite trigger-input snapshot, shared by per-line rendering and per-line
    // collision (captured once per raster line in AdvanceRaster -> CaptureLineSpriteSnapshot).
    public byte LineSpriteEnableMask { get; private set; }
    public int[] LineSpriteY { get; } = new int[NUMBERS_OF_SPRITES];

    // What the VIC-II shows of each sprite on the line that begins: the sprites whose display is
    // on (decided in cycle 58 of the line before) and the three bytes their s-accesses fetch for
    // it, at the sprite pointer read in the same accesses plus the data counter MC. Captured once
    // per raster line for the per-line renderer, so a change of the Y-expand bit, of the pointer
    // or of the data mid-sprite shows on the line it reaches.
    private const int MAX_RASTER_LINES = 320;   // covers the PAL (312) and NTSC (263) line counts
    private readonly byte[] _lineSpriteDisplayMasks = new byte[MAX_RASTER_LINES];
    private readonly byte[] _lineSpriteData = new byte[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * 3];
    public byte LineSpriteDisplayMask(int rasterLine) => _lineSpriteDisplayMasks[rasterLine];
    public ReadOnlySpan<byte> LineSpriteData(int rasterLine, int sprite) => _lineSpriteData.AsSpan((rasterLine * NUMBERS_OF_SPRITES + sprite) * 3, 3);

    // The output runs of each sprite on each line, as the VIC-II derives them when the line ends
    // from the X compare per pixel: where the run starts (a pixel index within the line, from the
    // line's first cycle), the row it shifts out, how many of its pixels are shown and how many
    // more repeat the last shown one. A sprite can be output twice on a line: once before its
    // fetch and once, with the row the fetch loaded, after it.
    public const int MaxSpriteRunsPerLine = 2;
    /// <summary>
    /// The most pixels one run outputs: 48 when X-expanded, plus the seven a sprite still shifting
    /// repeats while its own fetch halts it.
    /// </summary>
    public const int RunPixelCapacity = 56;
    // Per run: the X-expand, multicolour and priority bits in force when it started, and whether
    // it is stored decoded (one of those bits changed while the sprite was shifting, so the run
    // is kept pixel by pixel: bits 0-1 the pixel value, 0 transparent, 1 the first shared colour,
    // 2 the sprite's colour, 3 the second shared colour; bit 2 set where the sprite is behind the
    // foreground graphics).
    public const byte RunFlagXExpand = 1;
    public const byte RunFlagMultiColor = 2;
    public const byte RunFlagBehindForeground = 4;
    public const byte RunFlagDecoded = 8;
    public const byte RunPixelValueMask = 3;
    public const byte RunPixelBehindForeground = 4;
    private readonly byte[] _lineSpriteRunMasks = new byte[MAX_RASTER_LINES];
    private readonly byte[] _lineSpriteRunCount = new byte[MAX_RASTER_LINES * NUMBERS_OF_SPRITES];
    private readonly short[] _lineSpriteRunStart = new short[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
    private readonly uint[] _lineSpriteRunData = new uint[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
    private readonly byte[] _lineSpriteRunLength = new byte[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
    private readonly byte[] _lineSpriteRunStretch = new byte[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
    private readonly byte[] _lineSpriteRunFlags = new byte[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
    private readonly byte[] _lineSpriteRunPixels = new byte[MAX_RASTER_LINES * NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine * RunPixelCapacity];
    private readonly byte[] _lineSpriteXExpand = new byte[MAX_RASTER_LINES];
    private readonly byte[] _lineSpriteMultiColor = new byte[MAX_RASTER_LINES];
    public byte LineSpriteRunMask(int rasterLine) => _lineSpriteRunMasks[rasterLine];
    public int LineSpriteRunCount(int rasterLine, int sprite) => _lineSpriteRunCount[rasterLine * NUMBERS_OF_SPRITES + sprite];
    public int LineSpriteRunStart(int rasterLine, int sprite, int run) => _lineSpriteRunStart[(rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run];
    public uint LineSpriteRunData(int rasterLine, int sprite, int run) => _lineSpriteRunData[(rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run];
    public int LineSpriteRunLength(int rasterLine, int sprite, int run) => _lineSpriteRunLength[(rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run];
    public int LineSpriteRunStretch(int rasterLine, int sprite, int run) => _lineSpriteRunStretch[(rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run];
    public byte LineSpriteRunFlags(int rasterLine, int sprite, int run) => _lineSpriteRunFlags[(rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run];
    public ReadOnlySpan<byte> LineSpriteRunPixels(int rasterLine, int sprite, int run)
    {
        var index = (rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run;
        return _lineSpriteRunPixels.AsSpan(index * RunPixelCapacity, _lineSpriteRunLength[index]);
    }
    public byte LineSpriteXExpand(int rasterLine) => _lineSpriteXExpand[rasterLine];
    public byte LineSpriteMultiColor(int rasterLine) => _lineSpriteMultiColor[rasterLine];

    public void AddLineSpriteRun(int rasterLine, int sprite, int run, int startPixel, uint rowBits, int length, int stretch, byte flags)
    {
        var index = (rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run;
        _lineSpriteRunStart[index] = (short)startPixel;
        _lineSpriteRunData[index] = rowBits;
        _lineSpriteRunLength[index] = (byte)length;
        _lineSpriteRunStretch[index] = (byte)stretch;
        _lineSpriteRunFlags[index] = (byte)(flags & ~RunFlagDecoded);
        _lineSpriteRunCount[rasterLine * NUMBERS_OF_SPRITES + sprite] = (byte)(run + 1);
        _lineSpriteRunMasks[rasterLine] |= (byte)(1 << sprite);
    }

    public void AddLineSpriteDecodedRun(int rasterLine, int sprite, int run, int startPixel, ReadOnlySpan<byte> pixels)
    {
        var index = (rasterLine * NUMBERS_OF_SPRITES + sprite) * MaxSpriteRunsPerLine + run;
        var length = Math.Min(pixels.Length, RunPixelCapacity);
        pixels.Slice(0, length).CopyTo(_lineSpriteRunPixels.AsSpan(index * RunPixelCapacity, length));
        _lineSpriteRunStart[index] = (short)startPixel;
        _lineSpriteRunData[index] = 0;
        _lineSpriteRunLength[index] = (byte)length;
        _lineSpriteRunStretch[index] = 0;
        _lineSpriteRunFlags[index] = RunFlagDecoded;
        _lineSpriteRunCount[rasterLine * NUMBERS_OF_SPRITES + sprite] = (byte)(run + 1);
        _lineSpriteRunMasks[rasterLine] |= (byte)(1 << sprite);
    }

    // The kinds of register change a run's decoding follows, and the bit's new value.
    public const byte RunEventMultiColor = 0;
    public const byte RunEventXExpand = 1;
    public const byte RunEventPriority = 2;
    public const byte RunEventKindMask = 3;
    public const byte RunEventBitSet = 4;

    /// <summary>
    /// The pixels a sprite outputs from an X match when its multicolour, X-expand or priority bit
    /// changes while it shifts, followed pixel by pixel the way the chip's sprite data sequencer
    /// works (as the VICE project's cycle-based VIC-II establishes it, verified by its spritesplit
    /// test programs): the data register shifts one bit per pixel, or per two pixels when
    /// X-expanded, under an expansion flip-flop that toggles every pixel while the bit is set and
    /// is held set while it is clear, so setting the bit repeats the pixel being shown and clearing
    /// it fetches the next at once. In multicolour a pair is taken from the register's top two bits
    /// every other pixel under a second flip-flop; changing the multicolour bit clears that
    /// flip-flop, which delays the next pair by a pixel and so realigns the pairs on the register's
    /// odd bits. The priority bit is read at every pixel. From <paramref name="haltPixel"/> the
    /// sprite's own fetch halts the shifting and the last pixel repeats, until
    /// <paramref name="stopPixel"/>, where the sprite is switched off. A sprite whose register and
    /// last pixel are both empty stops at once. The events are given in pixel order, each seen
    /// from its pixel on. Returns the pixel count written to <paramref name="pixels"/>.
    /// </summary>
    public static int DecodeSpriteRun(uint register, int startPixel, int haltPixel, int stopPixel,
        bool multiColor, bool xExpand, bool behindForeground,
        ReadOnlySpan<int> eventPixels, ReadOnlySpan<byte> eventKinds, Span<byte> pixels)
    {
        var count = 0;
        var pixelValue = 0;
        var expandFlipFlop = true;
        var multiColorFlipFlop = true;
        var nextEvent = 0;
        for (var p = startPixel; p < stopPixel && count < pixels.Length; p++)
        {
            while (nextEvent < eventPixels.Length && eventPixels[nextEvent] <= p)
            {
                var kind = eventKinds[nextEvent++];
                var set = (kind & RunEventBitSet) != 0;
                switch (kind & RunEventKindMask)
                {
                    case RunEventMultiColor:
                        if (multiColor != set)
                        {
                            multiColor = set;
                            multiColorFlipFlop = false;
                        }
                        break;
                    case RunEventXExpand:
                        xExpand = set;
                        break;
                    default:
                        behindForeground = set;
                        break;
                }
            }
            if (register == 0 && pixelValue == 0)
                break;
            if (p < haltPixel)
            {
                if (expandFlipFlop)
                {
                    if (multiColor)
                    {
                        if (multiColorFlipFlop)
                            pixelValue = (int)(register >> 22) & 3;
                        multiColorFlipFlop = !multiColorFlipFlop;
                    }
                    else
                    {
                        pixelValue = (int)((register >> 23) & 1) << 1;
                    }
                    register = (register << 1) & 0xFFFFFF;
                }
                expandFlipFlop = !xExpand || !expandFlipFlop;
            }
            pixels[count++] = (byte)(pixelValue | (behindForeground ? RunPixelBehindForeground : 0));
        }
        return count;
    }

    /// <summary>
    /// A run stored as a row, its shown pixels and stretch, expanded to the same pixel values a
    /// decoded run holds (see <see cref="RunFlagDecoded"/>). Returns the pixel count.
    /// </summary>
    public static int ExpandRunPixels(uint row, byte flags, int length, int stretch, Span<byte> pixels)
    {
        var behind = (flags & RunFlagBehindForeground) != 0 ? RunPixelBehindForeground : 0;
        var multiColor = (flags & RunFlagMultiColor) != 0;
        var width = (flags & RunFlagXExpand) != 0 ? 2 : 1;
        var count = 0;
        var shown = Math.Min(length, pixels.Length);
        if (multiColor)
        {
            for (var pair = 0; pair < 12 && count < shown; pair++)
            {
                var value = (int)(row >> (22 - pair * 2)) & 3;
                for (var k = 0; k < 2 * width && count < shown; k++)
                    pixels[count++] = (byte)(value | behind);
            }
        }
        else
        {
            for (var bit = 0; bit < 24 && count < shown; bit++)
            {
                var value = (int)((row >> (23 - bit)) & 1) << 1;
                for (var k = 0; k < width && count < shown; k++)
                    pixels[count++] = (byte)(value | behind);
            }
        }
        var last = count > 0 ? pixels[count - 1] : (byte)behind;
        for (var s = 0; s < stretch && count < pixels.Length; s++)
            pixels[count++] = last;
        return count;
    }

    /// <summary>
    /// The sprite-to-sprite collisions of a line that has just ended, from the runs the VIC-II
    /// derived: two sprites collide where both output an opaque pixel. Evaluated at the line's
    /// end, so the register shows the collision after the line's pixels, as on the chip, not
    /// before the CPU has run the line's code.
    /// </summary>
    public void EndLineSpriteCollisions(int rasterLine)
    {
        var runMask = _lineSpriteRunMasks[rasterLine];
        if (runMask == 0 || (runMask & (runMask - 1)) == 0)
            return;   // fewer than two sprites output
        Span<ulong> masks = stackalloc ulong[NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
        Span<int> starts = stackalloc int[NUMBERS_OF_SPRITES * MaxSpriteRunsPerLine];
        for (int n = 0; n < NUMBERS_OF_SPRITES; n++)
        {
            var runs = LineSpriteRunCount(rasterLine, n);
            for (int r = 0; r < MaxSpriteRunsPerLine; r++)
            {
                var i = n * MaxSpriteRunsPerLine + r;
                if (r >= runs)
                {
                    masks[i] = 0;
                    continue;
                }
                var flags = _lineSpriteRunFlags[(rasterLine * NUMBERS_OF_SPRITES + n) * MaxSpriteRunsPerLine + r];
                masks[i] = (flags & RunFlagDecoded) != 0
                    ? DecodedRunOpaqueMask(LineSpriteRunPixels(rasterLine, n, r))
                    : RunOpaqueMask(LineSpriteRunData(rasterLine, n, r), (flags & RunFlagXExpand) != 0, (flags & RunFlagMultiColor) != 0,
                        LineSpriteRunLength(rasterLine, n, r), LineSpriteRunStretch(rasterLine, n, r));
                starts[i] = LineSpriteRunStart(rasterLine, n, r);
            }
        }
        var collided = false;
        for (int a = 0; a < NUMBERS_OF_SPRITES; a++)
        {
            for (int ra = 0; ra < MaxSpriteRunsPerLine; ra++)
            {
                var ia = a * MaxSpriteRunsPerLine + ra;
                if (masks[ia] == 0)
                    continue;
                for (int b = a + 1; b < NUMBERS_OF_SPRITES; b++)
                {
                    if ((SpriteToSpriteCollisionStore & (1 << a)) != 0 && (SpriteToSpriteCollisionStore & (1 << b)) != 0)
                        continue;
                    for (int rb = 0; rb < MaxSpriteRunsPerLine; rb++)
                    {
                        var ib = b * MaxSpriteRunsPerLine + rb;
                        if (masks[ib] == 0)
                            continue;
                        var shift = starts[ib] - starts[ia];   // b's run relative to a's, in pixels
                        if (shift <= -RunMaskBits || shift >= RunMaskBits)
                            continue;
                        var overlap = shift >= 0 ? masks[ia] & (masks[ib] << shift) : (masks[ia] << -shift) & masks[ib];
                        if (overlap != 0)
                        {
                            SpriteToSpriteCollisionStore |= (byte)(1 << a);
                            SpriteToSpriteCollisionStore |= (byte)(1 << b);
                            collided = true;
                        }
                    }
                }
            }
        }
        if (collided)
            RaiseCollisionIRQsIfNeeded();
    }

    private const int RunMaskBits = RunPixelCapacity;   // up to 48 shown pixels and 7 repeated

    // The opaque pixels of a decoded run as a mask, bit 0 its first pixel.
    private static ulong DecodedRunOpaqueMask(ReadOnlySpan<byte> pixels)
    {
        ulong mask = 0;
        for (var i = 0; i < pixels.Length && i < RunMaskBits; i++)
        {
            if ((pixels[i] & RunPixelValueMask) != 0)
                mask |= 1ul << i;
        }
        return mask;
    }

    // The opaque pixels of a run as a mask, bit 0 its first pixel: every set bit in standard mode,
    // both pixels of every non-zero pair in multicolour mode, each pixel doubled when X-expanded;
    // only the shown pixels, then the last shown one repeated for the stretch.
    private static ulong RunOpaqueMask(uint row, bool xExpand, bool multiColor, int length, int stretch)
    {
        var b0 = (byte)(row >> 16);
        var b1 = (byte)(row >> 8);
        var b2 = (byte)row;
        if (multiColor)
        {
            b0 = s_opaquePairs[b0];
            b1 = s_opaquePairs[b1];
            b2 = s_opaquePairs[b2];
        }
        ulong mask;
        int shown;
        if (xExpand)
        {
            mask = s_doubledBits[s_reversedBits[b0]] | (ulong)s_doubledBits[s_reversedBits[b1]] << 16 | (ulong)s_doubledBits[s_reversedBits[b2]] << 32;
            shown = 48;
        }
        else
        {
            mask = s_reversedBits[b0] | (ulong)s_reversedBits[b1] << 8 | (ulong)s_reversedBits[b2] << 16;
            shown = 24;
        }
        if (length < shown)
        {
            var lastShownOpaque = length > 0 && (mask & (1ul << (length - 1))) != 0;
            mask &= (1ul << length) - 1;
            if (lastShownOpaque)
                mask |= ((1ul << stretch) - 1) << length;
        }
        return mask;
    }

    // A byte's bits reversed (bit 7 to bit 0), each bit doubled (bit i to bits 2i and 2i+1), and
    // each non-zero bit pair made both bits set.
    private static readonly byte[] s_reversedBits = BuildTable(v => { var r = 0; for (int i = 0; i < 8; i++) if ((v & (1 << i)) != 0) r |= 0x80 >> i; return r; });
    private static readonly ushort[] s_doubledBits = BuildWideTable(v => { var r = 0; for (int i = 0; i < 8; i++) if ((v & (1 << i)) != 0) r |= 3 << (2 * i); return r; });
    private static readonly byte[] s_opaquePairs = BuildTable(v => { var r = 0; for (int p = 0; p < 4; p++) if ((v & (3 << (2 * p))) != 0) r |= 3 << (2 * p); return r; });

    private static byte[] BuildTable(Func<int, int> f)
    {
        var table = new byte[256];
        for (int v = 0; v < 256; v++)
            table[v] = (byte)f(v);
        return table;
    }

    private static ushort[] BuildWideTable(Func<int, int> f)
    {
        var table = new ushort[256];
        for (int v = 0; v < 256; v++)
            table[v] = (ushort)f(v);
        return table;
    }

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

        // Per-frame: raise at end-of-frame. Per-line: this is a harmless safety net (the IRQ was
        // already raised mid-frame at the collision's raster line; the calls below are idempotent
        // thanks to the block/triggered guards).
        RaiseCollisionIRQsIfNeeded();
    }

    /// <summary>
    /// Raises the sprite-to-sprite / sprite-to-background collision IRQs if a collision is latched in
    /// the stores, the corresponding source is enabled ($D01A), and it isn't currently blocked (the
    /// block is cleared when the game reads the collision IO register). Idempotent: safe to call
    /// repeatedly (e.g. once per raster line in per-line mode and again at end-of-frame).
    ///
    /// Sprite-to-sprite and sprite-to-background collisions are their own VIC-II interrupt sources
    /// ($D019 bits 1 and 2). They must NOT be raised as a raster-compare IRQ ($D019 bit 0): doing so
    /// makes a sprite collision look like a raster interrupt, so games that drive raster splits (e.g.
    /// Giana Sisters) service the collision as a spurious extra raster IRQ, displacing their splits.
    /// </summary>
    private void RaiseCollisionIRQsIfNeeded()
    {
        // The interrupt flag is latched whether or not the source is enabled in $D01A (the mask
        // only gates the CPU's interrupt line), so a program can poll $D019 for collisions; reading
        // the collision register does not clear the flag, only a write to $D019 does.
        if (SpriteToSpriteCollisionStore != 0 && !SpriteToSpriteCollisionIRQBlock
            && !Vic2.Vic2IRQ.IsTriggered(IRQSource.SpriteToSpriteCollision))
        {
            Vic2.Vic2IRQ.Trigger(IRQSource.SpriteToSpriteCollision, Vic2.C64.CPU);
            SpriteToSpriteCollisionIRQBlock = true;
        }

        if (SpriteToBackgroundCollisionStore != 0 && !SpriteToBackgroundCollisionIRQBlock
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
    public void CaptureLineSpriteSnapshot(int rasterLine)
    {
        var displayMask = Vic2.SpriteDisplayMask;
        _lineSpriteDisplayMasks[rasterLine] = displayMask;
        _lineSpriteXExpand[rasterLine] = Vic2.C64.ReadIOStorage(Vic2Addr.SPRITE_X_EXPAND);
        _lineSpriteMultiColor[rasterLine] = Vic2.C64.ReadIOStorage(Vic2Addr.SPRITE_MULTICOLOR_ENABLE);
        Array.Clear(_lineSpriteRunCount, rasterLine * NUMBERS_OF_SPRITES, NUMBERS_OF_SPRITES);
        _lineSpriteRunMasks[rasterLine] = 0;
        if (displayMask != 0)
        {
            for (int i = 0; i < NUMBERS_OF_SPRITES; i++)
            {
                if ((displayMask & (1 << i)) == 0)
                    continue;
                var pointer = Vic2.ReadMemory((ushort)(SpritePointerStartAddress + i));
                var address = pointer * 64 + Vic2.SpriteMc(i);
                var dataIndex = (rasterLine * NUMBERS_OF_SPRITES + i) * 3;
                _lineSpriteData[dataIndex] = Vic2.ReadMemory((ushort)address);
                _lineSpriteData[dataIndex + 1] = Vic2.ReadMemory((ushort)((address + 1) & 0x3FFF));
                _lineSpriteData[dataIndex + 2] = Vic2.ReadMemory((ushort)((address + 2) & 0x3FFF));
            }
        }

        LineSpriteEnableMask = Vic2.C64.ReadIOStorage(Vic2Addr.SPRITE_ENABLE);
        if (LineSpriteEnableMask == 0)
            return;
        // Only sample Y for enabled sprites (disabled entries keep stale values, never consumed).
        for (int i = 0; i < NUMBERS_OF_SPRITES; i++)
        {
            if ((LineSpriteEnableMask & (1 << i)) != 0)
                LineSpriteY[i] = Sprites[i].Y;
        }
    }

    public void AccumulatePerLineCollisions(int rasterLine)
    {
        // Uses the shared start-of-line snapshot (captured just before this call in AdvanceRaster).
        var enableMask = LineSpriteEnableMask;
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
            var spriteScreenLine = rasterLine - LineSpriteY[i];
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
        Span<byte> bgRow = stackalloc byte[DEFAULT_WIDTH / 8 * 2 + 1];

        var collisionAddedThisLine = false;

        // Sprite-to-background, per active sprite on this line, from the sprite's shape against the
        // character data under its start-of-line position; a render provider that resolves the
        // line's pixels supplies these collisions exactly instead (AddSpriteToBackgroundCollisions).
        for (int i = 0; i < NUMBERS_OF_SPRITES; i++)
        {
            if (BackgroundCollisionsFromRenderer)
                break;
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
            {
                SpriteToBackgroundCollisionStore |= (byte)(1 << i);
                collisionAddedThisLine = true;
            }
        }

        // Sprite-to-sprite collisions are evaluated when the line ends, from the output runs the
        // VIC-II derives with the X compare per pixel (EndLineSpriteCollisions).

        // Mid-frame collision IRQ: raise as soon as a new collision is latched on this raster line
        // (the CPU services it on the next instruction boundary, like the raster IRQ), instead of
        // waiting for the end-of-frame SetCollitionDetectionStatesAndIRQ.
        if (collisionAddedThisLine)
            RaiseCollisionIRQsIfNeeded();
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
        if (textScreenPosY < 0 || textScreenPosY >= (8 * 25))   // below the last text row: no graphics to meet
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
