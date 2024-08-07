using Highbyte.DotNet6502.Instrumentation;

namespace Highbyte.DotNet6502.Systems;

public interface IAudioHandler
{
    void Init();
    void AfterFrame();
    void StartPlaying();
    void StopPlaying();
    void PausePlaying();
    void Cleanup();

    List<string> GetDebugInfo();
    Instrumentations Instrumentations { get; }
    ISystem System { get; }
}

public class NullAudioHandler : IAudioHandler
{
    private readonly ISystem _system;
    public ISystem System => _system;

    public NullAudioHandler(ISystem system)
    {
        _system = system;
    }
    public void Init()
    {
    }
    public void AfterFrame()
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
    public void Cleanup()
    {
    }

    public List<string> GetDebugInfo() => new();

    public Instrumentations Instrumentations { get; } = new();
}
