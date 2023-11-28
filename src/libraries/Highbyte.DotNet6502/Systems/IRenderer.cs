using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IRenderer
{
    void Init(ISystem system, IRenderContext renderContext);
    void Draw(ISystem system);

    Instrumentations Stats { get; }
}

public interface IRenderer<TSystem, TRenderContext> : IRenderer
    where TSystem : ISystem
    where TRenderContext : IRenderContext
{
    void Init(TSystem system, TRenderContext renderContext);
    void Draw(TSystem system);
}

public class NullRenderer : IRenderer
{
    public Instrumentations Stats { get; } = new();

    public void Init(ISystem system, IRenderContext renderContext)
    {
    }

    public void Draw(ISystem system)
    {
    }
}
