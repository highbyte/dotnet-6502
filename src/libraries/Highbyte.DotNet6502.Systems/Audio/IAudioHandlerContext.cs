namespace Highbyte.DotNet6502.Systems.Audio;

public interface IAudioHandlerContext
{
    void Init();
    void Cleanup();

    public bool IsInitialized { get; }

}

public class NullAudioHandlerContext : IAudioHandlerContext
{
    public bool IsInitialized { get; private set; }

    public void Cleanup()
    {
    }
    public void Init()
    {
        IsInitialized = true;
    }
}
