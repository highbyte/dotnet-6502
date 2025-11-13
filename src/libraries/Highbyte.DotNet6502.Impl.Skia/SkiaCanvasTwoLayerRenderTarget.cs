using System.Reflection;
using Highbyte.DotNet6502.Systems.Rendering.VideoFrameProvider;
using Highbyte.DotNet6502.Systems.Utils;

namespace Highbyte.DotNet6502.Impl.Skia;

[DisplayName("Skia 2-layer Canvas")]
[HelpText("Renders two layers to a SkiaSharp SKCanvas using a GPU shader for compositing.\nIt uses two RenderFrames (byte arrays) provided by the render source.\nThe render source must provide exactly two layers: background and foreground.")]
public sealed class SkiaCanvasTwoLayerRenderTarget : IRenderFrameTarget
{
    private readonly Func<SKCanvas?> _canvasAccessor; // provided by host per-frame
    private readonly bool _flush;

    public string Name => "SkiaCanvasTwoLayerRenderTarget";

    public RenderSize TargetSize { get; }
    public PixelFormat AcceptedFormat { get; } = PixelFormat.Rgba32; // TODO: What to I need AcceptedFormat for?

    public bool SupportsCompositing => true;

    private SKRuntimeEffect _sKRuntimeEffect; // Shader
    private SKRuntimeEffectUniforms _sKRuntimeEffectUniforms; // Shader uniforms
    private SKRuntimeEffectChildren _sKRuntimeEffectChildren; // Shader children (textures)
    private SKPaint _shaderPaint;


    public SkiaCanvasTwoLayerRenderTarget(RenderSize size, Func<SKCanvas?> canvasAccessor, bool flush)
    {
        TargetSize = size;
        _canvasAccessor = canvasAccessor;
        _flush = flush;

        InitSkiaShader(size);
    }

    // Present two layers via GPU shader compositing
    public unsafe ValueTask PresentAsync(RenderFrame frame, CancellationToken ct = default)
    {
        return PresentAsyncSKImage(frame, ct);
    }

    private unsafe ValueTask PresentAsyncSKImage(RenderFrame frame, CancellationToken ct = default)
    {
        var canvas = _canvasAccessor();
        if (canvas is null) return ValueTask.CompletedTask;

        if (!frame.IsMultiLayer || frame.LayerInfos.Count != 2 || frame.LayerPixels.Count != 2)
            throw new ArgumentException("SkiaCanvasTwoLayerRenderTarget expects exactly 2 layers (background, foreground).", nameof(frame));

        // Build image views over the pinned layer memory and render using the shader
        // Assume layer 0 is background, layer 1 is foreground
        var bgInfo = frame.LayerInfos[0];
        var fgInfo = frame.LayerInfos[1];

        var bgColorType = bgInfo.PixelFormat switch
        {
            PixelFormat.Rgba32 => SKColorType.Rgba8888,
            PixelFormat.Bgra32 => SKColorType.Bgra8888,
            _ => throw new NotSupportedException($"Pixel format {bgInfo.PixelFormat} not supported in {nameof(SkiaCanvasTwoLayerRenderTarget)}")
        };
        var fgColorType = fgInfo.PixelFormat switch
        {
            PixelFormat.Rgba32 => SKColorType.Rgba8888,
            PixelFormat.Bgra32 => SKColorType.Bgra8888,
            _ => throw new NotSupportedException($"Pixel format {fgInfo.PixelFormat} not supported in {nameof(SkiaCanvasTwoLayerRenderTarget)}")
        };


        var bgImageInfo = new SKImageInfo(bgInfo.Size.Width, bgInfo.Size.Height, bgColorType, SKAlphaType.Unpremul);
        var fgImageInfo = new SKImageInfo(fgInfo.Size.Width, fgInfo.Size.Height, fgColorType, SKAlphaType.Unpremul);

        using var mhBg = frame.LayerPixels[0].Pin();
        using var mhFg = frame.LayerPixels[1].Pin();

        using var bgImage = SKImage.FromPixels(bgImageInfo, (IntPtr)mhBg.Pointer, bgInfo.StrideBytes);
        using var fgImage = SKImage.FromPixels(fgImageInfo, (IntPtr)mhFg.Pointer, fgInfo.StrideBytes);

        WriteImageToCanvas(bgImage, fgImage, canvas);

        if (_flush)
            canvas.Flush();

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void WriteImageToCanvas(SKImage backgroundImage, SKImage foregroundImage, SKCanvas canvas)
    {
        // shader uniform values
        //_sKRuntimeEffectUniforms["parameterName"] = 1234;

        // Convert images to shader textures
        using var backgroundTexture = backgroundImage.ToShader();
        using var foregroundTexture = foregroundImage.ToShader();

        _sKRuntimeEffectChildren["background_texture"] = backgroundTexture;
        _sKRuntimeEffectChildren["foreground_texture"] = foregroundTexture;

        using var shader = _sKRuntimeEffect.ToShader(_sKRuntimeEffectUniforms, _sKRuntimeEffectChildren);
        _shaderPaint.Shader = shader;

        canvas.DrawRect(0, 0, backgroundImage.Width, backgroundImage.Height, _shaderPaint);
    }

    private void InitSkiaShader(RenderSize size)
    {
        // --------------------
        // Load and compile shader.
        // --------------------
        var src = LoadShaderSource("sksl_shader_two_layers.frag");
        src = ReplaceShaderPlaceholders(src, size.Height);
        _sKRuntimeEffect = SKRuntimeEffect.CreateShader(src, out var error);
        if (!string.IsNullOrEmpty(error))
            throw new DotNet6502Exception($"Shader compilation error: {error}");

        _sKRuntimeEffectUniforms = new SKRuntimeEffectUniforms(_sKRuntimeEffect);
        _sKRuntimeEffectChildren = new SKRuntimeEffectChildren(_sKRuntimeEffect);
        _shaderPaint = new SKPaint();
    }

    private string ReplaceShaderPlaceholders(string src, int height)
    {
        src = src.Replace("#VISIBLE_HEIGHT", height.ToString());
        return src;
    }

    private string LoadShaderSource(string shaderFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{"Highbyte.DotNet6502.Impl.Skia.Resources.Shaders"}.{shaderFileName}";
        using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
        {
            if (resourceStream == null)
                throw new ArgumentException($"Cannot load shader from embedded resource. Resource: {resourceName}", nameof(shaderFileName));

            // Read contents of stream to string
            using var reader = new StreamReader(resourceStream);
            return reader.ReadToEnd();
        }
    }
}