using System.Diagnostics;

namespace Highbyte.DotNet6502.Systems;

public class SystemRunner
{
    private readonly ISystem _system;
    private IRenderer _renderer = default!;
    private IInputHandler _inputHandler = default!;
    private IAudioHandler _audioHandler = default!;

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

    /// <summary>
    /// Called by host app that runs the emulator.
    /// Typically before RunEmulatorOneFrame is called.
    /// </summary>
    public void ProcessInput()
    {
        _inputHandler?.ProcessInput(_system);
    }

    /// <summary>
    /// Called by host app by a timer (or similar) that runs the emulator, tied to the update frequency of the emulated system.
    /// Typically called before after ProcessInput is called.
    /// </summary>
    public ExecEvaluatorTriggerResult RunEmulatorOneFrame(out Dictionary<string, double> detailedStats)
    {
        detailedStats = new()
        {
            ["Audio"] = 0
        };

        var execEvaluatorTriggerResult = _system.ExecuteOneFrame(this, detailedStats, _customExecEvaluator);
        return execEvaluatorTriggerResult;
    }

    /// <summary>
    /// Called by host app that runs the emulator, typically once per frame tied to the host app rendering frequency.
    /// </summary>
    public void Draw()
    {
        _renderer?.Draw(_system);
    }

    /// <summary>
    /// Called by the specific ISystem implementation after each instruction or entire frame worth of instructions, depending how audio is implemented.
    /// </summary>
    /// <param name="detailedStats"></param>
    public void GenerateAudio(Dictionary<string, double> detailedStats)
    {
        if (_audioHandler is not null)
        {
            _audioSw.Restart();
            _audioHandler.GenerateAudio(_system);
            //var t = new Task(() => _audioHandler?.GenerateAudio(system));
            //t.RunSynchronously();
            _audioSw.Stop();

            detailedStats["Audio"] += _audioSw.Elapsed.TotalMilliseconds;
        }
    }
}
