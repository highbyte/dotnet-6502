using System.Diagnostics;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// MoonSharp-based Lua scripting engine.
/// Loads all .lua files from the configured <see cref="ScriptingConfig.ScriptDirectory"/> and
/// invokes <c>on_before_frame()</c> / <c>on_after_frame()</c> hooks each emulator frame.
///
/// Lua scripts have access to these globals:
/// <list type="bullet">
///   <item><c>cpu</c> — CPU state (pc, a, x, y, sp, carry, zero, negative, overflow, interrupt_disable, decimal_mode)</item>
///   <item><c>mem</c> — Memory access (mem.read(addr), mem.write(addr, value))</item>
///   <item><c>log</c> — Logging (log.info, log.debug, log.warn, log.error)</item>
/// </list>
/// </summary>
public class MoonSharpScriptingEngine : IScriptingEngine
{
    private readonly ScriptingConfig _config;
    private readonly ILogger<MoonSharpScriptingEngine> _logger;
    private Script? _script;

    public bool IsEnabled => true;

    public MoonSharpScriptingEngine(ScriptingConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger<MoonSharpScriptingEngine>();
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
        _script.Globals["log"] = new LuaLogProxy(_logger);

        LoadScriptFiles();
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
            try
            {
                _script!.DoFile(file);
                _logger.LogInformation("[Scripting] Loaded: {File}", Path.GetFileName(file));
            }
            catch (SyntaxErrorException ex)
            {
                _logger.LogError("[Scripting] Syntax error in {File}: {Message}", Path.GetFileName(file), ex.DecoratedMessage);
            }
            catch (ScriptRuntimeException ex)
            {
                _logger.LogError("[Scripting] Runtime error loading {File}: {Message}", Path.GetFileName(file), ex.DecoratedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Scripting] Failed to load {File}", Path.GetFileName(file));
            }
        }
    }

    public void InvokeBeforeFrame() => InvokeHook("on_before_frame");

    public void InvokeAfterFrame() => InvokeHook("on_after_frame");

    private void InvokeHook(string functionName)
    {
        if (_script == null)
            return;

        var sw = Stopwatch.StartNew();
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
                _logger.LogWarning("[Scripting] '{Hook}' took {Ms}ms (threshold: {Threshold}ms)",
                    functionName, sw.ElapsedMilliseconds, _config.MaxExecutionWarningMs);
        }
    }
}
