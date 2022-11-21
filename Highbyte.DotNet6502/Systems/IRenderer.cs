namespace Highbyte.DotNet6502.Systems;

public interface IRenderer
{
    void Init(ISystem system, IRenderContext renderContext);
    void Draw(ISystem system);
}

public interface IRenderer<TSystem, TRenderContext> : IRenderer
    where TSystem : ISystem
    where TRenderContext : IRenderContext
{
    void Init(TSystem system, TRenderContext renderContext);
    void Draw(TSystem system);
}

public class NullRenderer<TSystem> : IRenderer<TSystem, NullRenderContext>, IRenderer
        where TSystem : ISystem
{
    public void Init(ISystem system, IRenderContext renderContext)
    {
    }

    public void Init(TSystem system, NullRenderContext renderContext)
    {
        Init((ISystem)system, renderContext);
    }

    public void Draw(ISystem system)
    {
    }

    public void Draw(TSystem system)
    {
        Draw((ISystem)system);
    }
}