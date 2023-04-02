namespace Highbyte.DotNet6502.Systems;

public interface ISoundHandlerContext
{
}

public interface ISoundHandlerContext<TSystem> : ISoundHandlerContext
    where TSystem : ISystem
{
}

public class NullSoundHandlerContext : ISoundHandlerContext
{

}
