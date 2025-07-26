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
    public Task Reset();

    public void RunEmulatorOneFrame();

    public Task<bool> IsAudioSupported();
    public Task<bool> IsAudioEnabled();
    public Task<ISystem> GetSelectedSystem();
    public void UpdateHostSystemConfig(IHostSystemConfig newConfig);
    public Task PersistCurrentHostSystemConfig();
}
