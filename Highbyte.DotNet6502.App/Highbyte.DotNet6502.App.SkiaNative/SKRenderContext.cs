using SkiaSharp;

namespace Highbyte.DotNet6502.App.SkiaNative;

public class SKRenderContext
{
    public GRContext? Context { get; private set;}
    public GRBackendRenderTarget? RenderTarget { get; private set;}
    public SKSurface? RenderSurface { get; private set; }
    public SKCanvas? Canvas { get {return RenderSurface != null ? RenderSurface.Canvas: null; } }
    

    public SKRenderContext(int sizeX, int sizeY, float scale = 1.0f)
    {
        // Create the SkiaSharp context
        var glInterface = GRGlInterface.Create();
        var grContextOptions = new GRContextOptions{};
        Context = GRContext.CreateGl(glInterface, grContextOptions);
        if(Context == null)
            throw new Exception("Cannot create OpenGL context.");

        // Create main Skia surface from OpenGL context
        var glFramebufferInfo = new GRGlFramebufferInfo(
                fboId: 0,
                format: SKColorType.Rgba8888.ToGlSizedFormat());

        RenderTarget = new GRBackendRenderTarget(sizeX, sizeY, sampleCount: 0, stencilBits: 0, glInfo: glFramebufferInfo);

        // Create the SkiaSharp render target surface
        RenderSurface = SKSurface.Create(Context, RenderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        if(Context == null)
            throw new Exception("Cannot create SkiaSharp SKSurface.");

        RenderSurface.Canvas.Scale(scale);
    }

    public void CleanUp()
    {
        RenderSurface?.Dispose();
        RenderSurface = null;
        Context?.Dispose();
        Context = null;        
    }

}
