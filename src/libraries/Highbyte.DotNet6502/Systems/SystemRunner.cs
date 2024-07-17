namespace Highbyte.DotNet6502.Systems;

public class SystemRunner
{
    private readonly ISystem _system;
    private IRenderer _renderer = default!;
    private IInputHandler _inputHandler = default!;
    private IAudioHandler _audioHandler = default!;

    public ISystem System => _system;
    public IRenderer Renderer { get => _renderer; }
    public IInputHandler InputHandler { get => _inputHandler; }
    public IAudioHandler AudioHandler { get => _audioHandler; }

    private IExecEvaluator? _customExecEvaluator;
    public IExecEvaluator? CustomExecEvaluator => _customExecEvaluator;

    public SystemRunner(ISystem system)
    {
        _system = system;
    }

    public void InitRenderer(IRenderer renderer, IRenderContext renderContext)
    {
        _renderer = renderer;
        _renderer.Init(_system, renderContext);
    }
    public void InitInputHandler(IInputHandler inputHandler, IInputHandlerContext inputHandlerContext)
    {
        _inputHandler = inputHandler;
        _inputHandler.Init(_system, inputHandlerContext);
    }
    public void InitAudioHandler(IAudioHandler audioHandler, IAudioHandlerContext audioHandlerContext)
    {
        _audioHandler = audioHandler;
        _audioHandler.Init(_system, audioHandlerContext);
    }

    /// <summary>
    /// Set a ExecEvaluator that is used for when executing the CPU instructions. 
    /// This will be used in addition to what "normally" is used (running for x cycles or instructions).
    /// Useful for setting breakpoints.
    /// </summary>
    /// <param name="execEvaluator"></param>
    public void SetCustomExecEvaluator(IExecEvaluator execEvaluator)
    {
        _customExecEvaluator = execEvaluator;
    }
    public void ClearCustomExecEvaluator()
    {
        _customExecEvaluator = null;
    }

    /// <summary>
    /// Called by host app that runs the emulator.
    /// Typically before RunEmulatorOneFrame is called.
    /// </summary>
    public void ProcessInputBeforeFrame()
    {
        _inputHandler?.BeforeFrame();
    }

    /// <summary>
    /// Called by host app by a timer (or similar) that runs the emulator, tied to the update frequency of the emulated system.
    /// Typically called before after ProcessInput is called.
    /// </summary>
    public ExecEvaluatorTriggerResult RunEmulatorOneFrame()
    {
        var execEvaluatorTriggerResult = _system.ExecuteOneFrame(this, _customExecEvaluator);
        return execEvaluatorTriggerResult;
    }

    /// <summary>
    /// Called by host app that runs the emulator, once per frame tied to the host app rendering frequency.
    /// </summary>
    public void Draw()
    {
        _renderer?.DrawFrame();
    }

    public void Cleanup()
    {
        _renderer?.Cleanup();

        _audioHandler?.StopPlaying();
        //_audioHandler?.Cleanup();

        //_inputHandler?.Cleanup();
    }
}
