using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.DebugAdapter;

/// <summary>
/// Extends <see cref="IHostApp"/> with the debug adapter contract required for
/// external debugger integration (e.g., the VSCode DAP extension via TCP).
///
/// Any host application that supports an external debugger must implement this
/// interface in addition to <see cref="IHostApp"/>.
/// </summary>
public interface IDebuggableHostApp : IHostApp
{
    /// <summary>
    /// When true, the emulator will not execute any instructions until an external
    /// debugger connects. Cleared automatically when the debugger attaches.
    /// Set before system start when debugging from the very first instruction.
    /// </summary>
    bool WaitForExternalDebugger { get; set; }

    /// <summary>
    /// Whether an external debugger (e.g., VSCode) is currently attached.
    /// When true, the built-in monitor should not activate on breakpoints.
    /// </summary>
    bool IsExternalDebuggerAttached { get; }

    /// <summary>
    /// Installs <paramref name="debugAdapter"/>'s breakpoint evaluator into the
    /// host's execution loop and sets <see cref="IsExternalDebuggerAttached"/> = true.
    /// </summary>
    void SetExternalDebugAdapter(DebugAdapterLogic debugAdapter);

    /// <summary>
    /// Removes the debug adapter, restores the original breakpoint evaluator,
    /// and sets <see cref="IsExternalDebuggerAttached"/> = false.
    /// </summary>
    void ClearExternalDebugAdapter();
}
