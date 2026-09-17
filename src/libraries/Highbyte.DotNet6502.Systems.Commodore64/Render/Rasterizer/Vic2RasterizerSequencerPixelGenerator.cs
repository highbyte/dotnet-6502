using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;

public sealed class Vic2RasterizerSequencerPixelGenerator : IVic2RasterizerPixelGenerator
{
    private readonly C64 _c64;
    // Arrays of color for C64 screen to render to
    //private readonly uint[] PixelArray_BackgroundAndBorder;
    //private readonly uint[] PixelArray_Foreground;

    private uint[] _c64ToRenderColorMap;
    private uint TransparentColor { get; }
    private bool FlipY { get; }


    // Pre-calculated pixel arrays
    private uint[][] _oneLineSameColorPixels; // a line of each colour, for the border runs


    // Line render state
    private int _lastScreenLineDataUpdate = -1;
    // Screen line of each raster line, from the model once: looked up per cycle.
    private int[] _rasterToScreenLine = Array.Empty<int>();

    // --- The video matrix line and the counters that address it, cycle by cycle after the VIC-II
    // article's section 3.7.2. The video matrix line is the 40 screen codes and colour nibbles the
    // c-accesses of a bad line read, at the position VMLI; the g-accesses read them from there in
    // display state and advance VMLI and VC after each. In cycle 14 VC is loaded from VCBASE and
    // VMLI cleared, and RC reset if there is a bad line condition then; in cycle 58, RC 7 sends the
    // sequencer to idle state and VC into VCBASE, and in display state RC advances. The bad line
    // condition is the article's: raster line $30-$F7, its low three bits equal to YSCROLL as it is
    // in that cycle, DEN seen during line $30. It can come and go within a line: a condition in
    // cycles 12-54 pulls BA low and starts the c-accesses (cycles 15-54), whose first three read $FF
    // and the low nibble of the CPU's next opcode while the CPU still holds the bus (3.14.6, the DMA
    // delay); one taken away before cycle 14 leaves RC as it is (3.14.4, linecrunch); one asserted
    // in cycles 54-57 of a row's last line keeps display state and repeats the row (3.14.5).
    private readonly byte[] _matrixLine = new byte[40];
    private readonly byte[] _colorLine = new byte[40];
    private int _vc;              // video counter, 10 bits
    private int _vcBase;          // video counter base
    private int _rc;              // row counter, 3 bits
    private int _vmli;            // video matrix line index
    private bool _displayState;   // display state, else idle state
    // Whether DEN was seen set in a cycle of raster line $30 this frame, kept by the cycle walk
    // from the journaled $D011; the core's latch seeds it for a walk that starts after that line.
    private bool _displayEnabledThisFrame;
    private byte _d011PreviousCycle;   // $D011 as the fetch of the cycle before saw it
    private int _baLowSince = -1; // the cycle BA went low for this line's c-accesses, -1 if it has not
    private const int BadLineFirstRasterLine = 0x30;
    private const int BadLineLastRasterLine = 0xF7;
    private ulong _lastCyclesConsumedCurrentVblank;


    // Copies of C64 screen values that should'nt change
    private int _screenLayoutInclNonVisibleScreenStartX;
    private int _screenLayoutInclNonVisibleScreenStartY;
    private int _screenLayoutInclNonVisibleScreenEndX;
    private int _screenLayoutInclNonVisibleScreenEndY;
    private int _vic2ScreenTextCols;

    private int _screenStartY;
    private int _screenStartX;

    private int _topBorderStartX;
    private int _topBorderStartY;
    private int _topBorderEndX;
    private int _topBorderEndY;

    private int _bottomBorderStartX;
    private int _bottomBorderStartY;
    private int _bottomBorderEndX;
    private int _bottomBorderEndY;

    private int _leftBorderStartX;
    private int _leftBorderStartY;
    private int _leftBorderEndX;
    private int _leftBorderEndY;

    private int _rightBorderStartX;
    private int _rightBorderStartY;
    private int _rightBorderEndX;
    private int _rightBorderEndY;

    private int _vic2ScreenCharacterHeight;
    private int _width;
    private int _height;
    private int _drawableAreaWidth;
    private ulong _cyclesPerLine;
    private int _colorChangePixelDelay;

    // --- The graphics data sequencer, pixel by pixel.
    // The VIC-II article (Christian Bauer, "The MOS 6567/6569 video controller (VIC-II) and its
    // application in the Commodore 64", sections 3.7 and 3.8) describes the sequencer: the g-access
    // of each cycle 16-55 fetches one byte, the sequencer shifts it out one bit (one pair of bits in
    // multicolour) per pixel, the shift register is loaded at the pixel XSCROLL selects, the modes
    // decide what colour each bit or pair stands for and which of them are foreground, and in idle
    // state the byte comes from $3FFF ($39FF with ECM) with the matrix data read as zero. The output
    // shows a byte two cycles after its g-access. Where in a cycle a register change reaches the
    // output is not in the article; those points (the mode bits four pixels in, or six when a bit is
    // cleared; the multicolour decoding a cycle after its colour selection; the colour of a set bit in
    // a multicolour cell during that gap) are the chip's observed behaviour as established by the
    // VICE project's viciisc emulation (vicii-draw-cycle.c) and its VICII test programs, whose
    // reference pictures this generator is checked against. VICE is the reference, not the source.

    // The display registers as the sequencer sees them, through the register write journal: a
    // write is seen from the cycle after it lands.
    private byte _d011;   // ECM, BMM (and the vertical state's bits, used elsewhere)
    private byte _d016;   // MCM, CSEL, XSCROLL
    private byte _d018;   // video matrix, character set and bitmap pointers

    // What the last three cycles' g-accesses fetched, by cycle number modulo 3: the graphics byte and
    // the matrix byte and colour nibble that belong to it. The shift register takes the entry fetched
    // two cycles earlier.
    private readonly byte[] _fetchedData = new byte[3];
    private readonly byte[] _fetchedMatrix = new byte[3];
    private readonly byte[] _fetchedColor = new byte[3];
    private byte _loadPixel;   // the pixel within a cycle the shift register is loaded at (XSCROLL)

    // The shift register and what it was loaded with.
    private byte _shiftData;
    private byte _shiftMatrix;
    private byte _shiftColor;
    private bool _pairSecondHalf;   // in multicolour the second pixel of a pair repeats the first
    private byte _pixelValue;       // the value of the pixel being output: the bit twice, or the pair

    // The modes as they stand at the output. ECM and BMM are held as a pair of bits that a change
    // reaches four pixels into a cycle when set and six when cleared; the colour selection follows
    // MCM four pixels in, the decoding into pairs a cycle later.
    private const byte MODE_ECM = 0x04, MODE_BMM = 0x02, MODE_MCM = 0x01;
    private byte _modeEcmBmm;      // MODE_ECM | MODE_BMM as they stand
    private bool _colorMcm;        // MCM as the colour selection has it
    private bool _decodeMcm;       // MCM as the decoding into pairs has it
    private const int FirstFetchCycle = 15;   // the g-access of column 0 (the chip's cycle 16)

    // The line's graphics, as colour codes, resolved into the two layers when the line ends: a
    // code below 16 is a colour, CODE_BG0 + n the background colour register n as it stands at
    // that pixel, CODE_NONE no foreground pixel.
    private const byte CODE_BG0 = 16;
    private const byte CODE_NONE = 255;
    // The colour code for each of the four pixel values with the modes and the shift register's
    // matrix byte and colour nibble as they are now.
    private readonly byte[] _colorCodes = new byte[4];
    private bool _colorCodesValid;
    private int _colorCodesKey = -1;
    // Colour per code for a line without background colour writes (codes 0-15 and the four
    // registers), and per foreground code (CODE_NONE is transparent); rebuilt when a line is resolved.
    private readonly uint[] _bgCodeColor = new uint[CODE_BG0 + 4];
    private readonly uint[] _fgCodeColor = new uint[256];
    private byte[] _lineBgCodes = default!;
    private byte[] _lineFgCodes = default!;
    private uint[] _lineBgPixels = default!;
    private uint[] _lineFgPixels = default!;
    // The background colour register writes that landed on the current line (normalized x and
    // colour, per register), and the registers' values when the line began.
    private const int BG_COLOR_EVENT_CAPACITY = 32;
    private readonly int[] _bgColorEventX = new int[4 * BG_COLOR_EVENT_CAPACITY];
    private readonly byte[] _bgColorEventColor = new byte[4 * BG_COLOR_EVENT_CAPACITY];
    private readonly int[] _bgColorEventCount = new int[4];
    private readonly byte[] _bgColorAtLineStart = new byte[4];

    // The vertical border flip-flop for the line being drawn, as the VIC-II settled it when the
    // raster entered the line (see Vic2LineDisplayState): while it is set the sequencer's output
    // is the background colour whatever it fetches (3.7.3).
    private bool _lineVerticalBorder = true;

    // VIC-II colour registers as the rasterizer holds them. They change only through the register
    // write journal below, at the cycle after the write lands, so a write in the middle of a line
    // takes effect at that pixel position instead of at the point where the line's registers happen
    // to be sampled. Resynchronised from the register storage at the end of every frame.
    private byte _borderColor;
    private byte _backgroundColor0;
    private byte _backgroundColor1;
    private byte _backgroundColor2;
    private byte _backgroundColor3;

    // Journal of VIC-II register writes, filled by the VIC-II as the CPU writes (see
    // Vic2.RegisterWriteObserver) and consumed cycle by cycle in CatchUpToVic2, which then
    // keeps only the entries it could not apply yet (a write on the very cycle it stopped at takes
    // effect on the next one). One instruction makes at most a few writes, so the capacity is only
    // reached when this generator is not the render provider being driven; then the journal is
    // abandoned and the colours resynchronised from the register storage.
    private struct RegisterWrite
    {
        public ulong FrameCycle;
        public ushort Register;
        public byte Value;
    }
    private const int REGISTER_WRITE_CAPACITY = 64;
    private readonly RegisterWrite[] _registerWrites = new RegisterWrite[REGISTER_WRITE_CAPACITY];
    private int _registerWriteCount;
    private int _registerWriteNext;
    private bool _registerWritesOverflowed;

    // The border is drawn as runs along the line: a run is closed where a border colour write
    // lands and the rest of the line is drawn when the line ends. The graphics between the runs
    // are resolved from their colour codes when the line ends too (see ResolveLineGraphics).
    private int _runLine = -1;          // screen line (Visible layout) whose runs are open
    private int _borderRunStartX;       // normalized x where the open border run starts

    // --- The border unit (main border flip-flop), per pixel.
    // The VIC-II shows border colour wherever its main border flip-flop is set. The flip-flop is
    // set when the X coordinate reaches the right compare value (344 with 40 columns, 335 with 38)
    // and reset when it reaches the left one (24 or 31) while the vertical border flip-flop is
    // clear; nothing else touches it, so it carries over from one line to the next and from one
    // frame to the next. The ordinary 40 and 38 column layouts are what those rules produce on a
    // line where nothing changes; a program that has 40 columns selected at the 335 compare and 38
    // at the 344 compare misses both and keeps the side borders open on that line and the left
    // border of the next. CSEL follows the register write journal, at the cycle boundary after
    // the write, as do XSCROLL, the mode bits and the memory pointers the sequencer uses.
    private int _xCoordinateAtLineStart;
    private bool _csel40 = true;
    private bool _mainBorder = true;
    private int _leftCompareX40, _leftCompareX38, _rightCompareX40, _rightCompareX38;   // normalized x
    private int _leftCompareCycle40, _leftCompareCycle38, _rightCompareCycle40, _rightCompareCycle38;
    // The span of the current line where the flip-flop is clear (normalized x, end exclusive):
    // graphics and sprites show only there. Per frame row copies feed the sprite passes.
    private int _lineClearStartX = int.MaxValue;
    private int _lineClearEndX;
    private int[] _lineClearStartXs = default!;
    private int[] _lineClearEndXs = default!;

    private int _screenLayoutInclNonVisibleTopBorderStartY;
    private int _screenLayoutInclNonVisibleBottomBorderEndY;
    private int _screenLayoutInclNonVisibleLeftBorderStartX;
    private int _screenLayoutInclNonVisibleRightBorderEndX;

    private readonly Action<uint, int, bool> _setPixel; // pixelColor, destIndex, foreground
    private readonly Action<Span<uint>, int, int, int> _setBackgroundPixels; // source, sourceIndex, destIndex, width
    private readonly Action<int, int> _clearBackgroundPixels; // destIndex, width
    private readonly Action<Span<uint>, int, int, int> _setForegroundPixels; // source, sourceIndex, destIndex, width
    private readonly Action<int, int> _clearForegroundPixels; // destIndex, width

    // When true, sprites are rendered per raster line during CatchUpToVic2 (enables
    // sprite multiplexing) instead of once at end-of-frame. See DrawSpritesForLine.
    private readonly bool _perLineSprites;

    // Sprite clipping/positioning (main screen area, without 38-col / 24-row consideration).
    private int _spriteScreenOffsetX;
    private int _spriteScreenOffsetY;

