using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IRenderer
{
    void Init(ISystem system, IRenderContext renderContext);
    void DrawFrame();

    void Cleanup();

    Instrumentations Instrumentations { get; }
}

public interface IRenderer<TSystem, TRenderContext> : IRenderer
    where TSystem : ISystem
    where TRenderContext : IRenderContext
{
    void Init(TSystem system, TRenderContext renderContext);
}

public class NullRenderer : IRenderer
{
    public Instrumentations Instrumentations { get; } = new();

    public void Init(ISystem system, IRenderContext renderContext)
    {
    }

    public void DrawFrame()
    {
    }

    public void Cleanup()
    {
    }
}
