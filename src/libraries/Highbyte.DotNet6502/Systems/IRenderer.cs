namespace Highbyte.DotNet6502.Systems;

public interface IRenderer
{
    void Init(ISystem system, IRenderContext renderContext);
    void Draw(ISystem system, Dictionary<string, double> detailedStats);

    public bool HasDetailedStats { get; }
    public List<string> DetailedStatNames { get; }

}

public interface IRenderer<TSystem, TRenderContext> : IRenderer
    where TSystem : ISystem
    where TRenderContext : IRenderContext
{
    void Init(TSystem system, TRenderContext renderContext);
    void Draw(TSystem system, Dictionary<string, double> detailedStats);
}

public class NullRenderer : IRenderer
{
    public bool HasDetailedStats => false;
    public List<string> DetailedStatNames => new List<string>();

    public void Init(ISystem system, IRenderContext renderContext)
    {
    }

    public void Draw(ISystem system, Dictionary<string, double> detailedStats)
    {
    }
}
