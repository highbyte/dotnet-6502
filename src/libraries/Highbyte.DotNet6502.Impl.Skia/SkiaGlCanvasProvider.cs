
namespace Highbyte.DotNet6502.Impl.Skia;

public class SkiaGlCanvasProvider : IDisposable
{
    public SKCanvas Canvas => _canvas;
    public GRContext GRContext => _grContext;

    private readonly GRGlInterface? _glInterface;
    private readonly GRBackendRenderTarget _renderTarget;
    private readonly GRContext _grContext;
    private readonly SKSurface _renderSurface;
    private readonly SKCanvas _canvas;

    public SkiaGlCanvasProvider(GRGlGetProcedureAddressDelegate getProcAddress, int sizeX, int sizeY, float scale = 1.0f)
    {
        // Create the SkiaSharp context
        //var glInterface = GRGlInterface.Create();

        Console.WriteLine("Creating GRGlInterface...");
        _glInterface = GRGlInterface.Create(name => getProcAddress(name)) ?? throw new DotNet6502Exception("Cannot create OpenGL interface.");
        Console.WriteLine("GRGlInterface created.");

        _glInterface.Validate();
        var grContextOptions = new GRContextOptions { };
        _grContext = GRContext.CreateGl(_glInterface, grContextOptions) ?? throw new DotNet6502Exception("Cannot create OpenGL context.");

        // Create main Skia surface from OpenGL context
        GRGlFramebufferInfo glFramebufferInfo = new(0, SKColorType.Rgba8888.ToGlSizedFormat());

        _renderTarget = new(
            sizeX,
            sizeY,
            0,
            8,
            glFramebufferInfo);

        // Create the SkiaSharp render target surface
        _renderSurface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888) ?? throw new DotNet6502Exception("Cannot create SkiaSharp SKSurface.");
        _renderSurface.Canvas.Scale(scale);

        _canvas = _renderSurface.Canvas;
    }

    public void Dispose()
    {
        _canvas.Clear();
        _canvas?.Dispose();
        _renderSurface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _glInterface?.Dispose();
    }
}
