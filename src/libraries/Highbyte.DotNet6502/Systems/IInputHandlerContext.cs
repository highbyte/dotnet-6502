namespace Highbyte.DotNet6502.Systems;

public interface IInputHandlerContext
{
    void Init();
    void Cleanup();
}

public class NullInputHandlerContext : IInputHandlerContext
{
    public void Cleanup()
    {
    }
    public void Init()
    {
    }
}
