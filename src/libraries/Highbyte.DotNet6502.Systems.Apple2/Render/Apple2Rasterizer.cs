using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Render;

/// <summary>
/// Pixel-exact Apple II renderer for the 280x192 display, honoring the display soft switches:
/// 40x24 text (each cell drawn from the real 5x7 dot patterns in the character generator ROM),
/// lo-res 40x48 color blocks, hi-res 280x192 monochrome with page flipping ($2000/$4000), and
/// mixed mode (graphics with the bottom 4 text rows).
///
/// Hi-res is rendered as the monochrome dot pattern (bit 7, the NTSC color-shift bit, is
/// ignored) in the configured monitor color; lo-res uses the 16-color lo-res palette.
///
/// Two layers, matching the VIC-20 rasterizer: layer 0 is the background, layer 1 the lit
/// pixels (transparent where unlit) so hosts can composite them.
/// </summary>
[DisplayName("Rasterizer")]
[HelpText("Renders the Apple II text and graphics modes as exact pixels.")]
public sealed class Apple2Rasterizer : IRenderProvider, IVideoFrameLayerProvider
{
    private readonly Apple2System _apple2;
    private readonly ReaderWriterLockSlim _bufferLock = new(LockRecursionPolicy.NoRecursion);

    private uint[] _frontBackground;
    private uint[] _frontForeground;
    private uint[] _backBackground;
    private uint[] _backForeground;
    private readonly ReadOnlyMemory<uint>[] _cachedLayerBuffers;

    private int _frameCounter;

    public string Name => "Apple2Rasterizer";
    public RenderSize NativeSize { get; }
    public PixelFormat PixelFormat { get; } = PixelFormat.Bgra32;
    public int StrideBytes { get; }
    public event EventHandler? FrameCompleted;

