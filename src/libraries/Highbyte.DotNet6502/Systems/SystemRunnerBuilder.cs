namespace Highbyte.DotNet6502.Systems;

public class SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext>
    where TSystem : ISystem
    where TRenderContext : IRenderContext
    where TInputHandlerContext : IInputHandlerContext
    where TAudioHandlerContext : IAudioHandlerContext
{
    private readonly SystemRunner _systemRunner;
    private IRenderer? _renderer;
    private TRenderContext? _renderContext;
    private IInputHandler? _inputHandler;
    private TInputHandlerContext? _inputHandlerContext;
    private IAudioHandler? _audioHandler;
    private TAudioHandlerContext? _audioHandlerContext;

    public SystemRunnerBuilder(TSystem system)
    {
        _systemRunner = new SystemRunner(system);
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithRenderer(IRenderer<TSystem, TRenderContext> renderer, TRenderContext? renderContext = default)
    {
        _renderer = renderer;
        _renderContext = renderContext;
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithRenderer(IRenderer renderer, TRenderContext? renderContext = default)
    {
        _renderer = renderer;
        _renderContext = renderContext;
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithInputHandler(IInputHandler<TSystem, TInputHandlerContext> inputHandler, TInputHandlerContext? inputHandlerContext = default)
    {
        _inputHandler = inputHandler;
        _inputHandlerContext = inputHandlerContext;
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithInputHandler(IInputHandler inputHandler, TInputHandlerContext? inputHandlerContext = default(TInputHandlerContext))
    {
        _inputHandler = inputHandler;
        _inputHandlerContext = inputHandlerContext;
        return this;
    }

    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithAudioHandler(IAudioHandler<TSystem, TAudioHandlerContext> audioHandler, TAudioHandlerContext? audioHandlerContext = default(TAudioHandlerContext))
    {
        _audioHandler = audioHandler;
        _audioHandlerContext = audioHandlerContext;
        return this;
    }
    public SystemRunnerBuilder<TSystem, TRenderContext, TInputHandlerContext, TAudioHandlerContext> WithAudioHandler(IAudioHandler audioHandler, TAudioHandlerContext? audioHandlerContext = default(TAudioHandlerContext))
    {
        _audioHandler = audioHandler;
        _audioHandlerContext = audioHandlerContext;
        return this;
    }

    public SystemRunner Build()
    {
        if (_renderer != default)
        {
            _systemRunner.InitRenderer(_renderer, _renderContext != null ? _renderContext : new NullRenderContext());
        }

        if (_inputHandler != default)
        {
            _systemRunner.InitInputHandler(_inputHandler, _inputHandlerContext != null ? _inputHandlerContext : new NullInputHandlerContext());
        }

        if (_audioHandler != default)
        {
            _systemRunner.InitAudioHandler(_audioHandler, _audioHandlerContext != null ? _audioHandlerContext : new NullAudioHandlerContext());
        }

        return _systemRunner;
    }
}
