using Highbyte.DotNet6502.Systems.Snapshots;
using Highbyte.DotNet6502.Systems.Configuration;

namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// </summary>
public interface IHostApp
{
    /// <summary>
    /// Identifier of the host app / settings-schema owner (e.g. <c>"Avalonia"</c> shared by the
    /// Desktop and Browser apps, <c>"Headless"</c>). Used to tag and gate host-specific snapshot
    /// config (the general host-app settings block).
    /// </summary>
    public string HostName { get; }

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

    /// <summary>
    /// Deterministically advances the loaded system by <paramref name="frameCount"/> frames and
    /// renders the result to the host's render target (if any). Intended for automation: load a
    /// snapshot (paused), step N frames, then screenshot. Requires the emulator to be stopped/paused
    /// and a system to be loaded; throws if it is Running (the real-time run loop would make stepping
    /// non-deterministic) or if no system is loaded.
    /// </summary>
    public Task StepEmulatorFramesAsync(int frameCount);

    public Task<(bool IsValid, List<string> Errors)> IsCurrentSystemConfigValid();

    /// <summary>
    /// Validates the configuration of a named system without selecting it as the current system.
    /// Used by automated startup to detect, before any system is selected, whether a system's
    /// prerequisites are met (e.g. C64 ROMs present) so a pre-selection prompt can act on it while
    /// still being able to abort to a pristine (no system selected) state.
    /// </summary>
    public Task<(bool IsValid, List<string> Errors)> IsSystemConfigValid(string systemName);
    public Task<StoragePathsInfo> GetStoragePathsInfoAsync();
    public Task<bool> IsAudioSupported();
    public Task<bool> IsAudioEnabled();
    public Task<ISystem?> GetSelectedSystem();
    public void UpdateHostSystemConfig(IHostSystemConfig newConfig);
    public Task PersistCurrentHostSystemConfig();

    /// <summary>
    /// True if there is a live system instance (running or selected-but-not-started) that supports
    /// snapshots. False after a stop. Gates <b>saving</b> (needs a live system to read state from).
    /// </summary>
    public bool CanSnapshotCurrentSystem { get; }

    /// <summary>
    /// True if the currently selected system (by name) supports snapshots, independent of run state
    /// and surviving a stop. Gates <b>loading</b> (which rebuilds the machine).
    /// </summary>
    public bool SelectedSystemSupportsSnapshots { get; }

    /// <summary>
    /// Captures a snapshot of the current system to <paramref name="output"/>. Pauses the emulator
    /// for the (read-only) capture and resumes it afterwards if it was running. When
    /// <paramref name="includeConfig"/> is true, the current runtime settings ("config") are also
    /// captured into the snapshot (see the config extension in the feature design doc).
    /// </summary>
    public Task SaveSnapshotAsync(System.IO.Stream output, SnapshotSaveOptions? options = null, bool includeConfig = false);

    /// <summary>
    /// Restores a snapshot from <paramref name="input"/>: stops any running system, rebuilds the
    /// snapshot's machine + variant, restores module state into it, and leaves it paused. When
    /// <paramref name="applyConfig"/> is true, any runtime settings ("config") embedded in the
    /// snapshot are applied (settings not relevant to this host app are ignored).
    /// </summary>
    public Task<SnapshotRestoreResult> LoadSnapshotAsync(System.IO.Stream input, bool applyConfig = false);
}
