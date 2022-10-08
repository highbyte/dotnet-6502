namespace Highbyte.DotNet6502.Systems
{
    public interface IInputHandler
    {
        void Init(ISystem system, IInputHandlerContext inputContext);
        void ProcessInput(ISystem system);

    }

    public interface IInputHandler<TSystem, TInputContext> : IInputHandler
        where TSystem : ISystem
        where TInputContext : IInputHandlerContext
    {
        void Init(TSystem system, TInputContext inputContext);

        void ProcessInput(TSystem system);
    }
}