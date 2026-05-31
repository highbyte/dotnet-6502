using System.Runtime.CompilerServices;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;
using Highbyte.DotNet6502.Systems.Vic20.Config;
using Highbyte.DotNet6502.Systems.Vic20.Video;

namespace Highbyte.DotNet6502.Systems.Vic20.Render;

[DisplayName("Rasterizer")]
[HelpText("A VIC-20 rasterizer that renders character graphics as exact pixels in two layers.")]
public sealed class Vic20Rasterizer : IRenderProvider, IVideoFrameLayerProvider
{
    private readonly Vic20 _vic20;
    private readonly ReaderWriterLockSlim _bufferLock = new(LockRecursionPolicy.NoRecursion);

    private uint[] _frontBackground;
    private uint[] _frontForeground;
    private uint[] _backBackground;
    private uint[] _backForeground;
    private readonly ReadOnlyMemory<uint>[] _cachedLayerBuffers;
    private readonly Dictionary<byte, uint> _colorMap = new();

    public string Name => "Vic20Rasterizer";
    public RenderSize NativeSize { get; }
    public PixelFormat PixelFormat { get; } = PixelFormat.Bgra32;
    public int StrideBytes { get; }
    public event EventHandler? FrameCompleted;

    public Vic20Rasterizer(Vic20 vic20)
    {
        _vic20 = vic20;

        var width = vic20.VisibleWidth;
        var height = vic20.VisibleHeight;
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

        foreach (var entry in ColorMaps.Vic20ColorMap)
            _colorMap[entry.Key] = PackBgra(entry.Value.B, entry.Value.G, entry.Value.R, entry.Value.A);
    }

    public IReadOnlyList<LayerInfo> Layers => new LayerInfo[]
    {
        new(NativeSize, PixelFormat, StrideBytes, 1f, BlendMode.Normal, 0),
        new(NativeSize, PixelFormat, StrideBytes, 1f, BlendMode.Overlay, 1)
    };

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
        var layout = _vic20.CurrentVideoLayout;
        const int scaleX = Vic20Config.PixelScaleX;
        const int cellPixelWidth = 8 * scaleX;

        var cols = layout.Columns;
        var rows = layout.Rows;
        var originX = layout.HorizontalOriginPixels;
        var originY = layout.VerticalOriginPixels;
        var verticalScale = Math.Max(1, layout.CharacterHeight / 8);

        var borderColor = GetColor(layout.BorderColor);
        var backgroundColor = GetColor(layout.BackgroundColor);
        var auxiliaryColor = GetColor(layout.AuxiliaryColor);

        Array.Fill(_backBackground, borderColor);
        Array.Clear(_backForeground);

        if (originX >= NativeSize.Width || originY >= NativeSize.Height)
            return;

        var firstVisibleCol = Math.Max(0, DivideCeiling(-originX, cellPixelWidth));
        var firstVisibleRow = Math.Max(0, DivideCeiling(-originY, layout.CharacterHeight));
        var pastLastVisibleCol = Math.Min(cols, DivideCeiling(NativeSize.Width - originX, cellPixelWidth));
        var pastLastVisibleRow = Math.Min(rows, DivideCeiling(NativeSize.Height - originY, layout.CharacterHeight));

        if (firstVisibleCol >= pastLastVisibleCol || firstVisibleRow >= pastLastVisibleRow)
            return;

        for (var row = firstVisibleRow; row < pastLastVisibleRow; row++)
        {
            for (var col = firstVisibleCol; col < pastLastVisibleCol; col++)
            {
                var screenAddress = (ushort)(layout.ScreenStartAddress + (row * layout.Columns) + col);
                var colorAddress = (ushort)(layout.ColorStartAddress + (row * layout.Columns) + col);
                var characterCode = _vic20.Mem[screenAddress];
                var colorRamValue = (byte)(_vic20.Mem[colorAddress] & 0x0F);
                var multicolor = (colorRamValue & 0x08) != 0;
                var foregroundColor = GetColor((byte)(colorRamValue & 0x07));
                var zeroBitColor = layout.ReverseScreen ? foregroundColor : backgroundColor;
                var oneBitColor = layout.ReverseScreen ? backgroundColor : foregroundColor;

                var cellPixelX = originX + (col * cellPixelWidth);
                var cellPixelY = originY + (row * layout.CharacterHeight);
                var glyphBaseAddress = ResolveGlyphBaseAddress(layout.CharacterStartAddress, characterCode);

                for (var glyphRow = 0; glyphRow < 8; glyphRow++)
                {
                    var glyphLine = _vic20.Mem[(ushort)(glyphBaseAddress + glyphRow)];
                    for (var stretchRow = 0; stretchRow < verticalScale; stretchRow++)
                    {
                        var pixelY = cellPixelY + (glyphRow * verticalScale) + stretchRow;
                        if (pixelY < 0)
                            continue;
                        if (pixelY >= NativeSize.Height)
                            break;

                        var rowOffset = pixelY * NativeSize.Width;

                        if (multicolor)
                        {
                            for (var pair = 0; pair < 4; pair++)
                            {
                                var pairValue = (glyphLine >> (6 - (pair * 2))) & 0x03;
                                var pairColor = pairValue switch
                                {
                                    0b00 => backgroundColor,
                                    0b01 => borderColor,
                                    0b10 => foregroundColor,
                                    _ => auxiliaryColor,
                                };

                                // Each pair covers 2 source pixels; with horizontal scaling each pair spans 2*scaleX buffer pixels.
                                var pairPixelStart = cellPixelX + (pair * 2 * scaleX);
                                for (var x = 0; x < 2 * scaleX; x++)
                                {
                                    var pixelX = pairPixelStart + x;
                                    if (pixelX < 0)
                                        continue;
                                    if (pixelX >= NativeSize.Width)
                                        break;
                                    SetRasterPixel(rowOffset + pixelX, backgroundColor, pairColor);
                                }
                            }
                        }
                        else
                        {
                            for (var bit = 0; bit < 8; bit++)
                            {
                                var set = ((glyphLine >> (7 - bit)) & 0x01) != 0;
                                var pixelColor = set ? oneBitColor : zeroBitColor;
                                var bitPixelStart = cellPixelX + (bit * scaleX);
                                for (var x = 0; x < scaleX; x++)
                                {
                                    var pixelX = bitPixelStart + x;
                                    if (pixelX < 0)
                                        continue;
                                    if (pixelX >= NativeSize.Width)
                                        break;
                                    SetRasterPixel(rowOffset + pixelX, zeroBitColor, pixelColor);
                                }
                            }
                        }
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

    private static ushort ResolveGlyphBaseAddress(ushort characterStartAddress, byte characterCode)
    {
        var glyphOffset = characterCode * 8;
        var bankStartAddress = (characterStartAddress & 0x8000) != 0 ? 0x8000 : 0x0000;
        var offsetWithinBank = (characterStartAddress - bankStartAddress + glyphOffset) & 0x1FFF;
        return (ushort)(bankStartAddress + offsetWithinBank);
    }

    private static int DivideCeiling(int numerator, int denominator)
    {
        return (numerator + denominator - 1) / denominator;
    }

    private uint GetColor(byte colorCode)
    {
        if (_colorMap.TryGetValue(colorCode, out var color))
            return color;
        return PackBgra(0, 0, 0, 255);
    }
}
