using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// MoonSharp-specific implementation of <see cref="IScriptingEngineAdapter"/>.
/// Manages the MoonSharp Lua VM, per-file coroutines, and hook function caching.
/// All engine-agnostic orchestration (script state tracking, enable/disable, events)
/// is handled by <see cref="ScriptingEngine"/>.
/// </summary>
public class MoonSharpScriptingEngineAdapter : IScriptingEngineAdapter
{
    private readonly ILoggerFactory _loggerFactory;
    private ILogger? _logger;
    private Script? _script;
    private LuaLogProxy? _logProxy;
    private LuaCpuProxy? _cpuProxy;
    private LuaMemProxy? _memProxy;
    private ScriptingConfig? _config;

    // Tracks how each coroutine last yielded, keyed on the MoonSharp Coroutine object
    private readonly Dictionary<Coroutine, ScriptYieldType> _coroutineYieldType = new();

    // Cached DynValue references for per-frame hooks — avoids Globals.Get() every frame
    private readonly Dictionary<string, DynValue> _hookDynValueCache = new();

    // Sentinel strings passed through DynValue.NewYieldReq to distinguish yield types
    private const string FrameAdvanceSentinel = "frameadvance";
    private const string TickSentinel = "yield";

    // Pre-allocated yield DynValues — avoids allocation on every frame
    private static readonly DynValue s_frameAdvanceYield =
        DynValue.NewYieldReq(new[] { DynValue.NewString(FrameAdvanceSentinel) });
    private static readonly DynValue s_tickYield =
        DynValue.NewYieldReq(new[] { DynValue.NewString(TickSentinel) });

