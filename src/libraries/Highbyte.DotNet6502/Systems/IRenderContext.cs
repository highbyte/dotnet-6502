namespace Highbyte.DotNet6502.Systems;

public interface IRenderContext
{
    void Init();
    void Cleanup();
}

public class NullRenderContext : IRenderContext
{
    public void Cleanup()
    {
    }
    public void Init()
    {
    }
}