    public Apple2Rasterizer(Apple2System apple2)
    {
        _apple2 = apple2;

        var width = apple2.VisibleWidth;
        var height = apple2.VisibleHeight;
        NativeSize = new RenderSize(width, height);
        StrideBytes = width * 4;

        var pixelCount = width * height;
        _frontBackground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);
        _frontForeground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);
        _backBackground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);
        _backForeground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);

        _cachedLayerBuffers = new ReadOnlyMemory<uint>[]
        {
            _frontBackground.AsMemory(),
            _frontForeground.AsMemory()
        };
    }

    public IReadOnlyList<LayerInfo> Layers => new LayerInfo[]
    {
        new(NativeSize, PixelFormat, StrideBytes, 1f, BlendMode.Normal, 0),
        new(NativeSize, PixelFormat, StrideBytes, 1f, BlendMode.Overlay, 1)
    };

    /// <summary>Whether flashing characters are currently in their inverted phase.</summary>
    public bool FlashPhaseInverted => (_frameCounter / Apple2Config.FlashFramesPerToggle) % 2 == 1;

    public IReadOnlyList<ReadOnlyMemory<uint>> CurrentFrontLayerBuffers
    {
        get
        {
            _bufferLock.EnterReadLock();
            try
            {
                return _cachedLayerBuffers;
            }
            finally
            {
                _bufferLock.ExitReadLock();
            }
        }
    }

    public ReadOnlyMemory<uint> CurrentFrontBuffer
    {
        get
        {
            _bufferLock.EnterReadLock();
            try
            {
                return _frontBackground.AsMemory();
            }
            finally
            {
                _bufferLock.ExitReadLock();
            }
        }
    }

    public void OnAfterInstruction()
    {
    }

    public void OnEndFrame()
    {
        _frameCounter++;
        RasterizeFrame();
        FlipBuffers();
        FrameCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void FlipBuffers()
    {
        _bufferLock.EnterWriteLock();
        try
        {
            (_frontBackground, _backBackground) = (_backBackground, _frontBackground);
            (_frontForeground, _backForeground) = (_backForeground, _frontForeground);
            _cachedLayerBuffers[0] = _frontBackground.AsMemory();
            _cachedLayerBuffers[1] = _frontForeground.AsMemory();
        }
        finally
        {
            _bufferLock.ExitWriteLock();
        }
    }

    public static uint PackBgra(byte b, byte g, byte r, byte a)
        => (uint)(b | (g << 8) | (r << 16) | (a << 24));

    private void RasterizeFrame()
    {
        var foregroundArgb = Apple2Colors.GetForeground(_apple2.Apple2Config.MonitorColor);
        var backgroundArgb = Apple2Colors.Background;
        var foreground = PackBgra(foregroundArgb.B, foregroundArgb.G, foregroundArgb.R, foregroundArgb.A);
        var background = PackBgra(backgroundArgb.B, backgroundArgb.G, backgroundArgb.R, backgroundArgb.A);

        Array.Fill(_backBackground, background);
        Array.Clear(_backForeground);

        var switches = _apple2.SoftSwitches;
        if (switches.TextMode)
        {
            RasterizeTextRows(0, Apple2Config.Rows, foreground, background);
            return;
        }

        var graphicsHeight = switches.MixedMode ? Apple2Config.MixedModeGraphicsHeight : NativeSize.Height;
        if (switches.HiRes)
            RasterizeHiRes(graphicsHeight, foreground, background);
        else
            RasterizeLoRes(graphicsHeight, background);

        if (switches.MixedMode)
            RasterizeTextRows(Apple2Config.MixedModeFirstTextRow, Apple2Config.Rows, foreground, background);
    }

    private void RasterizeTextRows(int firstRow, int rowsEnd, uint foreground, uint background)
    {
        var characterRom = _apple2.CharacterRom;
        if (characterRom == null)
            return;   // No character generator: blank text rows rather than garbage.

        var mem = _apple2.Mem;
        var pageBaseAddress = _apple2.SoftSwitches.ActiveTextPageBaseAddress;
        var flashInverted = FlashPhaseInverted;

        for (var row = firstRow; row < rowsEnd; row++)
        {
            var rowStartAddress = Apple2TextScreen.GetRowStartAddress(row, pageBaseAddress);
            var cellPixelY = row * Apple2Config.CharacterHeight;

            for (var col = 0; col < Apple2Config.Cols; col++)
            {
                var screenByte = mem[(ushort)(rowStartAddress + col)];
                var glyphIndex = Apple2CharSet.GetGlyphIndex(screenByte);
                var inverted = Apple2CharSet.GetAttribute(screenByte) switch
                {
                    Apple2TextAttribute.Inverse => true,
                    Apple2TextAttribute.Flash => flashInverted,
                    _ => false,
                };

                var cellPixelX = col * Apple2Config.CharacterWidth;
                var glyphBase = glyphIndex * Apple2Config.CharacterHeight;

                for (var glyphRow = 0; glyphRow < Apple2Config.CharacterHeight; glyphRow++)
                {
                    var glyphLine = characterRom[glyphBase + glyphRow];
                    var rowOffset = (cellPixelY + glyphRow) * NativeSize.Width;

                    for (var dot = 0; dot < Apple2Config.CharacterWidth; dot++)
                    {
                        // The 2513 stores 5 dots in bits 5-1, most significant leftmost; the
                        // remaining 2 columns of the 7-pixel cell are the inter-character gap.
                        var lit = dot < Apple2CharSet.GlyphDotWidth
                                  && ((glyphLine >> (Apple2CharSet.GlyphDotShift - dot)) & 0x01) != 0;
                        if (inverted)
                            lit = !lit;

                        SetRasterPixel(rowOffset + cellPixelX + dot, background, lit ? foreground : background);
                    }
                }
            }
        }
    }

    private void RasterizeHiRes(int lines, uint foreground, uint background)
    {
        var mem = _apple2.Mem;
        var pageBaseAddress = _apple2.SoftSwitches.ActiveHiResPageBaseAddress;

        for (var y = 0; y < lines; y++)
        {
            var lineStartAddress = Apple2HiResScreen.GetLineStartAddress(y, pageBaseAddress);
            var rowOffset = y * NativeSize.Width;

            for (var byteIndex = 0; byteIndex < Apple2HiResScreen.BytesPerLine; byteIndex++)
            {
                var screenByte = mem[(ushort)(lineStartAddress + byteIndex)];
                var pixelX = byteIndex * Apple2HiResScreen.PixelsPerByte;

                for (var bit = 0; bit < Apple2HiResScreen.PixelsPerByte; bit++)
                {
                    var lit = ((screenByte >> bit) & 0x01) != 0;
                    SetRasterPixel(rowOffset + pixelX + bit, background, lit ? foreground : background);
                }
            }
        }
    }

    private void RasterizeLoRes(int graphicsHeight, uint background)
    {
        var mem = _apple2.Mem;
        var pageBaseAddress = _apple2.SoftSwitches.ActiveTextPageBaseAddress;
        var textRows = graphicsHeight / Apple2Config.CharacterHeight;

        for (var row = 0; row < textRows; row++)
        {
            var rowStartAddress = Apple2TextScreen.GetRowStartAddress(row, pageBaseAddress);
            var cellPixelY = row * Apple2Config.CharacterHeight;

            for (var col = 0; col < Apple2Config.Cols; col++)
            {
                var screenByte = mem[(ushort)(rowStartAddress + col)];
                var cellPixelX = col * Apple2LoResScreen.BlockPixelWidth;

                RasterizeLoResBlock(screenByte, upperBlock: true, cellPixelX, cellPixelY, background);
                RasterizeLoResBlock(screenByte, upperBlock: false, cellPixelX, cellPixelY + Apple2LoResScreen.BlockPixelHeight, background);
            }
        }
    }

    private void RasterizeLoResBlock(byte screenByte, bool upperBlock, int pixelX, int pixelY, uint background)
    {
        var color = Apple2LoResScreen.Palette[Apple2LoResScreen.GetColorIndex(screenByte, upperBlock)];
        var packedColor = PackBgra(color.B, color.G, color.R, color.A);

        for (var y = 0; y < Apple2LoResScreen.BlockPixelHeight; y++)
        {
            var rowOffset = (pixelY + y) * NativeSize.Width;
            for (var x = 0; x < Apple2LoResScreen.BlockPixelWidth; x++)
                SetRasterPixel(rowOffset + pixelX + x, background, packedColor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetRasterPixel(int index, uint backgroundColor, uint pixelColor)
    {
        _backBackground[index] = backgroundColor;
        _backForeground[index] = pixelColor == backgroundColor ? 0u : pixelColor;
    }
}
