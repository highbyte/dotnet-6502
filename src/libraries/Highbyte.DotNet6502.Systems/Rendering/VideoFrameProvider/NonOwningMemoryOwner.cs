using System.Buffers;

namespace Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;

/// <summary>
/// A zero-copy memory owner that wraps an existing ReadOnlyMemory without actually owning it.
/// Used to pass rasterizer buffers directly to render targets without copying.
/// </summary>
internal sealed class NonOwningMemoryOwner<T> : IMemoryOwner<T>
{
    private readonly ReadOnlyMemory<T> _memory;

    public NonOwningMemoryOwner(ReadOnlyMemory<T> memory)
    {
        _memory = memory;
    }

    public Memory<T> Memory => System.Runtime.InteropServices.MemoryMarshal.AsMemory(_memory);

    public void Dispose()
    {
        // No-op: we don't own the underlying memory
    }
}
