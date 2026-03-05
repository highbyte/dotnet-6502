namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Optional interface that a system can implement to report when it has
/// finished its startup sequence and is ready for use (e.g. BASIC prompt
/// visible, OS initialised, etc.).
/// <para>
/// When <see cref="AutomatedStartupHandler"/> is waiting for a system to
/// become ready it checks whether the running system implements this
/// interface. If it does, <see cref="IsSystemReady"/> is polled until it
/// returns <see langword="true"/> (or a timeout is reached). If the
/// interface is not implemented a fixed delay is used as a fallback.
/// </para>
/// </summary>
public interface ISystemState
{
    /// <summary>
    /// Returns <see langword="true"/> when the system has completed its
    /// startup sequence and is ready for normal use.
    /// </summary>
    bool IsSystemReady();
}
