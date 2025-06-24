using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

public class BreakpointManager
{
    private readonly Dictionary<ushort, BreakPoint> _breakPoints = new();
    public Dictionary<ushort, BreakPoint> BreakPoints => _breakPoints;
    private IExecEvaluator? _originalExecEvaluator;

    private bool _cpuExecutionPaused = false;
    public bool CpuExecutionPaused => _cpuExecutionPaused;

    public void EnableBreakpointHandling(IHostApp hostApp)
    {
        if (hostApp.CurrentSystemRunner == null)
            throw new DotNet6502Exception("CurrentSystemRunner is not set in the host app.");
        _originalExecEvaluator = hostApp.CurrentSystemRunner.CustomExecEvaluator;
        hostApp.CurrentSystemRunner.SetCustomExecEvaluator(new BreakPointExecEvaluator(_breakPoints));
        hostApp.EnableExternalControl(OnBeforeRunEmulatorOneFrame, OnAfterRunEmulatorOneFrame);
    }

    public void DisableBreakpointHandling(IHostApp hostApp)
    {
        if (hostApp.CurrentSystemRunner == null)
            throw new DotNet6502Exception("CurrentSystemRunner is not set in the host app.");
        hostApp.CurrentSystemRunner.SetCustomExecEvaluator(_originalExecEvaluator);
        hostApp.DisableExternalControl();
    }

    public void AddBreakpoint(ushort address)
    {
        if (!_breakPoints.ContainsKey(address))
            _breakPoints.Add(address, new BreakPoint { Enabled = true });
        else
            _breakPoints[address].Enabled = true;
    }

    public void RemoveBreakpoint(ushort address)
    {
        if (_breakPoints.ContainsKey(address))
            _breakPoints.Remove(address);
    }

    public void RemoveAllBreakpoints()
    {
        _breakPoints.Clear();
    }

    public void PauseCPUExecution()
    {
        _cpuExecutionPaused = true;
    }

    public void ContinueCPUExecution()
    {
        _cpuExecutionPaused = false;
    }

    private (bool shouldRun, bool shouldReceiveInput) OnBeforeRunEmulatorOneFrame()
    {
        // If CPU execution is paused, we should not run the emulator.
        return (!_cpuExecutionPaused, true);
    }

    private void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        if (execEvaluatorTriggerResult.Triggered)
        {
            // If a breakpoint was hit, pause the CPU execution.
            PauseCPUExecution();
            if (execEvaluatorTriggerResult.TriggerType == ExecEvaluatorTriggerReasonType.DebugBreakPoint)
            {
                //Console.WriteLine($"Breakpoint hit at address: {execEvaluatorTriggerResult.TriggerDescription}");
            }
        }
    }
}
