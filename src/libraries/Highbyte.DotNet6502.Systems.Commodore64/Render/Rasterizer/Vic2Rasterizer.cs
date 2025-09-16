using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;

[DisplayName("Rasterizer")]
[HelpText("A VIC-II rasterizer that generates raw pixel data in two layers: background and foreground.\nThe rasterizer writes directly to byte arrays for efficient pixel manipulation.")]
public sealed class Vic2Rasterizer : IRenderProvider, IVideoFrameLayerProvider
{
    public string Name => "Vic2Rasterizer";

    private readonly C64 _c64;

    // Double buffered raw bytes (front/back)
    private readonly bool _useDoubleBuffering;
    private byte[] _frontBackground, _frontForeground;  // Buffer 1 Front -> Read by RenderTarget
    private byte[] _backBackground, _backForeground;    // Buffer 2 Back -> Written to by Rasterizer

    public RenderSize NativeSize { get; }
    public PixelFormat PixelFormat { get; } = PixelFormat.Bgra32; //PixelFormat.Rgba32;
    public int StrideBytes { get; }

    public event EventHandler<int>? ScanlineCompleted;
    public event EventHandler? FrameCompleted;

    public ReadOnlyMemory<byte> CurrentFrontBuffer => _frontBackground; // Should not be used by RenderTarget, it must use CurrentFrontLayerBuffers instead.

    private readonly Vic2RasterizerUintPixelGenerator _pixelGenerator;
    public Vic2Rasterizer(C64 c64, bool useDoubleBuffering = true)
    {
        var width = c64.Screen.VisibleWidth;
        var height = c64.Screen.VisibleHeight;
        NativeSize = new(width, height);
        StrideBytes = width * 4;
        _frontBackground = new byte[StrideBytes * height];
        _frontForeground = new byte[StrideBytes * height];
        _backBackground = new byte[StrideBytes * height];
        _backForeground = new byte[StrideBytes * height];
        _c64 = c64;
        _useDoubleBuffering = useDoubleBuffering;

        _pixelGenerator = new Vic2RasterizerUintPixelGenerator(
            _c64,
            SetBackgroundPixels,
            ClearBackgroundPixels,
            SetForegroundPixels,
            ClearForegroundPixels);
    }

    #region C64 emulator integration points
    // For possible future improvement: Called by the master clock as the VIC-II advances
    //public void OnCycle(/* bus signals, registers, fetches */)
    //{
    //    // Perform character/bitmap fetch, sprites, borders, color mixing, etc.
    //    // Write pixels of current x,y into _back at [y*StrideBytes + x*4 ..]
    //}

    // Called after each instruction
    public void OnAfterInstruction()
    {
        // Write pixels of current x,y into _back at [y*StrideBytes + x*4 ..]
        _pixelGenerator.OnAfterInstruction();
    }

    public void OnEndScanline(int y)
    {
        ScanlineCompleted?.Invoke(this, y);
    }

    public void OnEndFrame()
    {
        _pixelGenerator.OnEndFrame();

        // Swap front/back so readers see a coherent frame
        FlipBuffers();
        FrameCompleted?.Invoke(this, EventArgs.Empty);
        // Clear/prepare _back for next frame if needed
    }

    public void SetPreCreatedBufferFromLegacyRender(uint[] pixelArrayBackground, uint[] pixelArrayForeground)
    {
        // Note: This will create a copy of the pixelArray, it will do for now until the emulator image generation code is implemented here by writing to byte arrays instead of to uint arrays in C64RenderBase.
        // Note: MemoryMarshal.AsBytes will require setting PixelFormat to Bgra32 so correct rgba order is used by the render code that writes it to the screen.
        var pixelBytesBackground = MemoryMarshal.AsBytes(pixelArrayBackground.AsSpan()).ToArray();
        var pixelBytesForeground = MemoryMarshal.AsBytes(pixelArrayForeground.AsSpan()).ToArray();

        _backBackground = pixelBytesBackground;
        _backForeground = pixelBytesForeground;

        _frontBackground = pixelBytesBackground;
        _frontForeground = pixelBytesForeground;
    }
    #endregion

