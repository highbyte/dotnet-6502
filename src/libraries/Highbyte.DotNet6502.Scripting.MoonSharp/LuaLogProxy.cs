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
/// All messages are prefixed with <c>[Lua]</c> in the log output.
/// </summary>
[MoonSharpUserData]
public class LuaLogProxy
{
    private readonly ILogger _logger;

    internal LuaLogProxy(ILogger logger) => _logger = logger;

    public void info(string msg) => _logger.LogInformation("[Lua] {Message}", msg);
    public void debug(string msg) => _logger.LogDebug("[Lua] {Message}", msg);
    public void warn(string msg) => _logger.LogWarning("[Lua] {Message}", msg);
    public void error(string msg) => _logger.LogError("[Lua] {Message}", msg);
}
