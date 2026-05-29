namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Binds a system to one emulator run. Input is no longer held here: each system exposes its own
/// <see cref="ISystem.InputConsumer"/> (mirroring <see cref="ISystem.RenderProvider"/> /
/// <see cref="ISystem.AudioProvider"/>), and the host app binds it to the host input state.
/// </summary>
public class SystemRunner
{
    private readonly ISystem _system;
    public ISystem System => _system;

    private IExecEvaluator? _customExecEvaluator;
    public IExecEvaluator? CustomExecEvaluator => _customExecEvaluator;

    public SystemRunner(ISystem system)
    {
        _system = system;
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
        _system.InputConsumer?.BeforeFrame();
    }

    /// <summary>
    /// Called by host app by a timer (or similar) that runs the emulator, tied to the update frequency of the emulated system.
    /// Typically called before after ProcessInput is called.
    /// </summary>
    public ExecEvaluatorTriggerResult RunEmulatorOneFrame()
    {
        var execEvaluatorTriggerResult = _system.ExecuteOneFrame(_customExecEvaluator);
        return execEvaluatorTriggerResult;
    }

    public void Cleanup()
    {
        _system.InputConsumer?.Cleanup();
        (_system as ISystemCleanup)?.Cleanup();
    }
}
