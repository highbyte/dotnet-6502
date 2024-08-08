namespace Highbyte.DotNet6502.Systems;

public interface IRenderContext
{
    void Init();
    void Cleanup();

    public bool IsInitialized { get; }
}

public class NullRenderContext : IRenderContext
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
