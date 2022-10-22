using Highbyte.DotNet6502.Systems;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia;

public class SkiaRenderContext : IRenderContext
{
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _renderSurface;

    public Func<SKCanvas> GetCanvas => GetCanvasInternal;
    public Func<GRContext> GetGRContext => GetGRContextInternal;

    private SKCanvas? _canvas;
    private readonly Func<SKCanvas>? _getSkCanvasExternal;

    private GRContext? _grContext;
    private readonly Func<GRContext>? _getGrContextExternal;

    private SKCanvas GetCanvasInternal()
    {
        if (_canvas != null)
            return _canvas;
        if (_getSkCanvasExternal != null)
            return _getSkCanvasExternal();
        throw new Exception("Internal error. SkCanvas not configured.");
    }

    private GRContext GetGRContextInternal()
    {
        if (_grContext != null)
            return _grContext;
        if (_getGrContextExternal != null)
            return _getGrContextExternal();
        throw new Exception("Internal error. GRContext not configured.");
    }

    public SkiaRenderContext(Func<SKCanvas> getSkCanvas, Func<GRContext> getGrContext)
    {
        _getSkCanvasExternal = getSkCanvas;
        _getGrContextExternal = getGrContext;
    }

    public SkiaRenderContext(int sizeX, int sizeY, float scale = 1.0f)
    {
        // Create the SkiaSharp context
        var glInterface = GRGlInterface.Create();
        var grContextOptions = new GRContextOptions{};
        _grContext = GRContext.CreateGl(glInterface, grContextOptions);
        if (_grContext == null)
            throw new Exception("Cannot create OpenGL context.");

        // Create main Skia surface from OpenGL context
        var glFramebufferInfo = new GRGlFramebufferInfo(
                fboId: 0,
                format: SKColorType.Rgba8888.ToGlSizedFormat());

        _renderTarget = new GRBackendRenderTarget(sizeX, sizeY, sampleCount: 0, stencilBits: 0, glInfo: glFramebufferInfo);

        // Create the SkiaSharp render target surface
        _renderSurface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        if (_renderSurface == null)
            throw new Exception("Cannot create SkiaSharp SKSurface.");

        _renderSurface.Canvas.Scale(scale);

        _canvas = _renderSurface.Canvas;
    }

    public void Cleanup()
    {
        _canvas?.Dispose();
        _canvas = null;
        _renderSurface?.Dispose();
        _renderSurface = null;
        _grContext?.Dispose();
        _grContext = null;
    }

}
