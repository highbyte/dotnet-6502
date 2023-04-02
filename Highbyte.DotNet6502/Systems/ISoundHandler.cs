namespace Highbyte.DotNet6502.Systems;

public interface ISoundHandler
{
    void Init(ISystem system, ISoundHandlerContext soundHandlerContext);
    Task GenerateSound(ISystem system);
}

public interface ISoundHandler<TSystem, TSoundHandlerContext> : ISoundHandler
    where TSystem : ISystem
    where TSoundHandlerContext : ISoundHandlerContext
{
    void Init(TSystem system, TSoundHandlerContext soundHandlerContext);

    Task GenerateSound(TSystem system);
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

    public async Task GenerateSound(ISystem system)
    {
    }
}
