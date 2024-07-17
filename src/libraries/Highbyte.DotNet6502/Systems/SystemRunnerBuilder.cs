namespace Highbyte.DotNet6502.Systems;

public class SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext>
    where TSystem : ISystem
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext : IAudioHandlerContext
{
    private readonly SystemRunner _systemRunner;
    private readonly TRenderContext? _renderContext;
    private readonly TInputHandlerContext? _inputHandlerContext;
    private readonly TAudioHandlerContext? _audioHandlerContext;

    public SystemRunnerBuilder(TSystem system)
    {
        _systemRunner = new SystemRunner(system);
    }
    public SystemRunnerBuilder(TSystem system, TRenderContext renderContext, TInputHandlerContext inputHandlerContext, TAudioHandlerContext audioHandlerContext)
    {
        _systemRunner = new SystemRunner(system);
        _renderContext = renderContext;
        _inputHandlerContext = inputHandlerContext;
        _audioHandlerContext = audioHandlerContext;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer)
    {
        if (_renderContext != null)
            _systemRunner.InitRenderer(renderer, _renderContext);
        else
            _systemRunner.InitRenderer(renderer, new NullRenderContext());
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithRenderer(IRenderer renderer)
    {
        if (_renderContext != null)
            _systemRunner.InitRenderer(renderer, _renderContext);
        else
            _systemRunner.InitRenderer(renderer, new NullRenderContext());
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithInputHandler(IInputHandler<TSystem, TInputHandlerContext> inputHandler)
    {
        if (_inputHandlerContext != null)
            _systemRunner.InitInputHandler(inputHandler, _inputHandlerContext);
        else
            _systemRunner.InitInputHandler(inputHandler, new NullInputHandlerContext());
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithInputHandler(IInputHandler inputHandler)
    {
        if (_inputHandlerContext != null)
            _systemRunner.InitInputHandler(inputHandler, _inputHandlerContext);
        else
            _systemRunner.InitInputHandler(inputHandler, new NullInputHandlerContext());
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithAudioHandler(IAudioHandler<TSystem, TAudioHandlerContext> audioHandler)
    {
        if (_audioHandlerContext != null)
            _systemRunner.InitAudioHandler(audioHandler, _audioHandlerContext);
        else
            _systemRunner.InitAudioHandler(audioHandler, new NullAudioHandlerContext());
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithAudioHandler(IAudioHandler audioHandler)
    {
        if (_audioHandlerContext != null)
            _systemRunner.InitAudioHandler(audioHandler, _audioHandlerContext);
        else
            _systemRunner.InitAudioHandler(audioHandler, new NullAudioHandlerContext());
        return this;
    }

    public SystemRunner Build()
    {
        return _systemRunner;
    }
}
