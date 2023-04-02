namespace Highbyte.DotNet6502.Systems;

public class SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TSoundHandlerContext>
    where TSystem : ISystem
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TSoundHandlerContext: ISoundHandlerContext
{
    private readonly SystemRunner _systemRunner;

    public SystemRunnerBuilder(TSystem system)
    {
        _systemRunner = new SystemRunner(system);
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TSoundHandlerContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer)
    {
        _systemRunner.Renderer = renderer;
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TSoundHandlerContext> WithInputHandler(IInputHandler<TSystem, TInputHandlerContext> inputHandler)
    {
        _systemRunner.InputHandler = inputHandler;
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TSoundHandlerContext> WithSoundHandler(ISoundHandler<TSystem, TSoundHandlerContext> soundHandler)
    {
        _systemRunner.SoundHandler = soundHandler;
        return this;
    }

    public SystemRunner Build()
    {
        return _systemRunner;
    }
}