    public MoonSharpScriptingEngineAdapter(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public void InitializeVm(
        IEmulatorControl? emulatorControl,
        ScriptingConfig config,
        ILogger logger,
        Func<int> getFrameCount,
        Func<double> getElapsedSeconds)
    {
        _config = config;
        _logger = logger;
        _coroutineYieldType.Clear();
        _hookDynValueCache.Clear();

        // Create a sandboxed Lua environment: safe standard libs only, no file I/O from scripts
        _script = new Script(CoreModules.Preset_SoftSandbox | CoreModules.String | CoreModules.Math | CoreModules.Table);

        // Register UserData types so MoonSharp can expose them to Lua
        UserData.RegisterType<LuaCpuProxy>();
        UserData.RegisterType<LuaMemProxy>();
        UserData.RegisterType<LuaLogProxy>();

        // cpu/mem proxies start with null references (safe defaults) until OnSystemStarted is called
        _cpuProxy = new LuaCpuProxy();
        _memProxy = new LuaMemProxy();
        _script.Globals["cpu"] = _cpuProxy;
        _script.Globals["mem"] = _memProxy;
        _logProxy = new LuaLogProxy(_loggerFactory.CreateLogger(nameof(LuaLogProxy)));
        _script.Globals["log"] = _logProxy;

        // emu table: frame control + emulator control operations
        var emuTable = new Table(_script);
        emuTable["frameadvance"] = DynValue.NewCallback((ctx, args) => s_frameAdvanceYield);
        emuTable["yield"] = DynValue.NewCallback((ctx, args) => s_tickYield);
        emuTable["framecount"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(getFrameCount()));
        emuTable["time"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(getElapsedSeconds()));

        // Emulator state queries
        emuTable["state"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(emulatorControl?.CurrentState ?? "unknown"));
        emuTable["systems"] = DynValue.NewCallback((ctx, args) =>
        {
            var t = new Table(_script!);
            var systems = emulatorControl?.AvailableSystems ?? [];
            for (int i = 0; i < systems.Count; i++)
                t[i + 1] = systems[i];
            return DynValue.NewTable(t);
        });
        emuTable["selected_system"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(emulatorControl?.SelectedSystem ?? ""));
        emuTable["selected_variant"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(emulatorControl?.SelectedVariant ?? ""));

        // Emulator control operations (deferred by the host via IEmulatorControl)
        emuTable["start"] = DynValue.NewCallback((ctx, args) =>
        {
            emulatorControl?.RequestStart();
            return DynValue.Void;
        });
        emuTable["pause"] = DynValue.NewCallback((ctx, args) =>
        {
            emulatorControl?.RequestPause();
            return DynValue.Void;
        });
        emuTable["stop"] = DynValue.NewCallback((ctx, args) =>
        {
            emulatorControl?.RequestStop();
            return DynValue.Void;
        });
        emuTable["reset"] = DynValue.NewCallback((ctx, args) =>
        {
            emulatorControl?.RequestReset();
            return DynValue.Void;
        });
        emuTable["select"] = DynValue.NewCallback((ctx, args) =>
        {
            var systemName = args.Count > 0 ? args[0].CastToString() : null;
            var variant = args.Count > 1 && args[1].Type == DataType.String ? args[1].String : null;
            if (systemName != null)
                emulatorControl?.RequestSelectSystem(systemName, variant);
            return DynValue.Void;
        });

        _script.Globals["emu"] = emuTable;
    }

    public void OnSystemStarted(ISystem system)
    {
        if (_cpuProxy == null || _memProxy == null)
            return;
        _cpuProxy.SetCpu(system.CPU);
        _memProxy.SetMem(system.Mem);
    }

    public AdapterScriptHandle? LoadFile(string filePath, string fileName)
    {
        if (_script == null) return null;
        try
        {
            var chunk = _script.LoadFile(filePath);
            var coroutine = _script.CreateCoroutine(chunk).Coroutine;
            if (_config!.MaxInstructionsPerResume > 0)
                coroutine.AutoYieldCounter = _config.MaxInstructionsPerResume;
            return new MoonSharpScriptHandle(fileName, coroutine);
        }
        catch (SyntaxErrorException ex)
        {
            _logger!.LogError("[Scripting] Syntax error in {File}: {Message}", fileName, ex.DecoratedMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Failed to load {File}", fileName);
            return null;
        }
    }

    public AdapterScriptState InitialResume(AdapterScriptHandle handle)
    {
        var mh = (MoonSharpScriptHandle)handle;

        // Snapshot globals before resume to detect newly registered hooks
        var prevHooks = ScriptingEngine.HookNames.ToDictionary(
            h => h,
            h => _script!.Globals.Get(h).Function);

        _logProxy!.CurrentScriptFile = handle.FileName;
        try
        {
            var result = mh.Coroutine.Resume();

            if (mh.Coroutine.State == CoroutineState.ForceSuspended)
            {
                _logger!.LogError(
                    "[Scripting] {File} exceeded instruction limit ({Limit}) during initial load without calling emu.yield() or emu.frameadvance(). Script disabled.",
                    handle.FileName, _config!.MaxInstructionsPerResume);
                _coroutineYieldType.Remove(mh.Coroutine);
                return new AdapterScriptState(
                    AdapterCoroutineState.ForceSuspended,
                    null,
                    DetectRegisteredHooks(prevHooks));
            }

            if (mh.Coroutine.State == CoroutineState.Suspended)
            {
                var yieldType = DetectYieldType(result);
                if (yieldType.HasValue)
                    _coroutineYieldType[mh.Coroutine] = yieldType.Value;
            }

            return new AdapterScriptState(
                MapCoroutineState(mh.Coroutine.State),
                _coroutineYieldType.GetValueOrDefault(mh.Coroutine),
                DetectRegisteredHooks(prevHooks));
        }
        catch (ScriptRuntimeException ex)
        {
            _logger!.LogError("[Scripting] Runtime error in {File}: {Message}", handle.FileName, ex.DecoratedMessage);
            return new AdapterScriptState(AdapterCoroutineState.RuntimeError, null, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Unexpected error in {File}", handle.FileName);
            return new AdapterScriptState(AdapterCoroutineState.RuntimeError, null, Array.Empty<string>());
        }
    }

    public void ResumeFrameAdvanceCoroutines(
        IReadOnlyList<AdapterScriptHandle> activeHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult)
    {
        foreach (var handle in activeHandles)
        {
            var mh = (MoonSharpScriptHandle)handle;

            // Only resume coroutines that last yielded via frameadvance
            var lastYield = _coroutineYieldType.GetValueOrDefault(mh.Coroutine, ScriptYieldType.FrameAdvance);
            if (lastYield != ScriptYieldType.FrameAdvance)
                continue;

            onResult(handle, ResumeCoroutine(mh));
        }
    }

    public void ResumeTickCoroutines(
        IReadOnlyList<AdapterScriptHandle> activeHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult)
    {
        foreach (var handle in activeHandles)
        {
            var mh = (MoonSharpScriptHandle)handle;

            // Only resume coroutines that last yielded via emu.yield()
            var lastYield = _coroutineYieldType.GetValueOrDefault(mh.Coroutine, ScriptYieldType.FrameAdvance);
            if (lastYield != ScriptYieldType.Tick)
                continue;

            onResult(handle, ResumeCoroutine(mh));
        }
    }

    private AdapterResumeResult ResumeCoroutine(MoonSharpScriptHandle handle)
    {
        _logProxy!.CurrentScriptFile = handle.FileName;
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = handle.Coroutine.Resume();

            if (handle.Coroutine.State == CoroutineState.ForceSuspended)
            {
                _logger!.LogError(
                    "[Scripting] {File} exceeded instruction limit ({Limit}) without calling emu.yield() or emu.frameadvance(). Script disabled.",
                    handle.FileName, _config!.MaxInstructionsPerResume);
                return new AdapterResumeResult(AdapterCoroutineState.ForceSuspended, null);
            }

            if (handle.Coroutine.State == CoroutineState.Suspended)
            {
                var yieldType = DetectYieldType(result);
                if (yieldType.HasValue)
                    _coroutineYieldType[handle.Coroutine] = yieldType.Value;
                return new AdapterResumeResult(AdapterCoroutineState.Suspended, _coroutineYieldType.GetValueOrDefault(handle.Coroutine));
            }

            return new AdapterResumeResult(MapCoroutineState(handle.Coroutine.State), null);
        }
        catch (ScriptRuntimeException ex)
        {
            _logger!.LogError("[Scripting] Runtime error in {File}: {Message}", handle.FileName, ex.DecoratedMessage);
            return new AdapterResumeResult(AdapterCoroutineState.RuntimeError, null);
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Unexpected error in {File}", handle.FileName);
            return new AdapterResumeResult(AdapterCoroutineState.RuntimeError, null);
        }
        finally
        {
            if (_config!.MaxExecutionWarningMs > 0)
            {
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                if (elapsedMs > _config.MaxExecutionWarningMs)
                    _logger!.LogWarning("[Scripting] {File} took {Ms:F1}ms (threshold: {Threshold}ms)",
                        handle.FileName, elapsedMs, _config.MaxExecutionWarningMs);
            }
        }
    }

    public bool InvokeHook(string hookName, string scriptFile)
    {
        if (_script == null) return true;
        if (!_hookDynValueCache.TryGetValue(hookName, out var fn))
            return true;

        _logProxy!.CurrentScriptFile = scriptFile;
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            _script.Call(fn);
            return true;
        }
        catch (ScriptRuntimeException ex)
        {
            _logger!.LogError("[Scripting] Runtime error in '{Hook}': {Message}", hookName, ex.DecoratedMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Unexpected error invoking Lua hook '{Hook}'", hookName);
            return false;
        }
        finally
        {
            if (_config!.MaxExecutionWarningMs > 0)
            {
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                if (elapsedMs > _config.MaxExecutionWarningMs)
                    _logger!.LogWarning("[Scripting] '{Hook}' ({Script}) took {Ms:F1}ms (threshold: {Threshold}ms)",
                        hookName, scriptFile, elapsedMs, _config.MaxExecutionWarningMs);
            }
        }
    }

    public bool InvokeHookWithArgs(string hookName, string scriptFile, params object[] args)
    {
        if (_script == null) return true;
        if (!_hookDynValueCache.TryGetValue(hookName, out var fn))
            return true;

        _logProxy!.CurrentScriptFile = scriptFile;
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            _script.Call(fn, args.Select(a => DynValue.FromObject(_script, a)).ToArray());
            return true;
        }
        catch (ScriptRuntimeException ex)
        {
            _logger!.LogError("[Scripting] Runtime error in '{Hook}': {Message}", hookName, ex.DecoratedMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Unexpected error invoking Lua hook '{Hook}'", hookName);
            return false;
        }
        finally
        {
            if (_config!.MaxExecutionWarningMs > 0)
            {
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                if (elapsedMs > _config.MaxExecutionWarningMs)
                    _logger!.LogWarning("[Scripting] '{Hook}' ({Script}) took {Ms:F1}ms (threshold: {Threshold}ms)",
                        hookName, scriptFile, elapsedMs, _config.MaxExecutionWarningMs);
            }
        }
    }

    public AdapterScriptState GetScriptState(AdapterScriptHandle handle)
    {
        var mh = (MoonSharpScriptHandle)handle;
        var yieldType = _coroutineYieldType.GetValueOrDefault(mh.Coroutine);
        return new AdapterScriptState(
            MapCoroutineState(mh.Coroutine.State),
            yieldType,
            Array.Empty<string>()); // hooks tracked by ScriptingEngine, not needed here
    }

    public bool RecompileFile(AdapterScriptHandle handle, string filePath)
    {
        if (_script == null) return false;
        var mh = (MoonSharpScriptHandle)handle;
        try
        {
            _coroutineYieldType.Remove(mh.Coroutine);
            var chunk = _script.LoadFile(filePath);
            var coroutine = _script.CreateCoroutine(chunk).Coroutine;
            if (_config!.MaxInstructionsPerResume > 0)
                coroutine.AutoYieldCounter = _config.MaxInstructionsPerResume;
            mh.Coroutine = coroutine;
            return true;
        }
        catch (SyntaxErrorException ex)
        {
            _logger!.LogError("[Scripting] Syntax error in {File}: {Message}", handle.FileName, ex.DecoratedMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Failed to recompile {File}", handle.FileName);
            return false;
        }
    }

    public void RebuildHookCache()
    {
        _hookDynValueCache.Clear();
        if (_script == null) return;
        foreach (var hookName in ScriptingEngine.HookNames)
        {
            var fn = _script.Globals.Get(hookName);
            if (fn.Type == DataType.Function)
                _hookDynValueCache[hookName] = fn;
        }
    }

    /// <summary>
    /// Detects which hook functions were newly registered by comparing globals before and after a resume.
    /// </summary>
    private IReadOnlyList<string> DetectRegisteredHooks(Dictionary<string, Closure?> prevHooks)
    {
        var registered = new List<string>();
        foreach (var hook in ScriptingEngine.HookNames)
        {
            var fn = _script!.Globals.Get(hook);
            if (fn.Type == DataType.Function && fn.Function != prevHooks[hook])
                registered.Add(hook);
        }
        return registered;
    }

    /// <summary>
    /// Extracts the yield type from a coroutine resume result.
    /// Handles both DataType.Tuple and DataType.String (MoonSharp may unwrap single-element yield).
    /// </summary>
    private static ScriptYieldType? DetectYieldType(DynValue result)
    {
        string? sentinel = null;

        if (result.Type == DataType.Tuple && result.Tuple.Length > 0 && result.Tuple[0].Type == DataType.String)
            sentinel = result.Tuple[0].String;
        else if (result.Type == DataType.String)
            sentinel = result.String;

        return sentinel switch
        {
            FrameAdvanceSentinel => ScriptYieldType.FrameAdvance,
            TickSentinel => ScriptYieldType.Tick,
            _ => null
        };
    }

    private static AdapterCoroutineState MapCoroutineState(CoroutineState state) => state switch
    {
        CoroutineState.Suspended => AdapterCoroutineState.Suspended,
        CoroutineState.Dead => AdapterCoroutineState.Dead,
        CoroutineState.ForceSuspended => AdapterCoroutineState.ForceSuspended,
        _ => AdapterCoroutineState.Dead
    };
}
