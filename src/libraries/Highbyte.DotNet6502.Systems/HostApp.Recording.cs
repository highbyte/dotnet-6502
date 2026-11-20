using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Recording;
using Highbyte.DotNet6502.Systems.Snapshots;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

public partial class HostApp : IRecordingHost
{
    public RecordingSession? DemoSession { get; private set; }
    public bool IsPreparingPlayback { get; private set; }
    public bool SupportsRecording => (CurrentRunningSystem ?? _selectedSystemTemporary) is IRecordingSystem;
    public bool HasLoadedDemo => _loadedDemo != null;
    private DemoReader? _loadedDemo;
    private MemoryStream? _loadedDemoStorage;
    private bool _demoTransition;
    private FreshHostInputState? _freshHostInput;
    private bool BlockScriptCallbacks => IsPreparingPlayback || _demoTransition || DemoSession?.OwnsInput == true;

    private void InvokeScriptEvent(string name, params object[] args)
    {
        // Lifecycle pauses/resumes are outside the recorded input timeline.
        if (!BlockScriptCallbacks && DemoSession?.Mode != RecordingMode.Recording)
            _scriptingEngine.InvokeEvent(name, args);
    }

    public async Task StartDemoRecordingAsync(Stream output)
    {
        if (DemoSession?.Mode is RecordingMode.Recording or RecordingMode.Playback || IsPreparingPlayback || _demoTransition)
            throw new InvalidOperationException("Stop the current demo session first.");
        var system = CurrentRunningSystem ?? throw new InvalidOperationException("Start a machine before recording.");
        if (system is not IRecordingSystem recordingSystem || system is not ISystemSnapshotProvider)
            throw new InvalidOperationException("This machine does not support recording/replay.");
        var adapter = recordingSystem.CreateRecordingAdapter();
        adapter.ValidateSession();
        bool resume = EmulatorState == EmulatorState.Running;
        _demoTransition = true;
        try
        {
            Pause();
            DrainPendingRemoteActions();
            using var snapshot = new MemoryStream();
            new SnapshotService().Save(system, snapshot, new SnapshotSaveOptions
            {
                ConfigurationVariant = SelectedSystemConfigurationVariant,
                Config = await CaptureConfigForSnapshotAsync(),
            });
            DemoSession?.Dispose();
            DemoSession = new RecordingSession(adapter);
            DemoSession.StartRecording(output, snapshot, SelectedSystemConfigurationVariant);
            RecordingSystemGuards.Bind(system, DemoSession, TryQueueKeyboardText);
        }
        finally
        {
            try { if (resume && EmulatorState == EmulatorState.Paused) await Start(); }
            finally { _demoTransition = false; }
        }
    }

    public void StopDemoRecording()
    {
        if (DemoSession?.Mode != RecordingMode.Recording)
            throw new InvalidOperationException("No recording is active.");
        if (_scriptingEngine.FrameHold != null)
        {
            _scriptingEngine.CancelFrameHold();
            Pause();
        }
        DemoSession.StopRecording();
    }

