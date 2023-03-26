namespace Highbyte.DotNet6502.Systems;

public interface IInputHandlerContext
{
}

public interface IInputHandlerContext<TSystem> : IInputHandlerContext
    where TSystem : ISystem
{
}

public class NullInputHandlerContext : IInputHandlerContext
{
}