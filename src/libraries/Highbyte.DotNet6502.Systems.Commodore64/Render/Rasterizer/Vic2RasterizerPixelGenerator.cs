using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.ColorMaps;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;

public sealed class Vic2RasterizerUintPixelGenerator
{
    private readonly C64 _c64;
    // Arrays of color for C64 screen to render to
    //private readonly uint[] PixelArray_BackgroundAndBorder;
    //private readonly uint[] PixelArray_Foreground;

    private uint[] _c64ToRenderColorMap;
    private uint TransparentColor { get; }
    private bool FlipY { get; }


    // Pre-calculated pixel arrays
    private uint[][] _oneLineSameColorPixels; // pixelArray
    private uint[] _oneLineTransparentPixels = default!; // a line of the transparent color, used to clear the foreground layer

    // Text standard mode: 8-bit patterns mapped to 8 pixels (1 pixel = 1 uint rgba).
    // 1 maps to the color in the lookup table, and 0 maps to a predefined "background" color that will be replaced in shader.
    private uint[][] _eightPixelsOneColorAndBackground;

    // Text extended and bitmap "Standard" (HiRes) mode: 8-bit patterns mapped to 8 pixels (1 pixel = 1 uint rgba).
    // 1 and 0 maps to the two colors in the lookup table.
    private uint[][] _eightPixelsTwoColors;

    // For text and bitmap mode "Multicolor": 8-bit patterns mapped to 4 width 2 pixels (1 pixel = 1 uint rgba).
    // 01, 10, and 11 maps to the colors in the lookup table, and 00 maps to a predefined "background" color that will be replaced in shader.
    private uint[][] _eightPixelsThreeColorsAndBackground;


    // Line render state
    private int _lastScreenLineDataUpdate = -1;

    // The character row's 40 screen codes and colour nibbles, as the VIC-II holds them: it fetches
    // them once per row, on the row's first line (the bad line), and displays them for the row's
    // remaining seven lines whatever the CPU writes meanwhile. A row is latched once a full line of
    // it has been read live (normally its first line).
    private readonly byte[] _rowScreenCodes = new byte[40];
    private readonly byte[] _rowColorRam = new byte[40];
    private int _latchedCharacterRow = -1;   // row whose latch is complete (all 40 columns fetched)
    private int _fetchingCharacterRow = -1;  // row currently being fetched live
    private ulong _fetchedColumnsMask;       // columns of that row fetched so far
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
    private ushort _vic2VideoMatrixBaseAddress;
    private ushort _vic2BitmapBaseAddress;
    private ushort _vic2CharacterSetAddressInVIC2Bank;
    private bool _isTextMode;
    private CharMode _characterMode;
    private BitmMode _bitmapMode;
    private bool _invalidMode; // ECM combined with BMM/MCM: VIC-II outputs black for the display area.
    private int _scrollX;

    // The VIC-II's vertical state for the line being drawn, as it settled it when the raster
    // entered the line (see Vic2LineDisplayState). Which row is drawn and which of its lines is
    // not arithmetic on the raster line: rows start where bad lines occur, the chip drops to idle
    // state after a row's eighth line until the next bad line, and the vertical border flip-flop
    // decides whether the line shows graphics at all. That is what makes vertical fine scroll,
    // FLD-style row stretching, a switched-off display and an opened border come out as on hardware.
    private bool _lineDisplayState;
    private bool _lineVerticalBorder = true;
    private int _lineVideoCounterBase;
    private int _lineRowCounter;

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

    // The background layer's border and (standard text mode) background are drawn as runs along
    // the line: a run is closed where a colour write lands and the rest of the line is drawn when
    // the line ends, so a line without colour writes still costs one copy per border part.
    private int _runLine = -1;          // screen line (Visible layout) whose runs are open
    private int _borderRunStartX;       // normalized x where the open border run starts
    private int _backgroundRunStartX;   // normalized x where the open background run starts

    // --- The border unit (main border flip-flop), per pixel.
    // The VIC-II shows border colour wherever its main border flip-flop is set. The flip-flop is
    // set when the X coordinate reaches the right compare value (344 with 40 columns, 335 with 38)
    // and reset when it reaches the left one (24 or 31) while the vertical border flip-flop is
    // clear; nothing else touches it, so it carries over from one line to the next and from one
    // frame to the next. The ordinary 40 and 38 column layouts are what those rules produce on a
    // line where nothing changes; a program that has 40 columns selected at the 335 compare and 38
    // at the 344 compare misses both and keeps the side borders open on that line and the left
    // border of the next. CSEL follows the register write journal, at the cycle boundary after
    // the write; XSCROLL and the mode bits are still sampled once per line.
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

    // Per-line sprite display state machine (mirrors the VIC-II sprite display latch).
    //
    // Design: the per-line pass only *detects latches* (a sprite's Y matching the raster line) and
    // records a "band" - one displayed run of a hardware sprite - capturing its position, shape,
    // geometry and colors at that moment. The actual pixels are drawn at end-of-frame, after the
    // whole main screen is rendered. This is essential: the main-screen character foreground is
    // written scroll-adjusted (ypos += GetScrollY(), which is -3..+4), so with a negative fine
    // scroll it writes *upward* into rows below the current line. If sprites were composited inline
    // per line, that later main-screen write would clobber them (the cause of sprites vanishing at
    // certain vertical scroll positions). Drawing all bands last makes them immune - exactly why
    // the old end-of-frame path never had the problem - while one band per latch still reproduces
    // multiplexing.
    private const int SPRITE_COUNT = 8;
    private const int SPRITE_ROWS = Vic2Sprite.DEFAULT_HEIGTH;         // 21
    private const int SPRITE_ROW_BYTES = Vic2Sprite.DEFAULT_WIDTH / 8; // 3

