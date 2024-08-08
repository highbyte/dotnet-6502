using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;

namespace Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers;

public static class BufferHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe BufferInfo CreateBuffer<TData>(
        GL gl,
        BufferTargetARB bufferTarget,
        BufferUsageARB bufferUsage,
        ReadOnlySpan<TData> data
        )
        where TData : unmanaged
    {
        var handle = gl.GenBuffer();

        gl.BindBuffer(bufferTarget, handle);
        gl.BufferData(bufferTarget, data, bufferUsage);
        gl.BindBuffer(bufferTarget, 0);
        return new BufferInfo(bufferTarget, handle, (uint)(sizeof(TData) * data.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void UpdateBuffer<TData>(
        GL gl,
        BufferInfo uniformBufferInfo,
        ReadOnlySpan<TData> data,
        int offset = 0
        )
        where TData : unmanaged
    {
        if (uniformBufferInfo.Size < sizeof(TData) * (data.Length + offset))
            throw new ArgumentException($"{nameof(data)} doesn't fit in provided {nameof(BufferInfo)}");

        gl.BindBuffer(uniformBufferInfo.BufferTarget, uniformBufferInfo.Handle);
        gl.BufferSubData(uniformBufferInfo.BufferTarget, offset * sizeof(TData), data);
        gl.BindBuffer(uniformBufferInfo.BufferTarget, 0);
    }
}
