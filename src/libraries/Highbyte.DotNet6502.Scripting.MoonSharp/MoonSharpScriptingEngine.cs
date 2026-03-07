using System.Diagnostics;
using System.Linq;
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
/// Coroutines support two yield primitives:
/// <list type="bullet">
///   <item><c>emu.frameadvance()</c> — yields until the next emulator frame executes. Frozen while paused.</item>
///   <item><c>emu.yield()</c> — yields until the next timer tick, regardless of emulator state. Keeps ticking during pause.</item>
/// </list>
///
/// Lua globals available to scripts:
/// <list type="bullet">
///   <item><c>cpu</c> — CPU state (pc, a, x, y, sp, carry, zero, negative, overflow, interrupt_disable, decimal_mode)</item>
///   <item><c>mem</c> — Memory access (mem.read(addr), mem.write(addr, value))</item>
///   <item><c>log</c> — Logging (log.info, log.debug, log.warn, log.error)</item>
///   <item><c>emu</c> — Emulator control (emu.frameadvance(), emu.yield(), emu.framecount(), emu.time())</item>
/// </list>
/// </summary>
public class MoonSharpScriptingEngine : IScriptingEngine
{
    /// <summary>
    /// Distinguishes how a coroutine last yielded, determining when it should be resumed.
    /// </summary>
    private enum YieldType
    {
        /// <summary>Yielded via <c>emu.frameadvance()</c> — only resumed on actual emulator frames.</summary>
        FrameAdvance,
        /// <summary>Yielded via <c>emu.yield()</c> — resumed on every timer tick, even while paused.</summary>
        Tick,
        /// <summary>Coroutine disabled due to exceeding instruction limit (runaway script).</summary>
        Disabled
    }

    private readonly ScriptingConfig _config;
    private readonly ILogger _logger;
    private Script? _script;
    private LuaLogProxy? _logProxy;
    private LuaCpuProxy? _cpuProxy;
    private LuaMemProxy? _memProxy;
    private int _frameCount;
    private readonly Stopwatch _wallClock = new();
    // Per-file coroutines: each file's body runs as its own coroutine
    private readonly List<(Coroutine Coroutine, string FileName)> _fileCoroutines = new();
    // Tracks how each coroutine last yielded
    private readonly Dictionary<Coroutine, YieldType> _coroutineYieldType = new();
    // Maps hook function name -> filename of the script that last defined it
    private readonly Dictionary<string, string> _hookSourceFiles = new();
    // Tracks which files the user has explicitly disabled at runtime
    private readonly HashSet<string> _userDisabledFiles = new(StringComparer.OrdinalIgnoreCase);
    // Tracks scripts that failed to load (syntax error or other compile failure)
    private readonly List<string> _failedFiles = new();
    private IEmulatorControl? _emulatorControl;

    // Sentinel strings passed through DynValue.NewYieldReq to distinguish yield types
    private const string FrameAdvanceSentinel = "frameadvance";
    private const string TickSentinel = "yield";

    private static readonly string[] s_hookNames =
    [
        "on_before_frame", "on_after_frame",
        "on_started", "on_paused", "on_stopped",
        "on_system_selected", "on_variant_selected"
    ];

    public bool IsEnabled => true;

    public event EventHandler? ScriptStatusChanged;

    public void SetEmulatorControl(IEmulatorControl? control) => _emulatorControl = control;

