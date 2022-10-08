namespace Highbyte.DotNet6502.Systems
{

    public class SystemRunnerBuilder<TSystem, TRenderContext, TInputContext>
        where TSystem : ISystem
        where TRenderContext : IRenderContext
        where TInputContext : IInputHandlerContext
    {
        private readonly SystemRunner _systemRunner;

        public SystemRunnerBuilder(TSystem system)
        {
            _systemRunner = new SystemRunner(system);
        }

        public SystemRunnerBuilder<TSystem, TRenderContext, TInputContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer)
        {
            _systemRunner.Renderer = renderer;
            return this;
        }

        public SystemRunnerBuilder<TSystem, TRenderContext, TInputContext> WithInputHandler(IInputHandler<TSystem, TInputContext> inputHandler)
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