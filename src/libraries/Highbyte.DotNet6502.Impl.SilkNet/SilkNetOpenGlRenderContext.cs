using System.Numerics;
using Highbyte.DotNet6502.Systems;
using Silk.NET.OpenGL;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetOpenGlRenderContext : IRenderContext
{
    private readonly GL _gl;
    public GL Gl => _gl;

    private readonly IWindow _window;
    public IWindow Window => _window;

    private readonly float _drawScale;
    public float DrawScale => _drawScale;


    public SilkNetOpenGlRenderContext(IWindow window, float drawScale)
    {
        _gl = window.CreateOpenGL();
        _window = window;
        _drawScale = drawScale;
    }

    public void Cleanup()
    {
    }
}
