using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IRenderer
{
    void Init();
    void DrawFrame();
    void Cleanup();
    Instrumentations Instrumentations { get; }
    ISystem System { get; }
}

public class NullRenderer : IRenderer
{
    private readonly ISystem _system;
    public ISystem System => _system;
    public Instrumentations Instrumentations { get; } = new();


    public NullRenderer(ISystem system)
    {
        _system = system;
    }
    public void Init()
    {
    }
    public void DrawFrame()
    {
    }
    public void Cleanup()
    {
    }
}
