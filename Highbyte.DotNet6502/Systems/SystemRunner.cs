using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems;

public class SystemRunner
{
    private readonly ISystem _system;
    private IRenderer _renderer;
    private IInputHandler _inputHandler;
    private ISoundHandler _soundHandler;

    public ISystem System => _system;
    public IRenderer Renderer { get => _renderer; set => _renderer = value; }
    public IInputHandler InputHandler { get => _inputHandler; set => _inputHandler = value; }
    public ISoundHandler SoundHandler { get => _soundHandler; set => _soundHandler = value; }

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
            bool executeOk = RunOneFrame(out _);
            if (!executeOk)
                quit = true;
        }
    }

    public bool RunOneFrame(out Dictionary<string, double> detailedStats)
    {
        ProcessInput();

        bool executeOk = RunEmulatorOneFrame(out detailedStats);
        if (!executeOk)
            return false;

        Draw();

        return true;
    }

    public bool RunOneInstruction()
    {
        bool executeOk = _system.ExecuteOneInstruction();
        if (!executeOk)
            return false;
        return true;
    }

    public void ProcessInput()
    {
        _inputHandler?.ProcessInput(_system);
    }

    public bool RunEmulatorOneFrame(out Dictionary<string, double> detailedStats)
    {
        detailedStats = new()
        {
            ["Audio"] = 0
        };

        bool shouldContinue = _system.ExecuteOneFrame(_customExecEvaluator, PostInstruction, detailedStats);
        if (!shouldContinue)
            return false;
        return true;
    }

    // PostInstruction is meant to be called after each instruction has executed.
    private void PostInstruction(ISystem system, Dictionary<string, double> detailedStats)
    {
        // Generate sound by inspecting the current system state
        if (_soundHandler is not null)
        {
            _audioSw.Restart();
            _soundHandler.GenerateSound(system);
            //var t = new Task(() => _soundHandler?.GenerateSound(system));
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
