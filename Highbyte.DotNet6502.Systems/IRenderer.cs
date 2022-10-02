namespace Highbyte.DotNet6502.Systems
{
    public interface IRenderer
    {
        int Width { get; }
        int Height { get; }
        void Draw(ISystem system);
    }

    public interface IRenderer<TSystem> : IRenderer where TSystem : ISystem
    {
        void Draw(TSystem system);
    }
}