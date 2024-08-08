using Silk.NET.OpenGL;

namespace Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers;

public class BufferObject<TData> : IDisposable
    where TData : unmanaged
{
    private GL _gl;
    private BufferInfo _bufferInfo;
    public BufferInfo BufferInfo => _bufferInfo;

    public unsafe BufferObject(GL gl, ReadOnlySpan<TData> data, BufferTargetARB bufferTarget, BufferUsageARB bufferUsage = BufferUsageARB.StaticDraw)
    {
        _gl = gl;
        _bufferInfo = BufferHelper.CreateBuffer(gl, bufferTarget, bufferUsage, data);
    }

    public unsafe void Update(ReadOnlySpan<TData> data, int offset = 0)
    {
        BufferHelper.UpdateBuffer(_gl, _bufferInfo, data, offset);
    }

    public void Bind()
    {
        _gl.BindBuffer(_bufferInfo.BufferTarget, _bufferInfo.Handle);
    }
    public void Unbind()
    {
        _gl.BindBuffer(_bufferInfo.BufferTarget, 0);
    }
    public void Dispose()
    {
        _gl.DeleteBuffer(_bufferInfo.Handle);
    }
}
