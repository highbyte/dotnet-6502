using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IAudioHandler
{
    void Init(ISystem system, IAudioHandlerContext audioHandlerContext);
    void GenerateAudio(ISystem system);

    void StartPlaying();
    void StopPlaying();
    void PausePlaying();
    List<string> GetDebugInfo();
    Instrumentations Stats { get; }
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

    public List<string> GetDebugInfo() => new();

    public Instrumentations Stats { get; } = new();
}
