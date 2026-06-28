using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Instrumentation.Stats;
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

    private string StatsCategory { get; } = string.Empty;
    public Instrumentations Instrumentations { get; } = new();
    private ElapsedMillisecondsTimedStatSystem _spritesStat;
    private ElapsedMillisecondsTimedStatSystem _renderArraysStat;


    // Pre-calculated pixel arrays
    private uint[][] _oneLineSameColorPixels; // pixelArray

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
    private int _drawableAreaHeight;
    private int _drawableAreaWidth;
    private ulong _cyclesPerLine;
    private ushort _vic2VideoMatrixBaseAddress;
    private ushort _vic2BitmapBaseAddress;
    private ushort _vic2CharacterSetAddressInVIC2Bank;
    private bool _isTextMode;
    private CharMode _characterMode;
    private BitmMode _bitmapMode;
    private int _scrollX;
    private int _scrollY;

    private byte _borderColor;
    private byte _backgroundColor0;
    private byte _backgroundColor1;
    private byte _backgroundColor2;
    private byte _backgroundColor3;

    private bool _is38ColModeEnabled;
    private bool _is24RowModeEnabled;

    private int _leftBorderEndXAdjusted;
    private int _leftBorderLengthAdjusted;
    private int _rightBorderStartXAdjusted;
    private int _rightBorderLengthAdjusted;
    private int _topBorderEndYAdjusted;
    private int _bottomBorderStartYAdjusted;
    private int _screenStartXAdjusted;

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
    private int _spriteScreenEndX;
    private int _spriteScreenEndY;
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
    private readonly uint[] _bandColorFg = new uint[MAX_BANDS];
    private readonly uint[] _bandColorMc0 = new uint[MAX_BANDS];
    private readonly uint[] _bandColorMc1 = new uint[MAX_BANDS];
    private int _bandCount;

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
        nameof(_spritesStat),
        nameof(_renderArraysStat),
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

        // Sprite clip bounds (closed borders) and the VIC-II sprite coordinate offsets.
        _spriteScreenEndX = visibleMainScreenAreaNormalized.Screen.End.X;
        _spriteScreenEndY = visibleMainScreenAreaNormalized.Screen.End.Y;
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
        _drawableAreaHeight = _c64.Vic2.Vic2Screen.DrawableAreaHeight;
        _drawableAreaWidth = _c64.Vic2.Vic2Screen.DrawableAreaWidth;
        _cyclesPerLine = _c64.Vic2.Vic2Model.CyclesPerLine;

        _lastScreenLineDataUpdate = -1;

        // Init bitmaps to render to
        InitBitmaps(_c64);
        InitBitPatternToPixelMaps(_c64);

        // Init instrumentation
        Instrumentations.Clear();
        _spritesStat = Instrumentations.Add($"{StatsCategory}-Sprites", new ElapsedMillisecondsTimedStatSystem(_c64));
        _renderArraysStat = Instrumentations.Add($"{StatsCategory}-RenderArrays", new ElapsedMillisecondsTimedStatSystem(_c64));
    }

    /// <summary>
    /// Write screen data for all clock cycles since last time this method was called.
    /// Instructions can take different amount of cycles to execute, so this method is called after each instruction to update the screen data and will catch up on what's to do since last time it was called.
    /// </summary>
    public void OnAfterInstruction()
    {
        // Loop cycles since last time we processed (each instruction)
        for (var cycleCurrentVblank = _lastCyclesConsumedCurrentVblank; cycleCurrentVblank < _c64.Vic2.CyclesConsumedCurrentVblank; cycleCurrentVblank++)
        {
            // For the cycle processed in current loop iteration, get line and x position.
            // Skip if not within visible C64 border/text/bitmap area

            // Line
            var rasterLine = (int)(cycleCurrentVblank / _cyclesPerLine);
            var screenLine = _c64.Vic2.Vic2Model.ConvertRasterLineToScreenLine(rasterLine);
            if (screenLine < _screenLayoutInclNonVisibleTopBorderStartY || screenLine > _screenLayoutInclNonVisibleBottomBorderEndY)
                continue;

            // X position
            var cycleOnScreenLine = cycleCurrentVblank % _cyclesPerLine;
            var posX = (int)(cycleOnScreenLine * 8); // 1 cycle = 8 pixels;
            if (posX < _screenLayoutInclNonVisibleLeftBorderStartX || posX > _screenLayoutInclNonVisibleRightBorderEndX)
                continue;

            var isNewLine = screenLine != _lastScreenLineDataUpdate;

            // On a new line, refresh from the current VIC-II state.
            if (isNewLine)
            {
                // Draw border once per line, after normal screen (to cover up any scrolling?). We take data from previous line.
                if (_lastScreenLineDataUpdate >= 0)
                {
                    DrawBorderPixels(normalizedScreenLine: _lastScreenLineDataUpdate - _screenLayoutInclNonVisibleTopBorderStartY);

                    // Now that the just-finished previous line's text/bitmap/border is laid down,
                    // composite that line's sprites on top of it (per-line / multiplexing path).
                    if (_perLineSprites)
                        DrawSpritesForLine(_lastScreenLineDataUpdate);
                }

                if (screenLine - _screenLayoutInclNonVisibleTopBorderStartY == 0)
                {
                    // First line of screen. Clear foreground bitmap, otherwise it will contain garbage from previous frame if fine scrolling is used.
                    //Array.Clear(PixelArray_Foreground, 0, PixelArray_Foreground.Length);
                    _clearForegroundPixels(0, _width * _height);

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
                _scrollX = _c64.Vic2.GetScrollX();
                _scrollY = _c64.Vic2.GetScrollY();

                _borderColor = _c64.ReadIOStorage(Vic2Addr.BORDER_COLOR);

                _backgroundColor0 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_0);
                _backgroundColor1 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1);
                _backgroundColor2 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2);
                _backgroundColor3 = _c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3);

                _is38ColModeEnabled = _c64.Vic2.Is38ColumnDisplayEnabled;
                _is24RowModeEnabled = _c64.Vic2.Is24RowDisplayEnabled;

                _leftBorderEndXAdjusted = _leftBorderEndX + (_is38ColModeEnabled ? Vic2Screen.COL_38_LEFT_BORDER_END_X_DELTA : 0);
                _leftBorderLengthAdjusted = _leftBorderEndXAdjusted - _leftBorderStartX + 1;
                _rightBorderStartXAdjusted = _rightBorderStartX + (_is38ColModeEnabled ? Vic2Screen.COL_38_RIGHT_BORDER_START_X_DELTA : 0);
                _rightBorderLengthAdjusted = _width - _rightBorderStartXAdjusted;

                _topBorderEndYAdjusted = _topBorderEndY + (_is24RowModeEnabled ? Vic2Screen.ROW_24_TOP_BORDER_END_Y_DELTA : 0);
                _bottomBorderStartYAdjusted = _bottomBorderStartY + (_is24RowModeEnabled ? Vic2Screen.ROW_24_BOTTOM_BORDER_START_Y_DELTA : 0);

                _screenStartXAdjusted = _leftBorderEndXAdjusted + 1;

                if (_isTextMode && _characterMode == CharMode.Standard)
                    PrefillStandardTextBackgroundLine(screenLine);

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

            // Only draw main screen area (text/bitmap) if within it
            if (!(screenLine < _screenLayoutInclNonVisibleScreenStartY || screenLine > _screenLayoutInclNonVisibleScreenEndY
                || posX < _screenLayoutInclNonVisibleScreenStartX || posX > _screenLayoutInclNonVisibleScreenEndX))
            {
                DrawTextAndBitmapPixels(_c64, drawLine: screenLine - _screenLayoutInclNonVisibleScreenStartY, col: (posX - _screenLayoutInclNonVisibleScreenStartX) / 8);
            }

        } // End for each cycle

        _lastCyclesConsumedCurrentVblank = _c64.Vic2.CyclesConsumedCurrentVblank;
    }

    public void OnEndFrame()
    {
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
                if (pixelArrayY == spriteScreenPosY)
                {
                    _spriteActive[spriteIndex] = true;
                    _spriteRow[spriteIndex] = 0;
                    _spriteExpandYPhase[spriteIndex] = false;
                    _spriteActiveDoubleHeight[spriteIndex] = sprites[spriteIndex].DoubleHeight;
                    _spriteHadBandThisFrame[spriteIndex] = true;
                    RecordBand(sprites[spriteIndex], pixelArrayY);
                }
            }

            if (!_spriteActive[spriteIndex])
                continue;

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
        _bandX[b] = sprite.X + _screenStartX - _spriteScreenOffsetX;
        _bandDoubleWidth[b] = sprite.DoubleWidth;
        _bandDoubleHeight[b] = sprite.DoubleHeight;
        _bandMultiColor[b] = sprite.Multicolor;
        _bandPriority[b] = sprite.PriorityOverForeground;
        _bandColorFg[b] = _c64ToRenderColorMap[sprite.Color];
        _bandColorMc0[b] = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0)];
        _bandColorMc1[b] = _c64ToRenderColorMap[_c64.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1)];

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
        var lineAdvance = _bandDoubleHeight[b] ? 2 : 1;
        var pixelArrayY = _bandRowStart[b];
        for (int row = 0; row < SPRITE_ROWS; row++)
        {
            if ((nonEmpty & (1u << row)) != 0)
            {
                DrawSpriteRowPixels(shapeBase + row * SPRITE_ROW_BYTES, b, pixelArrayY);
                if (lineAdvance == 2)
                    DrawSpriteRowPixels(shapeBase + row * SPRITE_ROW_BYTES, b, pixelArrayY + 1);
            }
            pixelArrayY += lineAdvance;
        }
    }

    /// <summary>Writes one sprite row's pixels from band <paramref name="b"/> at row offset <paramref name="rowOffset"/>.</summary>
    private void DrawSpriteRowPixels(int rowOffset, int b, int pixelArrayY)
    {
        var spriteScreenPosX = _bandX[b];
        var isMultiColor = _bandMultiColor[b];
        var priorityOverForeground = _bandPriority[b];
        var isDoubleWidth = _bandDoubleWidth[b];
        var singleColorPixelAdvance = isDoubleWidth ? 2 : 1;
        var multiColorPixelAdvance = isDoubleWidth ? 4 : 2;
        var spriteLinePartAdvance = isDoubleWidth ? 16 : 8;

        var spriteForegroundPixelColor = _bandColorFg[b];
        var spriteMultiColor0PixelColor = _bandColorMc0[b];
        var spriteMultiColor1PixelColor = _bandColorMc1[b];

        var x = 0;
        for (int byteIndex = 0; byteIndex < SPRITE_ROW_BYTES; byteIndex++)
        {
            var spriteLinePart = _bandShape[rowOffset + byteIndex];
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
                        WriteSpritePixel(spriteScreenPosX + x, pixelArrayY, spriteColor, priorityOverForeground);
                        WriteSpritePixel(spriteScreenPosX + x + 1, pixelArrayY, spriteColor, priorityOverForeground);
                        if (isDoubleWidth)
                        {
                            WriteSpritePixel(spriteScreenPosX + x + 2, pixelArrayY, spriteColor, priorityOverForeground);
                            WriteSpritePixel(spriteScreenPosX + x + 3, pixelArrayY, spriteColor, priorityOverForeground);
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
                        WriteSpritePixel(spriteScreenPosX + x, pixelArrayY, spriteForegroundPixelColor, priorityOverForeground);
                        if (isDoubleWidth)
                            WriteSpritePixel(spriteScreenPosX + x + 1, pixelArrayY, spriteForegroundPixelColor, priorityOverForeground);
                    }
                    mask >>= 1;
                    x += singleColorPixelAdvance;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSpritePixel(int screenPosX, int screenPosY, uint color, bool priorityOverForeground)
    {
        if (screenPosX < 0 || screenPosX >= _width || screenPosY < 0 || screenPosY > _height)
            return;
        if (screenPosX < _screenStartX || screenPosX > _spriteScreenEndX)   // side borders closed (TODO: open)
            return;
        if (screenPosY < _screenStartY || screenPosY > _spriteScreenEndY)   // top/bottom borders closed (TODO: open)
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
        _spritesStat.Start();
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

            var spriteScreenPosX = sprite.X + visibleMainScreenArea.Screen.Start.X - vic2.SpriteManager.ScreenOffsetX;
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
            var spriteLinePartAdvance = isDoubleWidth ? 16 : 8;
            var singleColorPixelAdvance = isDoubleWidth ? 2 : 1;
            var multiColorPixelAdvance = isDoubleWidth ? 4 : 2;
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

                // Loop each 8-bit part of the sprite line (3 bytes, 24 pixels).
                var x = 0;
                foreach (var spriteLinePart in spriteRow.Bytes)
                {
                    // 0 means the whole 8-bit sprite chunk is transparent, so skip the per-pixel decode work
                    // but still advance by the on-screen width that this sprite byte occupies.
                    if (spriteLinePart == 0)
                    {
                        x += spriteLinePartAdvance;
                        continue;
                    }

                    if (isMultiColor)
                    {
                        var maskMultiColor0Mask = 0b01000000;
                        var maskSpriteColorMask = 0b10000000;
                        var maskMultiColor1Mask = 0b11000000;

                        uint spriteColor;
                        for (var pixel = 0; pixel < 8; pixel += 2)
                        {
                            if ((spriteLinePart & maskMultiColor1Mask) == maskMultiColor1Mask)
                                spriteColor = spriteMultiColor1PixelColor;
                            else if ((spriteLinePart & maskSpriteColorMask) == maskSpriteColorMask)
                            {
                                spriteColor = spriteForegroundPixelColor;
                            }
                            else if ((spriteLinePart & maskMultiColor0Mask) == maskMultiColor0Mask)
                            {
                                spriteColor = spriteMultiColor0PixelColor;
                            }
                            else
                            {
                                spriteColor = 0;
                            }

                            if (spriteColor > 0)
                            {
                                WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y, spriteColor, priorityOverForground);
                                WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y, spriteColor, priorityOverForground);

                                if (isDoubleWidth)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 2, spriteScreenPosY + y, spriteColor, priorityOverForground);
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 3, spriteScreenPosY + y, spriteColor, priorityOverForground);
                                }

                                if (isDoubleHeight)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);

                                    if (isDoubleWidth)
                                    {
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 2, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 3, spriteScreenPosY + y + 1, spriteColor, priorityOverForground);
                                    }
                                }
                            }

                            maskMultiColor0Mask = maskMultiColor0Mask >> 2;
                            maskMultiColor1Mask = maskMultiColor1Mask >> 2;
                            maskSpriteColorMask = maskSpriteColorMask >> 2;

                            x += multiColorPixelAdvance;
                        }
                    }
                    else
                    {
                        var mask = 0b10000000;
                        for (var pixel = 0; pixel < 8; pixel++)
                        {
                            var pixelSet = (spriteLinePart & mask) == mask;
                            if (pixelSet)
                            {
                                WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y, spriteForegroundPixelColor, priorityOverForground);

                                if (isDoubleWidth)
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y, spriteForegroundPixelColor, priorityOverForground);

                                if (isDoubleHeight)
                                {
                                    WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x, spriteScreenPosY + y + 1, spriteForegroundPixelColor, priorityOverForground);
                                    if (isDoubleWidth)
                                        WriteSpritePixelWithAlphaPrio(spriteScreenPosX + x + 1, spriteScreenPosY + y + 1, spriteForegroundPixelColor, priorityOverForground);
                                }
                            }
                            mask = mask >> 1;

                            x += singleColorPixelAdvance;
                        }
                    }
                }
                y += spriteLineAdvance;
            }

            void WriteSpritePixelWithAlphaPrio(int screenPosX, int screenPosY, uint color, bool priorityOverForground)
            {
                // Check if pixel is outside the visible screen area
                if (screenPosX < 0 || screenPosX >= width || screenPosY < 0 || screenPosY > height)
                    return;

                // Check if pixel is within side borders, and if it should be shown there or not.
                // TODO: Detect if side borders are open? How to?
                var openSideBorders = false;
                if (!openSideBorders && (screenPosX < visibleMainScreenArea.Screen.Start.X || screenPosX > visibleMainScreenArea.Screen.End.X))
                    return;

                // Check if pixel is within top/bottom borders, and if it should be shown there or not.
                // TODO: Detect if top/bottom borders are open? How to?
                var openTopBottomBorders = false;
                if (!openTopBottomBorders && (screenPosY < visibleMainScreenArea.Screen.Start.Y || screenPosY > visibleMainScreenArea.Screen.End.Y))
                    return;

                // Calculate the position in the bitmap where the pixel should be drawn
                // If inverted Y coordinate system is used, flip it
                if (FlipY)
                    screenPosY = _height - screenPosY - 1;

                var bitmapIndex = screenPosY * width + screenPosX;

                //// If pixel to be set is from a low prio sprite, don't overwrite if current pixel is from high prio sprite
                //const uint BLUE_COLOR_MASK = 0x000000ff;
                if (!priorityOverForground)
                    //if ((backgroundPixelArray[bitmapIndex] & BLUE_COLOR_MASK) == HIGH_PRIO_SPRITE_BLUE)
                    //    return;
                    //backgroundPixelArray[bitmapIndex] = color;
                    //_setBackgroundPixels(new uint[] { color }, 0, bitmapIndex, 1);
                    _setPixel(color, bitmapIndex, false); // false = background
                else
                {
                    //foregroundPixelArray[bitmapIndex] = color;
                    //_setForegroundPixels(new uint[] { color }, 0, bitmapIndex, 1);
                    _setPixel(color, bitmapIndex, true); // true = foreground
                }
            }
        }

        _spritesStat.Stop();
    }

    private void DrawBorderPixels(int normalizedScreenLine)
    {
        // Top or bottom border
        if (normalizedScreenLine <= _topBorderEndYAdjusted || normalizedScreenLine >= _bottomBorderStartYAdjusted)
        {
            var topBottomBorderLineStartIndex = normalizedScreenLine * _width;

            //Array.Copy(_oneLineSameColorPixels[_borderColor], 0, PixelArray_BackgroundAndBorder, topBottomBorderLineStartIndex, _width);
            _setBackgroundPixels(_oneLineSameColorPixels[_borderColor], 0, topBottomBorderLineStartIndex, _width);
            return;
        }

        // Left border
        var lineStartIndex = normalizedScreenLine * _width;
        //Array.Copy(_oneLineSameColorPixels[_borderColor], 0, PixelArray_BackgroundAndBorder, lineStartIndex, _leftBorderLengthAdjusted);
        _setBackgroundPixels(_oneLineSameColorPixels[_borderColor], 0, lineStartIndex, _leftBorderLengthAdjusted);

        // Right border
        lineStartIndex += _rightBorderStartXAdjusted;
        //Array.Copy(_oneLineSameColorPixels[_borderColor], _rightBorderStartXAdjusted, PixelArray_BackgroundAndBorder, lineStartIndex, _rightBorderLengthAdjusted);
        _setBackgroundPixels(_oneLineSameColorPixels[_borderColor], 0, lineStartIndex, _rightBorderLengthAdjusted);
    }

    private void DrawTextAndBitmapPixels(C64 c64, int drawLine, int col)
    {
        var characterRow = drawLine / 8;
        var characterLine = (ushort)(drawLine % 8);
        var backgroundIsPrefilled = _isTextMode && _characterMode == CharMode.Standard;

        var characterAddress = (ushort)(_vic2VideoMatrixBaseAddress + characterRow * _vic2ScreenTextCols + col);
        var colorRamAddress = (ushort)(Vic2Addr.COLOR_RAM_START + characterRow * _vic2ScreenTextCols + col);
        var c64BitMapAddress = (ushort)(_vic2BitmapBaseAddress + characterRow * _vic2ScreenTextCols * 8 + col * 8 + characterLine);

        // Determine character code at current position from video matrix
        var characterCode = c64.Vic2.ReadMemory(characterAddress);
        var colorRamCode = c64.ReadIOStorage(colorRamAddress);

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
            else // Asume text multicolor mode
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
            WriteToPixelArray(_oneLineSameColorPixels[_backgroundColor0], foreground: false, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: false, fnAdjustForScrollY: false);

        // Write the character to the pixel array for foreground (adjusted for fine scrolling)
        WriteToPixelArray(eightPixels, foreground: true, drawLine, col * 8, fnLength: 8, fnAdjustForScrollX: true, fnAdjustForScrollY: true);


        //void WriteToPixelArray(uint[] fnEightPixels, uint[] fnPixelArray, int fnMainScreenY, int fnMainScreenX, int fnLength, bool fnAdjustForScrollX, bool fnAdjustForScrollY)
        void WriteToPixelArray(uint[] fnEightPixels, bool foreground, int fnMainScreenY, int fnMainScreenX, int fnLength, bool fnAdjustForScrollX, bool fnAdjustForScrollY)
        {
            // Draw 8 pixels (or less) of character on the the pixel array part used for the C64 drawable screen (320x200)

            // ----------
            // Y position
            // ----------
            if (fnAdjustForScrollY)
                fnMainScreenY += _scrollY;
            var ypos = _screenStartY + fnMainScreenY;
            if (ypos <= _topBorderEndYAdjusted || ypos >= _bottomBorderStartYAdjusted)
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


            if (xpos + fnLength <= _screenStartXAdjusted || xpos >= _rightBorderStartXAdjusted)
                return;
            if (xpos < _screenStartXAdjusted)
            {
                fnLength = xpos + fnLength - _screenStartXAdjusted;
                xpos = _screenStartXAdjusted;
                sourcePixelStart = 8 - fnLength;
            }
            else if (xpos + fnLength >= _rightBorderStartXAdjusted)
            {
                fnLength = _rightBorderStartXAdjusted - xpos;
            }

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

    private void PrefillStandardTextBackgroundLine(int screenLine)
    {
        var drawLine = screenLine - _screenLayoutInclNonVisibleScreenStartY;
        var ypos = _screenStartY + drawLine;
        if (ypos <= _topBorderEndYAdjusted || ypos >= _bottomBorderStartYAdjusted)
            return;

        if (FlipY)
            ypos = _height - ypos - 1;

        var fillWidth = _rightBorderStartXAdjusted - _screenStartXAdjusted;
        if (fillWidth <= 0)
            return;

        var lineStartIndex = ypos * _width + _screenStartXAdjusted;
        _setBackgroundPixels(_oneLineSameColorPixels[_backgroundColor0], 0, lineStartIndex, fillWidth);
    }

}