    public void SetScriptEnabled(string fileName, bool enabled)
    {
        if (!_fileCoroutines.Any(fc => fc.FileName == fileName))
            return;

        // Don't allow re-enabling auto-disabled scripts (ForceSuspended coroutine is unrecoverable)
        if (enabled)
        {
            var entry = _fileCoroutines.FirstOrDefault(fc => fc.FileName == fileName);
            if (entry.Coroutine != null && entry.Coroutine.State == CoroutineState.ForceSuspended)
                return;
        }

        if (enabled)
            _userDisabledFiles.Remove(fileName);
        else
            _userDisabledFiles.Add(fileName);

        ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public MoonSharpScriptingEngine(ScriptingConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger(nameof(MoonSharpScriptingEngine));
    }

    /// <summary>
    /// Loads all .lua files as coroutines and performs the initial resume of each (running
    /// top-level code and suspending at the first <c>emu.frameadvance()</c> call if present).
    /// The <c>cpu</c> and <c>mem</c> globals are present but return safe defaults until
    /// <see cref="OnSystemStarted"/> is called. Scripts may call <c>emu.start()</c> etc. here.
    /// </summary>
    public void LoadScripts()
    {
        _fileCoroutines.Clear();
        _coroutineYieldType.Clear();
        _hookSourceFiles.Clear();
        _userDisabledFiles.Clear();
        _failedFiles.Clear();
        _frameCount = 0;
        _wallClock.Restart();

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
        _logProxy = new LuaLogProxy(_logger);
        _script.Globals["log"] = _logProxy;

        // emu table: frame control + emulator control operations
        var emuTable = new Table(_script);
        emuTable["frameadvance"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewYieldReq(new[] { DynValue.NewString(FrameAdvanceSentinel) }));
        emuTable["yield"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewYieldReq(new[] { DynValue.NewString(TickSentinel) }));
        emuTable["framecount"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(_frameCount));
        emuTable["time"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(_wallClock.Elapsed.TotalSeconds));

        // Emulator state queries
        emuTable["state"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(_emulatorControl?.CurrentState ?? "unknown"));
        emuTable["systems"] = DynValue.NewCallback((ctx, args) =>
        {
            var t = new Table(_script!);
            var systems = _emulatorControl?.AvailableSystems ?? [];
            for (int i = 0; i < systems.Count; i++)
                t[i + 1] = systems[i];
            return DynValue.NewTable(t);
        });
        emuTable["selected_system"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(_emulatorControl?.SelectedSystem ?? ""));
        emuTable["selected_variant"] = DynValue.NewCallback((ctx, args) =>
            DynValue.NewString(_emulatorControl?.SelectedVariant ?? ""));

        // Emulator control operations (deferred by the host)
        emuTable["start"] = DynValue.NewCallback((ctx, args) =>
        {
            _emulatorControl?.RequestStart();
            return DynValue.Void;
        });
        emuTable["pause"] = DynValue.NewCallback((ctx, args) =>
        {
            _emulatorControl?.RequestPause();
            return DynValue.Void;
        });
        emuTable["stop"] = DynValue.NewCallback((ctx, args) =>
        {
            _emulatorControl?.RequestStop();
            return DynValue.Void;
        });
        emuTable["reset"] = DynValue.NewCallback((ctx, args) =>
        {
            _emulatorControl?.RequestReset();
            return DynValue.Void;
        });
        emuTable["select"] = DynValue.NewCallback((ctx, args) =>
        {
            var systemName = args.Count > 0 ? args[0].CastToString() : null;
            var variant = args.Count > 1 && args[1].Type == DataType.String ? args[1].String : null;
            if (systemName != null)
                _emulatorControl?.RequestSelectSystem(systemName, variant);
            return DynValue.Void;
        });

        _script.Globals["emu"] = emuTable;

        LoadScriptFiles();
        RunInitialResumes();

        // If EnableScriptsAtStart is false, mark all loaded scripts as user-disabled
        if (!_config.EnableScriptsAtStart)
        {
            foreach (var (_, fileName) in _fileCoroutines)
                _userDisabledFiles.Add(fileName);
        }

        ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called each time the emulator system starts (including after reset).
    /// Updates the <c>cpu</c> and <c>mem</c> globals to point to the live system.
    /// </summary>
    public void OnSystemStarted(ISystem system)
    {
        if (_cpuProxy == null || _memProxy == null)
            return; // LoadScripts was not called yet
        _cpuProxy.SetCpu(system.CPU);
        _memProxy.SetMem(system.Mem);
        _frameCount = 0;
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
                if (_config.MaxInstructionsPerResume > 0)
                    coroutine.AutoYieldCounter = _config.MaxInstructionsPerResume;
                _fileCoroutines.Add((coroutine, fileName));
                _logger.LogInformation("[Scripting] Loaded: {File}", fileName);
            }
            catch (SyntaxErrorException ex)
            {
                _logger.LogError("[Scripting] Syntax error in {File}: {Message}", fileName, ex.DecoratedMessage);
                _failedFiles.Add(fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scripting] Failed to load {File}", fileName);
                _failedFiles.Add(fileName);
            }
        }
    }

