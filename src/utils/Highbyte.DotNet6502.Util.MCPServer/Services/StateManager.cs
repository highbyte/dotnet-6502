using Highbyte.DotNet6502;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;

public class StateManager
{
    private IExecEvaluator? _originalExecEvaluator;

    private bool _cpuExecutionPaused = false;
    private readonly BreakpointManager _breakPointManager;

    public bool IsCpuExecutionPaused => _cpuExecutionPaused;

    public StateManager(BreakpointManager breakpointManager)
    {
        _breakPointManager = breakpointManager;
    }

    public bool IsMCPControlEnabled(IHostApp hostApp)
    {
        return hostApp.ExternalControlEnabled;
    }

    public void EnableMCPControl(IHostApp hostApp)
    {
        if (hostApp.CurrentSystemRunner == null)
            throw new DotNet6502Exception("CurrentSystemRunner is not set in the host app.");
        if (IsMCPControlEnabled(hostApp))
            throw new DotNet6502Exception("MCP control is already enabled in the host app.");

        PauseCPUExecution();

        hostApp.EnableExternalControl(OnBeforeRunEmulatorOneFrame, OnAfterRunEmulatorOneFrame);
        _originalExecEvaluator = hostApp.CurrentSystemRunner.CustomExecEvaluator;
        hostApp.CurrentSystemRunner.SetCustomExecEvaluator(new BreakPointExecEvaluator(_breakPointManager.BreakPoints));
    }

    public void DisableMCPControl(IHostApp hostApp)
    {
        if (hostApp.CurrentSystemRunner == null)
            throw new DotNet6502Exception("CurrentSystemRunner is not set in the host app.");
        if (!IsMCPControlEnabled(hostApp))
            throw new DotNet6502Exception("MCP control is not enabled in the host app.");

        hostApp.DisableExternalControl();
        hostApp.CurrentSystemRunner.SetCustomExecEvaluator(_originalExecEvaluator);
        ResumeCPUExecution();
    }

    private void PauseCPUExecution()
    {
        _cpuExecutionPaused = true;
    }

    private void ResumeCPUExecution()
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
            if (execEvaluatorTriggerResult.TriggerType == ExecEvaluatorTriggerReasonType.DebugBreakPoint)
            {
            }
        }
    }
}