    // Per-line sprites (VIC-II article, section 3.8.1). The VIC-II decides per raster line which
    // sprites it displays and which row of each it fetches: its per-sprite data counter, kept in
    // Vic2, follows the Y-expand flip-flop, so a change of the expand bit mid-sprite, a sprite
    // pointer change mid-sprite or a Y rewrite after a sprite's last row (multiplexing) all show
    // on the line they reach. This generator takes the line's sprites from the start-of-line
    // snapshot the VIC-II captures (display mask and the three fetched bytes per sprite) and
    // records what each line shows; the pixels are drawn at end-of-frame, after the whole main
    // screen. That order is essential: the main-screen character foreground is written
    // scroll-adjusted (ypos += GetScrollY(), which is -3..+4), so with a negative fine scroll it
    // writes *upward* into rows below the current line, and sprites composited inline would be
    // clobbered by it (sprites vanishing at certain vertical scroll positions).
    private const int SPRITE_COUNT = 8;
    private const int SPRITE_ROWS = Vic2Sprite.DEFAULT_HEIGTH;         // 21
    private const int SPRITE_ROW_BYTES = Vic2Sprite.DEFAULT_WIDTH / 8; // 3

    // What each pixel-array line shows of each sprite, per output run: a sprite is output where
    // the chip's X compare matched, and can be output twice on a line when its X is moved past
    // the beam after its run (VIC-II article 3.8.1 rule 6; the runs come from the VIC-II).
    // Index = (line * SPRITE_COUNT + sprite) * SPRITE_RUNS + run.
    private const int SPRITE_RUNS = Vic2SpriteManager.MaxSpriteRunsPerLine;
    private byte[] _lineSpriteMask = default!;      // per line: bit n set when sprite n shows on it
    private byte[] _lineSpriteRunPresent = default!; // per record: 1 when the run is to be drawn
    private byte[] _lineSpriteData = default!;      // the three bytes of the row the run shifts out (index * 3)
    private byte[] _lineSpriteRunLength = default!; // the run's shown pixels
    private byte[] _lineSpriteRunStretch = default!; // and how many more repeat the last shown one
    private int[] _lineSpriteX = default!;          // pixel-array X
    private byte[] _lineSpriteFlags = default!;     // bit 0 X-expand, bit 1 multicolour, bit 2 priority over foreground
    // Colours as the line displays, so an intra-sprite per-raster colour change (striped sprites,
    // per-raster $D025/$D026 swaps) is preserved.
    private uint[] _lineSpriteColorFg = default!;
    private uint[] _lineSpriteColorMc0 = default!;
    private uint[] _lineSpriteColorMc1 = default!;
    // The span of the line where the border flip-flop was clear: the border covers sprites too.
    private int[] _lineSpriteClipStartX = default!;
    private int[] _lineSpriteClipEndX = default!;
    private const byte LineSpriteFlagDoubleWidth = 1;
    private const byte LineSpriteFlagMultiColor = 2;
    private const byte LineSpriteFlagPriority = 4;
    private const byte LineSpriteFlagDecoded = 8;
    // A decoded run's pixels (a mode or priority bit changed while the sprite shifted, so the
    // VIC-II followed it pixel by pixel; see Vic2SpriteManager.RunFlagDecoded), index * RUN_PIXELS.
    private const int RUN_PIXELS = Vic2SpriteManager.RunPixelCapacity;
    private byte[] _lineSpriteRunCodes = default!;
    // The sprite colour changes that land while a run is output (slot 0 the sprite's own colour,
    // 1 and 2 the shared multicolours), by pixel-array x: such a run is drawn pixel by pixel.
    private const int RUN_COLOR_EVENTS = 8;
    private byte[] _lineSpriteRunColorEventCount = default!;
    private int[] _lineSpriteRunColorEventX = default!;
    private byte[] _lineSpriteRunColorEventSlot = default!;
    private uint[] _lineSpriteRunColorEventColor = default!;
    private readonly byte[] _spriteRunLayers = new byte[RUN_PIXELS];
    // A line's sprites composited before they reach the layers: per pixel-array x the colour of
    // the topmost sprite's pixel (0 none) and whether that sprite is behind the foreground
    // graphics. The VIC-II takes the lowest-numbered sprite with a pixel at each position and
    // lets its priority bit alone decide against the graphics, so a sprite in front of the
    // graphics does not show through a higher-priority sprite that is behind them.
    private uint[] _spriteLineColor = default!;
    private byte[] _spriteLineLayer = default!;

    // The sprite colour registers as the sequencer sees them, through the register write journal
    // like the background colours: the eight sprite colours and the two shared multicolours by
    // slot, the writes that landed on the current line by pixel-array x, and the same for the
    // line before (its sprites are composited when the next line begins).
    private const int SPRITE_COLOR_SLOTS = 10;
    private const int SPRITE_COLOR_SLOT_MC0 = 8;
    private const int SPRITE_COLOR_SLOT_MC1 = 9;
    private const int SPRITE_COLOR_EVENT_CAPACITY = 8;
    private readonly byte[] _spriteColors = new byte[SPRITE_COLOR_SLOTS];
    private readonly byte[] _spriteColorAtLineStart = new byte[SPRITE_COLOR_SLOTS];
    private readonly int[] _spriteColorEventX = new int[SPRITE_COLOR_SLOTS * SPRITE_COLOR_EVENT_CAPACITY];
    private readonly byte[] _spriteColorEventColor = new byte[SPRITE_COLOR_SLOTS * SPRITE_COLOR_EVENT_CAPACITY];
    private readonly int[] _spriteColorEventCount = new int[SPRITE_COLOR_SLOTS];
    private readonly byte[] _prevSpriteColorAtLineStart = new byte[SPRITE_COLOR_SLOTS];
    private readonly int[] _prevSpriteColorEventX = new int[SPRITE_COLOR_SLOTS * SPRITE_COLOR_EVENT_CAPACITY];
    private readonly byte[] _prevSpriteColorEventColor = new byte[SPRITE_COLOR_SLOTS * SPRITE_COLOR_EVENT_CAPACITY];
    private readonly int[] _prevSpriteColorEventCount = new int[SPRITE_COLOR_SLOTS];
    private bool _spriteColorEventsPending;   // any event recorded on the current line


    // The raster line the screen line being recorded shows; its sprite runs are read from the
    // shared system-layer records when the line is finalized.
    private int _slRasterLine;

    public Vic2RasterizerSequencerPixelGenerator(
        C64 c64,
        Action<uint, int, bool> setPixel,
        Action<Span<uint>, int, int, int> setBackgroundPixels,
        Action<int, int> clearBackgroundPixels,
        Action<Span<uint>, int, int, int> setForegroundPixels,
        Action<int, int> clearForegroundPixels,
        bool perLineSprites = false)
    {
        _c64 = c64;
        _perLineSprites = perLineSprites;
        _displayEnabledThisFrame = c64.Vic2.DisplayEnabledThisFrame;

        // Use supplied pixel arrays or init new ones
        var width = c64.Vic2.Vic2Screen.VisibleWidth;
        var height = c64.Vic2.Vic2Screen.VisibleHeight;

        _setPixel = setPixel;
        _setBackgroundPixels = setBackgroundPixels;
        _clearBackgroundPixels = clearBackgroundPixels;
        _setForegroundPixels = setForegroundPixels;
        _clearForegroundPixels = clearForegroundPixels;

        Init();
    }

