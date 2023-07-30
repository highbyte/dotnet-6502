using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems;

public class SystemRunner
{
    private readonly ISystem _system;
    private IRenderer _renderer;
    private IInputHandler _inputHandler;
    private IAudioHandler _audioHandler;

    public ISystem System => _system;
    public IRenderer Renderer { get => _renderer; set => _renderer = value; }
    public IInputHandler InputHandler { get => _inputHandler; set => _inputHandler = value; }
    public IAudioHandler AudioHandler { get => _audioHandler; set => _audioHandler = value; }

    private IExecEvaluator? _customExecEvaluator;
    public IExecEvaluator? CustomExecEvaluator => _customExecEvaluator;

    // Detailed perf stat to audio generation that occurs after each instruction
    private readonly Stopwatch _audioSw = new();

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

    public void Run()
    {
        bool quit = false;
        while (!quit)
        {
            var execEvaluatorTriggerResult = RunOneFrame(out _);
            if (execEvaluatorTriggerResult.Triggered)
                quit = true;
        }
    }

    public ExecEvaluatorTriggerResult RunOneFrame(out Dictionary<string, double> detailedStats)
    {
        ProcessInput();

        var execEvaluatorTriggerResult = RunEmulatorOneFrame(out detailedStats);
        if (execEvaluatorTriggerResult.Triggered)
            return execEvaluatorTriggerResult;

        Draw();

        return ExecEvaluatorTriggerResult.NotTriggered;
    }

    public ExecEvaluatorTriggerResult RunOneInstruction()
    {
        return _system.ExecuteOneInstruction(_customExecEvaluator);
    }

    public void ProcessInput()
    {
        _inputHandler?.ProcessInput(_system);
    }

    public ExecEvaluatorTriggerResult RunEmulatorOneFrame(out Dictionary<string, double> detailedStats)
    {
        detailedStats = new()
        {
            ["Audio"] = 0
        };

        var execEvaluatorTriggerResult = _system.ExecuteOneFrame(_customExecEvaluator, PostInstruction, detailedStats);
        return execEvaluatorTriggerResult;
    }

    // PostInstruction is meant to be called after each instruction has executed.
    private void PostInstruction(ISystem system, Dictionary<string, double> detailedStats)
    {
        // Generate audio by inspecting the current system state
        if (_audioHandler is not null)
        {
            _audioSw.Restart();
            _audioHandler.GenerateAudio(system);
            //var t = new Task(() => _audioHandler?.GenerateAudio(system));
            //t.RunSynchronously();
            _audioSw.Stop();

            detailedStats["Audio"] += _audioSw.Elapsed.TotalMilliseconds;
        }
    }

    public void Draw()
    {
        _renderer?.Draw(_system);
    }
}
