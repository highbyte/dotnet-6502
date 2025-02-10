using System.Reflection;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Render;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video.v3;

/// <summary>
/// Renders a C64 system to a SkiaSharp canvas.  
/// 
/// An implementation of the common C64RenderBase base class, that provides mostly the same functionallity as C64SkiaRenderer2b.cs, with these changes
/// - Vastly reduced number of lines of code, because most logic could be moved to a common base class (which can be used by other target).
/// - No longer writes sprite color information in a separate texture data bitmap (that was used in logic in shader), instead the sprites are written with their correct color per line to either the background or foreground bitmap.
/// - The associated shader is simplified, and only uses the background and foreground bitmaps that are merged.
/// 
/// Overview
/// - Called after each instruction to generate Text and Bitmap graphics.
/// - Called once per frame to generate Sprites (if possible a future improvement should make this also be called after each instruction if performance allows it).
/// - Writes image data to a SKBitmap backed by a pixel array, and uses a shader + generated SKBitmap "textures" to do the actual drawing to the SkiaSharp canvas.
/// - Fast enough to be used for native and browser (WASM) hosts if the computer is reasonably fast.
/// 
/// Supports:
/// - Text mode (Standard, Extended, MultiColor)
/// - Bitmap mode (Standard/HiRes, MultiColor)
/// - Colors per raster line
/// - Fine scroll per raster line
/// - Sprites (Standard, MultiColor)
///   
/// </summary>
public class C64SkiaRenderer3 : C64RenderBase
{
    private readonly SkiaRenderContext _skiaRenderContext;

    private Func<SKCanvas> _getSkCanvas = default!;

    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_BackgroundAndBorder;
    private SkiaBitmapBackedByPixelArray _skiaPixelArrayBitmap_Foreground;

    private SKRuntimeEffect _sKRuntimeEffect; // Shader
    private SKRuntimeEffectUniforms _sKRuntimeEffectUniforms; // Shader uniforms
    private SKRuntimeEffectChildren _sKRuntimeEffectChildren; // Shader children (textures)
    private SKPaint _shaderPaint;

    // Lookup table for mapping C64 colors to shader colors
    private readonly Dictionary<uint, float[]> _sKColorToShaderColorMap = new Dictionary<uint, float[]>();
    private C64SkiaColors _c64SkiaColors;

    public C64SkiaRenderer3(C64 c64, SkiaRenderContext skiaRenderContext) : base(c64)
    {
        _skiaRenderContext = skiaRenderContext;
    }


    private Dictionary<byte, uint> _c64ToRenderColorMap;
    protected override Dictionary<byte, uint> C64ToRenderColorMap => _c64ToRenderColorMap;
    protected override bool FlipY => false;
    protected override uint TransparentColor => (uint)SKColors.DarkMagenta; // TODO: Shouldn't this be 0x00000000 ?

    protected override string StatsCategory => "SkiaSharp-custom";

    protected override void OnBeforeInit()
    {
        _getSkCanvas = _skiaRenderContext.GetCanvas;

        _c64SkiaColors = new C64SkiaColors(C64.ColorMapName);
        // Convert byte->SKColor to byte->uint lookup table used by base class.
        _c64ToRenderColorMap = _c64SkiaColors.C64ToSkColorMap.ToDictionary(colorItem => colorItem.Key, colorItem => (uint)colorItem.Value);
    }

    protected override void OnAfterInit()
    {
        InitSkiaBitmaps(C64);
        InitSkiaShader(C64);
    }

    protected override void RenderArrays()
    {
        // Draw to a canvas using a shader with textures from background and fotrgroumd bitmaps
        WriteBitmapToCanvas(_skiaPixelArrayBitmap_BackgroundAndBorder.Bitmap, _skiaPixelArrayBitmap_Foreground.Bitmap, _getSkCanvas());
    }

    protected override void OnCleanup()
    {
        _skiaPixelArrayBitmap_BackgroundAndBorder.Free();
        _skiaPixelArrayBitmap_Foreground.Free();
    }

    private void InitSkiaShader(C64 c64)
    {
        // --------------------
        // Load and compile shader.
        // --------------------
        var src = LoadShaderSource("C64_sksl_shader3.frag");
        src = ReplaceShaderPlaceholders(src, c64);
        _sKRuntimeEffect = SKRuntimeEffect.CreateShader(src, out var error);
        if (!string.IsNullOrEmpty(error))
            throw new DotNet6502Exception($"Shader compilation error: {error}");

        _sKRuntimeEffectUniforms = new SKRuntimeEffectUniforms(_sKRuntimeEffect);
        _sKRuntimeEffectChildren = new SKRuntimeEffectChildren(_sKRuntimeEffect);
        _shaderPaint = new SKPaint();

        InitShaderColorValueLookup();
    }

    private void InitShaderColorValueLookup()
    {
        // Map 32 bit unsigned int color values (from SKColors drawn on pixelarray/bitmap) to float[4] color values as seen in shader
        AddColorToShaderColorMap(TransparentColor);

        void AddColorToShaderColorMap(SKColor color)
        {
            _sKColorToShaderColorMap.Add((uint)color, [color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f]);
        }
    }

    private string ReplaceShaderPlaceholders(string src, C64 c64)
    {
        src = src.Replace("#VISIBLE_HEIGHT", c64.Vic2.Vic2Screen.VisibleHeight.ToString());
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

    private void InitSkiaBitmaps(C64 c64)
    {
        // Init SKBitmap backed by pixel array
        var vic2Screen = c64.Vic2.Vic2Screen;
        var width = vic2Screen.VisibleWidth;
        var height = vic2Screen.VisibleHeight;

        // Array/Bitmap for C64 background and borders
        _skiaPixelArrayBitmap_BackgroundAndBorder = SkiaBitmapBackedByPixelArray.Create(width, height, base.PixelArray_BackgroundAndBorder);

        // Array/Bitmap for C64 foreground color from text, bitmaps, and sprites.
        _skiaPixelArrayBitmap_Foreground = SkiaBitmapBackedByPixelArray.Create(width, height, base.PixelArray_Foreground);
    }

    private void WriteBitmapToCanvas(SKBitmap backgroundAndBorderBitmap, SKBitmap foregroundBitmap, SKCanvas canvas)
    {

        // _drawCanvasWithShaderStat.Start();

        // shader uniform values
        _sKRuntimeEffectUniforms["transparentColor"] = _sKColorToShaderColorMap[TransparentColor];

        // Convert bitmaps to shader textures
        using var backgroundAndBorderTexture = backgroundAndBorderBitmap.ToShader();
        using var foregroundTexture = foregroundBitmap.ToShader();

        _sKRuntimeEffectChildren["background_and_border_texture"] = backgroundAndBorderTexture;
        _sKRuntimeEffectChildren["foreground_texture"] = foregroundTexture;

        using var shader = _sKRuntimeEffect.ToShader(_sKRuntimeEffectUniforms, _sKRuntimeEffectChildren);
        _shaderPaint.Shader = shader;

        canvas.DrawRect(0, 0, backgroundAndBorderBitmap.Width, backgroundAndBorderBitmap.Height, _shaderPaint);

        //_drawCanvasWithShaderStat.Stop();
    }
}