    // Active-run gating: prevents a hardware sprite from re-latching until its 21-row run completes.
    private readonly bool[] _spriteActive = new bool[SPRITE_COUNT];
    private readonly int[] _spriteRow = new int[SPRITE_COUNT];            // logical row 0..20
    private readonly bool[] _spriteExpandYPhase = new bool[SPRITE_COUNT]; // double-height: each row on 2 lines
    private readonly bool[] _spriteActiveDoubleHeight = new bool[SPRITE_COUNT];
    private readonly bool[] _spriteHadBandThisFrame = new bool[SPRITE_COUNT]; // gate the end-of-frame fallback

    // Recorded bands to draw at end-of-frame. Parallel arrays indexed 0.._bandCount.
    private const int MAX_BANDS = 128; // ~ SPRITE_COUNT * (visible lines / SPRITE_ROWS), plus fallbacks
    private readonly byte[] _bandShape = new byte[MAX_BANDS * SPRITE_ROWS * SPRITE_ROW_BYTES];
    private readonly uint[] _bandNonEmpty = new uint[MAX_BANDS];
    private readonly int[] _bandRowStart = new int[MAX_BANDS]; // pixel-array row of the band's row 0
    private readonly int[] _bandX = new int[MAX_BANDS];        // already in pixel-array coords
    private readonly bool[] _bandDoubleWidth = new bool[MAX_BANDS];
    private readonly bool[] _bandDoubleHeight = new bool[MAX_BANDS];
    private readonly bool[] _bandMultiColor = new bool[MAX_BANDS];
    private readonly bool[] _bandPriority = new bool[MAX_BANDS];
    // Per-row colors (index = band * SPRITE_ROWS + row): captured per raster line as the band
    // displays, so an intra-sprite per-raster colour change (striped sprites, per-raster
    // $D025/$D026 swaps) is preserved - matching the end-of-frame path's per-line colour read.
    private readonly uint[] _bandRowColorFg = new uint[MAX_BANDS * SPRITE_ROWS];
    private readonly uint[] _bandRowColorMc0 = new uint[MAX_BANDS * SPRITE_ROWS];
    private readonly uint[] _bandRowColorMc1 = new uint[MAX_BANDS * SPRITE_ROWS];
    // Border clipping can change on raster splits ($D016 38/40 columns, $D011 24/25 rows).
    // Sprite bands are drawn at end-of-frame, so each sprite row keeps the clipping window that was
    // active while that row was displayed.
    private readonly int[] _bandRowClipStartX = new int[MAX_BANDS * SPRITE_ROWS];
    private readonly int[] _bandRowClipEndX = new int[MAX_BANDS * SPRITE_ROWS];
    private readonly int[] _bandRowClipStartY = new int[MAX_BANDS * SPRITE_ROWS];
    private readonly int[] _bandRowClipEndY = new int[MAX_BANDS * SPRITE_ROWS];
    private int _bandCount;

    // Band index of each sprite's currently-displaying band (-1 = none / dropped), so the gate can
    // record each row's live colour into that band as the raster passes.
    private readonly int[] _spriteCurrentBand = new int[SPRITE_COUNT];

    // Start-of-line snapshot of the trigger inputs (enable + Y), captured at the same phase as
    // the border/color snapshot. Reading these live at draw-time instead samples the CPU "ahead"
    // of the line being drawn (the draw runs once the next line has started). The enable bits are
    // kept as the raw $D015 mask (read once per line) and Y is only sampled for enabled sprites.
    private byte _slEnableMask;
    private readonly int[] _slY = new int[SPRITE_COUNT];

