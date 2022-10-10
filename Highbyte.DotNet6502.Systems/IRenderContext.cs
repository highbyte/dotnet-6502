namespace Highbyte.DotNet6502.Systems
{
    public interface IRenderContext
    {
    }

    public interface IRenderContext<TSystem> : IRenderContext
        where TSystem : ISystem
    {
    }


    public class NullRenderContext : IRenderContext
    {
    }
}