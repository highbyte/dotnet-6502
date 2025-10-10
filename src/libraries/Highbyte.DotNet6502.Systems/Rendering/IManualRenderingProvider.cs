namespace Highbyte.DotNet6502.Systems.Rendering;

/// <summary>
/// Interface for host applications that support manual invalidation rendering mode.
/// Provides access to the render coordinator and render targets needed for controls
/// that need to call FlushIfDirtyAsync() in their render methods.
/// </summary>
public interface IManualRenderingProvider
{
    /// <summary>
    /// Gets the current render coordinator, which provides FlushIfDirtyAsync() functionality
    /// for manual invalidation rendering mode.
    /// </summary>
    /// <returns>The render coordinator if available, null if not initialized or using a different rendering mode.</returns>
    public IRenderCoordinator? GetRenderCoordinator();

    /// <summary>
    /// Gets a render target of the specified type if it's currently active.
    /// </summary>
    /// <typeparam name="T">The type of render target to retrieve.</typeparam>
    /// <returns>The render target if available and of the correct type, null otherwise.</returns>
    public T? GetRenderTarget<T>() where T : class, IRenderTarget;

    /// <summary>
    /// Indicates whether the host app is using manual invalidation rendering mode.
    /// Controls should only call GetRenderCoordinator and related methods when this is true.
    /// </summary>
    public bool IsManualInvalidationMode { get; }
}