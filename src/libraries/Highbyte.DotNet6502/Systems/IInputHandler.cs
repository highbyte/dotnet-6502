using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IInputHandler
{
    void Init(ISystem system, IInputHandlerContext inputContext);
    void ProcessInput(ISystem system);

    List<string> GetStats();
    Instrumentations Stats { get; }
}

public interface IInputHandler<TSystem, TInputHandlerContext> : IInputHandler
    where TSystem : ISystem
    where TInputHandlerContext : IInputHandlerContext
{
    void Init(TSystem system, TInputHandlerContext inputContext);

    void ProcessInput(TSystem system);
}

public class NullInputHandler : IInputHandler
{
    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
    }

    public void ProcessInput(ISystem system)
    {
    }
    public List<string> GetStats() => new();

    public Instrumentations Stats { get; } = new();
}
