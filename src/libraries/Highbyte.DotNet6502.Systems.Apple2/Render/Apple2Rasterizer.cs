using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Apple2.Config;
using Highbyte.DotNet6502.Systems.Apple2.Video;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Apple2.Render;

/// <summary>
/// Pixel-exact Apple II text renderer: draws each character cell from the real 5x7 dot patterns
/// in the character generator ROM, into the hardware's 7x8 cell and 280x192 display.
///
/// Two layers, matching the VIC-20 rasterizer: layer 0 is the background, layer 1 the lit
/// pixels (transparent where unlit) so hosts can composite them.
/// </summary>
[DisplayName("Rasterizer")]
[HelpText("Renders the Apple II text screen as exact pixels using the character generator ROM.")]
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

        var characterRom = _apple2.CharacterRom;
        if (characterRom == null)
            return;   // No character generator: a blank screen rather than garbage.

        var mem = _apple2.Mem;
        var pageBaseAddress = _apple2.SoftSwitches.ActiveTextPageBaseAddress;
        var flashInverted = FlashPhaseInverted;

        for (var row = 0; row < Apple2Config.Rows; row++)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetRasterPixel(int index, uint backgroundColor, uint pixelColor)
    {
        _backBackground[index] = backgroundColor;
        _backForeground[index] = pixelColor == backgroundColor ? 0u : pixelColor;
    }
}
