using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v2;

/// <summary>
/// Encapsulation of a SKBitmap that is backed by a pixel array (uint[]).
/// The pixels are manipulated by writing to the PixelArray uint[] property.
/// </summary>
public class SkiaBitmapBackedByPixelArray : IDisposable
{
    private readonly uint[] _pixelArray;
    private readonly SKBitmap _bitmap;
    private GCHandle _gcHandle;

    public uint[] PixelArray => _pixelArray;
    public SKBitmap Bitmap => _bitmap;

    private SkiaBitmapBackedByPixelArray(uint[] pixelArray, SKBitmap bitmap, GCHandle gcHandle)
    {
        _pixelArray = pixelArray;
        _bitmap = bitmap;
        _gcHandle = gcHandle;
    }

    public void Free()
    {
        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }
    }

    public void Dispose()
    {
        Free();
    }

    public static SkiaBitmapBackedByPixelArray Create(int width, int height)
    {
        var pixelArray = new uint[width * height];

        // Note: SKColorType.Bgra8888 seems to be needed for Blazor WASM. TODO: Does this affect when running in Blazor on Mac/Linux?
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        // Pin the managed pixel array so that the GC doesn't move it.
        // (It is essential that the pinned memory be unpinned after usage so that the memory can be freed by the GC.)
        var gcHandle = GCHandle.Alloc(pixelArray, GCHandleType.Pinned);

        var bitmap = new SKBitmap();
        bitmap.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes, delegate { gcHandle.Free(); }, null);

        var skiaBitmapBackedByPixelArray = new SkiaBitmapBackedByPixelArray(pixelArray, bitmap, gcHandle);
        return skiaBitmapBackedByPixelArray;
    }
}
