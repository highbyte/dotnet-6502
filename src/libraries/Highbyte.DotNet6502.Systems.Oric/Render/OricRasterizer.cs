using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;
using OricMachine = Highbyte.DotNet6502.Systems.Oric.Oric;

namespace Highbyte.DotNet6502.Systems.Oric.Render;

/// <summary>
/// Renders the 240x224 active Oric display, including serial ink/paper, character and mode
/// attributes, text/hires switching, alternate character sets, inverse and flashing text.
/// </summary>
[DisplayName("Rasterizer")]
[HelpText("Renders Oric text and high-resolution graphics as exact pixels.")]
public sealed class OricRasterizer : IRenderProvider, IVideoFrameLayerProvider
{
    public const ushort TextScreenAddress = 0xbb80;
    public const ushort HiResScreenAddress = 0xa000;
    public const ushort TextStandardCharsetAddress = 0xb400;
    public const ushort TextAlternateCharsetAddress = 0xb800;
    public const ushort HiResStandardCharsetAddress = 0x9800;
    public const ushort HiResAlternateCharsetAddress = 0x9c00;

    private static readonly uint[] s_palette =
    [
        PackBgra(0x00, 0x00, 0x00, 0xff), // black
        PackBgra(0x00, 0x00, 0xff, 0xff), // red
        PackBgra(0x00, 0xff, 0x00, 0xff), // green
        PackBgra(0x00, 0xff, 0xff, 0xff), // yellow
        PackBgra(0xff, 0x00, 0x00, 0xff), // blue
        PackBgra(0xff, 0x00, 0xff, 0xff), // magenta
        PackBgra(0xff, 0xff, 0x00, 0xff), // cyan
        PackBgra(0xff, 0xff, 0xff, 0xff), // white
    ];

    private readonly OricMachine _oric;
    private readonly ReaderWriterLockSlim _bufferLock = new(LockRecursionPolicy.NoRecursion);
    private uint[] _frontBackground;
    private uint[] _frontForeground;
    private uint[] _backBackground;
    private uint[] _backForeground;
    private readonly ReadOnlyMemory<uint>[] _cachedLayerBuffers;
    private byte _screenAttributes;
    private int _frameCounter;

    public OricRasterizer(OricMachine oric)
    {
        _oric = oric;
        NativeSize = new RenderSize(OricConfig.VisibleWidth, OricConfig.VisibleHeight);
        StrideBytes = NativeSize.Width * 4;
        var pixels = NativeSize.Width * NativeSize.Height;
        _frontBackground = GC.AllocateUninitializedArray<uint>(pixels, pinned: true);
        _frontForeground = GC.AllocateUninitializedArray<uint>(pixels, pinned: true);
        _backBackground = GC.AllocateUninitializedArray<uint>(pixels, pinned: true);
        _backForeground = GC.AllocateUninitializedArray<uint>(pixels, pinned: true);
        _cachedLayerBuffers = [_frontBackground.AsMemory(), _frontForeground.AsMemory()];
    }

    public string Name => nameof(OricRasterizer);
    public RenderSize NativeSize { get; }
    public PixelFormat PixelFormat { get; } = PixelFormat.Bgra32;
    public int StrideBytes { get; }
    public event EventHandler? FrameCompleted;

    public IReadOnlyList<LayerInfo> Layers { get; } =
    [
        new(new RenderSize(OricConfig.VisibleWidth, OricConfig.VisibleHeight), PixelFormat.Bgra32,
            OricConfig.VisibleWidth * 4, 1f, BlendMode.Normal, 0),
        new(new RenderSize(OricConfig.VisibleWidth, OricConfig.VisibleHeight), PixelFormat.Bgra32,
            OricConfig.VisibleWidth * 4, 1f, BlendMode.Overlay, 1),
    ];

    public IReadOnlyList<ReadOnlyMemory<uint>> CurrentFrontLayerBuffers
    {
        get
        {
            _bufferLock.EnterReadLock();
            try { return _cachedLayerBuffers; }
            finally { _bufferLock.ExitReadLock(); }
        }
    }

    public ReadOnlyMemory<uint> CurrentFrontBuffer
    {
        get
        {
            _bufferLock.EnterReadLock();
            try { return _frontBackground.AsMemory(); }
            finally { _bufferLock.ExitReadLock(); }
        }
    }

    public void OnAfterInstruction() { }

    public void OnEndFrame()
    {
        _frameCounter++;
        RasterizeFrame();
        FlipBuffers();
        FrameCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _screenAttributes = 0;
        _frameCounter = 0;
    }

    private void RasterizeFrame()
    {
        Array.Fill(_backBackground, s_palette[0]);
        Array.Clear(_backForeground);

        for (var y = 0; y < OricConfig.VisibleHeight; y++)
            RasterizeLine(y);
    }

    private void RasterizeLine(int y)
    {
        byte ink = 7;
        byte paper = 0;
        byte characterAttributes = 0;

        for (var column = 0; column < OricConfig.Columns; column++)
        {
            var hires = (_screenAttributes & 0x04) != 0 && y < OricConfig.HiResHeight;
            var value = hires
                ? _oric.Mem[(ushort)(HiResScreenAddress + y * OricConfig.Columns + column)]
                : _oric.Mem[(ushort)(TextScreenAddress + (y / OricConfig.CharacterHeight) * OricConfig.Columns + column)];

            byte pattern;
            if ((value & 0x60) == 0)
            {
                pattern = 0;
                ApplyAttribute(value, ref ink, ref paper, ref characterAttributes);
            }
            else if (hires)
            {
                pattern = value;
            }
            else
            {
                var alternate = (characterAttributes & 0x01) != 0;
                var charset = (_screenAttributes & 0x04) != 0
                    ? (alternate ? HiResAlternateCharsetAddress : HiResStandardCharsetAddress)
                    : (alternate ? TextAlternateCharsetAddress : TextStandardCharsetAddress);
                var scanline = (characterAttributes & 0x02) != 0
                    ? (y / 2) & 0x07
                    : y & 0x07;
                var glyph = value & 0x7f;
                pattern = _oric.Mem[(ushort)(charset + glyph * OricConfig.CharacterHeight + scanline)];
            }

            var inverse = (value & 0x80) != 0;
            var blinkBlanked = (characterAttributes & 0x04) != 0 && ((_frameCounter / 25) & 1) != 0;
            var foreground = s_palette[inverse ? ink ^ 0x07 : ink];
            var background = s_palette[inverse ? paper ^ 0x07 : paper];
            if (blinkBlanked)
                foreground = background;

            var pixelOffset = y * NativeSize.Width + column * OricConfig.CharacterWidth;
            for (var bit = 0; bit < OricConfig.CharacterWidth; bit++)
            {
                var lit = (pattern & (0x20 >> bit)) != 0;
                _backBackground[pixelOffset + bit] = lit ? foreground : background;
            }
        }
    }

    private void ApplyAttribute(byte value, ref byte ink, ref byte paper, ref byte characterAttributes)
    {
        var setting = (byte)(value & 0x07);
        switch (value & 0x18)
        {
            case 0x00: ink = setting; break;
            case 0x08: characterAttributes = setting; break;
            case 0x10: paper = setting; break;
            case 0x18: _screenAttributes = setting; break;
        }
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
}
