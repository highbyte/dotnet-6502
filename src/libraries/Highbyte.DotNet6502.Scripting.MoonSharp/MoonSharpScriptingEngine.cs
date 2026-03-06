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
/// Two scripting styles are supported and can be mixed across files:
/// <list type="bullet">
///   <item>
///     Per-frame hooks: define <c>on_before_frame()</c> and/or <c>on_after_frame()</c> functions.
///     The host calls these once per frame.
///   </item>
///   <item>
///     Linear loop style (BizHawk-style): write a top-level <c>while true do ... emu.frameadvance() end</c> loop.
///     Each file runs as its own coroutine; <c>emu.frameadvance()</c> suspends it until the next frame.
///     No wrapper function is needed.
///   </item>
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
    private int _frameCount;
    // Per-file coroutines: each file's body runs as its own coroutine
    private readonly List<(Coroutine Coroutine, string FileName)> _fileCoroutines = new();
    // Maps hook function name -> filename of the script that last defined it
    private readonly Dictionary<string, string> _hookSourceFiles = new();

    private static readonly string[] s_hookNames = ["on_before_frame", "on_after_frame"];

    public bool IsEnabled => true;

    public MoonSharpScriptingEngine(ScriptingConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingEngine));
    }

    /// <summary>
    /// Initializes the Lua runtime, registers proxy types, loads all .lua files as coroutines,
    /// and performs the initial resume of each (running top-level code and suspending at the
    /// first <c>emu.frameadvance()</c> call if present).
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

        // emu table: frameadvance() yields the file coroutine back to the host; framecount() returns the current frame number
        var emuTable = new Table(_script);
        emuTable["frameadvance"] = DynValue.NewCallback((ctx, args) => DynValue.NewYieldReq(Array.Empty<DynValue>()));
        emuTable["framecount"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(_frameCount));
        _script.Globals["emu"] = emuTable;

        LoadScriptFiles();
        RunInitialResumes();
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
            try
            {
                // Compile without executing — each file body will run as a coroutine
                var chunk = _script!.LoadFile(file);
                var coroutine = _script.CreateCoroutine(chunk).Coroutine;
                _fileCoroutines.Add((coroutine, fileName));
                _logger.LogInformation("[Scripting] Loaded: {File}", fileName);
            }
            catch (SyntaxErrorException ex)
            {
                _logger.LogError("[Scripting] Syntax error in {File}: {Message}", fileName, ex.DecoratedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scripting] Failed to load {File}", fileName);
            }
        }
    }

    /// <summary>
    /// Performs the first resume of each file coroutine. This runs top-level code (e.g. function
    /// definitions, variable initialisation) and suspends at the first <c>emu.frameadvance()</c>
    /// call. Hook registrations are detected here so log messages carry the correct filename.
    /// </summary>
    private void RunInitialResumes()
    {
        foreach (var (coroutine, fileName) in _fileCoroutines)
        {
            var prevHooks = s_hookNames.ToDictionary(h => h, h => _script!.Globals.Get(h).Function);
            _logProxy!.CurrentScriptFile = fileName;
            try
            {
                coroutine.Resume();
                // Record which file defined each hook (last definition wins)
                foreach (var hook in s_hookNames)
                {
                    var fn = _script!.Globals.Get(hook);
                    if (fn.Type == DataType.Function && fn.Function != prevHooks[hook])
                        _hookSourceFiles[hook] = fileName;
                }
            }
            catch (ScriptRuntimeException ex)
            {
                _logger.LogError("[Scripting] Runtime error in {File}: {Message}", fileName, ex.DecoratedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scripting] Unexpected error in {File}", fileName);
            }
        }
    }

    public void InvokeBeforeFrame()
    {
        _frameCount++;
        ResumeFileCoroutines();
        InvokeHook("on_before_frame");
    }

    public void InvokeAfterFrame() => InvokeHook("on_after_frame");

    private void ResumeFileCoroutines()
    {
        foreach (var (coroutine, fileName) in _fileCoroutines)
        {
            if (coroutine.State == CoroutineState.Dead)
                continue;

            _logProxy!.CurrentScriptFile = fileName;
            var sw = Stopwatch.StartNew();
            try
            {
                coroutine.Resume();
            }
            catch (ScriptRuntimeException ex)
            {
                _logger.LogError("[Scripting] Runtime error in {File}: {Message}", fileName, ex.DecoratedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scripting] Unexpected error in {File}", fileName);
            }
            finally
            {
                sw.Stop();
                if (_config.MaxExecutionWarningMs > 0 && sw.ElapsedMilliseconds > _config.MaxExecutionWarningMs)
                {
                    _logger.LogWarning("[Scripting] {File} took {Ms}ms (threshold: {Threshold}ms)",
                        fileName, sw.ElapsedMilliseconds, _config.MaxExecutionWarningMs);
                }
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
