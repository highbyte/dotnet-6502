using Highbyte.DotNet6502.Systems;
using Silk.NET.OpenGL;

namespace Highbyte.DotNet6502.Impl.SilkNet;

public class SilkNetOpenGlRenderContext : IRenderContext
{
    private GL _gl;
    public GL Gl
    {
        get
        {
            if (_gl == null)
                _gl = _window.CreateOpenGL();
            return _gl;
        }
    }

    private readonly IView _window;
    public IView Window => _window;

    private readonly float _drawScale;

    public float DrawScale => _drawScale;

    public SilkNetOpenGlRenderContext(IView window, float drawScale)
    {
        _window = window;
        _drawScale = drawScale;
    }

    public void Cleanup()
    {
    }
}
