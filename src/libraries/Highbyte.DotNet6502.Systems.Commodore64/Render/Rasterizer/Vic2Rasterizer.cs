using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;

[DisplayName("Rasterizer")]
[HelpText("A VIC-II rasterizer that generates raw pixel data in two layers: background and foreground.\nThe rasterizer writes directly to uint arrays for efficient 32-bit pixel manipulation.")]
/// Generates bitmaps as uint arrays for the C64 screen.
/// 
/// Overview
/// - Called after each instruction to generate Text and Bitmap graphics.
/// - Called once per frame to generate Sprites (if possible a future improvement should make this also be called after each instruction if performance allows it).
/// - Writes background and foreground to separate uint arrays. Renderer needs to combine these two layers.
/// - Uses uint arrays directly for 32-bit pixel operations (no casting overhead).
/// - Fast enough to be used in native apps. For browser (WASM) app if the computer is reasonably fast.
/// 
/// Supports:
/// - Text mode (Standard, Extended, MultiColor)
/// - Bitmap mode (Standard/HiRes, MultiColor)
/// - Colors per raster line
/// - Fine scroll per raster line
/// - Sprites (Standard, MultiColor). No multiplexing support.

public sealed class Vic2Rasterizer : IRenderProvider, IVideoFrameLayerProvider
{
    public string Name => "Vic2Rasterizer";

    private readonly C64 _c64;

    // Double buffered raw uint pixels (front/back) - direct 32-bit pixel access
    private readonly bool _useDoubleBuffering;
    private uint[] _frontBackground, _frontForeground;  // Buffer 1 Front -> Read by RenderTarget
    private uint[] _backBackground, _backForeground;    // Buffer 2 Back -> Written to by Rasterizer

    // Thread-safe buffer access: allows multiple readers OR one writer
    private readonly ReaderWriterLockSlim _bufferLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    private readonly ReaderWriterLockSlim _bufferLock2 = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

    public RenderSize NativeSize { get; }
    public PixelFormat PixelFormat { get; } = PixelFormat.Bgra32; //PixelFormat.Rgba32;
    public int StrideBytes { get; }

    //public event EventHandler<int>? ScanlineCompleted;
    public event EventHandler? FrameCompleted;

    public ReadOnlyMemory<uint> CurrentFrontBuffer
    {
        get
        {
            _bufferLock2.EnterReadLock();
            try
            {
                // Return thin wrapper around the memory (zero-copy)
                return _frontBackground.AsMemory();
            }
            finally
            {
                _bufferLock2.ExitReadLock();
            }
        }
    }

    private readonly Vic2RasterizerUintPixelGenerator _pixelGenerator;

    public Vic2Rasterizer(C64 c64, bool useDoubleBuffering = true)
    {
        var width = c64.Screen.VisibleWidth;
        var height = c64.Screen.VisibleHeight;
        NativeSize = new(width, height);
        StrideBytes = width * 4;

        var pixelCount = width * height;
        _frontBackground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);
        _frontForeground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);
        _backBackground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);
        _backForeground = GC.AllocateUninitializedArray<uint>(pixelCount, pinned: true);

        _c64 = c64;
        _useDoubleBuffering = useDoubleBuffering;

        _pixelGenerator = new Vic2RasterizerUintPixelGenerator(
            _c64,
            SetPixel,
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

    //public void OnEndScanline(int y)
    //{
    //    ScanlineCompleted?.Invoke(this, y);
    //}

    // Called once per frame after all OnAfterInstruction calls are executed
    public void OnEndFrame()
    {
        _pixelGenerator.OnEndFrame();

        // Swap front/back so readers see a coherent frame
        FlipBuffers();
        FrameCompleted?.Invoke(this, EventArgs.Empty);
        // Clear/prepare _back for next frame if needed
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

    public IReadOnlyList<ReadOnlyMemory<uint>> CurrentFrontLayerBuffers
    {
        get
        {
            _bufferLock.EnterReadLock();
            try
            {
                return new List<ReadOnlyMemory<uint>>()
                {
                    // Return thin wrapper around the memory (zero-copy)
                    _frontBackground.AsMemory(),
                    _frontForeground.AsMemory()
                };
            }
            finally
            {
                _bufferLock.ExitReadLock();
            }
        }
    }

    public void FlipBuffers()
    {
        if (!_useDoubleBuffering)
            return;

        _bufferLock.EnterWriteLock();
        try
        {
            // Swap front/back references using a temp variable
            var tmpFrontBackground = _frontBackground;
            _frontBackground = _backBackground;
            _backBackground = tmpFrontBackground;

            var tmpFrontForeground = _frontForeground;
            _frontForeground = _backForeground;
            _backForeground = tmpFrontForeground;
        }
        finally
        {
            _bufferLock.ExitWriteLock();
        }
    }
    #endregion

    #region Helper methods for writing pixels

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetBackgroundPixels(Span<uint> source, int sourceIndex, int destIndex, int width)
    {
        Span<uint> dest = _backBackground.AsSpan();
        source.Slice(sourceIndex, width).CopyTo(dest.Slice(destIndex, width));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearBackgroundPixels(int destIndex, int width)
    {
        Span<uint> dest = _backBackground.AsSpan();
        dest.Slice(destIndex, width).Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetForegroundPixels(Span<uint> source, int sourceIndex, int destIndex, int width)
    {
        Span<uint> dest = _backForeground.AsSpan();
        source.Slice(sourceIndex, width).CopyTo(dest.Slice(destIndex, width));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearForegroundPixels(int destIndex, int width)
    {
        Span<uint> dest = _backForeground.AsSpan();
        dest.Slice(destIndex, width).Clear();
    }

    // Assume CPU is little-endian. All mainstream desktop/laptop CPUs run little-endian: x86-64 (Intel/AMD) and ARM64 (Apple Silicon, Qualcomm, most Chromebooks).
    // BGRA order (PixelFormat.Bgra32), packed as 0xAARRGGBB in register => B,G,R,A in memory (little-endian).
    // If PixelFormat is changed to Rgba32, adjust the packer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint PackBgra(byte b, byte g, byte r, byte a)
        => (uint)(b | g << 8 | r << 16 | a << 24);



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixel(uint packedBgra, int index, bool foreground)
    {
        // RELEASE builds optimization:
        //   - Removes the per-access bounds-check for the hot per-pixel write path.
        //   - If there's any chance index can be out-of-range, this will lead to memory corruption.
        // DEBUG builds
        //   - Keep the bounds-checks to catch possible bugs during development.

        if (foreground)
        {
#if DEBUG
            _backForeground[index] = packedBgra;
#else
            ref uint baseRef = ref MemoryMarshal.GetArrayDataReference(_backForeground);
            Unsafe.Add(ref baseRef, index) = packedBgra;
#endif
        }
        else
        {
#if DEBUG
            _backBackground[index] = packedBgra;
#else
            ref uint baseRef = ref MemoryMarshal.GetArrayDataReference(_backBackground);
            Unsafe.Add(ref baseRef, index) = packedBgra;
#endif
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixelPackedBgra(uint packedBgra, int x, int y, bool foreground)
    {
        var index = y * NativeSize.Width + x;
        SetPixel(packedBgra, index, foreground);
    }

    public void SetPixelBgra(byte b, byte g, byte r, byte a, int x, int y, bool foreground)
        => SetPixelPackedBgra(PackBgra(b, g, r, a), x, y, foreground);

#endregion
}
