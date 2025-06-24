using Highbyte.DotNet6502.Systems.Instrumentation.Stats;

namespace Highbyte.DotNet6502.Systems;

public interface IHostApp
{
    public string SelectedSystemName { get; }

    public SystemRunner? CurrentSystemRunner { get; }
    public ISystem? CurrentRunningSystem { get; }

    public EmulatorState EmulatorState { get; }

    public IHostSystemConfig CurrentHostSystemConfig { get; }
    public List<IHostSystemConfig> GetHostSystemConfigs();

    public Task SelectSystem(string systemName);
    public Task SelectSystemConfigurationVariant(string configurationVariant);

    public Task Start();
    public void Pause();
    public void Stop();
    public Task Reset();

    public void Close();
    public void RunEmulatorOneFrame();
    public void DrawFrame();

    public Task<bool> IsSystemConfigValid();
    public Task<(bool, List<string> validationErrors)> IsValidConfigWithDetails();
    public Task<bool> IsAudioSupported();
    public Task<bool> IsAudioEnabled();
    public Task SetAudioEnabled(bool enabled);

    public Task<ISystem> GetSelectedSystem();

    public void UpdateHostSystemConfig(IHostSystemConfig newConfig);
    public Task PersistCurrentHostSystemConfig();

    public List<(string name, IStat stat)> GetStats();

    /// <summary>
    /// Set to true if the host app doesn't have UI thread. Invoking external control actions will then be done on current thread.
    /// </summary>
    public bool ExternalControlDirectInvoke { get; }
    public Task ExternalControlInvokeOnUIThread(Func<Task> action);
    public void ExternalControlProcessUIActions();
    public void EnableExternalControl(
        Func<(bool shouldRun, bool shouldReceiveInput)>? externalOnBeforeRunEmulatorOneFrame = null,
        Action<ExecEvaluatorTriggerResult>? externalOnAfterRunEmulatorOneFrame = null);
    public void DisableExternalControl();
}
