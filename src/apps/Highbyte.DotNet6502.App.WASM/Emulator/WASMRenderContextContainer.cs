using Highbyte.DotNet6502.Impl.SilkNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.WASM.Emulator;

public class WASMRenderContextContainer : IRenderContext
{
    private SkiaRenderContext _skiaRenderContext;
    private SilkNetOpenGlRenderContext _silkNetOpenGlRenderContext;

    public SkiaRenderContext SkiaRenderContext => _skiaRenderContext;
    public SilkNetOpenGlRenderContext SilkNetOpenGlRenderContext => _silkNetOpenGlRenderContext;

    public WASMRenderContextContainer(SkiaRenderContext skiaRenderContext, SilkNetOpenGlRenderContext silkNetOpenGlRenderContext)
    {
        _skiaRenderContext = skiaRenderContext;
        _silkNetOpenGlRenderContext = silkNetOpenGlRenderContext;
    }

    internal void Cleanup()
    {
        SkiaRenderContext?.Cleanup();
        SilkNetOpenGlRenderContext?.Cleanup();
    }
}