    [MemberNotNull(
        nameof(_c64ToRenderColorMap),
        nameof(_oneLineSameColorPixels))]
    private void Init()
    {
        _c64ToRenderColorMap = new uint[16];
        foreach (byte c64Color in Enum.GetValues<C64Colors>())
        {
            _c64ToRenderColorMap[c64Color] = (uint)GetSystemColor(c64Color, _c64.ColorMapName).ToArgb();
        }

        // Configure callback method for video generation after each instruction.
        // Per-line sprites read live VIC-II registers as each line is drawn and don't need the
        // per-line snapshot, so it can be turned off (saves the StoreRasterLineIORegisters copy).
        // The end-of-frame sprite path still depends on the snapshot for per-line sprite colors.
        _c64.RememberVic2RegistersPerRasterLine = !_perLineSprites;
        if (_perLineSprites)
            _c64.Vic2.SpriteManager.BackgroundCollisionsFromRenderer = true;   // from the lines' resolved pixels (DrawSpritesForLine)

        // Init class variables with C64 screen values that should'nt change

        // Entire screen area, including non-visible parts. Without consideration to 38 column mode or 24 row mode.
        var screenLayoutInclNonVisible = _c64.Vic2.ScreenLayouts.GetLayout(LayoutType.Visible, for24RowMode: false, for38ColMode: false); // Full area of raster lines, including non-visible. Borders don't start at 0,0

        _screenLayoutInclNonVisibleTopBorderStartY = screenLayoutInclNonVisible.TopBorder.Start.Y;
        _screenLayoutInclNonVisibleBottomBorderEndY = screenLayoutInclNonVisible.BottomBorder.End.Y;
        _screenLayoutInclNonVisibleLeftBorderStartX = screenLayoutInclNonVisible.LeftBorder.Start.X;
        _screenLayoutInclNonVisibleRightBorderEndX = screenLayoutInclNonVisible.RightBorder.End.X;

        _screenLayoutInclNonVisibleScreenStartX = screenLayoutInclNonVisible.Screen.Start.X;
        _screenLayoutInclNonVisibleScreenStartY = screenLayoutInclNonVisible.Screen.Start.Y;
        _screenLayoutInclNonVisibleScreenEndX = screenLayoutInclNonVisible.Screen.End.X;
        _screenLayoutInclNonVisibleScreenEndY = screenLayoutInclNonVisible.Screen.End.Y;

        // Entire screen area with only visible parts (borders, screen). Without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenAreaNormalized = _c64.Vic2.ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        // Not considering 24 row mode or 38 col mode or fine scroll
        _screenStartX = visibleMainScreenAreaNormalized.Screen.Start.X;
        _screenStartY = visibleMainScreenAreaNormalized.Screen.Start.Y;

        // VIC-II sprite coordinate offsets.
        _spriteScreenOffsetX = _c64.Vic2.SpriteManager.ScreenOffsetX;
        _spriteScreenOffsetY = _c64.Vic2.SpriteManager.ScreenOffsetY;

        _topBorderStartX = visibleMainScreenAreaNormalized.TopBorder.Start.X;
        _topBorderStartY = visibleMainScreenAreaNormalized.TopBorder.Start.Y;
        _topBorderEndX = visibleMainScreenAreaNormalized.TopBorder.End.X;
        _topBorderEndY = visibleMainScreenAreaNormalized.TopBorder.End.Y;

        _bottomBorderStartX = visibleMainScreenAreaNormalized.BottomBorder.Start.X;
        _bottomBorderStartY = visibleMainScreenAreaNormalized.BottomBorder.Start.Y;
        _bottomBorderEndX = visibleMainScreenAreaNormalized.BottomBorder.End.X;
        _bottomBorderEndY = visibleMainScreenAreaNormalized.BottomBorder.End.Y;

        _leftBorderStartX = visibleMainScreenAreaNormalized.LeftBorder.Start.X;
        _leftBorderStartY = visibleMainScreenAreaNormalized.LeftBorder.Start.Y;
        _leftBorderEndX = visibleMainScreenAreaNormalized.LeftBorder.End.X;
        _leftBorderEndY = visibleMainScreenAreaNormalized.LeftBorder.End.Y;

        _rightBorderStartX = visibleMainScreenAreaNormalized.RightBorder.Start.X;
        _rightBorderStartY = visibleMainScreenAreaNormalized.RightBorder.Start.Y;
        _rightBorderEndX = visibleMainScreenAreaNormalized.RightBorder.End.X;
        _rightBorderEndY = visibleMainScreenAreaNormalized.RightBorder.End.Y;

        _vic2ScreenTextCols = _c64.Vic2.Vic2Screen.TextCols;
        _vic2ScreenCharacterHeight = _c64.Vic2.Vic2Screen.CharacterHeight;
        _width = _c64.Vic2.Vic2Screen.VisibleWidth;
        _height = _c64.Vic2.Vic2Screen.VisibleHeight;
        _lineBgCodes = new byte[_width];
        _lineFgCodes = new byte[_width];
        _lineBgPixels = new uint[_width];
        _lineFgPixels = new uint[_width];
        _drawableAreaWidth = _c64.Vic2.Vic2Screen.DrawableAreaWidth;
        _cyclesPerLine = _c64.Vic2.Vic2Model.CyclesPerLine;
        _colorChangePixelDelay = _c64.Vic2.Vic2Model.ColorChangePixelDelay;
        _xCoordinateAtLineStart = _c64.Vic2.Vic2Model.XCoordinateAtLineStart;

        // The border unit's compare points. The display window starts DisplayWindowStartX pixels
        // into the raster line (X 24, 4 pixels into the chip's cycle 16 = index 15); 38 columns move
        // the left edge 7 pixels right (X 31) and the right one 9 pixels left (X 335). The chip
        // evaluates each compare one cycle after the cycle its X coordinate falls in, with the
        // registers as written up to the cycle before that (VICE's cycle tables: the 38 column
        // right compare in cycle 56, the 40 column one in 57, counting from 1), which is why a 38
        // column write in cycle 56 opens the side border: the first compare does not see it yet and
        // the second does. The pixel positions the flip-flop changes at are the X coordinates.
        var displayWindowStartLineX = _c64.Vic2.Vic2Model.DisplayWindowStartX;
        _leftCompareX40 = _screenStartX;
        _leftCompareX38 = _screenStartX + Vic2Screen.COL_38_LEFT_BORDER_END_X_DELTA;
        _rightCompareX40 = _rightBorderStartX;
        _rightCompareX38 = _rightBorderStartX + Vic2Screen.COL_38_RIGHT_BORDER_START_X_DELTA;
        _leftCompareCycle40 = displayWindowStartLineX / 8 + 1;
        _leftCompareCycle38 = (displayWindowStartLineX + Vic2Screen.COL_38_LEFT_BORDER_END_X_DELTA) / 8 + 1;
        _rightCompareCycle40 = (displayWindowStartLineX + _drawableAreaWidth) / 8 + 1;
        _rightCompareCycle38 = (displayWindowStartLineX + _drawableAreaWidth + Vic2Screen.COL_38_RIGHT_BORDER_START_X_DELTA) / 8 + 1;
        _csel40 = !_c64.Vic2.Is38ColumnDisplayEnabled;

        // Until the raster has run a frame, every line reads as a plain 40 column display: the
        // sprite passes can then draw before any line has been processed (unit tests do).
        _lineClearStartXs = new int[_height];
        _lineClearEndXs = new int[_height];
        _lineRepeatMark = new int[_width + 8];
        _rasterToScreenLine = new int[_c64.Vic2.Vic2Model.TotalHeight];
        for (var line = 0; line < _rasterToScreenLine.Length; line++)
            _rasterToScreenLine[line] = _c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(line);
        _lineSpriteMask = new byte[_height];
        _lineSpriteRunPresent = new byte[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteData = new byte[_height * SPRITE_COUNT * SPRITE_RUNS * SPRITE_ROW_BYTES];
        _lineSpriteRunLength = new byte[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteRunStretch = new byte[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteX = new int[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteFlags = new byte[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteColorFg = new uint[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteColorMc0 = new uint[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteColorMc1 = new uint[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteRunCodes = new byte[_height * SPRITE_COUNT * SPRITE_RUNS * RUN_PIXELS];
        _spriteLineColor = new uint[_width];
        _spriteLineLayer = new byte[_width];
        _lineSpriteRunColorEventCount = new byte[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteRunColorEventX = new int[_height * SPRITE_COUNT * SPRITE_RUNS * RUN_COLOR_EVENTS];
        _lineSpriteRunColorEventSlot = new byte[_height * SPRITE_COUNT * SPRITE_RUNS * RUN_COLOR_EVENTS];
        _lineSpriteRunColorEventColor = new uint[_height * SPRITE_COUNT * SPRITE_RUNS * RUN_COLOR_EVENTS];
        _lineSpriteClipStartX = new int[_height * SPRITE_COUNT * SPRITE_RUNS];
        _lineSpriteClipEndX = new int[_height * SPRITE_COUNT * SPRITE_RUNS];
        for (var row = 0; row < _height; row++)
        {
            var displayArea = row >= _screenStartY && row < _screenStartY + _c64.Vic2.Vic2Screen.DrawableAreaHeight;
            _lineClearStartXs[row] = displayArea ? _leftCompareX40 : _width;
            _lineClearEndXs[row] = displayArea ? _rightCompareX40 : _width;
        }

        _lastScreenLineDataUpdate = -1;

        // Init bitmaps to render to
        InitBitmaps(_c64);
        InitBitPatternToPixelMaps(_c64);

        _c64.Vic2.RegisterWriteObserver = OnVic2RegisterWrite;
        ResyncColorRegisters();
    }

    private void OnVic2RegisterWrite(ulong frameCycle, ushort register, byte value)
    {
        if (_registerWriteCount == REGISTER_WRITE_CAPACITY)
        {
            _registerWritesOverflowed = true;
            return;
        }
        _registerWrites[_registerWriteCount++] = new RegisterWrite { FrameCycle = frameCycle, Register = register, Value = value };
    }

    // Take the colour and display registers as they are now and forget any pending writes.
    private void ResyncColorRegisters()
    {
        _borderColor = (byte)(_c64.ReadIOStorage(Vic2Addr.BORDER_COLOR) & 0x0F);
        _backgroundColor0 = (byte)(_c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0) & 0x0F);
        _backgroundColor1 = (byte)(_c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1) & 0x0F);
        _backgroundColor2 = (byte)(_c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2) & 0x0F);
        _backgroundColor3 = (byte)(_c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3) & 0x0F);
        _d011 = _c64.ReadIOStorage(Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER);
        _d016 = _c64.ReadIOStorage(Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER);
        _d018 = _c64.ReadIOStorage(Vic2Addr.MEMORY_SETUP);
        _csel40 = (_d016 & 0x08) != 0;
        for (var n = 0; n < SPRITE_COUNT; n++)
            _spriteColors[n] = (byte)(_c64.ReadIOStorage((ushort)(Vic2Addr.SPRITE_0_COLOR + n)) & 0x0F);
        _spriteColors[SPRITE_COLOR_SLOT_MC0] = (byte)(_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0) & 0x0F);
        _spriteColors[SPRITE_COLOR_SLOT_MC1] = (byte)(_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1) & 0x0F);
        _registerWriteCount = 0;
        _registerWriteNext = 0;
        _registerWritesOverflowed = false;
    }

    // Apply a journaled register write at the pixel position (normalized x on the open line)
    // from which it is visible. The journal carries the value as written; the colour registers
    // keep only their low four bits. A colour register change is visible from the pixel after
    // the first of the cycle following the write (ColorChangePixelDelay); the display registers
    // are seen by the sequencer from that cycle on, and it applies their own pipeline delays.
    private void ApplyRegisterWrite(in RegisterWrite write, int cycleStartX)
    {
        var color = (byte)(write.Value & 0x0F);
        var changeX = cycleStartX + _colorChangePixelDelay;
        switch (write.Register)
        {
            case Vic2Addr.BORDER_COLOR:
                if (_borderColor != color)
                {
                    if (_borderRunStartX >= 0)
                    {
                        CloseBorderRun(changeX);
                        _borderRunStartX = Math.Max(_borderRunStartX, changeX);
                    }
                    _borderColor = color;
                }
                break;
            case Vic2Addr.BACKGROUND_COLOR_0:
                RecordBackgroundColor(0, ref _backgroundColor0, color, changeX);
                break;
            case Vic2Addr.BACKGROUND_COLOR_1:
                RecordBackgroundColor(1, ref _backgroundColor1, color, changeX);
                break;
            case Vic2Addr.BACKGROUND_COLOR_2:
                RecordBackgroundColor(2, ref _backgroundColor2, color, changeX);
                break;
            case Vic2Addr.BACKGROUND_COLOR_3:
                RecordBackgroundColor(3, ref _backgroundColor3, color, changeX);
                break;
            case Vic2Addr.SCROLL_Y_AND_SCREEN_CONTROL_REGISTER:
                _d011 = write.Value;
                break;
            case Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER:
                // CSEL: in effect from the cycle boundary after the write, with no pipeline: it
                // feeds the compares, not the pixel output.
                _d016 = write.Value;
                _csel40 = (write.Value & 0x08) != 0;
                break;
            case Vic2Addr.MEMORY_SETUP:
                _d018 = write.Value;
                break;
            case Vic2Addr.SPRITE_MULTI_COLOR_0:
                RecordSpriteColor(SPRITE_COLOR_SLOT_MC0, color, changeX);
                break;
            case Vic2Addr.SPRITE_MULTI_COLOR_1:
                RecordSpriteColor(SPRITE_COLOR_SLOT_MC1, color, changeX);
                break;
            case >= Vic2Addr.SPRITE_0_COLOR and <= Vic2Addr.SPRITE_7_COLOR:
                RecordSpriteColor(write.Register - Vic2Addr.SPRITE_0_COLOR, color, changeX);
                break;
            default:
                break;
        }
    }

    // A sprite colour register changes on the open line at x: remember where, so a sprite run
    // output across it can be drawn with both colours.
    private void RecordSpriteColor(int slot, byte color, int x)
    {
        if (_spriteColors[slot] == color)
            return;
        _spriteColors[slot] = color;
        var n = _spriteColorEventCount[slot];
        if (n == SPRITE_COLOR_EVENT_CAPACITY)
            return;   // more changes on one line than the chip can show apart: the last value wins
        _spriteColorEventX[slot * SPRITE_COLOR_EVENT_CAPACITY + n] = x;
        _spriteColorEventColor[slot * SPRITE_COLOR_EVENT_CAPACITY + n] = color;
        _spriteColorEventCount[slot] = n + 1;
        _spriteColorEventsPending = true;
    }

    // A background colour register changes on the open line at x: remember where, so the line's
    // pixels that take their colour from it can be resolved when the line ends.
    private void RecordBackgroundColor(int register, ref byte current, byte color, int x)
    {
        if (current == color)
            return;
        current = color;
        var n = _bgColorEventCount[register];
        if (n == BG_COLOR_EVENT_CAPACITY)
            return;   // more changes on one line than the chip can show apart: the last value wins
        _bgColorEventX[register * BG_COLOR_EVENT_CAPACITY + n] = x;
        _bgColorEventColor[register * BG_COLOR_EVENT_CAPACITY + n] = color;
        _bgColorEventCount[register] = n + 1;
    }

    private bool IsRunLineVisible => _runLine >= _screenLayoutInclNonVisibleTopBorderStartY && _runLine <= _screenLayoutInclNonVisibleBottomBorderEndY;

    // Paint the open border run up to x (it stays open; the caller moves or ends it).
    private void CloseBorderRun(int x)
    {
        if (IsRunLineVisible)
            DrawBorderRun(_runLine - _screenLayoutInclNonVisibleTopBorderStartY, _borderRunStartX, x);
    }

    // The right compare: border colour from x on this line, and until the next left compare.
    private void SetMainBorder(int x)
    {
        if (_mainBorder)
            return;
        _mainBorder = true;
        if (x < _lineClearEndX)
            _lineClearEndX = x;
        _borderRunStartX = x;
    }

    // The left compare: graphics from x, but only while the vertical border flip-flop is clear.
    private void ResetMainBorder(int x, int rasterLine)
    {
        if (!_mainBorder || _c64.Vic2.GetLineDisplayState(rasterLine).VerticalBorder)
            return;
        _mainBorder = false;
        if (x < _lineClearStartX)
            _lineClearStartX = x;
        if (_borderRunStartX >= 0)
            CloseBorderRun(x);
        _borderRunStartX = -1;
    }

    // Draw the rest of the open line's run and close the line, keeping its clear span for the
    // sprite passes.
    private void FinishLineRuns()
    {
        if (IsRunLineVisible)
        {
            var normalizedLine = _runLine - _screenLayoutInclNonVisibleTopBorderStartY;
            if (_borderRunStartX >= 0)
                DrawBorderRun(normalizedLine, _borderRunStartX, _width);
            ResolveLineGraphics(normalizedLine);
            _lineClearStartXs[normalizedLine] = _lineClearStartX;
            _lineClearEndXs[normalizedLine] = _lineClearEndX;
        }
        _runLine = -1;
    }

    /// <summary>
    /// Write screen data for all clock cycles since last time this method was called.
    /// Instructions can take different amount of cycles to execute, so this method is called after each instruction to update the screen data and will catch up on what's to do since last time it was called.
    /// </summary>
    public void CatchUpToVic2() => CatchUpTo(_c64.Vic2.CyclesConsumedCurrentVblank);

    // Draws the cycles before endCycle (a cycle count into the frame).
    private void CatchUpTo(ulong endCycle)
    {
        // Already there, or one cycle beyond it (the cycle of a collision register read, drawn
        // before it ended). Further behind means a new frame has begun: start over from there.
        if (endCycle == _lastCyclesConsumedCurrentVblank || endCycle + 1 == _lastCyclesConsumedCurrentVblank)
            return;
        if (endCycle < _lastCyclesConsumedCurrentVblank)
        {
            _lastCyclesConsumedCurrentVblank = endCycle;
            return;
        }
        if (_registerWritesOverflowed)
            ResyncColorRegisters();

        // Loop cycles since last time we processed (each instruction). The line and the cycle
        // within it are derived once and then counted along: a division per cycle would be a
        // large share of the frame on its own.
        var cycleCurrentVblank = _lastCyclesConsumedCurrentVblank;
        var rasterLine = (int)(cycleCurrentVblank / _cyclesPerLine);
        var cycleOnScreenLine = cycleCurrentVblank - (ulong)rasterLine * _cyclesPerLine;
        for (; cycleCurrentVblank < endCycle; cycleCurrentVblank++, cycleOnScreenLine++)
        {
            if (cycleOnScreenLine == _cyclesPerLine)
            {
                cycleOnScreenLine = 0;
                rasterLine++;
            }
            var screenLine = rasterLine < _rasterToScreenLine.Length ? _rasterToScreenLine[rasterLine] : rasterLine;
            var posX = (int)(cycleOnScreenLine * 8); // 1 cycle = 8 pixels;

            // Line change: draw the rest of the previous line's border/background runs with the
            // colours in effect at its end, and open this line's first run in whichever state the
            // border flip-flop carried over.
            if (screenLine != _runLine)
            {
                FinishLineRuns();
                _runLine = screenLine;
                _lineSerial++;
                _lineClearStartX = _mainBorder ? int.MaxValue : 0;
                _lineClearEndX = _width;
                _borderRunStartX = _mainBorder ? 0 : -1;
                _bgColorEventCount[0] = _bgColorEventCount[1] = _bgColorEventCount[2] = _bgColorEventCount[3] = 0;
                _bgColorAtLineStart[0] = _backgroundColor0;
                _bgColorAtLineStart[1] = _backgroundColor1;
                _bgColorAtLineStart[2] = _backgroundColor2;
                _bgColorAtLineStart[3] = _backgroundColor3;
                // The ended line's sprite colour changes go to its sprite pass (below, when this
                // line's first cycle is processed); this line starts with none.
                _spriteColorAtLineStart.CopyTo(_prevSpriteColorAtLineStart, 0);
                _spriteColors.CopyTo(_spriteColorAtLineStart, 0);
                if (_spriteColorEventsPending)
                {
                    _spriteColorEventCount.CopyTo(_prevSpriteColorEventCount, 0);
                    _spriteColorEventX.CopyTo(_prevSpriteColorEventX, 0);
                    _spriteColorEventColor.CopyTo(_prevSpriteColorEventColor, 0);
                    Array.Clear(_spriteColorEventCount);
                    _spriteColorEventsPending = false;
                }
                else
                {
                    Array.Clear(_prevSpriteColorEventCount);
                }
            }

            // Register writes take effect from the cycle after the one they land in (colour
            // registers plus the pixels the chip's own pipeline takes to show the new value).
            while (_registerWriteNext < _registerWriteCount && _registerWrites[_registerWriteNext].FrameCycle < cycleCurrentVblank)
                ApplyRegisterWrite(in _registerWrites[_registerWriteNext++], posX - _screenLayoutInclNonVisibleLeftBorderStartX);

            // The border unit's compares for this cycle, with CSEL as it is now.
            if (cycleOnScreenLine == (ulong)_leftCompareCycle40 && _csel40)
                ResetMainBorder(_leftCompareX40, rasterLine);
            else if (cycleOnScreenLine == (ulong)_leftCompareCycle38 && !_csel40)
                ResetMainBorder(_leftCompareX38, rasterLine);
            else if (cycleOnScreenLine == (ulong)_rightCompareCycle38 && !_csel40)
                SetMainBorder(_rightCompareX38);
            else if (cycleOnScreenLine == (ulong)_rightCompareCycle40 && _csel40)
                SetMainBorder(_rightCompareX40);

            // Skip if not within visible C64 border/text/bitmap area
            if (screenLine < _screenLayoutInclNonVisibleTopBorderStartY || screenLine > _screenLayoutInclNonVisibleBottomBorderEndY)
                continue;

            var isNewLine = screenLine != _lastScreenLineDataUpdate;

            // On a new line, refresh from the current VIC-II state.
            if (isNewLine)
            {
                // The just-finished previous line's text/bitmap is laid down (its border and
                // background runs were completed when the line changed): composite that line's
                // sprites on top of it (per-line / multiplexing path).
                if (_lastScreenLineDataUpdate >= 0 && _perLineSprites)
                    DrawSpritesForLine(_lastScreenLineDataUpdate);

                // A new line: clear its foreground row before anything is drawn on it, so nothing
                // from the previous frame remains (fine scrolling leaves gaps). Per line rather than
                // once per frame at the first visible line, because on NTSC the visible frame's last
                // rows are raster lines 0-12, which are drawn before that first visible line and
                // would be wiped by a whole-frame clear there.
                _clearForegroundPixels((screenLine - _screenLayoutInclNonVisibleTopBorderStartY) * _width, _width);

                if (screenLine - _screenLayoutInclNonVisibleTopBorderStartY == 0)
                {
                    // New frame: VCBASE is reset once per frame outside the bad line range (rule 1,
                    // the chip presumably does it in line 0); the frame's first visible line is
                    // outside that range on both models, and the lines above it are not processed.
                    _vcBase = 0;
                }

                _lineVerticalBorder = _c64.Vic2.GetLineDisplayState(rasterLine).VerticalBorder;
                _baLowSince = -1;

                // Colour registers are not sampled here: they follow the register write journal.

                // The 38/24 column and row selections are not sampled here: CSEL goes through the
                // register write journal into the border unit, RSEL into the per-line vertical state.

                // The line's sprite runs are the VIC-II's, derived when the line ends (in
                // Vic2.AdvanceRaster, the single source of truth shared with per-line collision).
                // DrawSpritesForLine reads them when this line is finalized (on entry to the next).
                if (_perLineSprites)
                {
                    // This line's record starts empty (a per-line clear rather than one per frame,
                    // since on NTSC the visible frame's last rows are raster lines 0-12, recorded
                    // before the frame's first visible line).
                    var lineIndex = screenLine - _screenLayoutInclNonVisibleTopBorderStartY;
                    if (lineIndex >= 0 && lineIndex < _height)
                    {
                        _lineSpriteMask[lineIndex] = 0;
                        Array.Clear(_lineSpriteRunPresent, lineIndex * SPRITE_COUNT * SPRITE_RUNS, SPRITE_COUNT * SPRITE_RUNS);
                    }

                    _slRasterLine = rasterLine;
                }

                _lastScreenLineDataUpdate = screenLine;
            }

            // The graphics sequencer's eight pixels for this cycle. They land twelve pixels before the
            // cycle's position: the display window's columns start 4 pixels into a cycle, and the
            // pixels a cycle shows come from the byte fetched two cycles earlier, by which time the
            // compares that can clip them have been evaluated. The sequencer runs on every cycle
            // (its pipeline has to move on) and draws wherever the border flip-flop is clear.
            var blockX = posX - _screenLayoutInclNonVisibleLeftBorderStartX - 12;
            DrawGraphicsCycle((int)cycleOnScreenLine, rasterLine, blockX, render: blockX + 8 > 0 && blockX < _width && _lineClearStartX < _width);

        } // End for each cycle

        // Keep the writes that are not due yet, so the journal does not fill up and lose writes
        // over a frame's worth of colour changes.
        if (_registerWriteNext > 0)
        {
            var pending = _registerWriteCount - _registerWriteNext;
            for (var i = 0; i < pending; i++)
                _registerWrites[i] = _registerWrites[_registerWriteNext + i];
            _registerWriteCount = pending;
            _registerWriteNext = 0;
        }

        _lastCyclesConsumedCurrentVblank = endCycle;
    }

    // The raster line whose sprite-to-background collisions reads of the register have latched
    // or cleared up to a pixel; the line's end latches the rest.
    private int _backgroundCollisionsLatchedLine = -1;
    private int _backgroundCollisionsLatchedToPixel;

    public void LatchSpriteBackgroundCollisions(int rasterLine, int upToPixel, int clearedToPixel)
    {
        if (!_perLineSprites)
            return;
        // First the cycles before the read's, which can end the previous line and latch what was
        // left of its collisions.
        CatchUpToVic2();
        var spriteManager = _c64.Vic2.SpriteManager;
        var fromPixel = _backgroundCollisionsLatchedLine == rasterLine ? _backgroundCollisionsLatchedToPixel : 0;
        var hasPixels = upToPixel > fromPixel && spriteManager.LineSpriteRunMask(rasterLine) != 0;
        if (hasPixels)
        {
            // The graphics under the last of those pixels are the ones the cycle of the read
            // shows: a read changes nothing, so that cycle can be drawn before it has ended.
            var cycles = _c64.Vic2.CyclesConsumedCurrentVblank;
            if (cycles % _cyclesPerLine != 0 && cycles % _cyclesPerLine + 1 < _cyclesPerLine)
                CatchUpTo(cycles + 1);
        }
        _backgroundCollisionsLatchedLine = rasterLine;
        _backgroundCollisionsLatchedToPixel = Math.Max(fromPixel, clearedToPixel);
        var screenLine = rasterLine < _rasterToScreenLine.Length ? _rasterToScreenLine[rasterLine] : rasterLine;
        if (!hasPixels || screenLine != _lastScreenLineDataUpdate)
            return;   // nothing output yet, or not a line that is drawn
        byte backgroundCollisions = 0;
        for (int spriteIndex = 0; spriteIndex < SPRITE_COUNT; spriteIndex++)
        {
            var runs = Math.Min(spriteManager.LineSpriteRunCount(rasterLine, spriteIndex), SPRITE_RUNS);
            for (int run = 0; run < runs && (backgroundCollisions & (1 << spriteIndex)) == 0; run++)
            {
                var startPixel = spriteManager.LineSpriteRunStart(rasterLine, spriteIndex, run);
                var vicX = _xCoordinateAtLineStart + startPixel;
                if (vicX >= (int)_cyclesPerLine * 8)
                    vicX -= (int)_cyclesPerLine * 8;
                if (SpriteRunHitsForeground(rasterLine, spriteIndex, run, startPixel, SpriteScreenX(vicX),
                    spriteManager.LineSpriteRunFlags(rasterLine, spriteIndex, run), spriteManager.LineSpriteRunLength(rasterLine, spriteIndex, run),
                    spriteManager.LineSpriteRunStretch(rasterLine, spriteIndex, run), _lineClearStartX, _lineClearEndX, fromPixel, upToPixel))
                {
                    backgroundCollisions |= (byte)(1 << spriteIndex);
                }
            }
        }
        if (backgroundCollisions != 0)
            spriteManager.AddSpriteToBackgroundCollisions(backgroundCollisions);
    }

    /// <summary>
    /// Whether a sprite run of the line being drawn has an opaque pixel, among those of the line's
    /// pixels fromPixel..toPixel-1, where the graphics sequencer output a foreground pixel (a set
    /// bit, or a 10/11 pair in multicolour; nothing while the vertical border flip-flop is set,
    /// when the sequencer's output is off), resolved where the border flip-flop was clear.
    /// </summary>
    private bool SpriteRunHitsForeground(int rasterLine, int spriteIndex, int run, int startPixel, int screenX, byte runFlags, int length, int stretch,
        int clipStartX, int clipEndX, int fromPixel, int toPixel)
    {
        var decoded = (runFlags & Vic2SpriteManager.RunFlagDecoded) != 0;
        var pixelCount = decoded ? length : Math.Min(length + stretch, RUN_PIXELS);
        var first = Math.Max(0, fromPixel - startPixel);
        var end = toPixel == int.MaxValue ? pixelCount : Math.Min(pixelCount, toPixel - startPixel);
        var from = Math.Max(Math.Max(0, clipStartX), screenX + first);
        var to = Math.Min(Math.Min(_width, clipEndX), screenX + end);
        // Most lines have no foreground under the sprite (a sprite-only demo, the borders, blank
        // cells): one vectorised search before any decoding.
        var firstForeground = to > from ? _lineFgCodes.AsSpan(from, to - from).IndexOfAnyExcept(CODE_NONE) : -1;
        if (firstForeground < 0)
            return false;
        return SpriteRunHitsForegroundFrom(rasterLine, spriteIndex, run, screenX, runFlags, length, stretch, from + firstForeground, to);
    }

    private bool SpriteRunHitsForegroundFrom(int rasterLine, int spriteIndex, int run, int screenX, byte runFlags, int length, int stretch, int from, int to)
    {
        var spriteManager = _c64.Vic2.SpriteManager;
        Span<byte> codes = stackalloc byte[RUN_PIXELS];
        if ((runFlags & Vic2SpriteManager.RunFlagDecoded) != 0)
            spriteManager.LineSpriteRunPixels(rasterLine, spriteIndex, run).Slice(0, length).CopyTo(codes);
        else
            Vic2SpriteManager.ExpandRunPixels(spriteManager.LineSpriteRunData(rasterLine, spriteIndex, run), runFlags, length, stretch, codes);
        for (var x = from; x < to; x++)
        {
            if (_lineFgCodes[x] != CODE_NONE && (codes[x - screenX] & Vic2SpriteManager.RunPixelValueMask) != 0)
                return true;
        }
        return false;
    }

    public void OnEndFrame()
    {
        // Complete the last line drawn, then take the colour registers as they stand for the next
        // frame (also covers values set without going through the memory map, e.g. a snapshot).
        FinishLineRuns();
        ResyncColorRegisters();

        // Per-line mode draws sprites during CatchUpToVic2; skip the end-of-frame pass. A
        // sprite the VIC-II never output this frame (its X never met the beam, or met it only
        // inside its own fetch) is not shown, as on the chip.
        if (!_perLineSprites)
        {
            DrawSpritesToBitmapBackedByPixelArray();
            return;
        }

        // Now that the whole main screen is rendered, composite the lines' sprites on top.
        DrawSpriteLines();
    }

    /// <summary>
    /// Records what a raster line showed of each sprite. Called when the line is finalized (on
    /// entry to the next line): the sprites the VIC-II displayed on it and the bytes it fetched for
    /// them come from the start-of-line snapshot, the colours and the border clip as the line ends.
    /// The pixels are drawn at end-of-frame, after the whole main screen, so fine-scroll
    /// main-screen writes can't clobber them.
    /// </summary>
    private void DrawSpritesForLine(int screenLine)
    {
        var spriteManager = _c64.Vic2.SpriteManager;
        var runMask = spriteManager.LineSpriteRunMask(_slRasterLine);
        if (runMask == 0)
            return;
        var pixelArrayY = screenLine - _screenLayoutInclNonVisibleTopBorderStartY;
        if (pixelArrayY < 0 || pixelArrayY >= _height)
            return;

        var clipStartX = _lineClearStartXs[pixelArrayY];
        var clipEndX = _lineClearEndXs[pixelArrayY];
        var pixelsPerLine = (int)_cyclesPerLine * 8;
        byte backgroundCollisions = 0;
        // The line's pixels whose collisions a read of the register has latched or cleared already.
        var collisionsFromPixel = _backgroundCollisionsLatchedLine == _slRasterLine ? _backgroundCollisionsLatchedToPixel : 0;
        _backgroundCollisionsLatchedLine = -1;
        for (int spriteIndex = 0; spriteIndex < SPRITE_COUNT; spriteIndex++)
        {
            if ((runMask & (1 << spriteIndex)) == 0)
                continue;
            // The runs the chip derived for this sprite on this line, from its X compare per
            // pixel: none when the register never matched the beam (X beyond the line's reach, or
            // moved past the beam before matching).
            var runs = spriteManager.LineSpriteRunCount(_slRasterLine, spriteIndex);
            for (int run = 0; run < runs && run < SPRITE_RUNS; run++)
            {
                _lineSpriteMask[pixelArrayY] |= (byte)(1 << spriteIndex);
                var index = (pixelArrayY * SPRITE_COUNT + spriteIndex) * SPRITE_RUNS + run;
                var startPixel = spriteManager.LineSpriteRunStart(_slRasterLine, spriteIndex, run);
                var vicX = _xCoordinateAtLineStart + startPixel;
                if (vicX >= pixelsPerLine)
                    vicX -= pixelsPerLine;
                var runFlags = spriteManager.LineSpriteRunFlags(_slRasterLine, spriteIndex, run);
                var length = spriteManager.LineSpriteRunLength(_slRasterLine, spriteIndex, run);
                var stretch = spriteManager.LineSpriteRunStretch(_slRasterLine, spriteIndex, run);
                var decoded = (runFlags & Vic2SpriteManager.RunFlagDecoded) != 0;
                var flags = (byte)(((runFlags & Vic2SpriteManager.RunFlagXExpand) != 0 ? LineSpriteFlagDoubleWidth : 0)
                    | ((runFlags & Vic2SpriteManager.RunFlagMultiColor) != 0 ? LineSpriteFlagMultiColor : 0)
                    | ((runFlags & Vic2SpriteManager.RunFlagBehindForeground) == 0 ? LineSpriteFlagPriority : 0)
                    | (decoded ? LineSpriteFlagDecoded : 0));
                var row = spriteManager.LineSpriteRunData(_slRasterLine, spriteIndex, run);
                _lineSpriteRunPresent[index] = 1;
                _lineSpriteData[index * SPRITE_ROW_BYTES] = (byte)(row >> 16);
                _lineSpriteData[index * SPRITE_ROW_BYTES + 1] = (byte)(row >> 8);
                _lineSpriteData[index * SPRITE_ROW_BYTES + 2] = (byte)row;
                _lineSpriteRunLength[index] = (byte)length;
                _lineSpriteRunStretch[index] = (byte)stretch;
                if (decoded)
                    spriteManager.LineSpriteRunPixels(_slRasterLine, spriteIndex, run).CopyTo(_lineSpriteRunCodes.AsSpan(index * RUN_PIXELS, length));
                var screenX = SpriteScreenX(vicX);
                _lineSpriteX[index] = screenX;
                _lineSpriteFlags[index] = flags;
                // The colours as the run starts, and the changes that land while it is output.
                var count = 0;
                _lineSpriteColorFg[index] = RunStartColor(spriteIndex, 0, screenX, screenX + length + stretch, index, ref count);
                _lineSpriteColorMc0[index] = RunStartColor(SPRITE_COLOR_SLOT_MC0, 1, screenX, screenX + length + stretch, index, ref count);
                _lineSpriteColorMc1[index] = RunStartColor(SPRITE_COLOR_SLOT_MC1, 2, screenX, screenX + length + stretch, index, ref count);
                _lineSpriteRunColorEventCount[index] = (byte)count;
                _lineSpriteClipStartX[index] = clipStartX;
                _lineSpriteClipEndX[index] = clipEndX;

                // Sprite-to-background collision, from the line's codes, which are still those of
                // the line that has just ended.
                if ((backgroundCollisions & (1 << spriteIndex)) == 0
                    && SpriteRunHitsForeground(_slRasterLine, spriteIndex, run, startPixel, screenX, runFlags, length, stretch, clipStartX, clipEndX, collisionsFromPixel, int.MaxValue))
                {
                    backgroundCollisions |= (byte)(1 << spriteIndex);
                }
            }
        }
        if (backgroundCollisions != 0)
            spriteManager.AddSpriteToBackgroundCollisions(backgroundCollisions);
    }

    // The colour of a slot as a run beginning at startX shows it, from the ended line's start
    // value and the changes before startX; the changes that land within the run (before endX) are
    // recorded on the run record as its runSlot, in x order among the slots' events.
    private uint RunStartColor(int slot, byte runSlot, int startX, int endX, int index, ref int count)
    {
        var color = _prevSpriteColorAtLineStart[slot];
        var events = _prevSpriteColorEventCount[slot];
        for (var e = 0; e < events; e++)
        {
            var x = _prevSpriteColorEventX[slot * SPRITE_COLOR_EVENT_CAPACITY + e];
            var eventColor = _prevSpriteColorEventColor[slot * SPRITE_COLOR_EVENT_CAPACITY + e];
            if (x <= startX)
            {
                color = eventColor;
                continue;
            }
            if (x >= endX || count == RUN_COLOR_EVENTS)
                break;
            // Keep the run's events sorted by x (a few at most).
            var at = index * RUN_COLOR_EVENTS;
            var i = count;
            while (i > 0 && _lineSpriteRunColorEventX[at + i - 1] > x)
            {
                _lineSpriteRunColorEventX[at + i] = _lineSpriteRunColorEventX[at + i - 1];
                _lineSpriteRunColorEventSlot[at + i] = _lineSpriteRunColorEventSlot[at + i - 1];
                _lineSpriteRunColorEventColor[at + i] = _lineSpriteRunColorEventColor[at + i - 1];
                i--;
            }
            _lineSpriteRunColorEventX[at + i] = x;
            _lineSpriteRunColorEventSlot[at + i] = runSlot;
            _lineSpriteRunColorEventColor[at + i] = _c64ToRenderColorMap[eventColor];
            count++;
        }
        return _c64ToRenderColorMap[color];
    }

    /// <summary>
    /// Draws the recorded sprite lines. Each line's sprites are composited first, sprite 7 to
    /// sprite 0 so that sprite 0, the highest priority, lands on top, and the topmost pixel's own
    /// priority bit then decides whether it goes in front of or behind the foreground graphics.
    /// </summary>
    private void DrawSpriteLines()
    {
        for (int pixelArrayY = 0; pixelArrayY < _height; pixelArrayY++)
        {
            var mask = _lineSpriteMask[pixelArrayY];
            if (mask == 0)
                continue;
            // Where no two of the line's runs overlap, the topmost-sprite rule cannot bite and each
            // run goes straight to its layer; the composite is only built for lines with overlap.
            var overlapping = LineSpriteRunsOverlap(pixelArrayY, mask);
            var ypos = FlipY ? _height - pixelArrayY - 1 : pixelArrayY;
            var rowIndex = ypos * _width;
            var minX = int.MaxValue;
            var maxX = -1;
            for (int spriteIndex = SPRITE_COUNT - 1; spriteIndex >= 0; spriteIndex--)
            {
                if ((mask & (1 << spriteIndex)) == 0)
                    continue;
                for (int run = 0; run < SPRITE_RUNS; run++)
                {
                    var index = (pixelArrayY * SPRITE_COUNT + spriteIndex) * SPRITE_RUNS + run;
                    if (_lineSpriteRunPresent[index] == 0)
                        continue;
                    var flags = _lineSpriteFlags[index];
                    var destX = _lineSpriteX[index];
                    int from, to;
                    var perPixelLayers = (flags & LineSpriteFlagDecoded) != 0 || _lineSpriteRunColorEventCount[index] != 0;
                    byte layer = 0;
                    if (perPixelLayers)
                    {
                        if (!DecodeRunPixels(index, out from, out to))
                            continue;
                    }
                    else
                    {
                        var rowBytes = _lineSpriteData.AsSpan(index * SPRITE_ROW_BYTES, SPRITE_ROW_BYTES);
                        if (rowBytes[0] == 0 && rowBytes[1] == 0 && rowBytes[2] == 0)
                            continue;
                        var rowWidth = DecodeSpriteRowPixels(rowBytes, (flags & LineSpriteFlagDoubleWidth) != 0, (flags & LineSpriteFlagMultiColor) != 0,
                            _lineSpriteColorFg[index], _lineSpriteColorMc0[index], _lineSpriteColorMc1[index], _lineSpriteRunLength[index], _lineSpriteRunStretch[index]);
                        from = Math.Max(Math.Max(0, _lineSpriteClipStartX[index]), destX);
                        to = Math.Min(Math.Min(_width, _lineSpriteClipEndX[index]), destX + rowWidth);
                        if (from >= to)
                            continue;
                        layer = (flags & LineSpriteFlagPriority) != 0 ? (byte)0 : Vic2SpriteManager.RunPixelBehindForeground;
                    }
                    var pixels = _spriteRowPixels;
                    var layers = _spriteRunLayers;
                    if (!overlapping)
                    {
                        // Runs of set pixels bound for the same layer, straight from the row buffer.
                        var i2 = from - destX;
                        var end = to - destX;
                        while (i2 < end)
                        {
                            if (pixels[i2] == 0) { i2++; continue; }
                            var runStart = i2;
                            var runLayer = perPixelLayers ? layers[i2] : layer;
                            while (i2 < end && pixels[i2] != 0 && (!perPixelLayers || layers[i2] == runLayer)) i2++;
                            var write = runLayer == 0 ? _setForegroundPixels : _setBackgroundPixels;
                            write(pixels, runStart, rowIndex + destX + runStart, i2 - runStart);
                        }
                        continue;
                    }
                    for (var i = from - destX; i < to - destX; i++)
                    {
                        var color = pixels[i];
                        if (color == 0)
                            continue;
                        var x = destX + i;
                        _spriteLineColor[x] = color;
                        _spriteLineLayer[x] = perPixelLayers ? layers[i] : layer;
                    }
                    if (from < minX)
                        minX = from;
                    if (to - 1 > maxX)
                        maxX = to - 1;
                }
            }
            if (maxX < 0)
                continue;
            // The composited line to the layers, as runs of pixels bound for the same layer.
            var lineColor = _spriteLineColor;
            var lineLayer = _spriteLineLayer;
            var x2 = minX;
            while (x2 <= maxX)
            {
                if (lineColor[x2] == 0) { x2++; continue; }
                var runStart = x2;
                var runLayer = lineLayer[x2];
                while (x2 <= maxX && lineColor[x2] != 0 && lineLayer[x2] == runLayer) x2++;
                var write = runLayer == 0 ? _setForegroundPixels : _setBackgroundPixels;
                write(lineColor, runStart, rowIndex + runStart, x2 - runStart);
            }
            Array.Clear(lineColor, minX, maxX - minX + 1);
        }
    }

    // Whether any two of a line's recorded runs (of different sprites, or the two runs of one)
    // share a pixel-array x, from their x spans alone.
    private bool LineSpriteRunsOverlap(int pixelArrayY, byte mask)
    {
        Span<int> starts = stackalloc int[SPRITE_COUNT * SPRITE_RUNS];
        Span<int> ends = stackalloc int[SPRITE_COUNT * SPRITE_RUNS];
        var n = 0;
        for (int spriteIndex = 0; spriteIndex < SPRITE_COUNT; spriteIndex++)
        {
            if ((mask & (1 << spriteIndex)) == 0)
                continue;
            for (int run = 0; run < SPRITE_RUNS; run++)
            {
                var index = (pixelArrayY * SPRITE_COUNT + spriteIndex) * SPRITE_RUNS + run;
                if (_lineSpriteRunPresent[index] == 0)
                    continue;
                var flags = _lineSpriteFlags[index];
                var width = (flags & LineSpriteFlagDecoded) != 0
                    ? _lineSpriteRunLength[index]
                    : Math.Min(_lineSpriteRunLength[index] + _lineSpriteRunStretch[index], (flags & LineSpriteFlagDoubleWidth) != 0 ? Vic2Sprite.DEFAULT_WIDTH * 2 + SpriteRowStretchMax : Vic2Sprite.DEFAULT_WIDTH + SpriteRowStretchMax);
                var start = Math.Max(_lineSpriteX[index], _lineSpriteClipStartX[index]);
                var end = Math.Min(_lineSpriteX[index] + width, _lineSpriteClipEndX[index]);
                if (start >= end)
                    continue;
                for (var j = 0; j < n; j++)
                {
                    if (start < ends[j] && starts[j] < end)
                        return true;
                }
                starts[n] = start;
                ends[n++] = end;
            }
        }
        return false;
    }

    /// <summary>
    /// Decodes one sprite shape row (3 bytes / 24 px) and writes its pixels at (destX, destY).
    /// Shared by both the end-of-frame (per-frame) and per-line (band) sprite paths - the only
    /// difference between them is which producer supplies the position, shape and per-row colours.
    /// Handles single/multi colour and X expansion; the caller handles Y expansion (calls twice).
    /// </summary>
    /// <summary>
    /// Decodes a run recorded pixel by pixel into the row buffers: one the VIC-II decoded because
    /// a mode or priority bit changed while the sprite shifted, or one a sprite colour register
    /// changed under. Each pixel takes its colour from the run's colours as they stand at its x
    /// and the layer its own priority bit selects. Returns false when nothing of it is visible;
    /// otherwise the pixels from x <paramref name="from"/> to <paramref name="to"/> (exclusive) are
    /// in the buffers, indexed relative to the run's x.
    /// </summary>
    private bool DecodeRunPixels(int index, out int from, out int to)
    {
        Span<byte> codes = stackalloc byte[RUN_PIXELS];
        int count;
        if ((_lineSpriteFlags[index] & LineSpriteFlagDecoded) != 0)
        {
            count = _lineSpriteRunLength[index];
            _lineSpriteRunCodes.AsSpan(index * RUN_PIXELS, count).CopyTo(codes);
        }
        else
        {
            var flags = _lineSpriteFlags[index];
            var row = (uint)(_lineSpriteData[index * SPRITE_ROW_BYTES] << 16 | _lineSpriteData[index * SPRITE_ROW_BYTES + 1] << 8 | _lineSpriteData[index * SPRITE_ROW_BYTES + 2]);
            var runFlags = (byte)(((flags & LineSpriteFlagDoubleWidth) != 0 ? Vic2SpriteManager.RunFlagXExpand : 0)
                | ((flags & LineSpriteFlagMultiColor) != 0 ? Vic2SpriteManager.RunFlagMultiColor : 0)
                | ((flags & LineSpriteFlagPriority) == 0 ? Vic2SpriteManager.RunFlagBehindForeground : 0));
            count = Vic2SpriteManager.ExpandRunPixels(row, runFlags, _lineSpriteRunLength[index], _lineSpriteRunStretch[index], codes);
        }
        var destX = _lineSpriteX[index];
        from = Math.Max(Math.Max(0, _lineSpriteClipStartX[index]), destX);
        to = Math.Min(Math.Min(_width, _lineSpriteClipEndX[index]), destX + count);
        if (from >= to)
            return false;
        Span<uint> colors = stackalloc uint[4];
        colors[0] = 0;
        colors[1] = _lineSpriteColorMc0[index];
        colors[2] = _lineSpriteColorFg[index];
        colors[3] = _lineSpriteColorMc1[index];
        var events = _lineSpriteRunColorEventCount[index];
        var nextEvent = 0;
        var eventBase = index * RUN_COLOR_EVENTS;
        var pixels = _spriteRowPixels;
        var layers = _spriteRunLayers;
        for (var i = from - destX; i < to - destX; i++)
        {
            var x = destX + i;
            while (nextEvent < events && _lineSpriteRunColorEventX[eventBase + nextEvent] <= x)
            {
                var slot = _lineSpriteRunColorEventSlot[eventBase + nextEvent];
                colors[slot == 0 ? 2 : slot == 1 ? 1 : 3] = _lineSpriteRunColorEventColor[eventBase + nextEvent];
                nextEvent++;
            }
            var code = codes[i];
            pixels[i] = colors[code & Vic2SpriteManager.RunPixelValueMask];
            layers[i] = (byte)(code & Vic2SpriteManager.RunPixelBehindForeground);
        }
        return true;
    }

    // A decoded sprite row: the colour of each of its up to 48 pixels, 0 where transparent, plus
    // room for the repeated last pixel of a row cut short by the sprite's own fetch.
    private readonly uint[] _spriteRowPixels = new uint[Vic2Sprite.DEFAULT_WIDTH * 2 + SpriteRowStretchMax];
    private const int SpriteRowStretchMax = 8;

    /// <summary>
    /// Decodes one sprite shape row (3 bytes / 24 px) and writes its pixels at (destX, destY).
    /// Shared by both the end-of-frame (per-frame) and per-line sprite paths - the only difference
    /// between them is which producer supplies the position, shape and per-row colours. Handles
    /// single/multi colour and X expansion; the caller handles Y expansion (calls twice). The row
    /// is decoded into a buffer first and written as runs of set pixels, one copy per run rather
    /// than a call per pixel. Only the first shownPixels are written, then stretchPixels more
    /// repeating the last shown one (a row the sprite's own fetch cut short).
    /// </summary>
    private void DecodeAndWriteSpriteRow(ReadOnlySpan<byte> rowBytes, int destX, int destY, bool isDoubleWidth, bool isMultiColor, bool priorityOverForeground, uint spriteForegroundPixelColor, uint spriteMultiColor0PixelColor, uint spriteMultiColor1PixelColor, int clipStartX, int clipEndX, int clipStartY, int clipEndY, int shownPixels = int.MaxValue, int stretchPixels = 0)
    {
        if (destY < 0 || destY > _height || destY < clipStartY || destY >= clipEndY)
            return;
        var rowWidth = DecodeSpriteRowPixels(rowBytes, isDoubleWidth, isMultiColor, spriteForegroundPixelColor, spriteMultiColor0PixelColor, spriteMultiColor1PixelColor, shownPixels, stretchPixels);
        var pixels = _spriteRowPixels;

        // Runs of set pixels within the visible and unclipped span of the row.
        var from = Math.Max(Math.Max(0, clipStartX), destX);
        var to = Math.Min(Math.Min(_width, clipEndX), destX + rowWidth);
        if (from >= to)
            return;
        var ypos = FlipY ? _height - destY - 1 : destY;
        var rowIndex = ypos * _width;
        var write = priorityOverForeground ? _setForegroundPixels : _setBackgroundPixels;
        var i = from - destX;
        var end = to - destX;
        while (i < end)
        {
            if (pixels[i] == 0) { i++; continue; }
            var runStart = i;
            while (i < end && pixels[i] != 0) i++;
            write(pixels, runStart, rowIndex + destX + runStart, i - runStart);
        }
    }

    /// <summary>
    /// Decodes one sprite shape row into the row buffer (the colour of each pixel, 0 where
    /// transparent): single/multi colour, X expansion, only the first shownPixels and then
    /// stretchPixels more repeating the last shown one. Returns the row's width in pixels.
    /// </summary>
    private int DecodeSpriteRowPixels(ReadOnlySpan<byte> rowBytes, bool isDoubleWidth, bool isMultiColor, uint spriteForegroundPixelColor, uint spriteMultiColor0PixelColor, uint spriteMultiColor1PixelColor, int shownPixels, int stretchPixels)
    {
        var rowWidth = isDoubleWidth ? Vic2Sprite.DEFAULT_WIDTH * 2 : Vic2Sprite.DEFAULT_WIDTH;
        var pixels = _spriteRowPixels;
        var x = 0;
        if (isMultiColor)
        {
            var pairWidth = isDoubleWidth ? 4 : 2;
            for (int byteIndex = 0; byteIndex < SPRITE_ROW_BYTES; byteIndex++)
            {
                var part = rowBytes[byteIndex];
                for (var pair = 0; pair < 4; pair++)
                {
                    var color = ((part >> (6 - pair * 2)) & 3) switch
                    {
                        3 => spriteMultiColor1PixelColor,
                        2 => spriteForegroundPixelColor,
                        1 => spriteMultiColor0PixelColor,
                        _ => 0u,
                    };
                    for (var k = 0; k < pairWidth; k++)
                        pixels[x++] = color;
                }
            }
        }
        else
        {
            var pixelWidth = isDoubleWidth ? 2 : 1;
            for (int byteIndex = 0; byteIndex < SPRITE_ROW_BYTES; byteIndex++)
            {
                var part = rowBytes[byteIndex];
                for (var bit = 0; bit < 8; bit++)
                {
                    var color = (part & (0x80 >> bit)) != 0 ? spriteForegroundPixelColor : 0u;
                    for (var k = 0; k < pixelWidth; k++)
                        pixels[x++] = color;
                }
            }
        }

        if (shownPixels < rowWidth)
        {
            // A row the sprite's own fetch cut short: the last shown pixel repeats for the
            // stretch, then nothing.
            var last = shownPixels > 0 ? pixels[shownPixels - 1] : 0u;
            var stretch = Math.Min(stretchPixels, SpriteRowStretchMax);
            for (var s = 0; s < stretch; s++)
                pixels[shownPixels + s] = last;
            rowWidth = shownPixels + stretch;
        }
        return rowWidth;
    }

    private void InitBitmaps(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;

        // Init pixel arrays
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;
    }

    [MemberNotNull(nameof(_oneLineSameColorPixels))]
    private void InitBitPatternToPixelMaps(C64 c64)
    {
        // Create 8 precalculated pixels (with colors to be used in the shader) for each 8 bit pattern suited for C64 normal color or multicolor text/bitmap.
        // 
        // A 0 bit (or 00 bit pair) is the background color, and is set to specific color value to be checked for in the shader.
        // 

        var vic2 = c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;
        var width = vic2Screen.VisibleWidth;

        // A single line of the same color. Used for filling borders with various lengths.
        _oneLineSameColorPixels = new uint[16][];
        for (byte colorCode = 0; colorCode < 16; colorCode++)
        {
            var colorVal = _c64ToRenderColorMap[colorCode];
            var oneLine = new uint[width];
            for (var i = 0; i < oneLine.Length; i++)
                oneLine[i] = colorVal;
            _oneLineSameColorPixels[colorCode] = oneLine;
        }
    }

    public void DrawSpritesToBitmapBackedByPixelArray()
    {
        // Main screen, copy 8 pixels at a time
        var vic2 = _c64.Vic2;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        var visibleMainScreenAreaLineData = vic2ScreenLayouts.GetLayout(LayoutType.Visible);

        // Write sprites to a separate bitmap/pixel array
        var sprites = vic2.SpriteManager.Sprites;
        for (int spriteIndex = sprites.Length - 1; spriteIndex >= 0; spriteIndex--)
        {
            var sprite = sprites[spriteIndex];
            if (!sprite.Visible)
                continue;

            var spriteScreenPosX = SpriteScreenX(sprite.X);
            var spriteScreenPosY = sprite.Y + visibleMainScreenArea.Screen.Start.Y - vic2.SpriteManager.ScreenOffsetY;
            var priorityOverForground = sprite.PriorityOverForeground;
            var isMultiColor = sprite.Multicolor;

            //// START TEST
            //if (sprite.SpriteNumber == 0)
            //{
            //    spriteScreenPosX = 50 + visibleMainScreenArea.Screen.Start.X - c64.Vic2.SpriteManager.ScreenOffsetX;
            //    spriteScreenPosY = 60 + visibleMainScreenArea.Screen.Start.Y - c64.Vic2.SpriteManager.ScreenOffsetY;
            //    priorityOverForground = false;
            //}
            //if (sprite.SpriteNumber == 1)
            //{
            //    spriteScreenPosX = 67 + visibleMainScreenArea.Screen.Start.X - c64.Vic2.SpriteManager.ScreenOffsetX;
            //    spriteScreenPosY = 70 + visibleMainScreenArea.Screen.Start.Y - c64.Vic2.SpriteManager.ScreenOffsetY;
            //    priorityOverForground = true;
            //}
            //// END TEST

            var isDoubleWidth = sprite.DoubleWidth;
            var isDoubleHeight = sprite.DoubleHeight;
            var spriteLineAdvance = isDoubleHeight ? 2 : 1;

            uint spriteForegroundPixelColor;  // One color per sprite
            uint spriteMultiColor0PixelColor; // Shared between all sprites
            uint spriteMultiColor1PixelColor; // Shared between all sprites

            // Loop each sprite line (21 lines)
            var spriteData = sprite.Data;
            var y = 0;
            for (int rowIndex = 0; rowIndex < spriteData.Rows.Length; rowIndex++)
            {
                if (!spriteData.RowHasPixels(rowIndex))
                {
                    y += spriteLineAdvance;
                    continue;
                }

                var spriteRow = spriteData.Rows[rowIndex];
                var lineDataKey = spriteScreenPosY + y + visibleMainScreenAreaLineData.TopBorder.Start.Y;

                // Check if in total visible area, because c64ScreenLineIORegisterValues includes non-visible lines
                if (lineDataKey < visibleMainScreenAreaLineData.TopBorder.Start.Y || lineDataKey > visibleMainScreenAreaLineData.BottomBorder.End.Y)
                {
                    y += spriteLineAdvance;
                    continue;
                }

                var screenLineIORegisters = vic2.ScreenLineIORegisterValues[lineDataKey];
                var spriteColorValue = sprite.SpriteNumber switch
                {
                    0 => screenLineIORegisters.Sprite0Color,
                    1 => screenLineIORegisters.Sprite1Color,
                    2 => screenLineIORegisters.Sprite2Color,
                    3 => screenLineIORegisters.Sprite3Color,
                    4 => screenLineIORegisters.Sprite4Color,
                    5 => screenLineIORegisters.Sprite5Color,
                    6 => screenLineIORegisters.Sprite6Color,
                    7 => screenLineIORegisters.Sprite7Color,
                    _ => throw new DotNet6502Exception("Invalid sprite number."),
                };
                spriteForegroundPixelColor = _c64ToRenderColorMap[spriteColorValue];
                spriteMultiColor0PixelColor = _c64ToRenderColorMap[screenLineIORegisters.SpriteMultiColor0];
                spriteMultiColor1PixelColor = _c64ToRenderColorMap[screenLineIORegisters.SpriteMultiColor1];
                // Decode the row using the shared core (same code path as the per-line band draw).
                // For a Y-expanded sprite the second physical line is the same decode at y+1. The
                // border covers sprites: each physical line shows only where its border flip-flop
                // was clear (nowhere under the vertical border, out into an opened side border).
                for (var physicalLine = 0; physicalLine < spriteLineAdvance; physicalLine++)
                {
                    var frameRow = spriteScreenPosY + y + physicalLine;
                    if (frameRow < 0 || frameRow >= _height)
                        continue;
                    DecodeAndWriteSpriteRow(spriteRow.Bytes, spriteScreenPosX, frameRow, isDoubleWidth, isMultiColor, priorityOverForground, spriteForegroundPixelColor, spriteMultiColor0PixelColor, spriteMultiColor1PixelColor, _lineClearStartXs[frameRow], _lineClearEndXs[frameRow], 0, _height);
                }

                y += spriteLineAdvance;
            }
        }
    }



    /// <summary>
    /// Draw the border colour on one line between two normalized x positions (end exclusive),
    /// clipped to the line's border parts: the whole line in the top and bottom border, the left
    /// and right border parts elsewhere.
    /// </summary>
    private void DrawBorderRun(int normalizedScreenLine, int fromX, int toX)
    {
        fromX = Math.Max(fromX, 0);
        toX = Math.Min(toX, _width);
        if (fromX >= toX)
            return;

        // A border run only ever spans pixels where the flip-flop was set, so it is painted as is.
        _setBackgroundPixels(_oneLineSameColorPixels[_borderColor], 0, normalizedScreenLine * _width + fromX, toX - fromX);
    }

    // A sprite's X as a normalized frame x. The chip's X coordinate wraps at 512 and the line
    // starts at X 404 (PAL) or 412 (NTSC), so an X at or beyond that is at the line's start, in the
    // left border: where it shows when the border there is opened.
    private int SpriteScreenX(int spriteX)
        => spriteX >= _xCoordinateAtLineStart
            ? spriteX - _xCoordinateAtLineStart - _screenLayoutInclNonVisibleLeftBorderStartX
            : spriteX + _screenStartX - _spriteScreenOffsetX;

    // The sequencer's idle output in a part of the line outside the display window where the
    // border flip-flop is clear (an opened side border): the byte at the end of the VIC-II bank
    // in black over the background colour, on the character grid. col is negative to the left of
    // the window and 40 or more to its right; the clip in WriteToPixelArray keeps it to the span
    // that is open and inside the frame.
    /// <summary>
    /// One cycle of the graphics data sequencer: eight pixels shifted out, then the g-access of
    /// this cycle stored for the output two cycles on. Pixels are recorded as colour codes on the
    /// line's buffers, inside the border flip-flop's clear span only, and resolved into the layers
    /// when the line ends.
    /// </summary>
    private void DrawGraphicsCycle(int cycle, int rasterLine, int blockX, bool render)
    {
        var clipStart = Math.Max(_lineClearStartX, 0);
        var clipEnd = Math.Min(_lineClearEndX, _width);
        var draw = render && blockX + 8 > clipStart && blockX < clipEnd;
        var newEcmBmm = (byte)(((_d011 & 0x40) != 0 ? MODE_ECM : 0) | ((_d011 & 0x20) != 0 ? MODE_BMM : 0));
        var newMcm = (_d016 & 0x10) != 0;
        var modeStable = _modeEcmBmm == newEcmBmm && _colorMcm == newMcm && _decodeMcm == newMcm;
        var fetchSlot = cycle % 3;                       // this cycle's entry in the fetch ring
        var loadSlot = fetchSlot == 2 ? 0 : fetchSlot + 1;   // the entry fetched two cycles ago: (cycle - 2) mod 3
        var wholeBlock = draw && modeStable && blockX >= clipStart && blockX + 8 <= clipEnd;
        // The block's inputs and the pipeline state it starts from, as one key.
        var blockKey = (ulong)_fetchedData[loadSlot] | ((ulong)_fetchedMatrix[loadSlot] << 8) | ((ulong)_fetchedColor[loadSlot] << 16)
            | ((ulong)_shiftData << 24) | ((ulong)_shiftMatrix << 32) | ((ulong)_shiftColor << 40)
            | ((ulong)_pixelValue << 48) | (_pairSecondHalf ? 1UL << 56 : 0) | ((ulong)_loadPixel << 57);
        if (wholeBlock && blockX == _blockCacheX + 8 && blockKey == _blockCacheKey)
        {
            // The same inputs as the block before, in the same state: the eight codes repeat (a run
            // of identical cells, the idle byte across an opened border, blank cells under any
            // XSCROLL), and the state after is the state the previous block left.
            Unsafe.As<byte, ulong>(ref _lineFgCodes[blockX]) = Unsafe.As<byte, ulong>(ref _lineFgCodes[blockX - 8]);
            Unsafe.As<byte, ulong>(ref _lineBgCodes[blockX]) = Unsafe.As<byte, ulong>(ref _lineBgCodes[blockX - 8]);
            _lineRepeatMark[blockX] = _lineSerial;   // the resolve pass copies this block's colours from the one before
            _shiftData = _blockCachePostShiftData;
            _pixelValue = _blockCachePostPixelValue;
            _pairSecondHalf = _blockCachePostPairSecondHalf;
            _shiftMatrix = _fetchedMatrix[loadSlot];
            _shiftColor = _fetchedColor[loadSlot];
            _blockCacheX = blockX;
            FetchCycle(cycle, rasterLine, fetchSlot);
            return;
        }
        _blockCacheKey = blockKey;

        if (!draw && modeStable && _fetchedData[loadSlot] == 0 && _shiftData == 0 && _pixelValue == 0)
        {
            // Nothing to show and nothing in the shift register (the border, or a line the vertical
            // flip-flop covers): the cycle only takes the empty entry and settles the pair phase.
            _shiftMatrix = _fetchedMatrix[loadSlot];
            _shiftColor = _fetchedColor[loadSlot];
            _pairSecondHalf = ((8 - _loadPixel) & 1) != 0;
        }
        else if (draw && modeStable && _loadPixel == 0 && blockX >= clipStart && blockX + 8 <= clipEnd)
        {
            // The steady state: the byte is taken at pixel 0, the modes do not change and the whole
            // block shows. Decoded without the per-pixel bookkeeping of the general case below.
            _shiftMatrix = _fetchedMatrix[loadSlot];
            _shiftColor = _fetchedColor[loadSlot];
            var data = _fetchedData[loadSlot];
            var multicolorCell = (_modeEcmBmm & MODE_BMM) != 0 || (_shiftColor & 0x08) != 0;
            ResolveColorCodes();
            byte value = 0;
            if (data == 0)
            {
                // A blank byte (most of a text screen): every pixel is value 0, background priority.
                Array.Fill(_lineFgCodes, CODE_NONE, blockX, 8);
                Array.Fill(_lineBgCodes, _colorCodes[0], blockX, 8);
            }
            else if (_decodeMcm && multicolorCell)
            {
                for (var i = 0; i < 8; i += 2)
                {
                    value = (byte)((data >> (6 - i)) & 3);
                    WritePixelCode(blockX + i, value);
                    WritePixelCode(blockX + i + 1, value);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    value = (data & (0x80 >> i)) != 0 ? (byte)3 : (byte)0;
                    WritePixelCode(blockX + i, value);
                }
            }
            _shiftData = 0;
            _pixelValue = value;
            _pairSecondHalf = false;
        }
        else
        {
            for (var i = 0; i < 8; i++)
            {
                // A register change reaches the output part way through the cycle: MCM's colour
                // selection at pixel 4, ECM and BMM at pixel 4 when set and pixel 6 when cleared.
                // The decoding into pairs follows MCM a cycle later, at pixel 7, and MCM switched on
                // starts the pairs afresh from the next pixel.
                if (i == 4)
                {
                    _colorMcm = newMcm;
                    _modeEcmBmm |= newEcmBmm;
                    _colorCodesValid = false;
                }
                else if (i == 6)
                {
                    _modeEcmBmm &= newEcmBmm;
                    _colorCodesValid = false;
                }
                else if (i == 7)
                {
                    if (_colorMcm && !_decodeMcm)
                        _pairSecondHalf = true;
                    _decodeMcm = _colorMcm;
                }

                // The shift register is loaded at the pixel XSCROLL selects.
                if (i == _loadPixel)
                {
                    _shiftData = _fetchedData[loadSlot];
                    _shiftMatrix = _fetchedMatrix[loadSlot];
                    _shiftColor = _fetchedColor[loadSlot];
                    _pairSecondHalf = false;
                    _colorCodesValid = false;
                }

                // The pixel's value: in a multicolour cell (BMM, or a colour nibble with bit 3 set)
                // being decoded as pairs, the top two bits, held for the pair's second pixel; else
                // the top bit as 3 or 0. A set bit in a multicolour cell while the colour selection
                // is already multicolour but the decoding is not yet shows as value 2 (the chip's
                // $D023 flash at an MCM switch).
                var multicolorCell = (_modeEcmBmm & MODE_BMM) != 0 || (_shiftColor & 0x08) != 0;
                if (_decodeMcm && multicolorCell)
                {
                    if (!_pairSecondHalf)
                        _pixelValue = (byte)(_shiftData >> 6);
                }
                else if ((_shiftData & 0x80) == 0)
                {
                    _pixelValue = 0;
                }
                else
                {
                    _pixelValue = _colorMcm && multicolorCell ? (byte)2 : (byte)3;
                }
                _shiftData <<= 1;
                _pairSecondHalf = !_pairSecondHalf;

                if (!draw)
                    continue;
                var x = blockX + i;
                if (x < clipStart || x >= clipEnd)
                    continue;
                if (!_colorCodesValid)
                    ResolveColorCodes();
                WritePixelCode(x, _pixelValue);
            }
        }

        if (wholeBlock)
        {
            _blockCacheX = blockX;
            _blockCachePostShiftData = _shiftData;
            _blockCachePostPixelValue = _pixelValue;
            _blockCachePostPairSecondHalf = _pairSecondHalf;
        }
        else
        {
            _blockCacheX = int.MinValue;
        }
        FetchCycle(cycle, rasterLine, fetchSlot);
    }

    // The block cache: the inputs and state of the last whole block drawn, so an identical block
    // right after it repeats its codes without decoding.
    private int _blockCacheX = int.MinValue;
    private ulong _blockCacheKey;
    // Per pixel-array X: the serial of the line on which a block starting there repeated the block
    // before it, so the resolve pass copies its colours instead of looking them up. A serial per
    // line saves clearing the marks.
    private int[] _lineRepeatMark = Array.Empty<int>();
    private int _lineSerial = 1;
    private byte _blockCachePostShiftData, _blockCachePostPixelValue;
    private bool _blockCachePostPairSecondHalf;

    /// <summary>
    /// The cycle's accesses and counter work (3.7.2), then the fetched byte into the pipeline for
    /// the output two cycles on. Cycle numbers below count from 1 as the article does; `cycle`
    /// counts from 0.
    /// </summary>
    private void FetchCycle(int cycle, int rasterLine, int fetchSlot)
    {
        var chipCycle = cycle + 1;
        var previousSlot = fetchSlot == 0 ? 2 : fetchSlot - 1;   // the entry fetched the cycle before: (cycle - 1) mod 3
        // DEN decides the frame's bad lines by being set in any cycle of line $30 (3.5), taken from
        // this cycle's journaled $D011 rather than the live register, so a DEN set later in the line
        // does not make the cycles before it bad ones (VICE's dmadelay test3). A write lands in the
        // cycle after it, so the value at the next line's first cycle is line $30's last (dentest).
        if (rasterLine == BadLineFirstRasterLine || (rasterLine == BadLineFirstRasterLine + 1 && cycle == 0))
        {
            if (cycle == 0 && rasterLine == BadLineFirstRasterLine)
                _displayEnabledThisFrame = false;
            if ((_d011 & 0x10) != 0)
                _displayEnabledThisFrame = true;
        }
        var badLineCondition = rasterLine >= BadLineFirstRasterLine && rasterLine <= BadLineLastRasterLine
            && (rasterLine & 7) == (_d011 & 7) && _displayEnabledThisFrame;
        if (chipCycle == 14)
        {
            _vc = _vcBase;                                  // rule 2
            _vmli = 0;
            if (badLineCondition)
                _rc = 0;
        }
        // The g-access (cycles 16-55): a row's data in display state, the byte at the end of the bank
        // ($39FF with ECM) with the matrix data read as zero in idle state (3.7.3.9). VC and VMLI
        // advance after it in display state (rule 4).
        if (chipCycle >= 16 && chipCycle <= 55 && _lineVerticalBorder)
        {
            // The access and the counters go on, but while the vertical border flip-flop is set the
            // sequencer shows the last background colour (3.7.3): zeros over the matrix data it has.
            if (_displayState)
            {
                _vmli++;
                _vc = (_vc + 1) & 0x3FF;
            }
            _fetchedData[fetchSlot] = 0;
            _fetchedMatrix[fetchSlot] = _fetchedMatrix[previousSlot];
            _fetchedColor[fetchSlot] = _fetchedColor[previousSlot];
        }
        else if (chipCycle >= 16 && chipCycle <= 55)
        {
            _loadPixel = (byte)(_d016 & 0x07);
            if (_displayState)
            {
                FetchGraphics(fetchSlot);
                _vmli++;
                _vc = (_vc + 1) & 0x3FF;
            }
            else
            {
                _fetchedMatrix[fetchSlot] = 0;
                _fetchedColor[fetchSlot] = 0;
                // The idle byte, from $38FF in the cycle a bad line condition arises mid-line (the
                // 6569 and 6567R8 in the hardware table of VICE's vsp-tester readme).
                var idleAddress = badLineCondition ? 0x38FF : (_d011 & 0x40) != 0 ? 0x39FF : 0x3FFF;
                _fetchedData[fetchSlot] = _c64.Vic2.ReadMemory((ushort)idleAddress);
            }
        }
        else
        {
            _fetchedData[fetchSlot] = 0;
            _fetchedMatrix[fetchSlot] = _fetchedMatrix[previousSlot];
            _fetchedColor[fetchSlot] = _fetchedColor[previousSlot];
        }

        // A bad line condition in any cycle puts the sequencer in display state (3.5, 3.14.6), from
        // the cycle's second phase on: the g-access of the cycle the condition first appears in is
        // still an idle one, which is what makes a condition created in cycle 16 shift the screen
        // by two columns (VICE's screenpos test) and puts the first $FF of a DMA delay at the start
        // of the video matrix line. One in cycles 12-54 also pulls BA low and starts the c-accesses
        // (rule 3).
        if (badLineCondition)
        {
            _displayState = true;
            if (_baLowSince < 0 && chipCycle >= 12 && chipCycle <= 54)
                _baLowSince = cycle;
        }

        // The c-access (cycles 15-54 with the condition): the screen code and colour nibble at VC
        // into the video matrix line at VMLI. In the three cycles after BA went low the CPU still
        // holds the bus, and the chip reads $FF and the low nibble of the CPU's next opcode instead.
        if (badLineCondition && _baLowSince >= 0 && chipCycle >= 15 && chipCycle <= 54 && _vmli < _matrixLine.Length)
        {
            if (cycle - _baLowSince < 3)
            {
                _matrixLine[_vmli] = 0xFF;
                _colorLine[_vmli] = (byte)(_c64.Mem[_c64.CPU.PC] & 0x0F);
            }
            else
            {
                var videoMatrixBase = (_d018 & 0xF0) << 6;
                _matrixLine[_vmli] = _c64.Vic2.ReadMemory((ushort)(videoMatrixBase + _vc));
                _colorLine[_vmli] = (byte)(_c64.ReadIOStorage((ushort)(Vic2Addr.COLOR_RAM_START + _vc)) & 0x0F);
            }
        }
        if (!badLineCondition)
            _baLowSince = -1;                               // the condition taken away: BA high again

        _d011PreviousCycle = _d011;

        if (chipCycle == 58)
        {
            // Rule 5: a row's eighth line ends it, unless a bad line condition keeps the display
            // state going (3.14.5, the row then repeats); in display state RC advances.
            if (_rc == 7)
            {
                _displayState = false;
                _vcBase = _vc;
            }
            if (_displayState || badLineCondition)
            {
                _rc = (_rc + 1) & 7;
                _displayState = true;
            }
        }
    }

    /// <summary>
    /// The g-access in display state: the screen code and colour nibble at VMLI in the video
    /// matrix line, the graphics byte from the bitmap at VC and RC or from the character set the
    /// memory pointers select now (with ECM only the low six bits of the screen code select the
    /// shape).
    /// </summary>
    private void FetchGraphics(int slot)
    {
        var index = _vmli < _matrixLine.Length ? _vmli : _matrixLine.Length - 1;
        var screenCode = _matrixLine[index];
        _fetchedMatrix[slot] = screenCode;
        _fetchedColor[slot] = _colorLine[index];

        // The address follows BMM as it is now or as it was in the cycle before, whichever is set:
        // on the NMOS chips (6569, 6567R8) switching bitmap mode off reaches the fetch a cycle
        // after it reaches the sequencer. ECM is taken as it is now.
        var address = GraphicsAddress((byte)(_d011 | (_d011PreviousCycle & 0x20)), screenCode);
        if (((_d011 ^ _d011PreviousCycle) & 0x20) != 0)
        {
            // In the cycle BMM changes, a fetch that moves from RAM into the character ROM takes the
            // address's low byte from the mode of the cycle before and the rest from the mode now
            // (observed on the 6569; VICE's videomode test programs).
            var addressBefore = GraphicsAddress(_d011PreviousCycle, screenCode);
            var addressNow = GraphicsAddress(_d011, screenCode);
            if (!_c64.Vic2.IsCharacterRomAddress(addressBefore) && _c64.Vic2.IsCharacterRomAddress(addressNow))
                address = (ushort)((addressBefore & 0x00FF) | (addressNow & 0x3F00));
        }
        _fetchedData[slot] = _c64.Vic2.ReadMemory(address);
    }

    // The g-access address for a $D011 value: the bitmap at VC and RC with BMM, otherwise the
    // shape of the screen code at RC; with ECM bits 9 and 10 of the address are held low.
    private ushort GraphicsAddress(byte d011, byte screenCode)
    {
        var address = (d011 & 0x20) != 0
            ? ((_d018 & 0x08) << 10) | (_vc << 3) | _rc
            : ((_d018 & 0x0E) << 10) | (screenCode << 3) | _rc;
        if ((d011 & 0x40) != 0)
            address &= 0x39FF;
        return (ushort)address;
    }

    // The colour codes of the four pixel values, from the article's mode tables (3.7.3): what a bit
    // or a pair stands for in each mode, with the matrix byte and colour nibble in the shift register.
    // Values 2 and 3 are foreground pixels in every mode (3.8.5); value 2 only occurs as a pair, or
    // as the flash at an MCM switch. The invalid modes (ECM with BMM or MCM) show black.
    private void ResolveColorCodes()
    {
        var key = (_modeEcmBmm << 17) | (_colorMcm ? 1 << 16 : 0) | (_shiftMatrix << 8) | _shiftColor;
        if (key == _colorCodesKey)
        {
            _colorCodesValid = true;
            return;
        }
        _colorCodesKey = key;
        var matrix = _shiftMatrix;
        var color = _shiftColor;
        byte c0, c1, c2, c3;
        switch (_modeEcmBmm)
        {
            case 0:   // text
                if (_colorMcm)
                {
                    // Multicolour text: pairs 00, 01, 10 the background colours 0-2, 11 the colour
                    // nibble's low three bits; a hires cell (bit 3 clear) shows those bits for its
                    // set bits. Value 2 is background colour 2 either way.
                    c0 = CODE_BG0;
                    c1 = CODE_BG0 + 1;
                    c2 = CODE_BG0 + 2;
                    c3 = (byte)(color & 0x07);
                }
                else
                {
                    // Standard text: a clear bit background colour 0, a set bit the colour nibble.
                    c0 = c1 = CODE_BG0;
                    c2 = c3 = color;
                }
                break;
            case MODE_BMM:
                if (_colorMcm)
                {
                    // Multicolour bitmap: 00 background colour 0, 01 the matrix byte's high nibble,
                    // 10 its low nibble, 11 the colour nibble.
                    c0 = CODE_BG0;
                    c1 = (byte)(matrix >> 4);
                    c2 = (byte)(matrix & 0x0F);
                    c3 = color;
                }
                else
                {
                    // Standard bitmap: a clear bit the low nibble, a set bit the high nibble.
                    c0 = c1 = (byte)(matrix & 0x0F);
                    c2 = c3 = (byte)(matrix >> 4);
                }
                break;
            case MODE_ECM when !_colorMcm:
                // Extended colour text: a clear bit background colour 0-3 by the matrix byte's top
                // two bits, a set bit the colour nibble.
                c0 = c1 = (byte)(CODE_BG0 + (matrix >> 6));
                c2 = c3 = color;
                break;
            default:
                c0 = c1 = c2 = c3 = 0;
                break;
        }
        _colorCodes[0] = c0;
        _colorCodes[1] = c1;
        _colorCodes[2] = c2;
        _colorCodes[3] = c3;
        _colorCodesValid = true;
    }

    // Record a pixel on the line's code buffers. Values 2 and 3 are foreground pixels, which
    // sprites with the priority bit set go behind.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WritePixelCode(int x, byte value)
    {
        var code = _colorCodes[value];
        if ((value & 2) != 0)
        {
            _lineFgCodes[x] = code;
            _lineBgCodes[x] = CODE_BG0;
        }
        else
        {
            _lineFgCodes[x] = CODE_NONE;
            _lineBgCodes[x] = code;
        }
    }

    /// <summary>
    /// Resolve the line's colour codes into the background and foreground layers over the span
    /// where the border flip-flop was clear, giving each pixel that takes its colour from a
    /// background colour register the register's value at that pixel.
    /// </summary>
    private void ResolveLineGraphics(int normalizedLine)
    {
        var start = Math.Max(_lineClearStartX, 0);
        var end = Math.Min(_lineClearEndX, _width);
        if (start >= end)
            return;
        var ypos = FlipY ? _height - normalizedLine - 1 : normalizedLine;
        var transparent = TransparentColor;

        var anyEvents = _bgColorEventCount[0] > 0 || _bgColorEventCount[1] > 0 || _bgColorEventCount[2] > 0 || _bgColorEventCount[3] > 0;
        if (!anyEvents)
        {
            // No background colour write on the line: one lookup per pixel and layer.
            for (var c = 0; c < CODE_BG0; c++)
                _bgCodeColor[c] = _c64ToRenderColorMap[c];
            for (var r = 0; r < 4; r++)
                _bgCodeColor[CODE_BG0 + r] = _c64ToRenderColorMap[_bgColorAtLineStart[r]];
            for (var c = 0; c < CODE_BG0 + 4; c++)
                _fgCodeColor[c] = _bgCodeColor[c];
            _fgCodeColor[CODE_NONE] = transparent;
            var serial = _lineSerial;
            var x = start;
            while (x < end)
            {
                if (_lineRepeatMark[x] == serial && x >= start + 8 && x + 8 <= end)
                {
                    var bg = _lineBgPixels;
                    var fg = _lineFgPixels;
                    for (var k = 0; k < 8; k++)
                    {
                        bg[x + k] = bg[x - 8 + k];
                        fg[x + k] = fg[x - 8 + k];
                    }
                    x += 8;
                    continue;
                }
                _lineBgPixels[x] = _bgCodeColor[_lineBgCodes[x]];
                _lineFgPixels[x] = _fgCodeColor[_lineFgCodes[x]];
                x++;
            }
        }
        else
        {
            Span<byte> registerColor = stackalloc byte[4];
            Span<int> next = stackalloc int[4];
            for (var r = 0; r < 4; r++)
            {
                registerColor[r] = _bgColorAtLineStart[r];
                next[r] = 0;
            }
            for (var x = start; x < end; x++)
            {
                for (var r = 0; r < 4; r++)
                {
                    var n = next[r];
                    while (n < _bgColorEventCount[r] && _bgColorEventX[r * BG_COLOR_EVENT_CAPACITY + n] <= x)
                    {
                        registerColor[r] = _bgColorEventColor[r * BG_COLOR_EVENT_CAPACITY + n];
                        n++;
                    }
                    next[r] = n;
                }
                var bg = _lineBgCodes[x];
                _lineBgPixels[x] = _c64ToRenderColorMap[bg < CODE_BG0 ? bg : registerColor[bg - CODE_BG0]];
                var fg = _lineFgCodes[x];
                _lineFgPixels[x] = fg == CODE_NONE ? transparent : _c64ToRenderColorMap[fg < CODE_BG0 ? fg : registerColor[fg - CODE_BG0]];
            }
        }

        var index = ypos * _width + start;
        _setBackgroundPixels(_lineBgPixels, start, index, end - start);
        _setForegroundPixels(_lineFgPixels, start, index, end - start);
    }

}
