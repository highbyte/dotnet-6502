namespace Highbyte.DotNet6502.Systems;

public interface IAudioHandlerContext
{
    void Init();
    void Cleanup();
}

public class NullAudioHandlerContext : IAudioHandlerContext
{
    public void Cleanup()
    {
    }
    public void Init()
    {
    }
}
