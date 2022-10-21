using Highbyte.DotNet6502.Systems;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia;

public class SkiaRenderContext : IRenderContext
{
    public GRContext? GRContext { get; private set; }
    public GRBackendRenderTarget? RenderTarget { get; private set; }
    public SKSurface? RenderSurface { get; private set; }

    public Func<SKCanvas> GetCanvas => GetCanvasInternal;

    private readonly SKCanvas? _canvas;
    private readonly Func<SKCanvas>? _getSkCanvasExternal;

    private SKCanvas GetCanvasInternal()
    {
        if (_canvas != null)
            return _canvas;
        if (_getSkCanvasExternal != null)
            return _getSkCanvasExternal();
        throw new Exception("Internal error. SkCanvas not configured.");
    }

    public SkiaRenderContext(SKCanvas skCanvas)
    {
        _canvas = skCanvas;
    }

    public SkiaRenderContext(Func<SKCanvas> getSkCanvas)
    {
        _getSkCanvasExternal = getSkCanvas;
    }

    public SkiaRenderContext(int sizeX, int sizeY, float scale = 1.0f)
    {
        // Create the SkiaSharp context
        var glInterface = GRGlInterface.Create();
        var grContextOptions = new GRContextOptions{};
        GRContext = GRContext.CreateGl(glInterface, grContextOptions);
        if (GRContext == null)
            throw new Exception("Cannot create OpenGL context.");

        // Create main Skia surface from OpenGL context
        var glFramebufferInfo = new GRGlFramebufferInfo(
                fboId: 0,
                format: SKColorType.Rgba8888.ToGlSizedFormat());

        RenderTarget = new GRBackendRenderTarget(sizeX, sizeY, sampleCount: 0, stencilBits: 0, glInfo: glFramebufferInfo);

        // Create the SkiaSharp render target surface
        RenderSurface = SKSurface.Create(GRContext, RenderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        if (GRContext == null)
            throw new Exception("Cannot create SkiaSharp SKSurface.");

        RenderSurface.Canvas.Scale(scale);

        _canvas = RenderSurface.Canvas;
    }

    public void Cleanup()
    {
        RenderSurface?.Dispose();
        RenderSurface = null;
        GRContext?.Dispose();
        GRContext = null;
    }

}
