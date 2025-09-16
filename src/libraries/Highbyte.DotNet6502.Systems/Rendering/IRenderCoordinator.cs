using Highbyte.DotNet6502.Systems.Instrumentation;

namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Marker interface used as the coordinator of a specific rendering pipeline.
/// Each style of coordinator (FrameSourceRenderCoordinator, CommandCoordinator, and PayloadCoordinator) implements this interface, and has different capabilities that must be taken into account.
/// </summary>
public interface IRenderCoordinator : IAsyncDisposable
{
    public Instrumentations Instrumentations { get; }
}
