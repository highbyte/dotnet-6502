using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IInputHandler
{
    void Init(ISystem system, IInputHandlerContext inputContext);
    void BeforeFrame();

    List<string> GetDebugInfo();
    Instrumentations Instrumentations { get; }
}

public interface IInputHandler<TSystem, TInputHandlerContext> : IInputHandler
    where TSystem : ISystem
    where TInputHandlerContext : IInputHandlerContext
{
    void Init(TSystem system, TInputHandlerContext inputContext);
}

public class NullInputHandler : IInputHandler
{
    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
    }

    public void BeforeFrame()
    {
    }
    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
