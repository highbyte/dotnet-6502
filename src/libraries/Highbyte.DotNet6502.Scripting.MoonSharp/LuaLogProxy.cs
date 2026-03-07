using Microsoft.Extensions.Logging;
using MoonSharp.Interpreter;

namespace Highbyte.DotNet6502.Scripting.MoonSharp;

/// <summary>
/// Exposes logging to Lua scripts via the global <c>log</c> table:
/// <code>
/// log.info("message")
/// log.debug("message")
/// log.warn("message")
/// log.error("message")
/// </code>
/// Messages are prefixed with <c>[Lua:filename.lua]</c> in the log output.
/// The current script file is set by <see cref="MoonSharpScriptingEngine"/> before invoking each hook.
/// </summary>
[MoonSharpUserData]
public class LuaLogProxy
{
    private readonly ILogger _logger;

    /// <summary>
    /// Set by the engine before invoking a hook so log messages include the originating script filename.
    /// </summary>
    internal string CurrentScriptFile { get; set; } = "?";

    internal LuaLogProxy(ILogger logger) => _logger = logger;

    public void info(string msg) => _logger.LogInformation("[Lua:{File}] {Message}", CurrentScriptFile, msg);
    public void debug(string msg) => _logger.LogDebug("[Lua:{File}] {Message}", CurrentScriptFile, msg);
    public void warn(string msg) => _logger.LogWarning("[Lua:{File}] {Message}", CurrentScriptFile, msg);
    public void error(string msg) => _logger.LogError("[Lua:{File}] {Message}", CurrentScriptFile, msg);
}
