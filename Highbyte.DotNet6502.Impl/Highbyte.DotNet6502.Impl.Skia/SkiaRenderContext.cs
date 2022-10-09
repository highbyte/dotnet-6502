using Highbyte.DotNet6502.Systems;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia;

public class SkiaRenderContext: IRenderContext
{
    public GRContext? GRContext { get; private set;}
    public GRBackendRenderTarget? RenderTarget { get; private set;}
    public SKSurface? RenderSurface { get; private set; }
    public SKCanvas? Canvas => RenderSurface?.Canvas;


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
    }

    public void Cleanup()
    {
        RenderSurface?.Dispose();
        RenderSurface = null;
        GRContext?.Dispose();
        GRContext = null;
    }

}
