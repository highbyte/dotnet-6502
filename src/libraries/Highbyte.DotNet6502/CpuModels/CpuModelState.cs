namespace Highbyte.DotNet6502;

/// <summary>
/// Base for per-CPU-instance state a CPU model carries beyond the standard registers
/// (e.g. the 6510's on-chip I/O port). Created by the model definition's state factory
/// at CPU construction — mutable state never lives on the immutable, shareable
/// definition itself.
/// </summary>
public abstract class CpuModelState
{
    /// <summary>
    /// A copy for a cloned CPU: same state values, but with no event subscribers —
    /// a clone must never retain callbacks into the original machine.
    /// </summary>
    public abstract CpuModelState Clone();

    /// <summary>
    /// The state's snapshot payload — CHIP state only, never board wiring (input levels,
    /// subscriptions), which the target machine re-establishes itself. The layout is
    /// owned by the concrete state type and versioned by the cpu-6502 snapshot module
    /// version that embeds it.
    /// </summary>
    public abstract byte[] SerializeState();

    /// <summary>
    /// Applies a payload produced by <see cref="SerializeState"/>. Implementations must
    /// apply atomically (a single change notification with the final state).
    /// </summary>
    public abstract void RestoreState(byte[] data);
}
