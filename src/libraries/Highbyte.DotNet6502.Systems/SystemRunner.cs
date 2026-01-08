namespace Highbyte.DotNet6502.Systems;

public class SystemRunner
{
    private readonly ISystem _system;
    private readonly IInputHandler _inputHandler = default!;
    private readonly IAudioHandler _audioHandler = default!;

    public ISystem System => _system;
    public IInputHandler InputHandler { get => _inputHandler; }
    public IAudioHandler AudioHandler { get => _audioHandler; }

    private IExecEvaluator? _customExecEvaluator;
    public IExecEvaluator? CustomExecEvaluator => _customExecEvaluator;

    public SystemRunner(ISystem system) : this(system,  new NullInputHandler(system), new NullAudioHandler(system))
    {
    }

    public SystemRunner(ISystem system, IInputHandler inputHandler) : this(system, inputHandler, new NullAudioHandler(system))
    {
    }

    public SystemRunner(ISystem system, IAudioHandler audioHandler) : this(system, new NullInputHandler(system), audioHandler)
    {
    }


    public SystemRunner(ISystem system, IInputHandler inputHandler, IAudioHandler audioHandler)
    {
        if (system != inputHandler.System)
            throw new DotNet6502Exception("InputHandler must be for the same system as the SystemRunner.");
        if (system != audioHandler.System)
            throw new DotNet6502Exception("AudioHandler must be for the same system as the SystemRunner.");

        _system = system;
        _inputHandler = inputHandler;
        _audioHandler = audioHandler;
    }

    public void Init()
    {
        _audioHandler.Init();
        _inputHandler.Init();
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
        _inputHandler.BeforeFrame();
    }

    /// <summary>
    /// Called by host app by a timer (or similar) that runs the emulator, tied to the update frequency of the emulated system.
    /// Typically called before after ProcessInput is called.
    /// </summary>
    public ExecEvaluatorTriggerResult RunEmulatorOneFrame()
    {
        var execEvaluatorTriggerResult = _system.ExecuteOneFrame(this, _customExecEvaluator);
        //_renderer?.GenerateFrame();
        return execEvaluatorTriggerResult;
    }

    public void Cleanup()
    {
        _audioHandler.Cleanup();
        _inputHandler.Cleanup();
    }
}
