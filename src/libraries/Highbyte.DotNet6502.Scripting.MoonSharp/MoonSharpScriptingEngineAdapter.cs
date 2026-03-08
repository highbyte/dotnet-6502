using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Utils;
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
    private LuaFileProxy? _fileProxy;
    private LuaHttpProxy? _httpProxy;
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
        IHostApp? hostApp,
        Action<Func<Task>> enqueueAction,
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

        // file table: only registered when AllowFileIO is true.
        // Set AllowFileIO: false in environments without filesystem access (e.g. WASM/browser).
        if (config.AllowFileIO)
        {
            var fileBaseDir = string.IsNullOrWhiteSpace(config.FileBaseDirectory)
                ? config.ScriptDirectory
                : config.FileBaseDirectory;
            _fileProxy = new LuaFileProxy(fileBaseDir, config.AllowFileWrite);

            var fileTable = new Table(_script);
            fileTable["read"] = DynValue.NewCallback((ctx, args) =>
            {
                var content = _fileProxy.Read(args.Count > 0 ? args[0].CastToString() : null!);
                return content != null ? DynValue.NewString(content) : DynValue.Nil;
            });
            fileTable["read_bytes"] = DynValue.NewCallback((ctx, args) =>
            {
                var bytes = _fileProxy.ReadBytes(args.Count > 0 ? args[0].CastToString() : null!);
                if (bytes == null) return DynValue.Nil;
                var t = new Table(_script!);
                for (int i = 0; i < bytes.Length; i++) t[i + 1] = (double)bytes[i];
                return DynValue.NewTable(t);
            });
            fileTable["write"] = DynValue.NewCallback((ctx, args) =>
            {
                try { _fileProxy.Write(args[0].CastToString(), args.Count > 1 ? args[1].CastToString() ?? "" : ""); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { throw new ScriptRuntimeException(ex.Message); }
                return DynValue.Void;
            });
            fileTable["append"] = DynValue.NewCallback((ctx, args) =>
            {
                try { _fileProxy.Append(args[0].CastToString(), args.Count > 1 ? args[1].CastToString() ?? "" : ""); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { throw new ScriptRuntimeException(ex.Message); }
                return DynValue.Void;
            });
            fileTable["exists"] = DynValue.NewCallback((ctx, args) =>
                DynValue.NewBoolean(_fileProxy.Exists(args.Count > 0 ? args[0].CastToString() : "")));
            fileTable["list"] = DynValue.NewCallback((ctx, args) =>
            {
                var pattern = args.Count > 0 ? args[0].CastToString() ?? "*" : "*";
                var t = new Table(_script!);
                int i = 1;
                foreach (var f in _fileProxy.List(pattern)) t[i++] = f;
                return DynValue.NewTable(t);
            });
            fileTable["delete"] = DynValue.NewCallback((ctx, args) =>
            {
                try { _fileProxy.Delete(args.Count > 0 ? args[0].CastToString() : ""); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { throw new ScriptRuntimeException(ex.Message); }
                return DynValue.Void;
            });
            _script.Globals["file"] = fileTable;
        }

        // http table: outbound HTTP operations (GET, POST).
        // Only registered when AllowHttpRequests is true.
        if (config.AllowHttpRequests)
        {
            _httpProxy?.Dispose();
            _httpProxy = new LuaHttpProxy();

            // Helper: convert an optional Lua table argument (headers) to a Dictionary
            static Dictionary<string, string>? ExtractHeaders(DynValue arg)
            {
                if (arg.Type != DataType.Table) return null;
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in arg.Table.Pairs)
                    dict[pair.Key.CastToString()] = pair.Value.CastToString();
                return dict;
            }

            // Helper: build a Lua response table from an HttpProxyResponse
            DynValue BuildResponseTable(HttpProxyResponse r)
            {
                var t = new Table(_script!);
                t["ok"] = DynValue.NewBoolean(r.Ok);
                t["status"] = DynValue.NewNumber(r.Status);
                t["body"] = r.Body != null ? DynValue.NewString(r.Body) : DynValue.Nil;
                t["error"] = r.Error != null ? DynValue.NewString(r.Error) : DynValue.Nil;
                return DynValue.NewTable(t);
            }

            // Helper: build a Lua response table where body is a 1-indexed byte table
            DynValue BuildBytesResponseTable(HttpProxyResponse r)
            {
                var t = new Table(_script!);
                t["ok"] = DynValue.NewBoolean(r.Ok);
                t["status"] = DynValue.NewNumber(r.Status);
                if (r.BodyBytes != null)
                {
                    var bodyTable = new Table(_script!);
                    for (int i = 0; i < r.BodyBytes.Length; i++) bodyTable[i + 1] = (double)r.BodyBytes[i];
                    t["body"] = DynValue.NewTable(bodyTable);
                }
                else
                {
                    t["body"] = DynValue.Nil;
                }
                t["error"] = r.Error != null ? DynValue.NewString(r.Error) : DynValue.Nil;
                return DynValue.NewTable(t);
            }

            var httpProxyCapture = _httpProxy;
            var httpTable = new Table(_script);

            // http.get(url [, headers]) → { ok, status, body, error }
            httpTable["get"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var headers = args.Count > 1 ? ExtractHeaders(args[1]) : null;
                return BuildResponseTable(httpProxyCapture.GetString(url, headers));
            });

            // http.get_bytes(url [, headers]) → { ok, status, body (byte table), error }
            httpTable["get_bytes"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var headers = args.Count > 1 ? ExtractHeaders(args[1]) : null;
                return BuildBytesResponseTable(httpProxyCapture.GetBytes(url, headers));
            });

            // http.post(url, body, content_type [, headers]) → { ok, status, body, error }
            httpTable["post"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var body = args.Count > 1 ? args[1].CastToString() ?? "" : "";
                var contentType = args.Count > 2 ? args[2].CastToString() ?? "text/plain" : "text/plain";
                var headers = args.Count > 3 ? ExtractHeaders(args[3]) : null;
                return BuildResponseTable(httpProxyCapture.Post(url, body, contentType, headers));
            });

            // http.post_json(url, json_body [, headers]) → { ok, status, body, error }
            httpTable["post_json"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var body = args.Count > 1 ? args[1].CastToString() ?? "" : "";
                var headers = args.Count > 2 ? ExtractHeaders(args[2]) : null;
                return BuildResponseTable(httpProxyCapture.Post(url, body, "application/json", headers));
            });

            // http.download(url, filename [, headers]) → { ok, status, error }
            // Streams the response body directly to the sandboxed file path.
            // Requires AllowFileIO and AllowFileWrite to be true.
            if (config.AllowFileIO && _fileProxy != null)
            {
                var fileProxyCapture = _fileProxy;
                httpTable["download"] = DynValue.NewCallback((ctx, args) =>
                {
                    var url = args.Count > 0 ? args[0].CastToString() : "";
                    var filename = args.Count > 1 ? args[1].CastToString() : null;
                    var headers = args.Count > 2 ? ExtractHeaders(args[2]) : null;

                    try { fileProxyCapture.ThrowIfWriteDisabled("download"); }
                    catch (InvalidOperationException ex) { throw new ScriptRuntimeException(ex.Message); }

                    var safePath = fileProxyCapture.GetSafePath(filename);
                    if (safePath == null)
                        throw new ScriptRuntimeException($"http.download(): unsafe or invalid filename: {filename}");

                    return BuildResponseTable(httpProxyCapture.DownloadToFile(url, safePath, headers));
                });
            }

            _script.Globals["http"] = httpTable;
        }

        // emu table: frame control + emulator control operations
        var emuTable = new Table(_script);
        emuTable["frameadvance"] = DynValue.NewCallback((ctx, args) => s_frameAdvanceYield);
        emuTable["yield"] = DynValue.NewCallback((ctx, args) => s_tickYield);
        emuTable["framecount"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(getFrameCount()));
        emuTable["time"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(getElapsedSeconds()));

        // Emulator state queries
        emuTable["state"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(hostApp?.EmulatorState switch
            {
                EmulatorState.Running => "running",
                EmulatorState.Paused  => "paused",
                _                     => "stopped"
            } ?? "unknown"));
        emuTable["systems"] = DynValue.NewCallback((ctx, args) =>
        {
            var t = new Table(_script!);
            var systems = hostApp?.AvailableSystemNames.ToList() ?? [];
            for (int i = 0; i < systems.Count; i++)
                t[i + 1] = systems[i];
            return DynValue.NewTable(t);
        });
        emuTable["selected_system"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(hostApp?.SelectedSystemName ?? ""));
        emuTable["selected_variant"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(hostApp?.SelectedSystemConfigurationVariant ?? ""));

        // Emulator control operations (deferred via enqueueAction to run after the current frame/tick)
        emuTable["start"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(async () => { if (hostApp.EmulatorState != EmulatorState.Running) await hostApp.Start(); });
            return DynValue.Void;
        });
        emuTable["pause"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(() => { hostApp.Pause(); return Task.CompletedTask; });
            return DynValue.Void;
        });
        emuTable["stop"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(() => { hostApp.Stop(); return Task.CompletedTask; });
            return DynValue.Void;
        });
        emuTable["reset"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(() => hostApp.Reset());
            return DynValue.Void;
        });
        emuTable["select"] = DynValue.NewCallback((ctx, args) =>
        {
            var systemName = args.Count > 0 ? args[0].CastToString() : null;
            var variant = args.Count > 1 && args[1].Type == DataType.String ? args[1].String : null;
            if (hostApp != null && systemName != null)
                enqueueAction(async () =>
                {
                    await hostApp.SelectSystem(systemName);
                    if (variant != null) await hostApp.SelectSystemConfigurationVariant(variant);
                });
            return DynValue.Void;
        });

        // emu.load is only registered when AllowFileIO is true — not available in environments
        // without filesystem access (e.g. WASM/browser).
        if (config.AllowFileIO && _fileProxy != null)
        {
            // emu.load(filename [, start])
            // emu.load(filename, address [, start])
            // Loads a binary file into emulator memory.
            // With no address: reads 2-byte little-endian PRG header to determine load address.
            // With address: treats file as raw binary and loads it at the given address.
            // start (bool, optional): if true, sets CPU.PC to the load address after loading.
            var fileProxyCapture = _fileProxy;
            emuTable["load"] = DynValue.NewCallback((ctx, args) =>
            {
                if (hostApp == null) return DynValue.Void;
                var filename = args.Count > 0 ? args[0].CastToString() : null;
                if (filename == null) return DynValue.Void;

                ushort? forceAddress = null;
                bool fileHasHeader = true;
                bool startAfterLoad = false;
                if (args.Count > 1 && args[1].Type == DataType.Number)
                {
                    forceAddress = (ushort)(int)args[1].Number;
                    fileHasHeader = false; // explicit address = treat as raw binary, no header
                    if (args.Count > 2 && args[2].Type == DataType.Boolean)
                        startAfterLoad = args[2].Boolean;
                }
                else if (args.Count > 1 && args[1].Type == DataType.Boolean)
                {
                    startAfterLoad = args[1].Boolean;
                }

                enqueueAction(() =>
                {
                    var path = fileProxyCapture.GetSafePath(filename);
                    if (path == null || !File.Exists(path)) return Task.CompletedTask;
                    var mem = hostApp.CurrentRunningSystem?.Mem;
                    if (mem == null) return Task.CompletedTask;
                    mem.Load(path, out var loadedAtAddress, out _, forceLoadAddress: forceAddress, fileContainsLoadAddress: fileHasHeader);
                    if (startAfterLoad)
                    {
                        var cpu = hostApp.CurrentRunningSystem?.CPU;
                        if (cpu != null) cpu.PC = loadedAtAddress;
                    }
                    return Task.CompletedTask;
                });
                return DynValue.Void;
            });
        }

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
