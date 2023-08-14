namespace Highbyte.DotNet6502.Systems;

public class SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext>
    where TSystem : ISystem
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext: IAudioHandlerContext
{
    private readonly SystemRunner _systemRunner;

    public SystemRunnerBuilder(TSystem system)
    {
        _systemRunner = new SystemRunner(system);
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer)
    {
        _systemRunner.Renderer = renderer;
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithInputHandler(IInputHandler<TSystem, TInputHandlerContext> inputHandler)
    {
        _systemRunner.InputHandler = inputHandler;
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithAudioHandler(IAudioHandler<TSystem, TAudioHandlerContext> audioHandler)
    {
        _systemRunner.AudioHandler = audioHandler;
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithAudioHandler(IAudioHandler audioHandler)
    {
        _systemRunner.AudioHandler = audioHandler;
        return this;
    }

    public SystemRunner Build()
    {
        return _systemRunner;
    }
}
