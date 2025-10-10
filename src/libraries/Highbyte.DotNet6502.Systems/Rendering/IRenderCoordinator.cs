using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Marker interface used as the coordinator of a specific rendering pipeline.
/// Each style of coordinator (FrameSourceRenderCoordinator, CommandCoordinator, and PayloadCoordinator) implements this interface, and has different capabilities that must be taken into account.
/// </summary>
public interface IRenderCoordinator : IAsyncDisposable
{
    public Instrumentations Instrumentations { get; }

    /// <summary>
    /// Asynchronously flushes pending changes to the underlying storage if modifications have occurred since the last
    /// flush.
    /// 
    /// Used from hosts that uses manual invalidation (not host frame callback) and need to ensure that all pending changes are written.
    /// </summary>
    /// <remarks>If no changes are pending, the method completes without performing any I/O. This method is
    /// safe to call multiple times; only dirty state will trigger a flush.</remarks>
    /// <param name="ct">A cancellation token that can be used to cancel the flush operation.</param>
    /// <returns>A ValueTask that represents the asynchronous flush operation. The task completes when all pending changes have
    /// been written, or immediately if no changes are pending.</returns>
    public ValueTask FlushIfDirtyAsync(CancellationToken ct = default);
}