    /// <summary>Copies bounded input to owned storage so the caller can close its picker/file stream.</summary>
    public async Task LoadDemoAsync(Stream input)
    {
        if (DemoSession?.Mode == RecordingMode.Recording || IsPreparingPlayback || _demoTransition)
            throw new InvalidOperationException("Stop and save the current recording before opening a demo.");
        var storage = new MemoryStream();
        DemoReader? reader = null;
        try
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer)) != 0)
            {
                if (storage.Length + count > DemoReader.MaxPackageBytes)
                    throw new InvalidDataException("Demo exceeds 128 MiB.");
                storage.Write(buffer, 0, count);
            }
            storage.Position = 0;
            reader = new DemoReader(storage);
            if (reader.Manifest.FormatVersion == DemoManifest.CurrentFormatVersion
                && reader.Manifest.RecordedBuildId is { } recordedBuild && recordedBuild != DemoManifest.RunningBuildId)
                _logger.LogInformation("Replay build differs. Recorded {RecordedBuild}, running {RunningBuild}.", recordedBuild, DemoManifest.RunningBuildId);
            await PrepareDemoAsync(reader);
            _loadedDemo?.Dispose();
            _loadedDemoStorage?.Dispose();
            _loadedDemo = reader;
            _loadedDemoStorage = storage;
        }
        catch
        {
            reader?.Dispose();
            storage.Dispose();
            throw;
        }
    }

    public Task RestartDemoAsync() => PrepareDemoAsync(_loadedDemo
        ?? throw new InvalidOperationException("Open a demo before restarting playback."));

    private async Task PrepareDemoAsync(DemoReader reader)
    {
        if (DemoSession?.Mode == RecordingMode.Recording || IsPreparingPlayback || _demoTransition)
            throw new InvalidOperationException("Wait for the current demo operation or stop recording first.");
        IsPreparingPlayback = true;
        try { await PrepareDemoCoreAsync(reader); }
        finally { IsPreparingPlayback = false; }
    }

    private async Task PrepareDemoCoreAsync(DemoReader reader)
    {
        var manifest = reader.Manifest;
        if (!AvailableSystemNames.Contains(manifest.SystemName))
            throw new InvalidDataException($"Demo machine '{manifest.SystemName}' is unavailable in this host.");
        var config = await _systemList.GetHostSystemConfig(manifest.SystemName);
        var variants = await _systemList.GetSystemConfigurationVariants(manifest.SystemName, config);
        if (manifest.ConfigurationVariant == null || !variants.Contains(manifest.ConfigurationVariant))
            throw new InvalidDataException("Demo machine variant is unavailable.");
        // Fully validate and restore into a fresh machine before replacing the running one.
        var fresh = await _systemList.BuildSystem(manifest.SystemName, manifest.ConfigurationVariant);
        if (fresh is not IRecordingSystem recordingSystem)
            throw new InvalidDataException("This machine does not support recording/replay.");
        var adapter = recordingSystem.CreateRecordingAdapter();
        reader.ValidateAdapter(adapter);
        using (var snapshot = reader.OpenSnapshot())
        {
            var restored = new SnapshotService().Restore(fresh, snapshot);
            if (restored.Warnings.Count != 0)
                throw new InvalidDataException("Demo snapshot cannot be restored exactly: " + string.Join("; ", restored.Warnings));
        }
        var session = new RecordingSession(adapter);
        session.StartPlayback(reader);
        RecordingSystemGuards.Bind(fresh, session, TryQueueKeyboardText);

        try
        {
            // Scripts bind to this machine. Leave them explicitly disabled at replay exit so
            // suspended coroutines/queued work cannot silently resume against the final state.
            foreach (var script in _scriptingEngine.GetScriptStatuses())
                _scriptingEngine.SetScriptEnabled(script.FileName, false);
            _scriptingEngine.DiscardPendingActions();
            DrainPendingRemoteActions();
            DemoSession?.StopPlayback();
            DemoSession?.Dispose();
            DemoSession = null;
            Stop();
            await SelectSystem(manifest.SystemName);
            await SelectSystemConfigurationVariant(manifest.ConfigurationVariant);
            _selectedSystemTemporary = fresh;
            DemoSession = session;
            await Start();
            Pause();
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void StopDemoPlayback()
    {
        if (DemoSession?.OwnsInput != true) return;
        _demoTransition = true;
        try
        {
            Pause();
            DemoSession.StopPlayback();
            RequireFreshLiveInput();
        }
        finally { _demoTransition = false; }
    }

    private void RequireFreshLiveInput()
    {
        _freshHostInput?.RequireFreshInput();
        KeyboardTextInput.Clear();
        CurrentRunningSystem?.InputInjector?.Clear();
    }

    private bool _steppingRecording;

    // A paused recording still dispatches hooks. Yield to the host dispatcher while the
    // controller thinks, and count the requested step only once the guest actually ran.
    private async Task StepRecordingFrameAsync()
    {
        _steppingRecording = true;
        try
        {
            while (!RunDemoFrame(receiveInput: true))
                await WaitForRecordingHookAsync();
            if (_scriptingEngine.FrameHold is { Finished: false })
                await WaitForRecordingHookAsync();
        }
        finally { _steppingRecording = false; }
    }

    private async Task WaitForRecordingHookAsync()
    {
        var hold = _scriptingEngine.FrameHold;
        while (hold != null && _scriptingEngine.FrameHold == hold && EmulatorState == EmulatorState.Paused
            && DemoSession?.Mode == RecordingMode.Recording)
        {
            if (_scriptingEngine.PollFrameHold()) return;
            await Task.Delay(TimeSpan.FromMilliseconds(ScriptingTickIntervalMs));
        }
        throw new InvalidOperationException("Recording frame step interrupted.");
    }

    private bool RunDemoFrame(bool receiveInput)
    {
        var session = DemoSession!;
        bool recording = session.Mode == RecordingMode.Recording;
        try
        {
            if (recording)
            {
                if (!_scriptingEngine.TryBeginFrame(_beginLiveFrame)) return false;
                if (receiveInput) _systemRunner!.ProcessInputBeforeFrame();
            }
            else DrainPendingRemoteActions();
            if (!session.BeforeFrame())
                return false;
            var result = _systemRunner!.RunEmulatorOneFrame();
            session.AfterFrame(!result.Triggered && CurrentRunningSystem?.CPU.IsHalted == false);
            OnAfterRunEmulatorOneFrame(result);
            if (recording && session.Mode == RecordingMode.Recording)
                return FinishLiveFrame();
            return true;
        }
        catch (Exception ex)
        {
            session.Fail(ex.Message);
            if (session.CheckpointMismatch)
                _logger.LogError("{ReplayError}", session.Error);
            else
                _logger.LogError(ex, "Recording/replay failed at frame {Frame}.", session.Frame);
            return false;
        }
        finally
        {
            if (session.Mode == RecordingMode.Idle)
            {
                _demoTransition = true;
                try { Pause(); if (!recording) RequireFreshLiveInput(); }
                finally { _demoTransition = false; }
            }
        }
    }
}
