namespace Highbyte.DotNet6502.Systems;

public interface IInputHandler
{
    void Init(ISystem system, IInputHandlerContext inputContext);
    void ProcessInput(ISystem system);

    List<string> GetDebugMessages();
}

public interface IInputHandler<TSystem, TInputHandlerContext> : IInputHandler
    where TSystem : ISystem
    where TInputHandlerContext : IInputHandlerContext
{
    void Init(TSystem system, TInputHandlerContext inputContext);

    void ProcessInput(TSystem system);
}

public class NullInputHandler<TSystem> : IInputHandler<TSystem, NullInputHandlerContext>, IInputHandler
        where TSystem : ISystem
{
    public void Init(ISystem system, IInputHandlerContext inputHandlerContext)
    {
    }

    public void Init(TSystem system, NullInputHandlerContext inputHandlerContext)
    {
        Init((ISystem)system, inputHandlerContext);
    }

    public void ProcessInput(ISystem system)
    {
    }

    public void ProcessInput(TSystem system)
    {
        ProcessInput((ISystem)system);
    }

    private readonly List<string> _debugMessages = new List<string>();
    public List<string> GetDebugMessages() => _debugMessages;
}
