// For possible future use as IVideoProvider with custom rendering using Skia-specific drawing - not currently used.

//using System.Runtime.InteropServices;
//using Highbyte.DotNet6502.Systems.Rendering;

//namespace Highbyte.DotNet6502.Impl.Skia;
///// <summary>
///// Off-screen Skia raster target that exposes frames via IVideoProvider.
///// Draw by calling <see cref="DrawFrame"/> with an Action&lt;SKCanvas&gt;.
///// </summary>
//public sealed class SkiaOffscreenVideoProvider : IVideoProvider, IDisposable
//{
//    private readonly object _sync = new();
//    private readonly byte[] _bufA, _bufB;
//    private readonly GCHandle _pinA, _pinB;
//    private readonly SKSurface _surfA, _surfB;
//    private readonly SKCanvas _canA, _canB;

//    private byte[] _front;
//    private byte[] _back;
//    private SKSurface _backSurface;
//    private SKCanvas _backCanvas;

//    public RenderSize NativeSize { get; }
//    public PixelFormat PixelFormat { get; }
//    public int StrideBytes { get; }
//    public event EventHandler? FrameCompleted;
//    public event EventHandler<int>? ScanlineCompleted; // Note: No ScanlineCompleted events from this video provider

//    public ReadOnlyMemory<byte> CurrentFrontBuffer
//    {
//        get
//        {
//            lock (_sync) return _front;
//        }
//    }

//    public SkiaOffscreenVideoProvider(RenderSize size, PixelFormat fmt = PixelFormat.Bgra32)
//    {
//        NativeSize = size;
//        PixelFormat = fmt;
//        StrideBytes = size.StrideBytes(fmt);

//        // Allocate two managed buffers and pin them so Skia can draw directly into them.
//        _bufA = GC.AllocateArray<byte>(StrideBytes * size.Height, pinned: false);
//        _bufB = GC.AllocateArray<byte>(StrideBytes * size.Height, pinned: false);
//        _pinA = GCHandle.Alloc(_bufA, GCHandleType.Pinned);
//        _pinB = GCHandle.Alloc(_bufB, GCHandleType.Pinned);

//        var colorType = fmt == PixelFormat.Bgra32 ? SKColorType.Bgra8888 : SKColorType.Rgba8888;
//        var info = new SKImageInfo(size.Width, size.Height, colorType, SKAlphaType.Unpremul);

//        _surfA = SKSurface.Create(info, _pinA.AddrOfPinnedObject(), StrideBytes);
//        _surfB = SKSurface.Create(info, _pinB.AddrOfPinnedObject(), StrideBytes);

//        _canA = _surfA.Canvas;
//        _canB = _surfB.Canvas;

//        // Start with A as front, B as back.
//        _front = _bufA;
//        _back = _bufB;
//        _backSurface = _surfB;
//        _backCanvas = _canB;

//        // Optional: clear both
//        _canA.Clear(SKColors.Transparent);
//        _canB.Clear(SKColors.Transparent);
//    }

//    /// <summary>
//    /// Draw one frame by issuing arbitrary Skia commands into the back buffer.
//    /// This method flips buffers and raises FrameCompleted when done.
//    /// </summary>
//    public void DrawFrame(Action<SKCanvas> draw)
//    {
//        if (draw is null) throw new ArgumentNullException(nameof(draw));
//        lock (_sync)
//        {
//            draw(_backCanvas);
//            _backCanvas.Flush();

//            // Present: swap front/back so readers get a coherent frame.
//            FlipLocked_NoEvent();
//        }
//        FrameCompleted?.Invoke(this, EventArgs.Empty);
//    }

//    /// <summary>
//    /// Swap front/back without drawing (useful if you render elsewhere and just want to present).
//    /// </summary>
//    public void FlipBuffers()
//    {
//        lock (_sync) FlipLocked_NoEvent();
//        FrameCompleted?.Invoke(this, EventArgs.Empty);
//    }

//    private void FlipLocked_NoEvent()
//    {
//        // Swap managed buffers
//        (_front, _back) = (_back, _front);

//        // Swap Skia surfaces/canvases to keep drawing into the "new" back buffer
//        if (ReferenceEquals(_back, _bufA))
//        {
//            _backSurface = _surfA;
//            _backCanvas = _canA;
//        }
//        else
//        {
//            _backSurface = _surfB;
//            _backCanvas = _canB;
//        }
//    }

//    /// <summary>
//    /// Gives you direct access to the back-canvas for more complex lifetimes:
//    /// using (var frame = provider.BeginDraw()) { var c = frame.Canvas; ... } // auto flip on Dispose
//    /// </summary>
//    public FrameScope BeginDraw(SKColor? clear = null)
//    {
//        System.Threading.Monitor.Enter(_sync);
//        if (clear is SKColor c) _backCanvas.Clear(c);
//        return new FrameScope(this, _backCanvas);
//    }

//    public readonly ref struct FrameScope
//    {
//        private readonly SkiaOffscreenVideoProvider _owner;
//        public SKCanvas Canvas { get; }
//        internal FrameScope(SkiaOffscreenVideoProvider owner, SKCanvas canvas)
//        {
//            _owner = owner; Canvas = canvas;
//        }
//        public void Dispose()
//        {
//            _owner._backCanvas.Flush();
//            _owner.FlipLocked_NoEvent();
//            System.Threading.Monitor.Exit(_owner._sync);
//            _owner.FrameCompleted?.Invoke(_owner, EventArgs.Empty);
//        }
//    }

//    public void Dispose()
//    {
//        // Surfaces own no extra memory; safe to dispose any order
//        _surfA.Dispose();
//        _surfB.Dispose();
//        // Canvases disposed with surfaces, but disposing explicitly is fine
//        _canA.Dispose();
//        _canB.Dispose();
//        if (_pinA.IsAllocated) _pinA.Free();
//        if (_pinB.IsAllocated) _pinB.Free();
//    }
//}
