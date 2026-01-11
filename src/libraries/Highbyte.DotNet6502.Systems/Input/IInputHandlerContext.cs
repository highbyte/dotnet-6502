namespace Highbyte.DotNet6502.Systems.Input;

public interface IInputHandlerContext
{
    void Init();
    void Cleanup();
    public bool IsInitialized { get; }
}

public class NullInputHandlerContext : IInputHandlerContext
{
    public bool IsInitialized { get; private set; } = false;

    public void Cleanup()
    {
    }
    public void Init()
    {
        IsInitialized = true;
    }
}
