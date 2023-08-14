namespace Highbyte.DotNet6502.Systems;

public interface IAudioHandler
{
    void Init(ISystem system, IAudioHandlerContext audioHandlerContext);
    void GenerateAudio(ISystem system);

    void StartPlaying();
    void StopPlaying();
    void PausePlaying();

    List<string> GetDebugMessages();

}

public interface IAudioHandler<TSystem, TAudioHandlerContext> : IAudioHandler
    where TSystem : ISystem
    where TAudioHandlerContext : IAudioHandlerContext
{
    void Init(TSystem system, TAudioHandlerContext audioHandlerContext);

    void GenerateAudio(TSystem system);
}

public class NullAudioHandler : IAudioHandler
{
    public NullAudioHandler()
    {
    }

    public void Init(ISystem system, IAudioHandlerContext audioHandlerContext)
    {
    }

    public void GenerateAudio(ISystem system)
    {
    }

    public void StartPlaying()
    {
    }

    public void PausePlaying()
    {
    }

    public void StopPlaying()
    {
    }

    private readonly List<string> _debugMessages = new List<string>();

    public List<string> GetDebugMessages() => _debugMessages;

}