    #region IVideoLayerProvider related methods for providing the pixels to the consumers
    public IReadOnlyList<LayerInfo> Layers => new LayerInfo[]
    {
        new LayerInfo(
            Size: NativeSize,
            PixelFormat: PixelFormat,
            StrideBytes: StrideBytes,
            Opacity: 1f,
            BlendMode: BlendMode.Normal,
            ZOrder: 0),
        new LayerInfo(
            Size: NativeSize,
            PixelFormat: PixelFormat,
            StrideBytes: StrideBytes,
            Opacity: 1f,
            BlendMode: BlendMode.Overlay,
            ZOrder: 1)
    };

    public IReadOnlyList<ReadOnlyMemory<byte>> CurrentFrontLayerBuffers => new List<ReadOnlyMemory<byte>>()
    {
        _frontBackground,
        _frontForeground
    };

    public void FlipBuffers()
    {
        if (!_useDoubleBuffering)
            return;

        // Swap front/back references using a temp variable
        var tmpFrontBackground = _frontBackground;
        _frontBackground = _backBackground;
        _backBackground = tmpFrontBackground;

        var tmpFrontForeground = _frontForeground;
        _frontForeground = _backForeground;
        _backForeground = tmpFrontForeground;
    }
    #endregion

    #region Helper methods for writing pixels

    private void SetBackgroundPixels(Span<uint> source, int sourceIndex, int destIndex, int width)
    {
        source.Slice(sourceIndex, width).CopyTo(BackBackgroundBufferU32.Slice(destIndex, width));
    }
    private void ClearBackgroundPixels(int destIndex, int width)
    {
        BackBackgroundBufferU32.Slice(destIndex, width).Clear();
    }

    private void SetForegroundPixels(Span<uint> source, int sourceIndex, int destIndex, int width)
    {
        source.Slice(sourceIndex, width).CopyTo(BackForegroundBufferU32.Slice(destIndex, width));
    }
    private void ClearForegroundPixels(int destIndex, int width)
    {
        BackForegroundBufferU32.Slice(destIndex, width).Clear();
    }


    // Efficient pixel write APIs on top of a byte[] using 32-bit stores.
    // Prefer using a cached Span<uint> outside tight loops to avoid re-creating the span repeatedly.
    // Recreate the Span<uint> after buffers has been swapped by FlipBuffers() (typically after EndFrame is called).
    private Span<uint> BackBackgroundBufferU32 => MemoryMarshal.Cast<byte, uint>(_backBackground);
    private Span<uint> BackForegroundBufferU32 => MemoryMarshal.Cast<byte, uint>(_backForeground);

    // Assume CPU is little-endian. All mainstream desktop/laptop CPUs run little-endian: x86-64 (Intel/AMD) and ARM64 (Apple Silicon, Qualcomm, most Chromebooks).
    // BGRA order (PixelFormat.Bgra32), packed as 0xAARRGGBB in register => B,G,R,A in memory (little-endian).
    // If PixelFormat is changed to Rgba32, adjust the packer.
    public static uint PackBgra(byte b, byte g, byte r, byte a)
        => (uint)(b | g << 8 | r << 16 | a << 24);
    public void SetPixelPackedBgra(int x, int y, uint packedBgra, bool foreground)
    {
        var index = y * NativeSize.Width + x;
        if (foreground)
            BackForegroundBufferU32[index] = packedBgra; // single 32-bit store
        else
            BackBackgroundBufferU32[index] = packedBgra; // single 32-bit store
    }
    public void SetPixelBgra(int x, int y, byte b, byte g, byte r, byte a, bool foreground)
        => SetPixelPackedBgra(x, y, PackBgra(b, g, r, a), foreground);

    #endregion
}
