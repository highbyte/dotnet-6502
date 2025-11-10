using Avalonia.Media.Imaging;
using Highbyte.DotNet6502.Systems.Rendering;

namespace Highbyte.DotNet6502.Impl.Avalonia.Render;

public interface IAvaloniaBitmapRenderTarget : IRenderTarget
{
    public WriteableBitmap Bitmap { get; }
}
