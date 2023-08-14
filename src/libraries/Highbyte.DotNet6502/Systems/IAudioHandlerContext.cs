namespace Highbyte.DotNet6502.Systems;

public interface IAudioHandlerContext
{
}

public interface IAudioHandlerContext<TSystem> : IAudioHandlerContext
    where TSystem : ISystem
{
}

public class NullAudioHandlerContext : IAudioHandlerContext
{

}
