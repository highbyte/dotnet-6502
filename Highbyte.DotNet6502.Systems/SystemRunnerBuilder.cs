namespace Highbyte.DotNet6502.Systems
{

    public class SystemRunnerBuilder<TSystem, TRenderContext>
        where TSystem : ISystem 
        where TRenderContext : IRenderContext
    {
        private readonly SystemRunner _systemRunner;

        public SystemRunnerBuilder(TSystem system)
        {
            _systemRunner = new SystemRunner(system);
        }

        public SystemRunnerBuilder<TSystem, TRenderContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer)
        {
            _systemRunner.Renderer = renderer;
            return this;
        }

        public SystemRunnerBuilder<TSystem, TRenderContext> WithInputHandler(IInputHandler<TSystem> inputHandler)
        {
            _systemRunner.InputHandler = inputHandler;
            return this;
        }

        public SystemRunner Build()
        {
            return _systemRunner;
        }
    }
}