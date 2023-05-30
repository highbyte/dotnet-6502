namespace Highbyte.DotNet6502.Systems;

public interface ISoundHandler
{
    void Init(ISystem system, ISoundHandlerContext soundHandlerContext);
    void GenerateSound(ISystem system);

    void StopAllSounds();

    List<string> GetDebugMessages();

}

public interface ISoundHandler<TSystem, TSoundHandlerContext> : ISoundHandler
    where TSystem : ISystem
    where TSoundHandlerContext : ISoundHandlerContext
{
    void Init(TSystem system, TSoundHandlerContext soundHandlerContext);

    void GenerateSound(TSystem system);
}

public class NullSoundHandler : ISoundHandler<ISystem, NullSoundHandlerContext>, ISoundHandler
{
    public NullSoundHandler()
    {
    }

    public void Init(ISystem system, ISoundHandlerContext soundHandlerContext)
    {
    }

    public void Init(ISystem system, NullSoundHandlerContext soundHandlerContext)
    {
    }

    public void GenerateSound(ISystem system)
    {
    }

    public void StopAllSounds()
    {
    }

    private readonly List<string> _debugMessages = new List<string>();

    public List<string> GetDebugMessages() => _debugMessages;
}
