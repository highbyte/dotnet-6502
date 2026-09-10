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
    // Vic2.RegisterWriteObserver) and consumed cycle by cycle in OnAfterInstruction, which then
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

    // When true, sprites are rendered per raster line during OnAfterInstruction (enables
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

    // What each pixel-array line shows of each sprite. Index = line * SPRITE_COUNT + sprite.
    private byte[] _lineSpriteMask = default!;      // per line: bit n set when sprite n shows on it
    private byte[] _lineSpriteData = default!;      // the three fetched bytes (index * 3)
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

    private readonly bool[] _spriteShownThisFrame = new bool[SPRITE_COUNT]; // gates the end-of-frame fallback

    // Start-of-line snapshot of what the VIC-II displays on the line: the sprites whose display
    // is on and the bytes fetched for them (from the shared system-layer snapshot), with each
    // sprite's X and shape flags read at the same moment. Reading these live at draw-time instead
    // would sample the CPU "ahead" of the line being drawn (the draw runs once the next line has
    // started).
    private byte _slDisplayMask;
    private readonly byte[] _slData = new byte[SPRITE_COUNT * SPRITE_ROW_BYTES];
    private readonly int[] _slX = new int[SPRITE_COUNT];
    private readonly byte[] _slFlags = new byte[SPRITE_COUNT];

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
        _rasterToScreenLine = new int[_c64.Vic2.Vic2Model.TotalHeight];
        for (var line = 0; line < _rasterToScreenLine.Length; line++)
            _rasterToScreenLine[line] = _c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(line);
        _lineSpriteMask = new byte[_height];
        _lineSpriteData = new byte[_height * SPRITE_COUNT * SPRITE_ROW_BYTES];
        _lineSpriteX = new int[_height * SPRITE_COUNT];
        _lineSpriteFlags = new byte[_height * SPRITE_COUNT];
        _lineSpriteColorFg = new uint[_height * SPRITE_COUNT];
        _lineSpriteColorMc0 = new uint[_height * SPRITE_COUNT];
        _lineSpriteColorMc1 = new uint[_height * SPRITE_COUNT];
        _lineSpriteClipStartX = new int[_height * SPRITE_COUNT];
        _lineSpriteClipEndX = new int[_height * SPRITE_COUNT];
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
            default:
                break;
        }
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
    public void OnAfterInstruction()
    {
        if (_registerWritesOverflowed)
            ResyncColorRegisters();

        // Loop cycles since last time we processed (each instruction). The line and the cycle
        // within it are derived once and then counted along: a division per cycle would be a
        // large share of the frame on its own.
        var endCycle = _c64.Vic2.CyclesConsumedCurrentVblank;
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
                _lineClearStartX = _mainBorder ? int.MaxValue : 0;
                _lineClearEndX = _width;
                _borderRunStartX = _mainBorder ? 0 : -1;
                _bgColorEventCount[0] = _bgColorEventCount[1] = _bgColorEventCount[2] = _bgColorEventCount[3] = 0;
                _bgColorAtLineStart[0] = _backgroundColor0;
                _bgColorAtLineStart[1] = _backgroundColor1;
                _bgColorAtLineStart[2] = _backgroundColor2;
                _bgColorAtLineStart[3] = _backgroundColor3;
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
                // rows are raster lines 0-5, which are drawn before that first visible line and
                // would be wiped by a whole-frame clear there.
                _clearForegroundPixels((screenLine - _screenLayoutInclNonVisibleTopBorderStartY) * _width, _width);

                if (screenLine - _screenLayoutInclNonVisibleTopBorderStartY == 0)
                {
                    // New frame: VCBASE is reset once per frame outside the bad line range (rule 1,
                    // the chip presumably does it in line 0); the frame's first visible line is
                    // outside that range on both models, and the lines above it are not processed.
                    _vcBase = 0;

                    // New frame: the fallback's gate starts over.
                    if (_perLineSprites)
                        Array.Clear(_spriteShownThisFrame, 0, _spriteShownThisFrame.Length);
                }

                _lineVerticalBorder = _c64.Vic2.GetLineDisplayState(rasterLine).VerticalBorder;
                _baLowSince = -1;

                // Colour registers are not sampled here: they follow the register write journal.

                // The 38/24 column and row selections are not sampled here: CSEL goes through the
                // register write journal into the border unit, RSEL into the per-line vertical state.

                // Take the sprites the VIC-II displays on this line from the shared system-layer
                // snapshot (captured in Vic2.AdvanceRaster earlier this same instruction, the single
                // source of truth shared with per-line collision), with each one's X and shape flags
                // as they stand now. DrawSpritesForLine consumes these when this line is finalized
                // (on entry to the next line).
                if (_perLineSprites)
                {
                    // This line's record starts empty (a per-line clear rather than one per frame,
                    // since on NTSC the visible frame's last rows are raster lines 0-5, recorded
                    // before the frame's first visible line).
                    var lineIndex = screenLine - _screenLayoutInclNonVisibleTopBorderStartY;
                    if (lineIndex >= 0 && lineIndex < _height)
                        _lineSpriteMask[lineIndex] = 0;

                    var spriteManager = _c64.Vic2.SpriteManager;
                    _slDisplayMask = spriteManager.LineSpriteDisplayMask(rasterLine);
                    if (_slDisplayMask != 0)
                    {
                        var sprites = spriteManager.Sprites;
                        for (int i = 0; i < SPRITE_COUNT; i++)
                        {
                            if ((_slDisplayMask & (1 << i)) == 0)
                                continue;
                            spriteManager.LineSpriteData(rasterLine, i).CopyTo(_slData.AsSpan(i * SPRITE_ROW_BYTES, SPRITE_ROW_BYTES));
                            var sprite = sprites[i];
                            _slX[i] = SpriteScreenX(sprite.X);
                            _slFlags[i] = (byte)((sprite.DoubleWidth ? LineSpriteFlagDoubleWidth : 0)
                                | (sprite.Multicolor ? LineSpriteFlagMultiColor : 0)
                                | (sprite.PriorityOverForeground ? LineSpriteFlagPriority : 0));
                        }
                    }
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

        _lastCyclesConsumedCurrentVblank = _c64.Vic2.CyclesConsumedCurrentVblank;
    }

    public void OnEndFrame()
    {
        // Complete the last line drawn, then take the colour registers as they stand for the next
        // frame (also covers values set without going through the memory map, e.g. a snapshot).
        FinishLineRuns();
        ResyncColorRegisters();

        // Per-line mode draws sprites during OnAfterInstruction; skip the end-of-frame pass.
        if (!_perLineSprites)
        {
            DrawSpritesToBitmapBackedByPixelArray();
            return;
        }

        // Fallback: an enabled sprite the VIC-II never displayed this frame (e.g. its Y was written
        // too late from the main loop, past its display line) is drawn at its settled end-of-frame
        // position - matching the old end-of-frame path so the per-line path is never worse than
        // it. Sprite 7 first so that sprite 0, written last, lands on top where records overlap.
        var sprites = _c64.Vic2.SpriteManager.Sprites;
        for (int i = SPRITE_COUNT - 1; i >= 0; i--)
        {
            if (_spriteShownThisFrame[i] || !sprites[i].Visible)
                continue;
            RecordSettledSprite(sprites[i]);
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
        if (_slDisplayMask == 0)
            return;
        var pixelArrayY = screenLine - _screenLayoutInclNonVisibleTopBorderStartY;
        if (pixelArrayY < 0 || pixelArrayY >= _height)
            return;

        var sprites = _c64.Vic2.SpriteManager.Sprites;
        var mc0 = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0)];
        var mc1 = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1)];
        var clipStartX = _lineClearStartXs[pixelArrayY];
        var clipEndX = _lineClearEndXs[pixelArrayY];
        _lineSpriteMask[pixelArrayY] |= _slDisplayMask;
        for (int spriteIndex = 0; spriteIndex < SPRITE_COUNT; spriteIndex++)
        {
            if ((_slDisplayMask & (1 << spriteIndex)) == 0)
                continue;
            _spriteShownThisFrame[spriteIndex] = true;
            var index = pixelArrayY * SPRITE_COUNT + spriteIndex;
            _lineSpriteData[index * SPRITE_ROW_BYTES] = _slData[spriteIndex * SPRITE_ROW_BYTES];
            _lineSpriteData[index * SPRITE_ROW_BYTES + 1] = _slData[spriteIndex * SPRITE_ROW_BYTES + 1];
            _lineSpriteData[index * SPRITE_ROW_BYTES + 2] = _slData[spriteIndex * SPRITE_ROW_BYTES + 2];
            _lineSpriteX[index] = _slX[spriteIndex];
            _lineSpriteFlags[index] = _slFlags[spriteIndex];
            _lineSpriteColorFg[index] = _c64ToRenderColorMap[sprites[spriteIndex].Color];
            _lineSpriteColorMc0[index] = mc0;
            _lineSpriteColorMc1[index] = mc1;
            _lineSpriteClipStartX[index] = clipStartX;
            _lineSpriteClipEndX[index] = clipEndX;
        }
    }

    /// <summary>
    /// Records a sprite at its settled position with its current shape, rows on consecutive lines
    /// (two per row when Y-expanded), for the end-of-frame fallback.
    /// </summary>
    private void RecordSettledSprite(Vic2Sprite sprite)
    {
        var spriteIndex = sprite.SpriteNumber;
        var bit = (byte)(1 << spriteIndex);
        var x = SpriteScreenX(sprite.X);
        var flags = (byte)((sprite.DoubleWidth ? LineSpriteFlagDoubleWidth : 0)
            | (sprite.Multicolor ? LineSpriteFlagMultiColor : 0)
            | (sprite.PriorityOverForeground ? LineSpriteFlagPriority : 0));
        var fg = _c64ToRenderColorMap[sprite.Color];
        var mc0 = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0)];
        var mc1 = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1)];
        var lineAdvance = sprite.DoubleHeight ? 2 : 1;
        var spriteData = sprite.Data;
        var pixelArrayY = sprite.Y + _screenStartY - _spriteScreenOffsetY;
        for (int row = 0; row < SPRITE_ROWS; row++)
        {
            var rowBytes = spriteData.Rows[row].Bytes;
            for (int repeat = 0; repeat < lineAdvance; repeat++, pixelArrayY++)
            {
                if (pixelArrayY < 0 || pixelArrayY >= _height)
                    continue;
                var index = pixelArrayY * SPRITE_COUNT + spriteIndex;
                _lineSpriteMask[pixelArrayY] |= bit;
                _lineSpriteData[index * SPRITE_ROW_BYTES] = rowBytes[0];
                _lineSpriteData[index * SPRITE_ROW_BYTES + 1] = rowBytes[1];
                _lineSpriteData[index * SPRITE_ROW_BYTES + 2] = rowBytes[2];
                _lineSpriteX[index] = x;
                _lineSpriteFlags[index] = flags;
                _lineSpriteColorFg[index] = fg;
                _lineSpriteColorMc0[index] = mc0;
                _lineSpriteColorMc1[index] = mc1;
                _lineSpriteClipStartX[index] = _lineClearStartXs[pixelArrayY];
                _lineSpriteClipEndX[index] = _lineClearEndXs[pixelArrayY];
            }
        }
    }

    /// <summary>
    /// Draws the recorded sprite lines. Sprite 7 first on each line so that sprite 0, the highest
    /// priority, lands on top.
    /// </summary>
    private void DrawSpriteLines()
    {
        for (int pixelArrayY = 0; pixelArrayY < _height; pixelArrayY++)
        {
            var mask = _lineSpriteMask[pixelArrayY];
            if (mask == 0)
                continue;
            for (int spriteIndex = SPRITE_COUNT - 1; spriteIndex >= 0; spriteIndex--)
            {
                if ((mask & (1 << spriteIndex)) == 0)
                    continue;
                var index = pixelArrayY * SPRITE_COUNT + spriteIndex;
                var rowBytes = _lineSpriteData.AsSpan(index * SPRITE_ROW_BYTES, SPRITE_ROW_BYTES);
                if (rowBytes[0] == 0 && rowBytes[1] == 0 && rowBytes[2] == 0)
                    continue;
                var flags = _lineSpriteFlags[index];
                DecodeAndWriteSpriteRow(
                    rowBytes,
                    _lineSpriteX[index],
                    pixelArrayY,
                    (flags & LineSpriteFlagDoubleWidth) != 0,
                    (flags & LineSpriteFlagMultiColor) != 0,
                    (flags & LineSpriteFlagPriority) != 0,
                    _lineSpriteColorFg[index],
                    _lineSpriteColorMc0[index],
                    _lineSpriteColorMc1[index],
                    _lineSpriteClipStartX[index],
                    _lineSpriteClipEndX[index],
                    0,
                    _height);
            }
        }
    }

    /// <summary>
    /// Decodes one sprite shape row (3 bytes / 24 px) and writes its pixels at (destX, destY).
    /// Shared by both the end-of-frame (per-frame) and per-line (band) sprite paths - the only
    /// difference between them is which producer supplies the position, shape and per-row colours.
    /// Handles single/multi colour and X expansion; the caller handles Y expansion (calls twice).
    /// </summary>
    private void DecodeAndWriteSpriteRow(ReadOnlySpan<byte> rowBytes, int destX, int destY, bool isDoubleWidth, bool isMultiColor, bool priorityOverForeground, uint spriteForegroundPixelColor, uint spriteMultiColor0PixelColor, uint spriteMultiColor1PixelColor, int clipStartX, int clipEndX, int clipStartY, int clipEndY)
    {
        var singleColorPixelAdvance = isDoubleWidth ? 2 : 1;
        var multiColorPixelAdvance = isDoubleWidth ? 4 : 2;
        var spriteLinePartAdvance = isDoubleWidth ? 16 : 8;

        var x = 0;
        for (int byteIndex = 0; byteIndex < SPRITE_ROW_BYTES; byteIndex++)
        {
            var spriteLinePart = rowBytes[byteIndex];
            if (spriteLinePart == 0) { x += spriteLinePartAdvance; continue; }

            if (isMultiColor)
            {
                var maskMultiColor0Mask = 0b01000000;
                var maskSpriteColorMask = 0b10000000;
                var maskMultiColor1Mask = 0b11000000;

                for (var pixel = 0; pixel < 8; pixel += 2)
                {
                    uint spriteColor;
                    if ((spriteLinePart & maskMultiColor1Mask) == maskMultiColor1Mask)
                        spriteColor = spriteMultiColor1PixelColor;
                    else if ((spriteLinePart & maskSpriteColorMask) == maskSpriteColorMask)
                        spriteColor = spriteForegroundPixelColor;
                    else if ((spriteLinePart & maskMultiColor0Mask) == maskMultiColor0Mask)
                        spriteColor = spriteMultiColor0PixelColor;
                    else
                        spriteColor = 0;

                    if (spriteColor > 0)
                    {
                        WriteSpritePixel(destX + x, destY, spriteColor, priorityOverForeground, clipStartX, clipEndX, clipStartY, clipEndY);
                        WriteSpritePixel(destX + x + 1, destY, spriteColor, priorityOverForeground, clipStartX, clipEndX, clipStartY, clipEndY);
                        if (isDoubleWidth)
                        {
                            WriteSpritePixel(destX + x + 2, destY, spriteColor, priorityOverForeground, clipStartX, clipEndX, clipStartY, clipEndY);
                            WriteSpritePixel(destX + x + 3, destY, spriteColor, priorityOverForeground, clipStartX, clipEndX, clipStartY, clipEndY);
                        }
                    }

                    maskMultiColor0Mask >>= 2;
                    maskMultiColor1Mask >>= 2;
                    maskSpriteColorMask >>= 2;
                    x += multiColorPixelAdvance;
                }
            }
            else
            {
                var mask = 0b10000000;
                for (var pixel = 0; pixel < 8; pixel++)
                {
                    if ((spriteLinePart & mask) == mask)
                    {
                        WriteSpritePixel(destX + x, destY, spriteForegroundPixelColor, priorityOverForeground, clipStartX, clipEndX, clipStartY, clipEndY);
                        if (isDoubleWidth)
                            WriteSpritePixel(destX + x + 1, destY, spriteForegroundPixelColor, priorityOverForeground, clipStartX, clipEndX, clipStartY, clipEndY);
                    }
                    mask >>= 1;
                    x += singleColorPixelAdvance;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpritePixel(int screenPosX, int screenPosY, uint color, bool priorityOverForeground, int clipStartX, int clipEndX, int clipStartY, int clipEndY)
    {
        if (screenPosX < 0 || screenPosX >= _width || screenPosY < 0 || screenPosY > _height)
            return;
        if (screenPosX < clipStartX || screenPosX >= clipEndX)   // side borders closed (TODO: open)
            return;
        if (screenPosY < clipStartY || screenPosY >= clipEndY)   // top/bottom borders closed (TODO: open)
            return;

        if (FlipY)
            screenPosY = _height - screenPosY - 1;

        var bitmapIndex = screenPosY * _width + screenPosX;
        // priorityOverForeground => foreground layer (on top of text/bitmap), else background layer.
        _setPixel(color, bitmapIndex, priorityOverForeground);
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
        var loadSlot = (cycle + 1) % 3;   // the entry fetched two cycles ago: (cycle - 2) mod 3

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

        // The cycle's accesses and counter work (3.7.2), then the fetched byte into the pipeline for
        // the output two cycles on. Cycle numbers below count from 1 as the article does; `cycle`
        // counts from 0.
        var chipCycle = cycle + 1;
        var badLineCondition = rasterLine >= BadLineFirstRasterLine && rasterLine <= BadLineLastRasterLine
            && (rasterLine & 7) == (_d011 & 7) && _c64.Vic2.DisplayEnabledThisFrame;
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
        var fetchSlot = cycle % 3;
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
            _fetchedMatrix[fetchSlot] = _fetchedMatrix[(cycle + 2) % 3];
            _fetchedColor[fetchSlot] = _fetchedColor[(cycle + 2) % 3];
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
                _fetchedData[fetchSlot] = _c64.Vic2.ReadMemory((ushort)((_d011 & 0x40) != 0 ? 0x39FF : 0x3FFF));
            }
        }
        else
        {
            _fetchedData[fetchSlot] = 0;
            _fetchedMatrix[fetchSlot] = _fetchedMatrix[(cycle + 2) % 3];
            _fetchedColor[fetchSlot] = _fetchedColor[(cycle + 2) % 3];
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
        if ((_d011 & 0x20) != 0)
        {
            var bitmapBase = (_d018 & 0x08) << 10;
            _fetchedData[slot] = _c64.Vic2.ReadMemory((ushort)(bitmapBase + _vc * 8 + _rc));
        }
        else
        {
            var characterSetBase = (_d018 & 0x0E) << 10;
            var shape = (_d011 & 0x40) != 0 ? screenCode & 0x3F : screenCode;
            _fetchedData[slot] = _c64.Vic2.ReadMemory((ushort)(characterSetBase + shape * _vic2ScreenCharacterHeight + _rc));
        }
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
            for (var x = start; x < end; x++)
            {
                _lineBgPixels[x] = _bgCodeColor[_lineBgCodes[x]];
                _lineFgPixels[x] = _fgCodeColor[_lineFgCodes[x]];
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
