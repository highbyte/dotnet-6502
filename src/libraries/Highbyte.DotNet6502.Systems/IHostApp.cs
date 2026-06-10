namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// </summary>
public interface IHostApp
{
    public string SelectedSystemName { get; }
    public HashSet<string> AvailableSystemNames { get; }

    public string SelectedSystemConfigurationVariant { get; }
    public List<string> AllSelectedSystemConfigurationVariants { get; }

    public SystemRunner? CurrentSystemRunner { get; }
    public ISystem? CurrentRunningSystem { get; }
    public EmulatorState EmulatorState { get; }

    public IHostSystemConfig CurrentHostSystemConfig { get; }

    public Task SelectSystem(string systemName);
    public Task SelectSystemConfigurationVariant(string configurationVariant);

    public Task Start();
    public void Pause();
    public void Stop();
    public void QuitApplication();
    public Task Reset();

    public void RunEmulatorOneFrame();

    public Task<(bool IsValid, List<string> Errors)> IsCurrentSystemConfigValid();

    /// <summary>
    /// Validates the configuration of a named system without selecting it as the current system.
    /// Used by automated startup to detect, before any system is selected, whether a system's
    /// prerequisites are met (e.g. C64 ROMs present) so a pre-selection prompt can act on it while
    /// still being able to abort to a pristine (no system selected) state.
    /// </summary>
    public Task<(bool IsValid, List<string> Errors)> IsSystemConfigValid(string systemName);
    public Task<bool> IsAudioSupported();
    public Task<bool> IsAudioEnabled();
    public Task<ISystem?> GetSelectedSystem();
    public void UpdateHostSystemConfig(IHostSystemConfig newConfig);
    public Task PersistCurrentHostSystemConfig();
}
