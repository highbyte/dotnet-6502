using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.SilkNetNative
{
    public interface ISilkNetHostViewModel
    {
        public EmulatorState EmulatorState { get; }
        public Task Start();
        public void Pause();
        public void Stop();
        public Task Reset();

        public void SetVolumePercent(float volumePercent);
        public float Scale { get; set; }

        public void ToggleMonitor();
        public void ToggleStatsPanel();
        public void ToggleLogsPanel();

        public HashSet<string> AvailableSystemNames { get; }
        public string SelectedSystemName { get; }
        public void SelectSystem(string systemName);

        public Task<bool> IsSystemConfigValid();
        public Task<ISystemConfig> GetSystemConfig();
        public IHostSystemConfig GetHostSystemConfig();
        public void UpdateSystemConfig(ISystemConfig newConfig);

        public ISystem? CurrentRunningSystem { get; }
    }
}
