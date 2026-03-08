using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Engine-agnostic scripting engine that orchestrates script lifecycle, enable/disable state,
/// and hook routing. Delegates all Lua-VM-specific operations to an <see cref="IScriptingEngineAdapter"/>.
///
/// To support a different Lua backend, implement <see cref="IScriptingEngineAdapter"/> and
/// pass it to this class's constructor — no other changes are required.
/// </summary>
public class ScriptingEngine : IScriptingEngine
{
    private readonly IScriptingEngineAdapter _adapter;
    private readonly ScriptingConfig _config;
    private readonly ILogger _logger;

    // Ordered list of successfully loaded script handles
    private readonly List<AdapterScriptHandle> _handles = new();
    // Last known coroutine state per handle (updated from AdapterResumeResult callbacks)
    private readonly Dictionary<AdapterScriptHandle, AdapterCoroutineState> _coroutineStates = new();
    // Last known yield type per handle
    private readonly Dictionary<AdapterScriptHandle, ScriptYieldType?> _lastYieldTypes = new();
    // Maps hook function name -> file name of the script that last defined it
    private readonly Dictionary<string, string> _hookSourceFiles = new();
    // Files the user has explicitly disabled at runtime
    private readonly HashSet<string> _userDisabledFiles = new(StringComparer.OrdinalIgnoreCase);
    // Files that failed to compile (syntax errors, etc.)
    private readonly List<string> _failedFiles = new();
    // Files whose coroutines terminated due to a runtime error
    private readonly HashSet<string> _runtimeErrorFiles = new(StringComparer.OrdinalIgnoreCase);

    private IEmulatorControl? _emulatorControl;
    private int _frameCount;
    private readonly Stopwatch _wallClock = new();

    // Hook names that all Lua engines must support. Engine-agnostic contract.
    public static readonly string[] HookNames =
    [
        "on_before_frame", "on_after_frame",
        "on_started", "on_paused", "on_stopped",
        "on_system_selected", "on_variant_selected"
    ];

    public bool IsEnabled => true;
    public event EventHandler? ScriptStatusChanged;

    public ScriptingEngine(IScriptingEngineAdapter adapter, ScriptingConfig config, ILoggerFactory loggerFactory)
    {
        _adapter = adapter;
        _config = config;
        _logger = loggerFactory.CreateLogger(nameof(ScriptingEngine));
    }

    public void SetEmulatorControl(IEmulatorControl? control) => _emulatorControl = control;

