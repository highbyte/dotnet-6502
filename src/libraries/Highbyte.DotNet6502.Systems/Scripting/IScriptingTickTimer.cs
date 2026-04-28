namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Platform-specific periodic timer used by <see cref="HostApp{TInputHandlerContext,TAudioHandlerContext}"/>
/// to drive scripting coroutine resumption independently of the emulator frame loop.
/// Hosts supply a concrete timer via the HostApp scripting timer factory; the base class owns the lifecycle.
/// </summary>
public interface IScriptingTickTimer : IDisposable
{
    double IntervalMilliseconds { get; set; }

    event EventHandler Elapsed;

    void Start();
    void Stop();
}
