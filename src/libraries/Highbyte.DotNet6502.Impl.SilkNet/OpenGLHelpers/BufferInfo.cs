using Silk.NET.OpenGL;

namespace Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers;

public class BufferInfo
{
    public BufferTargetARB BufferTarget { get; private set; }
    public uint Handle { get; private set; }
    public uint Size { get; private set; }
    public BufferInfo(BufferTargetARB bufferTarget, uint handle, uint size)
    {
        BufferTarget = bufferTarget;
        Handle = handle;
        Size = size;
    }
}
