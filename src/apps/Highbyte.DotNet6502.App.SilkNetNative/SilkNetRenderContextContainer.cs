using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative;

public class SilkNetRenderContextContainer : IRenderContext
{
    private SkiaRenderContext _skiaRenderContext;
    private SilkNetOpenGlRenderContext _silkNetOpenGlRenderContext;

    public SkiaRenderContext SkiaRenderContext => _skiaRenderContext;
    public SilkNetOpenGlRenderContext SilkNetOpenGlRenderContext => _silkNetOpenGlRenderContext;

    public bool IsInitialized { get; private set; } = false;


    public SilkNetRenderContextContainer(SkiaRenderContext skiaRenderContext, SilkNetOpenGlRenderContext silkNetOpenGlRenderContext)
    {
        _skiaRenderContext = skiaRenderContext;
        _silkNetOpenGlRenderContext = silkNetOpenGlRenderContext;
    }

    public void Init()
    {
        _skiaRenderContext?.Init();
        _silkNetOpenGlRenderContext?.Init();
        IsInitialized = true;
    }

    public void Cleanup()
    {
        _skiaRenderContext?.Cleanup();
        _silkNetOpenGlRenderContext?.Cleanup();
    }
}