    /// <summary>
    /// Performs the first resume of each file coroutine. This runs top-level code (e.g. function
    /// definitions, variable initialisation) and suspends at the first <c>emu.frameadvance()</c>
    /// or <c>emu.yield()</c> call. Hook registrations are detected here so log messages carry
    /// the correct filename.
    /// </summary>
    private void RunInitialResumes()
    {
        foreach (var (coroutine, fileName) in _fileCoroutines)
        {
            var prevHooks = s_hookNames.ToDictionary(h => h, h => _script!.Globals.Get(h).Function);
            _logProxy!.CurrentScriptFile = fileName;
            try
            {
                var result = coroutine.Resume();

                // AutoYieldCounter causes ForceSuspended — the script ran too many
                // instructions without calling emu.yield() or emu.frameadvance().
                if (coroutine.State == CoroutineState.ForceSuspended)
                {
                    _logger.LogError("[Scripting] {File} exceeded instruction limit ({Limit}) during initial load without calling emu.yield() or emu.frameadvance(). Script disabled.",
                        fileName, _config.MaxInstructionsPerResume);
                    _coroutineYieldType[coroutine] = YieldType.Disabled;
                    ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
                }
                // Normal yield — track which yield primitive was used
                else if (coroutine.State == CoroutineState.Suspended)
                {
                    var yieldType = DetectYieldType(result);
                    if (yieldType.HasValue)
                        _coroutineYieldType[coroutine] = yieldType.Value;
                }

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
        ResumeFileCoroutines(YieldType.FrameAdvance);
        InvokeHook("on_before_frame");
    }

    public void ResumeCoroutines() => ResumeFileCoroutines(YieldType.Tick);

    public void InvokeAfterFrame() => InvokeHook("on_after_frame");

    public void InvokeEvent(string hookName, params object[] args) => InvokeHook(hookName, args);

    public IReadOnlyList<ScriptStatus> GetScriptStatuses()
    {
        // Build reverse lookup: fileName -> list of hooks defined by that file
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
        foreach (var (coroutine, fileName) in _fileCoroutines)
        {
            var hooks = (IReadOnlyList<string>)(hooksPerFile.GetValueOrDefault(fileName) ?? new List<string>());
            var yieldType = _coroutineYieldType.GetValueOrDefault(coroutine);

            ScriptExecutionState state;
            ScriptYieldType? yieldTypeDto = null;

            if (_userDisabledFiles.Contains(fileName))
            {
                state = ScriptExecutionState.UserDisabled;
                // Preserve yield type info so the UI can show what it was doing before disable.
                // Only look up if the coroutine was actually tracked (avoid enum default = FrameAdvance).
                if (_coroutineYieldType.TryGetValue(coroutine, out var savedYield))
                {
                    yieldTypeDto = savedYield switch
                    {
                        YieldType.FrameAdvance => ScriptYieldType.FrameAdvance,
                        YieldType.Tick => ScriptYieldType.Tick,
                        _ => null
                    };
                }
            }
            else if (yieldType == YieldType.Disabled || coroutine.State == CoroutineState.ForceSuspended)
            {
                state = ScriptExecutionState.Disabled;
            }
            else if (coroutine.State == CoroutineState.Dead)
            {
                state = hooks.Count > 0 ? ScriptExecutionState.HookOnly : ScriptExecutionState.Completed;
            }
            else if (coroutine.State == CoroutineState.Suspended)
            {
                state = ScriptExecutionState.Running;
                yieldTypeDto = yieldType switch
                {
                    YieldType.FrameAdvance => ScriptYieldType.FrameAdvance,
                    YieldType.Tick => ScriptYieldType.Tick,
                    _ => null
                };
            }
            else
            {
                state = ScriptExecutionState.Running;
            }

            // CanToggle: true for Running, HookOnly, UserDisabled; false for auto-Disabled, Completed
            var canToggle = state != ScriptExecutionState.Disabled
                         && state != ScriptExecutionState.Completed;

            statuses.Add(new ScriptStatus(fileName, state, yieldTypeDto, hooks, canToggle));
        }

        // Include scripts that failed to load (syntax errors, etc.) as system-disabled
        foreach (var failedFileName in _failedFiles)
        {
            statuses.Add(new ScriptStatus(failedFileName, ScriptExecutionState.Disabled, null, Array.Empty<string>(), false));
        }

        return statuses;
    }

    /// <summary>
    /// Extracts the yield type from a coroutine resume result.
    /// Handles both <c>DataType.Tuple</c> (array of yield args) and <c>DataType.String</c>
    /// (MoonSharp may unwrap a single-element yield).
    /// </summary>
    private static YieldType? DetectYieldType(DynValue result)
    {
        string? sentinel = null;

        if (result.Type == DataType.Tuple && result.Tuple.Length > 0 && result.Tuple[0].Type == DataType.String)
            sentinel = result.Tuple[0].String;
        else if (result.Type == DataType.String)
            sentinel = result.String;

        return sentinel switch
        {
            FrameAdvanceSentinel => YieldType.FrameAdvance,
            TickSentinel => YieldType.Tick,
            _ => null
        };
    }

    private void ResumeFileCoroutines(YieldType filterYieldType)
    {
        foreach (var (coroutine, fileName) in _fileCoroutines)
        {
            if (coroutine.State == CoroutineState.Dead || coroutine.State == CoroutineState.ForceSuspended)
                continue;

            // Skip user-disabled scripts
            if (_userDisabledFiles.Contains(fileName))
                continue;

            // Only resume coroutines that last yielded with the matching type.
            // Default to FrameAdvance so untracked coroutines are not picked up by the Tick timer.
            var lastYield = _coroutineYieldType.GetValueOrDefault(coroutine, YieldType.FrameAdvance);
            if (lastYield != filterYieldType)
                continue;

            _logProxy!.CurrentScriptFile = fileName;
            var sw = Stopwatch.StartNew();
            try
            {
                var result = coroutine.Resume();

                // AutoYieldCounter causes ForceSuspended — the script ran too many
                // instructions without calling emu.yield() or emu.frameadvance().
                if (coroutine.State == CoroutineState.ForceSuspended)
                {
                    _logger.LogError("[Scripting] {File} exceeded instruction limit ({Limit}) without calling emu.yield() or emu.frameadvance(). Script disabled.",
                        fileName, _config.MaxInstructionsPerResume);
                    _coroutineYieldType[coroutine] = YieldType.Disabled;
                    ScriptStatusChanged?.Invoke(this, EventArgs.Empty);
                }
                // Normal yield — track which yield primitive was used
                else if (coroutine.State == CoroutineState.Suspended)
                {
                    var yieldType = DetectYieldType(result);
                    if (yieldType.HasValue)
                        _coroutineYieldType[coroutine] = yieldType.Value;
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

    private void InvokeHook(string functionName, params object[] args)
    {
        if (_script == null)
            return;

        var scriptFile = _hookSourceFiles.GetValueOrDefault(functionName, "?");

        // Skip hooks from user-disabled scripts
        if (_userDisabledFiles.Contains(scriptFile))
            return;

        var sw = Stopwatch.StartNew();
        _logProxy!.CurrentScriptFile = scriptFile;
        try
        {
            var fn = _script.Globals.Get(functionName);
            if (fn.Type == DataType.Function)
            {
                if (args.Length == 0)
                    _script.Call(fn);
                else
                    _script.Call(fn, args.Select(a => DynValue.FromObject(_script, a)).ToArray());
            }
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
