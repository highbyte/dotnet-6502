using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IInputHandler
{
    void Init();
    void BeforeFrame();
    void Cleanup();

    List<string> GetDebugInfo();
    Instrumentations Instrumentations { get; }
    ISystem System { get; }
}

public class NullInputHandler : IInputHandler
{
    private readonly ISystem _system;
    public ISystem System => _system;

    public NullInputHandler(ISystem system)
    {
        _system = system;
    }
    public void Init()
    {
    }
    public void BeforeFrame()
    {
    }
    public void Cleanup()
    {
    }

    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
