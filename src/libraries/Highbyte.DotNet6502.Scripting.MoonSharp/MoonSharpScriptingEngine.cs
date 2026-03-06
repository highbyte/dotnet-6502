using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// MoonSharp-based Lua scripting engine.
/// Loads all .lua files from the configured <see cref="ScriptingConfig.ScriptDirectory"/> and
/// invokes hooks each emulator frame.
///
/// Two scripting styles are supported:
/// <list type="bullet">
///   <item>Per-frame hooks: define <c>on_before_frame()</c> and/or <c>on_after_frame()</c> functions.</item>
///   <item>Linear loop style: define <c>on_start()</c> and call <c>emu.frameadvance()</c> to yield to the next frame.</item>
/// </list>
///
/// Lua globals available to scripts:
/// <list type="bullet">
///   <item><c>cpu</c> — CPU state (pc, a, x, y, sp, carry, zero, negative, overflow, interrupt_disable, decimal_mode)</item>
///   <item><c>mem</c> — Memory access (mem.read(addr), mem.write(addr, value))</item>
///   <item><c>log</c> — Logging (log.info, log.debug, log.warn, log.error)</item>
///   <item><c>emu</c> — Emulator control (emu.frameadvance(), emu.framecount())</item>
/// </list>
/// </summary>
public class MoonSharpScriptingEngine : IScriptingEngine
{
    private readonly ScriptingConfig _config;
    private readonly ILogger _logger;
    private Script? _script;
    private LuaLogProxy? _logProxy;
    private Coroutine? _onStartCoroutine;
    private int _frameCount;
    // Maps hook function name -> filename of the script that last defined it
    private readonly Dictionary<string, string> _hookSourceFiles = new();

    public bool IsEnabled => true;

    public MoonSharpScriptingEngine(ScriptingConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingEngine));
    }

    /// <summary>
    /// Initializes the Lua runtime, registers proxy types, and loads all .lua files
    /// from the configured script directory.
    /// </summary>
    public void Initialize(ISystem system)
    {
        // Create a sandboxed Lua environment: safe standard libs only, no file I/O from scripts
        _script = new Script(CoreModules.Preset_SoftSandbox | CoreModules.String | CoreModules.Math | CoreModules.Table);

        // Register UserData types so MoonSharp can expose them to Lua
        UserData.RegisterType<LuaCpuProxy>();
        UserData.RegisterType<LuaMemProxy>();
        UserData.RegisterType<LuaLogProxy>();

        // Set global objects accessible from all scripts
        _script.Globals["cpu"] = new LuaCpuProxy(system.CPU);
        _script.Globals["mem"] = new LuaMemProxy(system.Mem);
        _logProxy = new LuaLogProxy(_logger);
        _script.Globals["log"] = _logProxy;

        // emu table: frameadvance() yields the on_start coroutine back to the host; framecount() returns the current frame number
        var emuTable = new Table(_script);
        emuTable["frameadvance"] = DynValue.NewCallback((ctx, args) => DynValue.NewYieldReq(Array.Empty<DynValue>()));
        emuTable["framecount"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(_frameCount));
        _script.Globals["emu"] = emuTable;

        LoadScriptFiles();

        // If on_start is defined, run it as a host-driven coroutine resumed once per frame
        var onStartFn = _script.Globals.Get("on_start");
        if (onStartFn.Type == DataType.Function)
        {
            _onStartCoroutine = _script.CreateCoroutine(onStartFn).Coroutine;
            _logger.LogInformation("[Scripting] on_start coroutine registered.");
        }
    }

    private static readonly string[] s_hookNames = ["on_before_frame", "on_after_frame", "on_start"];

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
            // Snapshot hook function references before loading so we can detect new/changed definitions
            var prevHooks = s_hookNames.ToDictionary(h => h, h => _script!.Globals.Get(h).Function);
            try
            {
                _script!.DoFile(file);
                _logger.LogInformation("[Scripting] Loaded: {File}", fileName);
                // Record which file defined each hook (last definition wins)
                foreach (var hook in s_hookNames)
                {
                    var fn = _script.Globals.Get(hook);
                    if (fn.Type == DataType.Function && fn.Function != prevHooks[hook])
                        _hookSourceFiles[hook] = fileName;
                }
            }
            catch (SyntaxErrorException ex)
            {
                _logger.LogError("[Scripting] Syntax error in {File}: {Message}", fileName, ex.DecoratedMessage);
            }
            catch (ScriptRuntimeException ex)
            {
                _logger.LogError("[Scripting] Runtime error loading {File}: {Message}", fileName, ex.DecoratedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scripting] Failed to load {File}", fileName);
            }
        }
    }

    public void InvokeBeforeFrame()
    {
        _frameCount++;
        ResumeOnStartCoroutine();
        InvokeHook("on_before_frame");
    }

    public void InvokeAfterFrame() => InvokeHook("on_after_frame");

    private void ResumeOnStartCoroutine()
    {
        if (_onStartCoroutine == null || _onStartCoroutine.State == CoroutineState.Dead)
            return;

        var sw = Stopwatch.StartNew();
        _logProxy!.CurrentScriptFile = _hookSourceFiles.GetValueOrDefault("on_start", "?");
        try
        {
            _onStartCoroutine.Resume();
        }
        catch (ScriptRuntimeException ex)
        {
            _logger.LogError("[Scripting] Runtime error in 'on_start': {Message}", ex.DecoratedMessage);
            _onStartCoroutine = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scripting] Unexpected error in 'on_start' coroutine");
            _onStartCoroutine = null;
        }
        finally
        {
            sw.Stop();
            if (_config.MaxExecutionWarningMs > 0 && sw.ElapsedMilliseconds > _config.MaxExecutionWarningMs)
            {
                var scriptFile = _hookSourceFiles.GetValueOrDefault("on_start", "?");
                _logger.LogWarning("[Scripting] 'on_start' ({Script}) took {Ms}ms (threshold: {Threshold}ms)",
                    scriptFile, sw.ElapsedMilliseconds, _config.MaxExecutionWarningMs);
            }
        }
    }

    private void InvokeHook(string functionName)
    {
        if (_script == null)
            return;

        var scriptFile = _hookSourceFiles.GetValueOrDefault(functionName, "?");
        var sw = Stopwatch.StartNew();
        _logProxy!.CurrentScriptFile = scriptFile;
        try
        {
            var fn = _script.Globals.Get(functionName);
            if (fn.Type == DataType.Function)
                _script.Call(fn);
        }
        catch (ScriptRuntimeException ex)
        {
            _logger.LogError("[Scripting] Runtime error in '{Hook}': {Message}", functionName, ex.DecoratedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Scripting] Unexpected error invoking Lua hook '{Hook}'", functionName);
        }
        finally
        {
            sw.Stop();
            if (_config.MaxExecutionWarningMs > 0 && sw.ElapsedMilliseconds > _config.MaxExecutionWarningMs)
                _logger.LogWarning("[Scripting] '{Hook}' ({Script}) took {Ms}ms (threshold: {Threshold}ms)",
                    functionName, scriptFile, sw.ElapsedMilliseconds, _config.MaxExecutionWarningMs);
        }
    }
}
