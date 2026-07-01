using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
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
    private LuaC64Proxy? _c64Proxy;
    private LuaFileProxy? _fileProxy;
    private LuaHttpProxy? _httpProxy;
    private LuaTcpProxy? _tcpProxy;
    private LuaScreenshotProxy? _screenshotProxy;
    private ScriptingConfig? _config;
    private IInputInjector? _inputProvider;
    private IHostApp? _hostApp;

    // Tracks how each coroutine last yielded, keyed on the MoonSharp Coroutine object
    private readonly Dictionary<Coroutine, ScriptYieldType> _coroutineYieldType = new();

    // Cached DynValue references for per-frame hooks — avoids Globals.Get() every frame
    private readonly Dictionary<string, DynValue> _hookDynValueCache = new();

    // Pending async HTTP tasks for main-body coroutines: coroutine → pending Task<DynValue>
    private readonly Dictionary<Coroutine, Task<DynValue>> _pendingHttpTasks = new();

    // Pending hook-invocation coroutines waiting on HTTP: coroutine → (handle, task)
    // Hook coroutines are not the same object as the main handle coroutine.
    private readonly List<(Coroutine co, AdapterScriptHandle handle, Task<DynValue> task)> _pendingHookHttpTasks = new();

    // Pending async TCP tasks for main-body coroutines: coroutine → pending Task<DynValue>
    private readonly Dictionary<Coroutine, Task<DynValue>> _pendingTcpTasks = new();

    // Pending hook-invocation coroutines waiting on TCP: (coroutine, handle, task)
    private readonly List<(Coroutine co, AdapterScriptHandle handle, Task<DynValue> task)> _pendingHookTcpTasks = new();

    // Set during any coroutine resume so HTTP callbacks can identify the active coroutine
    private Coroutine? _currentCoroutine;

    // All known script handles — kept in sync so InvokeHookCore can look up by file name
    private IReadOnlyList<AdapterScriptHandle> _allHandles = Array.Empty<AdapterScriptHandle>();

    /// <summary>Minimal AdapterScriptHandle used when a matching loaded handle is not found.</summary>
    private sealed class SyntheticScriptHandle : AdapterScriptHandle
    {
        internal SyntheticScriptHandle(string fileName) : base(fileName) { }
    }

    // Sentinel strings passed through DynValue.NewYieldReq to distinguish yield types
    private const string FrameAdvanceSentinel = "frameadvance";
    private const string TickSentinel = "yield";
    private const string HttpPendingSentinel = "http_pending";
    private const string TcpPendingSentinel = "tcp_pending";

    // Pre-allocated yield DynValues — avoids allocation on every frame
    private static readonly DynValue s_frameAdvanceYield =
        DynValue.NewYieldReq(new[] { DynValue.NewString(FrameAdvanceSentinel) });
    private static readonly DynValue s_tickYield =
        DynValue.NewYieldReq(new[] { DynValue.NewString(TickSentinel) });
    private static readonly DynValue s_httpPendingYield =
        DynValue.NewYieldReq(new[] { DynValue.NewString(HttpPendingSentinel) });
    private static readonly DynValue s_tcpPendingYield =
        DynValue.NewYieldReq(new[] { DynValue.NewString(TcpPendingSentinel) });

    private readonly string _hostType;

    public MoonSharpScriptingEngineAdapter(ILoggerFactory loggerFactory, string hostType = "unknown")
    {
        _loggerFactory = loggerFactory;
        _hostType = hostType;
    }

    // Tell the AOT trimmer to preserve all members of proxy types that MoonSharp reflects
    // on at runtime to build its UserData member descriptors. Required for it to work in Avalonia Browser WASM published with AOT.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LuaLogProxy))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LuaCpuProxy))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LuaMemProxy))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LuaC64Proxy))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LuaScreenshotProxy))]
    public void InitializeVm(
        IHostApp? hostApp,
        Action<string, Func<Task>> enqueueAction,
        ScriptingConfig config,
        ILogger logger,
        Func<int> getFrameCount,
        Func<double> getElapsedSeconds)
    {
        _config = config;
        _logger = logger;
        _hostApp = hostApp;
        _coroutineYieldType.Clear();
        _hookDynValueCache.Clear();
        _pendingHttpTasks.Clear();
        _pendingHookHttpTasks.Clear();
        _pendingTcpTasks.Clear();
        _pendingHookTcpTasks.Clear();
        _tcpProxy?.Dispose();
        _tcpProxy = null;
        _currentCoroutine = null;
        _allHandles = Array.Empty<AdapterScriptHandle>();

        // Create a sandboxed Lua environment: safe standard libs only, no file I/O from scripts
        _script = new Script(CoreModules.Preset_SoftSandbox | CoreModules.String | CoreModules.Math | CoreModules.Table);

        // Register UserData types so MoonSharp can expose them to Lua
        UserData.RegisterType<LuaCpuProxy>();
        UserData.RegisterType<LuaMemProxy>();
        UserData.RegisterType<LuaLogProxy>();
        UserData.RegisterType<LuaC64Proxy>();

        // cpu/mem proxies start with null references (safe defaults) until OnSystemStarted is called
        _cpuProxy = new LuaCpuProxy();
        _memProxy = new LuaMemProxy();
        _script.Globals["cpu"] = _cpuProxy;
        _script.Globals["mem"] = _memProxy;
        _logProxy = new LuaLogProxy(_loggerFactory.CreateLogger(nameof(LuaLogProxy)));
        _script.Globals["log"] = _logProxy;

        // c64 proxy starts with null reference (safe defaults) until OnSystemStarted is called with a C64 system
        _c64Proxy = new LuaC64Proxy();
        _script.Globals["c64"] = _c64Proxy;

        // file table: only registered when AllowFileIO is true.
        // Set AllowFileIO: false in environments without filesystem access (e.g. WASM/browser).
        if (config.AllowFileIO)
        {
            var fileBaseDir = string.IsNullOrWhiteSpace(config.FileBaseDirectory)
                ? config.ResolvedScriptDirectory()
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
            // Fires async HTTP, suspends coroutine with HttpPending, resumes with response table.
            httpTable["get"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var headers = args.Count > 1 ? ExtractHeaders(args[1]) : null;
                var task = httpProxyCapture.GetStringAsync(url, headers)
                    .ContinueWith(t => BuildResponseTable(t.Result));
                RegisterPendingHttpTask(task);
                return s_httpPendingYield;
            });

            // http.get_bytes(url [, headers]) → { ok, status, body (byte table), error }
            httpTable["get_bytes"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var headers = args.Count > 1 ? ExtractHeaders(args[1]) : null;
                var task = httpProxyCapture.GetBytesAsync(url, headers)
                    .ContinueWith(t => BuildBytesResponseTable(t.Result));
                RegisterPendingHttpTask(task);
                return s_httpPendingYield;
            });

            // http.post(url, body, content_type [, headers]) → { ok, status, body, error }
            httpTable["post"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var body = args.Count > 1 ? args[1].CastToString() ?? "" : "";
                var contentType = args.Count > 2 ? args[2].CastToString() ?? "text/plain" : "text/plain";
                var headers = args.Count > 3 ? ExtractHeaders(args[3]) : null;
                var task = httpProxyCapture.PostAsync(url, body, contentType, headers)
                    .ContinueWith(t => BuildResponseTable(t.Result));
                RegisterPendingHttpTask(task);
                return s_httpPendingYield;
            });

            // http.post_json(url, json_body [, headers]) → { ok, status, body, error }
            httpTable["post_json"] = DynValue.NewCallback((ctx, args) =>
            {
                var url = args.Count > 0 ? args[0].CastToString() : "";
                var body = args.Count > 1 ? args[1].CastToString() ?? "" : "";
                var headers = args.Count > 2 ? ExtractHeaders(args[2]) : null;
                var task = httpProxyCapture.PostAsync(url, body, "application/json", headers)
                    .ContinueWith(t => BuildResponseTable(t.Result));
                RegisterPendingHttpTask(task);
                return s_httpPendingYield;
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

                    var task = httpProxyCapture.DownloadToFileAsync(url, safePath, headers)
                        .ContinueWith(t => BuildResponseTable(t.Result));
                    RegisterPendingHttpTask(task);
                    return s_httpPendingYield;
                });
            }

            _script.Globals["http"] = httpTable;
        }

        // store table: cross-platform key/value persistence.
        // Uses StoreBackend (browser/localStorage) if set, otherwise FileSystemScriptStore.
        if (config.AllowStore)
        {
            IScriptStore? storeBackend = config.StoreBackend;
            if (storeBackend == null && !string.IsNullOrEmpty(config.ScriptDirectory))
            {
                var subDir = string.IsNullOrEmpty(config.StoreSubDirectory) ? ".store" : config.StoreSubDirectory;
                var storeDir = Path.Combine(config.ResolvedScriptDirectory(), subDir);
                storeBackend = new FileSystemScriptStore(storeDir);
            }

            if (storeBackend != null)
            {
                var store = storeBackend; // capture for closures
                var storeTable = new Table(_script);

                storeTable["get"] = DynValue.NewCallback((ctx, args) =>
                {
                    var key = args.Count > 0 ? args[0].CastToString() : null!;
                    var val = store.Get(key);
                    return val != null ? DynValue.NewString(val) : DynValue.Nil;
                });
                storeTable["set"] = DynValue.NewCallback((ctx, args) =>
                {
                    var key = args.Count > 0 ? args[0].CastToString() : null!;
                    var value = args.Count > 1 ? args[1].CastToString() ?? "" : "";
                    try { store.Set(key, value); }
                    catch (ArgumentException ex) { throw new ScriptRuntimeException(ex.Message); }
                    return DynValue.Void;
                });
                storeTable["delete"] = DynValue.NewCallback((ctx, args) =>
                {
                    var key = args.Count > 0 ? args[0].CastToString() : null!;
                    try { store.Delete(key); }
                    catch (ArgumentException ex) { throw new ScriptRuntimeException(ex.Message); }
                    return DynValue.Void;
                });
                storeTable["exists"] = DynValue.NewCallback((ctx, args) =>
                {
                    var key = args.Count > 0 ? args[0].CastToString() : "";
                    return DynValue.NewBoolean(store.Exists(key));
                });
                storeTable["list"] = DynValue.NewCallback((ctx, args) =>
                {
                    var t = new Table(_script!);
                    int i = 1;
                    foreach (var key in store.List()) t[i++] = key;
                    return DynValue.NewTable(t);
                });

                _script.Globals["store"] = storeTable;
            }
        }

        // tcp table: outbound TCP client connections.
        // Only registered when AllowTcpClient is true. Desktop-only — never registered in browser.
        if (config.AllowTcpClient)
        {
            _tcpProxy?.Dispose();
            _tcpProxy = new LuaTcpProxy();
            var tcpProxyCapture = _tcpProxy;
            var scriptCapture = _script;

            // Helper: convert a DynValue (string or 1-indexed byte table) to byte[]
            static byte[] ExtractBytes(DynValue dv)
            {
                if (dv.Type == DataType.String)
                    return System.Text.Encoding.UTF8.GetBytes(dv.String);
                if (dv.Type == DataType.Table)
                {
                    var list = new List<byte>();
                    for (int i = 1; ; i++)
                    {
                        var v = dv.Table.Get(i);
                        if (v.Type != DataType.Number) break;
                        list.Add((byte)(int)v.Number);
                    }
                    return list.ToArray();
                }
                return Array.Empty<byte>();
            }

            // Helper: build a Lua result table for TCP errors (no data)
            DynValue BuildTcpErrorTable(string error)
            {
                var t = new Table(scriptCapture!);
                t["ok"] = DynValue.NewBoolean(false);
                t["data"] = DynValue.Nil;
                t["error"] = DynValue.NewString(error);
                return DynValue.NewTable(t);
            }

            // Helper: build the Lua connection-object table whose methods close over 'conn'
            DynValue BuildConnectionTable(LuaTcpConnection conn)
            {
                var t = new Table(scriptCapture!);

                // conn:send(data) — data is a string or 1-indexed byte table
                // Returns { ok=bool, error=string|nil }
                t["send"] = DynValue.NewCallback((ctx, args) =>
                {
                    var data = args.Count > 0 ? args[0] : DynValue.Nil;
                    // When called with colon syntax (conn:send(data)), MoonSharp passes the
                    // connection table as args[0] (self) and the actual data as args[1].
                    var payload = ExtractBytes(data.Type == DataType.Table && ReferenceEquals(data.Table, t)
                        ? (args.Count > 1 ? args[1] : DynValue.Nil)
                        : data);
                    var task = conn.SendAsync(payload)
                        .ContinueWith(r =>
                        {
                            if (r.Exception != null)
                                return BuildTcpErrorTable((r.Exception.InnerException ?? r.Exception).Message);
                            var (ok, error) = r.Result;
                            var result = new Table(scriptCapture!);
                            result["ok"] = DynValue.NewBoolean(ok);
                            result["error"] = error != null ? DynValue.NewString(error) : DynValue.Nil;
                            return DynValue.NewTable(result);
                        });
                    RegisterPendingTcpTask(task);
                    return s_tcpPendingYield;
                });

                // conn:receive(pattern) — pattern is a number (N bytes) or the string "*l" (line)
                // Returns { ok=bool, data=byte_table|string, error=string|nil }
                t["receive"] = DynValue.NewCallback((ctx, args) =>
                {
                    // When called with colon syntax conn:receive(pattern), MoonSharp passes
                    // the table as args[0] and the pattern as args[1].
                    // When called as conn.receive(pattern), the pattern is args[0].
                    DynValue patternArg;
                    if (args.Count >= 2 && args[0].Type == DataType.Table && ReferenceEquals(args[0].Table, t))
                        patternArg = args[1];
                    else
                        patternArg = args.Count > 0 ? args[0] : DynValue.Nil;

                    Task<DynValue> task;
                    if (patternArg.Type == DataType.Number)
                    {
                        var count = (int)patternArg.Number;
                        task = conn.RecvNAsync(count)
                            .ContinueWith(r =>
                            {
                                if (r.Exception != null)
                                    return BuildTcpErrorTable((r.Exception.InnerException ?? r.Exception).Message);
                                var (ok, bytes, error) = r.Result;
                                var result = new Table(scriptCapture!);
                                result["ok"] = DynValue.NewBoolean(ok);
                                if (bytes != null)
                                {
                                    var byteTable = new Table(scriptCapture!);
                                    for (int i = 0; i < bytes.Length; i++) byteTable[i + 1] = (double)bytes[i];
                                    result["data"] = DynValue.NewTable(byteTable);
                                }
                                else result["data"] = DynValue.Nil;
                                result["error"] = error != null ? DynValue.NewString(error) : DynValue.Nil;
                                return DynValue.NewTable(result);
                            });
                    }
                    else if (patternArg.Type == DataType.String && patternArg.String == "*l")
                    {
                        task = conn.RecvLineAsync()
                            .ContinueWith(r =>
                            {
                                if (r.Exception != null)
                                    return BuildTcpErrorTable((r.Exception.InnerException ?? r.Exception).Message);
                                var (ok, line, error) = r.Result;
                                var result = new Table(scriptCapture!);
                                result["ok"] = DynValue.NewBoolean(ok);
                                result["data"] = line != null ? DynValue.NewString(line) : DynValue.Nil;
                                result["error"] = error != null ? DynValue.NewString(error) : DynValue.Nil;
                                return DynValue.NewTable(result);
                            });
                    }
                    else
                    {
                        // Unknown pattern — return error immediately (no yield needed)
                        var errTable = BuildTcpErrorTable("receive: pattern must be a number (byte count) or \"*l\" (line)");
                        return errTable;
                    }

                    RegisterPendingTcpTask(task);
                    return s_tcpPendingYield;
                });

                // conn:close() — synchronous, no async yield needed
                t["close"] = DynValue.NewCallback((ctx, args) =>
                {
                    conn.Dispose();
                    return DynValue.Void;
                });

                return DynValue.NewTable(t);
            }

            var tcpTable = new Table(_script);

            // tcp.connect(host, port [, timeout_ms]) → { ok=bool, data=conn_table|nil, error=string|nil }
            tcpTable["connect"] = DynValue.NewCallback((ctx, args) =>
            {
                var host = args.Count > 0 ? args[0].CastToString() : "";
                var port = args.Count > 1 ? (int)args[1].Number : 0;
                var timeoutMs = args.Count > 2 ? (int)args[2].Number : 5000;

                var task = tcpProxyCapture.ConnectAsync(host, port, timeoutMs)
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            return BuildTcpErrorTable((t.Exception.InnerException ?? t.Exception).Message);
                        var conn = t.Result;
                        tcpProxyCapture.Track(conn);
                        var result = new Table(scriptCapture!);
                        result["ok"] = DynValue.NewBoolean(true);
                        result["data"] = BuildConnectionTable(conn);
                        result["error"] = DynValue.Nil;
                        return DynValue.NewTable(result);
                    });
                RegisterPendingTcpTask(task);
                return s_tcpPendingYield;
            });

            _script.Globals["tcp"] = tcpTable;
        }

        // emu table: frame control + emulator control operations
        var emuTable = new Table(_script);
        emuTable["frameadvance"] = DynValue.NewCallback((ctx, args) => s_frameAdvanceYield);
        emuTable["yield"] = DynValue.NewCallback((ctx, args) => s_tickYield);
        emuTable["framecount"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(getFrameCount()));
        emuTable["time"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(getElapsedSeconds()));

        // Host type
        emuTable["host"] = DynValue.NewCallback((ctx, args) => DynValue.NewString(_hostType));

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
        emuTable["config_valid"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp == null) return DynValue.True;
            // Run off the UI thread to avoid deadlocking Avalonia's SynchronizationContext.
            var (isValid, errors) = Task.Run(() => hostApp.IsCurrentSystemConfigValid()).GetAwaiter().GetResult();
            if (isValid) return DynValue.True;
            var errTable = new Table(_script!);
            for (int i = 0; i < errors.Count; i++)
                errTable[i + 1] = errors[i];
            return DynValue.NewTuple(DynValue.False, DynValue.NewTable(errTable));
        });

        // Emulator control operations (deferred via enqueueAction to run after the current frame/tick)
        emuTable["start"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(CurrentScriptFileName(), async () => { if (hostApp.EmulatorState != EmulatorState.Running) await hostApp.Start(); });
            return DynValue.Void;
        });
        emuTable["pause"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(CurrentScriptFileName(), () => { hostApp.Pause(); return Task.CompletedTask; });
            return DynValue.Void;
        });
        emuTable["stop"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(CurrentScriptFileName(), () => { hostApp.Stop(); return Task.CompletedTask; });
            return DynValue.Void;
        });
        emuTable["reset"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(CurrentScriptFileName(), () => hostApp.Reset());
            return DynValue.Void;
        });
        emuTable["select"] = DynValue.NewCallback((ctx, args) =>
        {
            var systemName = args.Count > 0 ? args[0].CastToString() : null;
            var variant = args.Count > 1 && args[1].Type == DataType.String ? args[1].String : null;
            if (hostApp != null && systemName != null)
                enqueueAction(CurrentScriptFileName(), async () =>
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

                enqueueAction(CurrentScriptFileName(), () =>
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

        emuTable["quit"] = DynValue.NewCallback((ctx, args) =>
        {
            if (hostApp != null)
                enqueueAction(CurrentScriptFileName(), () => { hostApp.QuitApplication(); return Task.CompletedTask; });
            return DynValue.Void;
        });

        // emu.screenshot is registered whenever AllowFileIO is true (same gate as the file table).
        // AllowFileWrite is checked at call time to give a descriptive error,
        // consistent with how file.write() reports AllowFileWrite violations.
        if (config.AllowFileIO && _fileProxy != null && hostApp != null)
        {
            _screenshotProxy = new LuaScreenshotProxy(hostApp, _fileProxy);
            var screenshotProxyCapture = _screenshotProxy;
            var allowWrite = config.AllowFileWrite;
            emuTable["screenshot"] = DynValue.NewCallback((ctx, args) =>
            {
                if (!allowWrite)
                    throw new ScriptRuntimeException("emu.screenshot() requires AllowFileWrite: true in scripting config.");
                var filename = args.Count > 0 ? args[0].CastToString() : null;
                if (string.IsNullOrWhiteSpace(filename))
                    throw new ScriptRuntimeException("emu.screenshot() requires a filename argument.");
                int quality = args.Count > 1 ? (int)(args[1].CastToNumber() ?? 90) : 90;
                try { screenshotProxyCapture.SaveScreenshot(filename, quality); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                { throw new ScriptRuntimeException(ex.Message); }
                return DynValue.Void;
            });
        }

        // emu.save_snapshot / emu.load_snapshot — capture/restore full machine state to a .d6502snap file.
        // Registered only when AllowFileIO is true (paths are sandboxed to the scripting base dir via
        // LuaFileProxy, same as emu.screenshot / file.*). Mirrors the TCP emu.savesnapshot/emu.loadsnapshot
        // commands. save_snapshot requires AllowFileWrite; load_snapshot only needs read access.
        if (config.AllowFileIO && _fileProxy != null && hostApp != null)
        {
            var snapshotFileProxy = _fileProxy;
            var snapshotAllowWrite = config.AllowFileWrite;

            // save_snapshot runs inline (read-only capture) so the file exists and errors surface
            // immediately to the script. The async host call is bridged off the script thread the same
            // way emu.config_valid does, to avoid blocking on a captured SynchronizationContext.
            emuTable["save_snapshot"] = DynValue.NewCallback((ctx, args) =>
            {
                if (!snapshotAllowWrite)
                    throw new ScriptRuntimeException("emu.save_snapshot() requires AllowFileWrite: true in scripting config.");
                var filename = args.Count > 0 ? args[0].CastToString() : null;
                if (string.IsNullOrWhiteSpace(filename))
                    throw new ScriptRuntimeException("emu.save_snapshot() requires a filename argument.");
                var safePath = snapshotFileProxy.GetSafePath(filename)
                    ?? throw new ScriptRuntimeException($"emu.save_snapshot(): unsafe or invalid filename: {filename}");

                try
                {
                    Task.Run(async () =>
                    {
                        if (!hostApp.CanSnapshotCurrentSystem)
                            throw new InvalidOperationException(
                                $"system '{hostApp.SelectedSystemName}' does not support snapshots (none selected/running?).");
                        var dir = Path.GetDirectoryName(safePath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        await using var fileStream = File.Create(safePath);
                        await hostApp.SaveSnapshotAsync(fileStream);
                    }).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw new ScriptRuntimeException($"emu.save_snapshot(): {ex.Message}");
                }
                return DynValue.Void;
            });

            // load_snapshot is deferred (via enqueueAction) because restoring rebuilds the system, which
            // is unsafe to do re-entrantly from inside the current script frame/tick. Like the remote
            // emu.loadsnapshot, the restored machine is left paused. The manifest determines the system.
            emuTable["load_snapshot"] = DynValue.NewCallback((ctx, args) =>
            {
                var filename = args.Count > 0 ? args[0].CastToString() : null;
                if (string.IsNullOrWhiteSpace(filename))
                    throw new ScriptRuntimeException("emu.load_snapshot() requires a filename argument.");
                var safePath = snapshotFileProxy.GetSafePath(filename)
                    ?? throw new ScriptRuntimeException($"emu.load_snapshot(): unsafe or invalid filename: {filename}");

                enqueueAction(CurrentScriptFileName(), async () =>
                {
                    if (!File.Exists(safePath))
                        throw new FileNotFoundException($"emu.load_snapshot(): snapshot file not found: {safePath}");
                    await using var fileStream = File.OpenRead(safePath);
                    await hostApp.LoadSnapshotAsync(fileStream);
                });
                return DynValue.Void;
            });
        }

        var inputTable = new Table(_script);
        inputTable["key_press"] = DynValue.NewCallback((ctx, args) =>
        {
            var keyName = args.Count > 0 ? args[0].CastToString() : "";
            _inputProvider?.KeyPress(keyName);
            return DynValue.Void;
        });
        inputTable["key_release"] = DynValue.NewCallback((ctx, args) =>
        {
            var keyName = args.Count > 0 ? args[0].CastToString() : "";
            _inputProvider?.KeyRelease(keyName);
            return DynValue.Void;
        });
        inputTable["key_release_all"] = DynValue.NewCallback((ctx, args) =>
        {
            _inputProvider?.KeyReleaseAll();
            return DynValue.Void;
        });
        inputTable["is_key_down"] = DynValue.NewCallback((ctx, args) =>
        {
            var keyName = args.Count > 0 ? args[0].CastToString() : "";
            return _inputProvider != null && _inputProvider.IsKeyDown(keyName)
                ? DynValue.NewBoolean(true)
                : DynValue.False;
        });
        inputTable["available_keys"] = DynValue.NewCallback((ctx, args) =>
        {
            var keys = _inputProvider?.GetAvailableKeys() ?? [];
            var t = new Table(_script!);
            for (int i = 0; i < keys.Count; i++)
                t[i + 1] = keys[i];
            return DynValue.NewTable(t);
        });
        inputTable["joystick_set"] = DynValue.NewCallback((ctx, args) =>
        {
            if (_inputProvider == null) return DynValue.Void;
            var port = args.Count > 0 ? (int)args[0].Number : 1;
            var actionName = args.Count > 1 ? args[1].CastToString() : "";
            var pressed = args.Count > 2 && args[2].Boolean;
            _inputProvider.SetJoystickAction(port, actionName, pressed);
            return DynValue.Void;
        });
        inputTable["joystick_action"] = DynValue.NewCallback((ctx, args) =>
        {
            if (_inputProvider == null) return DynValue.False;
            var port = args.Count > 0 ? (int)args[0].Number : 1;
            var actionName = args.Count > 1 ? args[1].CastToString() : "";
            return _inputProvider.IsJoystickActionDown(port, actionName)
                ? DynValue.NewBoolean(true)
                : DynValue.False;
        });
        inputTable["joystick_count"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewNumber(_inputProvider?.JoystickPortCount ?? 0));
        inputTable["available_joystick_actions"] = DynValue.NewCallback((ctx, args) =>
        {
            if (_inputProvider == null)
            {
                _logProxy!.info("[Scripting] available_joystick_actions: provider is null");
                return DynValue.NewTable(new Table(_script!));
            }
            var actions = _inputProvider.GetAvailableJoystickActions();
            _logProxy!.info("[Scripting] available_joystick_actions: provider=" + _inputProvider.GetType().Name + ", count=" + actions.Count);
            var t = new Table(_script!);
            for (int i = 0; i < actions.Count; i++)
                t[i + 1] = actions[i];
            return DynValue.NewTable(t);
        });
        _script.Globals["input"] = inputTable;

        _script.Globals["emu"] = emuTable;
    }

    public void OnSystemStarted(ISystem system)
    {
        if (_cpuProxy == null || _memProxy == null)
            return;
        _cpuProxy.SetCpu(system.CPU);
        _memProxy.SetMem(system.Mem);
        if (system is C64 c64)
            _c64Proxy?.SetC64(c64);
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

    public AdapterScriptHandle? LoadScript(string content, string fileName)
    {
        if (_script == null) return null;
        try
        {
            var chunk = _script.LoadString(content, codeFriendlyName: fileName);
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
    }

    public AdapterScriptState InitialResume(AdapterScriptHandle handle)
    {
        var mh = (MoonSharpScriptHandle)handle;

        // Snapshot globals before resume to detect newly registered hooks
        var prevHooks = ScriptingEngine.HookNames.ToDictionary(
            h => h,
            h => (Closure?)_script!.Globals.Get(h).Function);

        _logProxy!.CurrentScriptFile = handle.FileName;
        // Ensure _allHandles includes this handle so that CurrentScriptFileName() can resolve
        // the filename when top-level code calls emu.start() / emu.select() etc. during initial resume.
        if (!_allHandles.Contains(handle))
            _allHandles = _allHandles.Append(handle).ToArray();
        // Set _currentCoroutine so that async operations (tcp.connect, http.get) called at the
        // top level of a script (before any emu.frameadvance() loop) can register their pending
        // tasks correctly via RegisterPendingTcpTask / RegisterPendingHttpTask.
        _currentCoroutine = mh.Coroutine;
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
        finally
        {
            _currentCoroutine = null;
        }
    }

    public void ResumeFrameAdvanceCoroutines(
        IReadOnlyList<AdapterScriptHandle> activeHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult)
    {
        foreach (var handle in activeHandles)
        {
            var mh = (MoonSharpScriptHandle)handle;

            // Only resume coroutines that last yielded via frameadvance (not HTTP-pending)
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

            // Only resume coroutines that last yielded via emu.yield() (not HTTP-pending)
            var lastYield = _coroutineYieldType.GetValueOrDefault(mh.Coroutine, ScriptYieldType.FrameAdvance);
            if (lastYield != ScriptYieldType.Tick)
                continue;

            onResult(handle, ResumeCoroutine(mh));
        }
    }

    public void ResumePendingHttpCoroutines(
        IReadOnlyList<AdapterScriptHandle> allHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult)
    {
        _allHandles = allHandles;

        // Resume main-body coroutines whose HTTP task has completed
        foreach (var handle in allHandles)
        {
            var mh = (MoonSharpScriptHandle)handle;
            if (!_pendingHttpTasks.TryGetValue(mh.Coroutine, out var task)) continue;
            if (!task.IsCompleted) continue;

            _pendingHttpTasks.Remove(mh.Coroutine);
            var responseTable = task.Exception != null
                ? BuildErrorTable(task.Exception.InnerException ?? task.Exception)
                : task.Result;

            onResult(handle, ResumeCoroutineWithValue(mh, responseTable));
        }

        // Resume hook coroutines whose HTTP task has completed
        for (int i = _pendingHookHttpTasks.Count - 1; i >= 0; i--)
        {
            var (co, handle, task) = _pendingHookHttpTasks[i];
            if (!task.IsCompleted) continue;

            _pendingHookHttpTasks.RemoveAt(i);
            var responseTable = task.Exception != null
                ? BuildErrorTable(task.Exception.InnerException ?? task.Exception)
                : task.Result;

            // Resume hook coroutine; if it yields again (another HTTP call), register it again
            RunHookCoroutineToCompletion(co, handle, responseTable);
        }
    }

    public void ResumePendingTcpCoroutines(
        IReadOnlyList<AdapterScriptHandle> allHandles,
        Action<AdapterScriptHandle, AdapterResumeResult> onResult)
    {
        _allHandles = allHandles;

        // Resume main-body coroutines whose TCP task has completed
        foreach (var handle in allHandles)
        {
            var mh = (MoonSharpScriptHandle)handle;
            if (!_pendingTcpTasks.TryGetValue(mh.Coroutine, out var task)) continue;
            if (!task.IsCompleted) continue;

            _pendingTcpTasks.Remove(mh.Coroutine);
            var responseTable = task.Exception != null
                ? BuildErrorTableForTcp(task.Exception.InnerException ?? task.Exception)
                : task.Result;

            onResult(handle, ResumeCoroutineWithValue(mh, responseTable));
        }

        // Resume hook coroutines whose TCP task has completed
        for (int i = _pendingHookTcpTasks.Count - 1; i >= 0; i--)
        {
            var (co, handle, task) = _pendingHookTcpTasks[i];
            if (!task.IsCompleted) continue;

            _pendingHookTcpTasks.RemoveAt(i);
            var responseTable = task.Exception != null
                ? BuildErrorTableForTcp(task.Exception.InnerException ?? task.Exception)
                : task.Result;

            RunHookCoroutineToCompletion(co, handle, responseTable);
        }
    }

    private DynValue BuildErrorTableForTcp(Exception ex)
    {
        var t = new Table(_script!);
        t["ok"] = DynValue.NewBoolean(false);
        t["data"] = DynValue.Nil;
        t["error"] = DynValue.NewString(ex.Message);
        return DynValue.NewTable(t);
    }

    private AdapterResumeResult ResumeCoroutine(MoonSharpScriptHandle handle)
        => ResumeCoroutineWithValue(handle, null);

    private AdapterResumeResult ResumeCoroutineWithValue(MoonSharpScriptHandle handle, DynValue? resumeValue)
    {
        _logProxy!.CurrentScriptFile = handle.FileName;
        _currentCoroutine = handle.Coroutine;
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var result = resumeValue != null
                ? handle.Coroutine.Resume(resumeValue)
                : handle.Coroutine.Resume();

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
            _currentCoroutine = null;
            if (_config!.MaxExecutionWarningMs > 0)
            {
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                if (elapsedMs > _config.MaxExecutionWarningMs)
                    _logger!.LogWarning("[Scripting] {File} took {Ms:F1}ms (threshold: {Threshold}ms)",
                        handle.FileName, elapsedMs, _config.MaxExecutionWarningMs);
            }
        }
    }

    /// <summary>
    /// Registers an HTTP task for the currently-executing coroutine.
    /// If called from within a hook coroutine, stores against that coroutine instead.
    /// </summary>
    private void RegisterPendingHttpTask(Task<DynValue> task)
    {
        if (_currentCoroutine != null)
            _pendingHttpTasks[_currentCoroutine] = task;
        // If _currentCoroutine is null we're in a hook-invocation coroutine tracked separately;
        // that case is handled by RunHookCoroutineToCompletion which sets _currentCoroutine too.
    }

    /// <summary>
    /// Registers a TCP task for the currently-executing coroutine.
    /// </summary>
    private void RegisterPendingTcpTask(Task<DynValue> task)
    {
        if (_currentCoroutine != null)
            _pendingTcpTasks[_currentCoroutine] = task;
    }

    private DynValue BuildErrorTable(Exception ex)
    {
        var t = new Table(_script!);
        t["ok"] = DynValue.NewBoolean(false);
        t["status"] = DynValue.NewNumber(0);
        t["body"] = DynValue.Nil;
        t["error"] = DynValue.NewString(ex.Message);
        return DynValue.NewTable(t);
    }

    /// <summary>
    /// Runs a hook coroutine until it either completes or suspends on an HTTP task.
    /// If it suspends, registers the pending HTTP task in _pendingHookHttpTasks for the given handle.
    /// <paramref name="resumeValue"/> is passed as the return value of the last HTTP yield (or null for first run).
    /// </summary>
    private void RunHookCoroutineToCompletion(Coroutine co, AdapterScriptHandle handle, DynValue? resumeValue = null)
    {
        _currentCoroutine = co;
        try
        {
            var result = resumeValue != null ? co.Resume(resumeValue) : co.Resume();

            if (co.State == CoroutineState.Suspended)
            {
                var yieldType = DetectYieldType(result);
                if (yieldType == ScriptYieldType.HttpPending
                    && _pendingHttpTasks.TryGetValue(co, out var httpTask))
                {
                    _pendingHttpTasks.Remove(co);
                    _pendingHookHttpTasks.Add((co, handle, httpTask));
                }
                else if (yieldType == ScriptYieldType.TcpPending
                    && _pendingTcpTasks.TryGetValue(co, out var tcpTask))
                {
                    _pendingTcpTasks.Remove(co);
                    _pendingHookTcpTasks.Add((co, handle, tcpTask));
                }
                // Any other yield type from a hook is unexpected — just let it sit
            }
            // If Dead or ForceSuspended, hook finished (or errored) — nothing more to do
        }
        catch (ScriptRuntimeException ex)
        {
            _logger!.LogError("[Scripting] Runtime error in hook for {File}: {Message}", handle.FileName, ex.DecoratedMessage);
        }
        catch (Exception ex)
        {
            _logger!.LogError(ex, "[Scripting] Unexpected error in hook for {File}", handle.FileName);
        }
        finally
        {
            _currentCoroutine = null;
        }
    }

    public bool InvokeHook(string hookName, string scriptFile)
        => InvokeHookCore(hookName, scriptFile, Array.Empty<DynValue>());

    public bool InvokeHookWithArgs(string hookName, string scriptFile, params object[] args)
        => InvokeHookCore(hookName, scriptFile,
               args.Select(a => DynValue.FromObject(_script, a)).ToArray());

    /// <summary>
    /// Runs a hook as a per-invocation coroutine so it can yield on async HTTP calls.
    /// If the hook suspends mid-execution (HTTP in-flight), it is queued in
    /// _pendingHookHttpTasks and will be resumed by ResumePendingHttpCoroutines.
    /// Returns false only if the hook threw a runtime error on its first run.
    /// </summary>
    private bool InvokeHookCore(string hookName, string scriptFile, DynValue[] args)
    {
        if (_script == null) return true;
        if (!_hookDynValueCache.TryGetValue(hookName, out var fn))
            return true;

        _logProxy!.CurrentScriptFile = scriptFile;

        // Find the handle whose FileName matches scriptFile so RunHookCoroutineToCompletion
        // can associate the pending HTTP task with the right script.
        // We use a synthetic handle carrying just the file name when no real handle is available.
        var handle = _allHandles.FirstOrDefault(h => h.FileName == scriptFile)
            ?? new SyntheticScriptHandle(scriptFile);

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            // Create a fresh per-invocation coroutine for this hook call.
            // This allows the hook to yield (e.g. on http.get) without blocking.
            var co = _script.CreateCoroutine(fn).Coroutine;
            _currentCoroutine = co;

            DynValue result;
            if (args.Length == 0)
                result = co.Resume();
            else
                result = co.Resume(args);

            if (co.State == CoroutineState.Suspended)
            {
                var yieldType = DetectYieldType(result);
                if (yieldType == ScriptYieldType.HttpPending
                    && _pendingHttpTasks.TryGetValue(co, out var httpTask))
                {
                    _pendingHttpTasks.Remove(co);
                    _pendingHookHttpTasks.Add((co, handle, httpTask));
                }
                else if (yieldType == ScriptYieldType.TcpPending
                    && _pendingTcpTasks.TryGetValue(co, out var tcpTask))
                {
                    _pendingTcpTasks.Remove(co);
                    _pendingHookTcpTasks.Add((co, handle, tcpTask));
                }
            }
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
            _currentCoroutine = null;
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
            HttpPendingSentinel => ScriptYieldType.HttpPending,
            TcpPendingSentinel => ScriptYieldType.TcpPending,
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

    public void SetInputProvider(IInputInjector? provider) => _inputProvider = provider;

    /// <summary>
    /// Returns the filename of the script whose coroutine is currently executing.
    /// Used to tag deferred actions with the script that enqueued them, so only
    /// that script is marked as errored if the action fails.
    /// Returns an empty string if no coroutine is currently active (e.g. called outside a resume).
    /// </summary>
    private string CurrentScriptFileName() =>
        _currentCoroutine == null
            ? string.Empty
            : _allHandles.OfType<MoonSharpScriptHandle>()
                         .FirstOrDefault(h => h.Coroutine == _currentCoroutine)?.FileName ?? string.Empty;
}