    public Vic2RasterizerUintPixelGenerator(
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
        nameof(_oneLineSameColorPixels),
        nameof(_eightPixelsOneColorAndBackground),
        nameof(_eightPixelsTwoColors),
        nameof(_eightPixelsThreeColorsAndBackground))]
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

    // Take the colour registers as they are now and forget any pending writes.
    private void ResyncColorRegisters()
    {
        _borderColor = _c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);
        _backgroundColor0 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);
        _backgroundColor1 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1);
        _backgroundColor2 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2);
        _backgroundColor3 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3);
        _csel40 = _c64.Vic2.Is38ColumnDisplayEnabled == false;
        _registerWriteCount = 0;
        _registerWriteNext = 0;
        _registerWritesOverflowed = false;
    }

    // Apply a journaled register write at the pixel position (normalized x on the open line)
    // from which it is visible. The journal carries the value as written; the colour registers
    // keep only their low four bits.
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
                if (_backgroundColor0 != color)
                {
                    if (_backgroundRunStartX >= 0)
                    {
                        CloseBackgroundRun(changeX);
                        _backgroundRunStartX = Math.Max(_backgroundRunStartX, changeX);
                    }
                    _backgroundColor0 = color;
                }
                break;
            case Vic2Addr.SCROLL_X_AND_SCREEN_CONTROL_REGISTER:
                // CSEL: in effect from the cycle boundary after the write, with no pipeline: it
                // feeds the compares, not the pixel output.
                _csel40 = (write.Value & 0x08) != 0;
                break;
            case Vic2Addr.BACKGROUND_COLOR_1:
                _backgroundColor1 = color;
                break;
            case Vic2Addr.BACKGROUND_COLOR_2:
                _backgroundColor2 = color;
                break;
            case Vic2Addr.BACKGROUND_COLOR_3:
                _backgroundColor3 = color;
                break;
            default:
                break;
        }
    }

    private bool IsRunLineVisible => _runLine >= _screenLayoutInclNonVisibleTopBorderStartY && _runLine <= _screenLayoutInclNonVisibleBottomBorderEndY;

    // Paint the open border run up to x (it stays open; the caller moves or ends it).
    private void CloseBorderRun(int x)
    {
        if (IsRunLineVisible)
            DrawBorderRun(_runLine - _screenLayoutInclNonVisibleTopBorderStartY, _borderRunStartX, x);
    }

    // Paint the open background run up to x (it stays open; the caller moves or ends it).
    private void CloseBackgroundRun(int x)
    {
        if (IsRunLineVisible)
            DrawBackgroundRun(_runLine - _screenLayoutInclNonVisibleTopBorderStartY, _backgroundRunStartX, x);
    }

    // The right compare: border colour from x on this line, and until the next left compare.
    private void SetMainBorder(int x)
    {
        if (_mainBorder)
            return;
        _mainBorder = true;
        if (x < _lineClearEndX)
            _lineClearEndX = x;
        if (_backgroundRunStartX >= 0)
            CloseBackgroundRun(x);
        _backgroundRunStartX = -1;
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
        _backgroundRunStartX = x;
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
            if (_backgroundRunStartX >= 0)
                DrawBackgroundRun(normalizedLine, _backgroundRunStartX, _width);
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

        // Loop cycles since last time we processed (each instruction)
        for (var cycleCurrentVblank = _lastCyclesConsumedCurrentVblank; cycleCurrentVblank < _c64.Vic2.CyclesConsumedCurrentVblank; cycleCurrentVblank++)
        {
            // For the cycle processed in current loop iteration, get line and x position.
            var rasterLine = (int)(cycleCurrentVblank / _cyclesPerLine);
            var screenLine = _c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
            var cycleOnScreenLine = cycleCurrentVblank % _cyclesPerLine;
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
                _backgroundRunStartX = _mainBorder ? -1 : 0;
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
            // (A cycle just outside the frame is kept when its 8-pixel block on the character grid,
            // which starts 4 pixels into the cycle, reaches into the frame: the opened side border's
            // outermost pixels are drawn from those blocks.)
            if (posX + 16 <= _screenLayoutInclNonVisibleLeftBorderStartX || posX - 12 > _screenLayoutInclNonVisibleRightBorderEndX)
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

                    // New frame: reset the sprite display latch so no sprite carries over.
                    if (_perLineSprites)
                    {
                        Array.Clear(_spriteActive, 0, _spriteActive.Length);
                        Array.Clear(_spriteHadBandThisFrame, 0, _spriteHadBandThisFrame.Length);
                        _bandCount = 0;
                    }
                }

                _vic2VideoMatrixBaseAddress = _c64.Vic2.VideoMatrixBaseAddress;
                _vic2BitmapBaseAddress = _c64.Vic2.BitmapManager.BitmapAddressInVIC2Bank;
                _vic2CharacterSetAddressInVIC2Bank = _c64.Vic2.CharsetManager.CharacterSetAddressInVIC2Bank;

                _isTextMode = _c64.Vic2.DisplayMode == DispMode.Text;
                _characterMode = _c64.Vic2.CharacterMode;
                _bitmapMode = _c64.Vic2.BitmapMode;
                _invalidMode = _c64.Vic2.IsInvalidVideoMode;

                _scrollX = _c64.Vic2.GetScrollX();

                var lineState = _c64.Vic2.GetLineDisplayState(rasterLine);
                _lineDisplayState = lineState.DisplayState;
                _lineVerticalBorder = lineState.VerticalBorder;
                _lineVideoCounterBase = lineState.VideoCounterBase;
                _lineRowCounter = lineState.RowCounter;

                // Colour registers are not sampled here: they follow the register write journal.

                // The 38/24 column and row selections are not sampled here: CSEL goes through the
                // register write journal into the border unit, RSEL into the per-line vertical state.

                // Copy the sprite trigger inputs (enable + Y) for this line from the shared system-layer
                // snapshot (captured in Vic2.AdvanceRaster earlier this same instruction - identical
                // register values, single source of truth shared with per-line collision).
                // DrawSpritesForLine consumes these when this line is finalized (on entry to next line).
                if (_perLineSprites)
                {
                    var spriteManager = _c64.Vic2.SpriteManager;
                    _slEnableMask = spriteManager.LineSpriteEnableMask;
                    if (_slEnableMask != 0)
                    {
                        var lineSpriteY = spriteManager.LineSpriteY;
                        for (int i = 0; i < SPRITE_COUNT; i++)
                        {
                            if ((_slEnableMask & (1 << i)) != 0)
                                _slY[i] = lineSpriteY[i];
                        }
                    }
                }

                _lastScreenLineDataUpdate = screenLine;
            }

            // Graphics show wherever the border flip-flop is clear. The display window's columns
            // start 4 pixels into a cycle and the compares that can clip a column are evaluated a
            // cycle after the cycle they fall in, so column k is drawn two cycles after the one it
            // starts in: by then those compares have been evaluated. Outside the window (a side
            // border a program has opened, or the top and bottom border with the vertical flip-flop
            // kept clear) the sequencer shows its idle output on the same 8-pixel grid.
            if (_lineClearStartX < _width)
            {
                var col = (posX - _screenLayoutInclNonVisibleScreenStartX - 12) >> 3;
                var drawLine = screenLine - _screenLayoutInclNonVisibleScreenStartY;
                if (col >= 0 && col < _vic2ScreenTextCols)
                    DrawTextAndBitmapPixels(_c64, drawLine, col);
                else
                    DrawIdleBlock(_c64, drawLine, col);
            }

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

        // Fallback: any enabled sprite that never latched a band this frame (e.g. its Y was written
        // too late from the main loop, past its display line) is recorded as a band at its settled
        // end-of-frame position - matching the old end-of-frame path so the per-line path is never
        // worse than it. Sprites that did latch keep their per-line (multiplexing) bands.
        var sprites = _c64.Vic2.SpriteManager.Sprites;
        // Iterate high->low so sprite 0 is recorded last (highest band index) and so drawn on top.
        for (int i = SPRITE_COUNT - 1; i >= 0; i--)
        {
            if (_spriteHadBandThisFrame[i] || !sprites[i].Visible)
                continue;
            var settledRow = sprites[i].Y + _screenStartY - _spriteScreenOffsetY;
            RecordBand(sprites[i], settledRow);
        }

        // Now that the whole main screen is rendered, composite all recorded sprite bands on top.
        // Bands are recorded high sprite number first per line, so drawing in ascending index order
        // makes lower sprite numbers (recorded later) land on top within a layer.
        for (int b = 0; b < _bandCount; b++)
            DrawBand(b);
    }

    /// <summary>
    /// Per-raster-line sprite *latch detection*. Called when a raster line is finalized (on entry to
    /// the next line). Implements a VIC-II-like display latch: when the raster reaches a sprite's Y,
    /// the sprite's shape/geometry/colors are recorded as a band (one displayed run). The band is
    /// drawn later, at end-of-frame, after the whole main screen - so fine-scroll main-screen writes
    /// can't clobber it. One band per latch reproduces multiplexing.
    /// </summary>
    private void DrawSpritesForLine(int screenLine)
    {
        var pixelArrayY = screenLine - _screenLayoutInclNonVisibleTopBorderStartY;

        var sprites = _c64.Vic2.SpriteManager.Sprites;
        // Highest sprite number first so lower sprite numbers (recorded later) draw on top.
        for (int spriteIndex = SPRITE_COUNT - 1; spriteIndex >= 0; spriteIndex--)
        {
            // Trigger from the start-of-line snapshot (NOT live registers - see field comment).
            if (!_spriteActive[spriteIndex] && (_slEnableMask & (1 << spriteIndex)) != 0)
            {
                var spriteScreenPosY = _slY[spriteIndex] + _screenStartY - _spriteScreenOffsetY;
                var doubleHeight = sprites[spriteIndex].DoubleHeight;
                // Lines above the visible area are never drawn, so a sprite that begins there
                // (NTSC shows the top border only from raster line 34) is latched on the first
                // visible line instead, with the rows the raster has already passed accounted for.
                var linesPassed = pixelArrayY == 0 && spriteScreenPosY < 0 ? -spriteScreenPosY : 0;
                if (pixelArrayY == spriteScreenPosY || (linesPassed > 0 && linesPassed < SPRITE_ROWS * (doubleHeight ? 2 : 1)))
                {
                    _spriteActive[spriteIndex] = true;
                    _spriteRow[spriteIndex] = doubleHeight ? linesPassed / 2 : linesPassed;
                    _spriteExpandYPhase[spriteIndex] = doubleHeight && (linesPassed & 1) == 1;
                    _spriteActiveDoubleHeight[spriteIndex] = doubleHeight;
                    _spriteHadBandThisFrame[spriteIndex] = true;
                    _spriteCurrentBand[spriteIndex] = _bandCount < MAX_BANDS ? _bandCount : -1;
                    RecordBand(sprites[spriteIndex], spriteScreenPosY);
                }
            }

            if (!_spriteActive[spriteIndex])
                continue;

            // Capture this row's live sprite colours into the band (preserves intra-sprite per-raster
            // colour changes). Done per line while displaying, like the end-of-frame colour read.
            var curBand = _spriteCurrentBand[spriteIndex];
            if (curBand >= 0)
            {
                var ci = curBand * SPRITE_ROWS + _spriteRow[spriteIndex];
                _bandRowColorFg[ci] = _c64ToRenderColorMap[sprites[spriteIndex].Color];
                _bandRowColorMc0[ci] = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0)];
                _bandRowColorMc1[ci] = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1)];
                // The border covers sprites too: a row shows only where this line's border
                // flip-flop was clear (nowhere on a line the vertical border covers, out into a
                // side border a program has opened). The line is finished, so its span is stored.
                _bandRowClipStartX[ci] = _lineClearStartXs[pixelArrayY];
                _bandRowClipEndX[ci] = _lineClearEndXs[pixelArrayY];
                _bandRowClipStartY[ci] = 0;
                _bandRowClipEndY[ci] = _height;
            }

            // Advance the active-run gate (double-height keeps each row for 2 lines). This only
            // gates re-latching; the pixels are drawn from the recorded band at end-of-frame.
            if (_spriteActiveDoubleHeight[spriteIndex] && !_spriteExpandYPhase[spriteIndex])
            {
                _spriteExpandYPhase[spriteIndex] = true;
            }
            else
            {
                _spriteExpandYPhase[spriteIndex] = false;
                _spriteRow[spriteIndex]++;
                if (_spriteRow[spriteIndex] >= SPRITE_ROWS)
                    _spriteActive[spriteIndex] = false;
            }
        }
    }

    /// <summary>
    /// Records one sprite band (a displayed run) to be drawn at end-of-frame: snapshots shape,
    /// geometry, colors and the pixel-array row of the band's first row.
    /// </summary>
    private void RecordBand(Vic2Sprite sprite, int rowStart)
    {
        if (_bandCount >= MAX_BANDS)
            return;

        var b = _bandCount;
        _bandRowStart[b] = rowStart;
        _bandX[b] = SpriteScreenX(sprite.X);
        _bandDoubleWidth[b] = sprite.DoubleWidth;
        _bandDoubleHeight[b] = sprite.DoubleHeight;
        _bandMultiColor[b] = sprite.Multicolor;
        _bandPriority[b] = sprite.PriorityOverForeground;

        // Default every row to the latch-time colours, so rows the gate never reaches (a cut-short
        // band) still have a sane colour. The gate overwrites each row's colour as it displays.
        // The default clip is the span where the row's own frame line last had the border
        // flip-flop clear: that covers rows the gate never reaches because the raster frame ends
        // first (on NTSC the visible frame's last rows are raster lines 0-5 of the next frame) and
        // sprites that never latched this frame.
        var fg = _c64ToRenderColorMap[sprite.Color];
        var mc0 = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0)];
        var mc1 = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1)];
        var colorBase = b * SPRITE_ROWS;
        var lineAdvance = sprite.DoubleHeight ? 2 : 1;
        for (int row = 0; row < SPRITE_ROWS; row++)
        {
            _bandRowColorFg[colorBase + row] = fg;
            _bandRowColorMc0[colorBase + row] = mc0;
            _bandRowColorMc1[colorBase + row] = mc1;
            var frameRow = rowStart + row * lineAdvance;
            var inFrame = frameRow >= 0 && frameRow < _height;
            _bandRowClipStartX[colorBase + row] = inFrame ? _lineClearStartXs[frameRow] : _width;
            _bandRowClipEndX[colorBase + row] = inFrame ? _lineClearEndXs[frameRow] : _width;
            _bandRowClipStartY[colorBase + row] = 0;
            _bandRowClipEndY[colorBase + row] = _height;
        }

        // Snapshot shape so a later pointer change (next band) can't corrupt this one.
        var spriteData = sprite.Data;
        _bandNonEmpty[b] = spriteData.NonEmptyRowMask;
        var shapeBase = b * SPRITE_ROWS * SPRITE_ROW_BYTES;
        for (int row = 0; row < SPRITE_ROWS; row++)
        {
            var rowBytes = spriteData.Rows[row].Bytes;
            var rowOffset = shapeBase + row * SPRITE_ROW_BYTES;
            for (int by = 0; by < SPRITE_ROW_BYTES; by++)
                _bandShape[rowOffset + by] = rowBytes[by];
        }
        _bandCount++;
    }

    /// <summary>Draws all 21 rows of a recorded band into the layers (called at end-of-frame).</summary>
    private void DrawBand(int b)
    {
        var nonEmpty = _bandNonEmpty[b];
        if (nonEmpty == 0)
            return;

        var shapeBase = b * SPRITE_ROWS * SPRITE_ROW_BYTES;
        var colorBase = b * SPRITE_ROWS;
        var destX = _bandX[b];
        var isDoubleWidth = _bandDoubleWidth[b];
        var isMultiColor = _bandMultiColor[b];
        var priority = _bandPriority[b];
        var lineAdvance = _bandDoubleHeight[b] ? 2 : 1;
        var pixelArrayY = _bandRowStart[b];
        for (int row = 0; row < SPRITE_ROWS; row++)
        {
            if ((nonEmpty & (1u << row)) != 0)
            {
                var rowBytes = _bandShape.AsSpan(shapeBase + row * SPRITE_ROW_BYTES, SPRITE_ROW_BYTES);
                var fg = _bandRowColorFg[colorBase + row];
                var mc0 = _bandRowColorMc0[colorBase + row];
                var mc1 = _bandRowColorMc1[colorBase + row];
                var clipStartX = _bandRowClipStartX[colorBase + row];
                var clipEndX = _bandRowClipEndX[colorBase + row];
                var clipStartY = _bandRowClipStartY[colorBase + row];
                var clipEndY = _bandRowClipEndY[colorBase + row];
                DecodeAndWriteSpriteRow(rowBytes, destX, pixelArrayY, isDoubleWidth, isMultiColor, priority, fg, mc0, mc1, clipStartX, clipEndX, clipStartY, clipEndY);
                if (lineAdvance == 2)
                    DecodeAndWriteSpriteRow(rowBytes, destX, pixelArrayY + 1, isDoubleWidth, isMultiColor, priority, fg, mc0, mc1, clipStartX, clipEndX, clipStartY, clipEndY);
            }
            pixelArrayY += lineAdvance;
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

    [MemberNotNull(
        nameof(_oneLineSameColorPixels),
        nameof(_eightPixelsOneColorAndBackground),
        nameof(_eightPixelsTwoColors),
        nameof(_eightPixelsThreeColorsAndBackground))]
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

        var transparentColorVal = TransparentColor;

        // A single line of the transparent color. Used to clear the foreground layer (e.g. invalid mode).
        _oneLineTransparentPixels = new uint[width];
        for (var i = 0; i < _oneLineTransparentPixels.Length; i++)
            _oneLineTransparentPixels[i] = transparentColorVal;

        // Text (normal) & bitmap (standard "HiRes") mode with one foreground color with a single "transparent" color as background color
        // 8 bits => 8 pixels
        _eightPixelsOneColorAndBackground = new uint[256 * 16][];
        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte bitmapFgColorCode = 0; bitmapFgColorCode < 16; bitmapFgColorCode++)
            {
                var bitmapFgColorVal = _c64ToRenderColorMap[bitmapFgColorCode];

                // Standard (Hires) mode, 8 bits => 8 pixels. 2 "foreground" colors (fg color and bg color from text screen). No background color that will be replaced in shader.
                var bitmapPixels = new uint[8];
                for (var pixelPos = 0; pixelPos < 8; pixelPos++)
                {
                    // If bit is set, use foreground color, else use background color
                    var isBitSet = (pixelPattern & 1 << 7 - pixelPos) != 0;
                    if (isBitSet)
                        bitmapPixels[pixelPos] = bitmapFgColorVal;
                    else
                        bitmapPixels[pixelPos] = transparentColorVal;
                }
                _eightPixelsOneColorAndBackground[GetOneColorAndBackgroundIndex((byte)pixelPattern, bitmapFgColorCode)] = bitmapPixels;
            }
        }

        // Text extended & bitmap standard "HiRes" mode with one foreground color and a "background" color (non-transparent)
        // 8 bits => 8 pixels
        _eightPixelsTwoColors = new uint[256 * 16 * 16][];

        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte bitmapBgColorCode = 0; bitmapBgColorCode < 16; bitmapBgColorCode++)
            {
                var bitmapBgColorVal = _c64ToRenderColorMap[bitmapBgColorCode];

                for (byte bitmapFgColorCode = 0; bitmapFgColorCode < 16; bitmapFgColorCode++)
                {
                    var bitmapFgColorVal = _c64ToRenderColorMap[bitmapFgColorCode];

                    // Standard (Hires) mode, 8 bits => 8 pixels. 2 "foreground" colors (fg color and bg color from text screen). No background color that will be replaced in shader.
                    var bitmapPixels = new uint[8];
                    for (var pixelPos = 0; pixelPos < 8; pixelPos++)
                    {
                        // If bit is set, use foreground color, else use background color
                        var isBitSet = (pixelPattern & 1 << 7 - pixelPos) != 0;
                        if (isBitSet)
                            bitmapPixels[pixelPos] = bitmapFgColorVal;
                        else
                            bitmapPixels[pixelPos] = bitmapBgColorVal;
                    }
                    _eightPixelsTwoColors[GetTwoColorsIndex((byte)pixelPattern, bitmapBgColorCode, bitmapFgColorCode)] = bitmapPixels;
                }
            }
        }


        // Text multicolor & bitmap multicolor mode with one foreground color, two other colors, with a single "transparent" color as background color
        // 8 bits => 4 pixels (with length 2)
        _eightPixelsThreeColorsAndBackground = new uint[256 * 16 * 16 * 16][];

        for (var pixelPattern = 0; pixelPattern < 256; pixelPattern++)
        {
            for (byte color1 = 0; color1 < 16; color1++)
            {
                var color1Val = _c64ToRenderColorMap[color1];

                for (byte color2 = 0; color2 < 16; color2++)
                {
                    var color2Val = _c64ToRenderColorMap[color2];

                    for (byte color3 = 0; color3 < 16; color3++)
                    {
                        var color3Val = _c64ToRenderColorMap[color3];

                        var bitmapMulicolorPixels = new uint[8];

                        // Loop each multi-color pixel pair (4 pixel pairs)
                        var mask = 0b11000000;
                        // Text multicolor pixel patterns
                        //      00 => screen bg color (transparent)
                        //      01 (multi color 1) => backgroundColor1
                        //      10 (multi color 2) => backgroundColor2
                        //      11 (multi color 3) => foreground color from color RAM.

                        // Bitmap multicolor pixel patterns
                        //      00 => screen bg color (transparent)
                        //      01 (multi color 1) => bitmap fg color (from text screen high 4 bits)
                        //      10 (multi color 2) => bitmap bg color (from text screen low 4 bits)
                        //      11 (multi color 3) => color RAM color (for corresponding position in text screen)


                        for (var pixel = 0; pixel < 4; pixel++)
                        {
                            var pixelPair = (pixelPattern & mask) >> 6 - pixel * 2;
                            var pairColorVal = pixelPair switch
                            {
                                0b00 => transparentColorVal,
                                0b01 => color1Val,
                                0b10 => color2Val,
                                0b11 => color3Val,
                                _ => throw new DotNet6502Exception("Invalid pixel pair value.")
                            };
                            mask = mask >> 2;
                            bitmapMulicolorPixels[pixel * 2] = pairColorVal;
                            bitmapMulicolorPixels[pixel * 2 + 1] = pairColorVal;
                        }
                        _eightPixelsThreeColorsAndBackground[GetThreeColorsIndex((byte)pixelPattern, color1, color2, color3)] = bitmapMulicolorPixels;
                    }
                }
            }
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOneColorAndBackgroundIndex(byte eightPixels, byte color1)
        => (eightPixels << 4) | color1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetTwoColorsIndex(byte eightPixels, byte color0, byte color1)
        => (eightPixels << 8) | (color0 << 4) | color1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetThreeColorsIndex(byte eightPixels, byte color1, byte color2, byte color3)
        => (eightPixels << 12) | (color1 << 8) | (color2 << 4) | color3;

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
    private void DrawIdleBlock(C64 c64, int drawLine, int col)
    {
        if (_invalidMode)
            return;
        var idleData = c64.Vic2.ReadMemory((ushort)(_isTextMode && _characterMode == CharMode.Extended ? 0x39FF : 0x3FFF));
        uint[] idlePixels;
        if (_isTextMode)
            idlePixels = _eightPixelsOneColorAndBackground[GetOneColorAndBackgroundIndex(idleData, (byte)C64Colors.Black)];
        else if (_bitmapMode == BitmMode.Standard)
            idlePixels = _eightPixelsTwoColors[GetTwoColorsIndex(idleData, (byte)C64Colors.Black, (byte)C64Colors.Black)];
        else
            idlePixels = _eightPixelsThreeColorsAndBackground[GetThreeColorsIndex(idleData, (byte)C64Colors.Black, (byte)C64Colors.Black, (byte)C64Colors.Black)];
        WriteToPixelArray(_oneLineSameColorPixels[_backgroundColor0], foreground: false, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true);
        WriteToPixelArray(idlePixels, foreground: true, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true);
    }

    private void DrawTextAndBitmapPixels(C64 c64, int drawLine, int col)
    {
        // Invalid VIC-II mode (ECM combined with BMM/MCM): the pixel sequencer is disabled and the
        // display area outputs black at the physical raster line, regardless of screen/char/bitmap
        // memory. (Without this, the BMM bit alone makes us render garbage bitmap data - the cause of
        // the garbled band seen in e.g. Commando, which toggles this mode on for a few raster lines.)
        // Fill the background layer black, and clear the foreground layer on the band's own raster
        // lines so nothing drawn earlier this frame remains visible inside the band. Clearing rather
        // than painting black keeps the foreground transparent so sprites still composite normally.
        if (_invalidMode)
        {
            // Clear pixels
            WriteToPixelArray(_oneLineSameColorPixels[(byte)C64Colors.Black], foreground: false, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: false);
            WriteToPixelArray(_oneLineTransparentPixels, foreground: true, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: false);
            return;
        }

        var backgroundIsPrefilled = _isTextMode && _characterMode == CharMode.Standard;

        // Idle state: no row is being displayed (before the first bad line, after a row's eighth
        // line until the next bad line, or all frame when the display was off during line $30).
        // The sequencer still runs, on the byte at the end of the VIC-II bank ($3FFF, or $39FF with
        // ECM) with no video matrix data: black over the background colour, or all black in the
        // bitmap modes where both colours would come from the matrix.
        if (!_lineDisplayState)
        {
            var idleData = c64.Vic2.ReadMemory((ushort)(_isTextMode && _characterMode == CharMode.Extended ? 0x39FF : 0x3FFF));
            uint[] idlePixels;
            if (_isTextMode)
                idlePixels = _eightPixelsOneColorAndBackground[GetOneColorAndBackgroundIndex(idleData, (byte)C64Colors.Black)];
            else if (_bitmapMode == BitmMode.Standard)
                idlePixels = _eightPixelsTwoColors[GetTwoColorsIndex(idleData, (byte)C64Colors.Black, (byte)C64Colors.Black)];
            else
                idlePixels = _eightPixelsThreeColorsAndBackground[GetThreeColorsIndex(idleData, (byte)C64Colors.Black, (byte)C64Colors.Black, (byte)C64Colors.Black)];
            if (!backgroundIsPrefilled)
                WriteToPixelArray(_oneLineSameColorPixels[_backgroundColor0], foreground: false, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: false);
            WriteToPixelArray(idlePixels, foreground: true, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true);
            return;
        }

        // Display state: the row is the one VC points at and the line within it is RC, both as the
        // VIC-II counted them; VC is ten bits and advances by one per column.
        var characterLine = (ushort)_lineRowCounter;
        var videoCounter = (ushort)((_lineVideoCounterBase + col) & 0x3FF);
        var characterRow = _lineVideoCounterBase;   // identifies the row for the row latch

        var characterAddress = (ushort)(_vic2VideoMatrixBaseAddress + videoCounter);
        var colorRamAddress = (ushort)(Vic2Addr.COLOR_RAM_START + videoCounter);
        var c64BitMapAddress = (ushort)(_vic2BitmapBaseAddress + videoCounter * 8 + characterLine);

        // Screen code and colour nibble for the cell: from the row latch when this row has been
        // fetched already (lines after the row's first), otherwise live from the video matrix and
        // colour RAM, filling the latch on the way.
        byte characterCode, colorRamCode;
        if (characterLine != 0 && _latchedCharacterRow == characterRow)
        {
            characterCode = _rowScreenCodes[col];
            colorRamCode = _rowColorRam[col];
        }
        else
        {
            characterCode = c64.Vic2.ReadMemory(characterAddress);
            colorRamCode = c64.ReadIOStorage(colorRamAddress);
            _rowScreenCodes[col] = characterCode;
            _rowColorRam[col] = colorRamCode;
            if (_fetchingCharacterRow != characterRow)
            {
                _fetchingCharacterRow = characterRow;
                _fetchedColumnsMask = 0;
                _latchedCharacterRow = -1;
            }
            _fetchedColumnsMask |= 1UL << col;
            if (_fetchedColumnsMask == (1UL << _vic2ScreenTextCols) - 1)
                _latchedCharacterRow = characterRow;   // every column read live: the row is fetched
        }

        uint[] eightPixels;
        if (_isTextMode)
        {
            var characterMode = _characterMode;
            // Determine colors
            var fgColorCode = colorRamCode;
            int bgColorNumber;  // 0-3
            if (characterMode == CharMode.Standard)
                bgColorNumber = 0;
            else if (characterMode == CharMode.Extended)
            {
                bgColorNumber = characterCode >> 6;   // Bit 6 and 7 of character byte is used to select background color (0-3)
                characterCode = (byte)(characterCode & 0b00111111); // The actual usable character codes are in the lower 6 bits (0-63)

            }
            else // Asume multicolor mode
            {
                bgColorNumber = 0;
                // When in MultiColor mode, a character can still be displayed in Standard mode depending on the value from color RAM.
                if (fgColorCode <= 7)
                    // If color RAM value is 0-7, normal Standard mode is used (not multi-color)
                    characterMode = CharMode.Standard;
                else
                {
                    // If displaying in MultiColor mode, the actual color used from color RAM will be values 0-7.
                    // Thus color values 8-15 are transformed to 0-7
                    fgColorCode = (byte)((fgColorCode & 0b00001111) - 8);
                }
            }

            // Read one line (8 bits/pixels) of character pixel data from character set from the current line of the character code
            var characterSetLineAddress = (ushort)(_vic2CharacterSetAddressInVIC2Bank
                + characterCode * _vic2ScreenCharacterHeight
                + characterLine);
            var lineData = c64.Vic2.ReadMemory(characterSetLineAddress);

            // Get pre-calculated 8 pixels that should be drawn on the bitmap, with correct colors for foreground and background
            if (characterMode == CharMode.Standard || characterMode == CharMode.Extended)
            {
                switch (bgColorNumber)
                {
                    case 0:
                        eightPixels = _eightPixelsOneColorAndBackground[GetOneColorAndBackgroundIndex(lineData, fgColorCode)];
                        break;
                    case 1:
                        eightPixels = _eightPixelsTwoColors[GetTwoColorsIndex(lineData, _backgroundColor1, fgColorCode)];
                        break;
                    case 2:
                        eightPixels = _eightPixelsTwoColors[GetTwoColorsIndex(lineData, _backgroundColor2, fgColorCode)];
                        break;
                    case 3:
                        eightPixels = _eightPixelsTwoColors[GetTwoColorsIndex(lineData, _backgroundColor3, fgColorCode)];
                        break;
                    default:
                        throw new DotNet6502Exception("Invalid background color number.");
                }
            }
            else // Assume text multicolor mode
            {
                // Text multicolor mode color usage (8 bits, 4 pixel pairs)
                // Transparent background = the color of pixel-pair 00
                // backgroundColor1       = the color of pixel-pair 01
                // backgroundColor2       = the color of pixel-pair 10
                // fgColorCode            = the color of pixel-pair 11

                // Get the corresponding array of uints representing the 8 pixels of the character
                eightPixels = _eightPixelsThreeColorsAndBackground[GetThreeColorsIndex(lineData, _backgroundColor1, _backgroundColor2, fgColorCode)];
            }
        }
        else
        {
            // Assume bitmap mode

            // 8 bits of bitmap data for the current line, at the current column
            var bitmapLineData = c64.Vic2.ReadMemory(c64BitMapAddress);

            // Bg color is picked from text screen, low 4 bits.
            var bitmapBgColorCode = (byte)(characterCode & 0b00001111);
            // Fg color is picked from text screen, high 4 bits.
            var bitmapFgColorCode = (byte)((characterCode & 0b11110000) >> 4);

            if (_bitmapMode == BitmMode.Standard)
                // Bitmap Standard (HiRes) mode, 8 bits => 8 pixels
                // ----------
                // Pixel not set (bit = 0) => bitmap bg color (from text screen low 4 bits)
                // Pixel set (bit = 1) => bitmap fg color
                eightPixels = _eightPixelsTwoColors[GetTwoColorsIndex(bitmapLineData, bitmapBgColorCode, bitmapFgColorCode)];
            else
            {
                // Bitmap Multi color mode, 8 bits => 4 pixels
                // ----------
                // Pixel pattern 00 => screen bg color
                // Pixel pattern 01 (multi color 1) => bitmap fg color (from text screen high 4 bits)
                // Pixel pattern 10 (multi color 2) => bitmap bg color (from text screen low 4 bits)
                // Pixel pattern 11 (multi color 3) => color RAM color (for corresponding position in text screen)
                eightPixels = _eightPixelsThreeColorsAndBackground[GetThreeColorsIndex(bitmapLineData, bitmapFgColorCode, bitmapBgColorCode, colorRamCode)];
            }
        }

        // Write the background color to the pixel array for background and border
        if (!backgroundIsPrefilled)
            WriteToPixelArray(_oneLineSameColorPixels[_backgroundColor0], foreground: false, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: false);

        // Write the character to the current raster line. Horizontal fine scroll still shifts the
        // destination X, but vertical fine scroll was already applied to gridLine above.
        WriteToPixelArray(eightPixels, foreground: true, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true);

    }

    /// <summary>
    /// Standard text mode draws its cells on the foreground layer only, so the background colour is
    /// laid down on the background layer separately: this draws it on one main-screen line between
    /// two normalized x positions (end exclusive), clipped to the screen area.
    /// </summary>
    private void DrawBackgroundRun(int normalizedScreenLine, int fromX, int toX)
    {
        if (!_isTextMode || _characterMode != CharMode.Standard)
            return;

        var ypos = normalizedScreenLine;
        if (FlipY)
            ypos = _height - ypos - 1;

        // The run spans pixels where the flip-flop was clear; the display window's part of it is
        // the prefill under the text columns, the rest is painted block by block as idle output.
        fromX = Math.Max(fromX, _screenStartX);
        toX = Math.Min(toX, _rightBorderStartX);
        if (fromX >= toX)
            return;

        _setBackgroundPixels(_oneLineSameColorPixels[_backgroundColor0], 0, ypos * _width + fromX, toX - fromX);
    }

    private void WriteToPixelArray(uint[] fnEightPixels, bool foreground, int fnMainScreenY, int fnMainScreenX, int fnLength, bool fnAdjustForScrollX)
    {
        // Draw 8 pixels (or less) of character on the the pixel array part used for the C64 drawable screen (320x200)

        // ----------
        // Y position
        // ----------
        var ypos = _screenStartY + fnMainScreenY;

        // The vertical border flip-flop decides whether this line shows graphics at all (the
        // caller checks it); here only the frame's edge clips.
        if (ypos < 0 || ypos >= _height)
            return;

        // If inverted Y coordinate system is used, flip it
        if (FlipY)
            ypos = _height - ypos - 1;

        // ----------
        // X position
        // ----------
        var sourcePixelStart = 0;
        if (fnAdjustForScrollX)
            fnMainScreenX += _scrollX;
        var xpos = _screenStartX + fnMainScreenX;


        // Only the pixels where the border flip-flop is clear on this line are shown.
        var clipStart = Math.Max(_lineClearStartX, 0);
        var clipEnd = Math.Min(_lineClearEndX, _width);
        if (xpos + fnLength <= clipStart || xpos >= clipEnd)
            return;
        if (xpos < clipStart)
        {
            sourcePixelStart = clipStart - xpos;
            fnLength -= sourcePixelStart;
            xpos = clipStart;
        }
        if (xpos + fnLength > clipEnd)
            fnLength = clipEnd - xpos;

        // ----------
        // Copy pixels to correct location in pixel array
        // ----------
        // Calculate the position in the bitmap where the 8 pixels should be drawn
        var lBitmapIndex = ypos * _width + xpos;

        // Copy array with Span
        // - Seems to be a bit faster on .NET 8 WASM than Array.Copy and Buffer.BlockCopy.
        // - TODO: Is the extra heap memory allocation of Span objects (which leads to GC pressure) worth the performance gain?
        //var source = new ReadOnlySpan<uint>(fnEightPixels, sourcePixelStart, fnLength);
        //var target = new Span<uint>(fnPixelArray, lBitmapIndex, fnLength);
        //source.CopyTo(target);

        // Or Copy array with Array.Copy
        //Array.Copy(fnEightPixels, 0, fnPixelArray, lBitmapIndex, fnLength);

        // Or Copy array with Buffer.BlockCopy
        //Buffer.BlockCopy(fnEightPixels, 0, fnPixelArray, lBitmapIndex * 4, fnLength * 4);   // Note: Buffer.BlockCopy uses byte size, so multiply by 4 to get uint size

        if (foreground)
            _setForegroundPixels(fnEightPixels, sourcePixelStart, lBitmapIndex, fnLength);
        else
            _setBackgroundPixels(fnEightPixels, sourcePixelStart, lBitmapIndex, fnLength);
    }

}
