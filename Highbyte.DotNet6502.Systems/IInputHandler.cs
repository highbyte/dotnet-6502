namespace Highbyte.DotNet6502.Systems
{
    public interface IInputHandler
    {
        void ProcessInput(ISystem system);
    }

    public interface IInputHandler<TSystem> : IInputHandler where TSystem : ISystem
    {
        void ProcessInput(TSystem system);
    }
}