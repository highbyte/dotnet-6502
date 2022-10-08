namespace Highbyte.DotNet6502.Systems
{
    public interface IRenderer
    {
        void Init(ISystem system, IRenderContext renderContext);
        void Draw(ISystem system);
    }

    public interface IRenderer<TSystem, TRenderContext> : IRenderer
        where TSystem : ISystem
        where TRenderContext: IRenderContext
    {
        void Init(TSystem system, TRenderContext renderContext);
        void Draw(TSystem system);
    }
}