    public void LoadScripts()
    {
        _handles.Clear();
        _coroutineStates.Clear();
        _lastYieldTypes.Clear();
        _hookSourceFiles.Clear();
        _userDisabledFiles.Clear();
        _failedFiles.Clear();
        _runtimeErrorFiles.Clear();
        _frameCount = 0;
        _wallClock.Restart();

        _adapter.InitializeVm(
            _emulatorControl,
            _config,
            _logger,
            () => _frameCount,
            () => _wallClock.Elapsed.TotalSeconds);

        LoadScriptFiles();
        RunInitialResumes();
        _adapter.RebuildHookCache();

        // If EnableScriptsAtStart is false, mark all loaded scripts as user-disabled
        if (!_config.EnableScriptsAtStart)
        {
            foreach (var handle in _handles)
                _userDisabledFiles.Add(handle.FileName);
        }

        ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadScriptFiles()
    {
        var dir = _config.ScriptDirectory;

        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("[Scripting] Script directory not found: {Dir}", dir);
            return;
        }

        var luaFiles = Directory.GetFiles(dir, "*.lua", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("[Scripting] Loading {Count} Lua script(s) from: {Dir}", luaFiles.Length, Path.GetFullPath(dir));

        foreach (var file in luaFiles)
        {
            var fileName = Path.GetFileName(file);
            var handle = _adapter.LoadFile(file, fileName);
            if (handle != null)
            {
                _handles.Add(handle);
                _logger.LogInformation("[Scripting] Loaded: {File}", fileName);
            }
            else
            {
                _failedFiles.Add(fileName);
            }
        }
    }

    private void RunInitialResumes()
    {
        foreach (var handle in _handles)
        {
            var state = _adapter.InitialResume(handle);
            _coroutineStates[handle] = state.CoroutineState;
            _lastYieldTypes[handle] = state.LastYieldType;

            if (state.CoroutineState == AdapterCoroutineState.ForceSuspended)
                ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
            else if (state.CoroutineState == AdapterCoroutineState.RuntimeError)
            {
                _runtimeErrorFiles.Add(handle.FileName);
                ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
            }

            foreach (var hook in state.RegisteredHooks)
                _hookSourceFiles[hook] = handle.FileName;
        }
    }

    public void OnSystemStarted(ISystem system)
    {
        _adapter.OnSystemStarted(system);
        _frameCount = 0;
    }

    public void InvokeBeforeFrame()
    {
        _frameCount++;
        var activeHandles = GetActiveHandles(filterByYieldType: ScriptYieldType.FrameAdvance);
        _adapter.ResumeFrameAdvanceCoroutines(activeHandles, OnResumeResult);
        InvokeHookIfEnabled("on_before_frame");
    }

    public void ResumeCoroutines()
    {
        var activeHandles = GetActiveHandles(filterByYieldType: ScriptYieldType.Tick);
        _adapter.ResumeTickCoroutines(activeHandles, OnResumeResult);
    }

    public void InvokeAfterFrame() => InvokeHookIfEnabled("on_after_frame");

    public void InvokeEvent(string hookName, params object[] args)
    {
        var sourceFile = _hookSourceFiles.GetValueOrDefault(hookName);
        if (sourceFile == null) return;
        if (_userDisabledFiles.Contains(sourceFile)) return;
        if (_runtimeErrorFiles.Contains(sourceFile)) return;

        if (!_adapter.InvokeHookWithArgs(hookName, sourceFile, args))
        {
            _runtimeErrorFiles.Add(sourceFile);
            ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetScriptEnabled(string fileName, bool enabled)
    {
        if (!_handles.Any(h => h.FileName == fileName))
            return;

        // Don't allow re-enabling auto-disabled (ForceSuspended) scripts
        if (enabled)
        {
            var handle = _handles.First(h => h.FileName == fileName);
            if (_coroutineStates.TryGetValue(handle, out var s) && s == AdapterCoroutineState.ForceSuspended)
                return;
        }

        if (enabled)
            _userDisabledFiles.Remove(fileName);
        else
            _userDisabledFiles.Add(fileName);

        ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadScript(string fileName)
    {
        var filePath = Path.Combine(_config.ScriptDirectory, fileName);
        if (!File.Exists(filePath))
        {
            _logger.LogError("[Scripting] Cannot reload {File}: file not found at {Path}", fileName, filePath);
            return;
        }

        // Don't reload scripts that are actively running
        var existing = _handles.FirstOrDefault(h => h.FileName == fileName);
        if (existing != null
            && _coroutineStates.GetValueOrDefault(existing) == AdapterCoroutineState.Suspended
            && !_userDisabledFiles.Contains(fileName)
            && !_runtimeErrorFiles.Contains(fileName))
        {
            _logger.LogWarning("[Scripting] Cannot reload {File}: script is currently running", fileName);
            return;
        }

        _logger.LogInformation("[Scripting] Reloading: {File}", fileName);

        // Remember insert position to preserve script order
        var insertIdx = existing != null ? _handles.IndexOf(existing) : -1;

        // Clean up old state
        if (existing != null)
        {
            _coroutineStates.Remove(existing);
            _lastYieldTypes.Remove(existing);
            _handles.RemoveAt(insertIdx);
        }
        _failedFiles.Remove(fileName);
        _runtimeErrorFiles.Remove(fileName);
        _userDisabledFiles.Remove(fileName);

        // Remove hook registrations from the old script version
        var hooksToRemove = _hookSourceFiles.Where(kv => kv.Value == fileName).Select(kv => kv.Key).ToList();
        foreach (var hook in hooksToRemove)
            _hookSourceFiles.Remove(hook);

        // Recompile (adapter replaces the coroutine inside the existing handle, or null = failed)
        AdapterScriptHandle? newHandle;
        if (existing != null && _adapter.RecompileFile(existing, filePath))
        {
            newHandle = existing;
        }
        else if (existing == null)
        {
            newHandle = _adapter.LoadFile(filePath, fileName);
        }
        else
        {
            // RecompileFile failed
            _logger.LogError("[Scripting] Failed to recompile {File}", fileName);
            _failedFiles.Add(fileName);
            ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (newHandle == null)
        {
            _failedFiles.Add(fileName);
            ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (insertIdx >= 0)
            _handles.Insert(insertIdx, newHandle);
        else
            _handles.Add(newHandle);

        _logger.LogInformation("[Scripting] Reloaded: {File}", fileName);

        // Run initial resume for the new coroutine
        var state = _adapter.InitialResume(newHandle);
        _coroutineStates[newHandle] = state.CoroutineState;
        _lastYieldTypes[newHandle] = state.LastYieldType;

        if (state.CoroutineState == AdapterCoroutineState.RuntimeError)
            _runtimeErrorFiles.Add(fileName);

        foreach (var hook in state.RegisteredHooks)
            _hookSourceFiles[hook] = fileName;

        _adapter.RebuildHookCache();
        ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<ScriptStatus> GetScriptStatuses()
    {
        // Build reverse lookup: fileName -> hooks defined by that file
        var hooksPerFile = new Dictionary<string, List<string>>();
        foreach (var (hookName, fileName) in _hookSourceFiles)
        {
            if (!hooksPerFile.TryGetValue(fileName, out var list))
            {
                list = new List<string>();
                hooksPerFile[fileName] = list;
            }
            list.Add(hookName);
        }

        var statuses = new List<ScriptStatus>();

        foreach (var handle in _handles)
        {
            var hooks = (IReadOnlyList<string>)(hooksPerFile.GetValueOrDefault(handle.FileName)
                ?? (IReadOnlyList<string>)Array.Empty<string>());

            var coroutineState = _coroutineStates.GetValueOrDefault(handle, AdapterCoroutineState.Suspended);
            var savedYieldType = _lastYieldTypes.GetValueOrDefault(handle);

            ScriptExecutionState state;
            ScriptYieldType? yieldTypeDto = null;

            if (_userDisabledFiles.Contains(handle.FileName))
            {
                state = ScriptExecutionState.UserDisabled;
                // Preserve yield type so UI can show what it was doing before disable
                yieldTypeDto = savedYieldType;
            }
            else if (coroutineState == AdapterCoroutineState.ForceSuspended)
            {
                state = ScriptExecutionState.Disabled;
            }
            else if (_runtimeErrorFiles.Contains(handle.FileName))
            {
                state = ScriptExecutionState.Disabled;
            }
            else if (coroutineState == AdapterCoroutineState.Dead)
            {
                state = hooks.Count > 0 ? ScriptExecutionState.HookOnly : ScriptExecutionState.Completed;
            }
            else // Suspended
            {
                state = ScriptExecutionState.Running;
                yieldTypeDto = savedYieldType;
            }

            var canToggle = state != ScriptExecutionState.Disabled
                         && state != ScriptExecutionState.Completed;
            var canReload = state != ScriptExecutionState.Running;

            statuses.Add(new ScriptStatus(handle.FileName, state, yieldTypeDto, hooks, canToggle, canReload));
        }

        // Include scripts that failed to load as system-disabled
        foreach (var failedFileName in _failedFiles)
        {
            statuses.Add(new ScriptStatus(failedFileName, ScriptExecutionState.Disabled, null,
                Array.Empty<string>(), false, true));
        }

        return statuses;
    }

    /// <summary>
    /// Builds the list of active (non-disabled, non-errored) handles for the given yield type filter.
    /// </summary>
    private List<AdapterScriptHandle> GetActiveHandles(ScriptYieldType filterByYieldType)
    {
        var result = new List<AdapterScriptHandle>();
        foreach (var handle in _handles)
        {
            if (_userDisabledFiles.Contains(handle.FileName)) continue;
            if (_runtimeErrorFiles.Contains(handle.FileName)) continue;

            var state = _coroutineStates.GetValueOrDefault(handle, AdapterCoroutineState.Suspended);
            if (state is AdapterCoroutineState.Dead or AdapterCoroutineState.ForceSuspended or AdapterCoroutineState.RuntimeError)
                continue;

            // Default to FrameAdvance so untracked coroutines are not picked up by the Tick timer.
            var yieldType = _lastYieldTypes.GetValueOrDefault(handle) ?? ScriptYieldType.FrameAdvance;
            if (yieldType != filterByYieldType) continue;

            result.Add(handle);
        }
        return result;
    }

    /// <summary>
    /// Callback invoked by the adapter after each individual coroutine resume.
    /// Updates tracking collections and fires ScriptStatusChanged when a script is auto-disabled.
    /// </summary>
    private void OnResumeResult(AdapterScriptHandle handle, AdapterResumeResult result)
    {
        _coroutineStates[handle] = result.NewState;
        _lastYieldTypes[handle] = result.YieldType;

        if (result.NewState == AdapterCoroutineState.ForceSuspended)
            ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
        else if (result.NewState == AdapterCoroutineState.RuntimeError)
        {
            _runtimeErrorFiles.Add(handle.FileName);
            ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Invokes a zero-arg hook if the source script is active (not user-disabled or runtime-errored).
    /// On adapter-reported failure, adds the script to runtimeErrorFiles.
    /// </summary>
    private void InvokeHookIfEnabled(string hookName)
    {
        var sourceFile = _hookSourceFiles.GetValueOrDefault(hookName);
        if (sourceFile == null) return;
        if (_userDisabledFiles.Contains(sourceFile)) return;
        if (_runtimeErrorFiles.Contains(sourceFile)) return;

        if (!_adapter.InvokeHook(hookName, sourceFile))
        {
            _runtimeErrorFiles.Add(sourceFile);
            ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
