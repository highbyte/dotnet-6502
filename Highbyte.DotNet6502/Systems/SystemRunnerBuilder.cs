namespace Highbyte.DotNet6502.Systems
{

    public class SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext>
        where TSystem : ISystem
        where TRenderContext : IRenderContext
        where TInputHandlerContext : IInputHandlerContext
    {
        private readonly SystemRunner _systemRunner;

        public SystemRunnerBuilder(TSystem system)
        {
            _systemRunner = new SystemRunner(system);
        }

        public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer)
        {
            _systemRunner.Renderer = renderer;
            return this;
        }

        public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext> WithInputHandler(IInputHandler<TSystem, TInputHandlerContext> inputHandler)
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