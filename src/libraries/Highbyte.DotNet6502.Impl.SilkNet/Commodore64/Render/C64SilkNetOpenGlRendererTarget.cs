using System.Numerics;
using Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Highbyte.DotNet6502.Systems.Instrumentation;
using Highbyte.DotNet6502.Systems.Rendering.Custom;
using Highbyte.DotNet6502.Systems.Utils;
using Silk.NET.OpenGL;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Render;

[DisplayName("SilkNet OpenGL")]
[HelpText("Renders C64GpuPacket data using OpenGL and a GLSL shader.\n" +
          "The shader does all rendering of text screen, bitmap screen and sprites.\n" +
          "Uses Silk.NET for OpenGL bindings.")]
public class C64SilkNetOpenGlRendererTarget : ICustomRenderTarget<C64GpuPacket>, IDisposable
{
    public string Name => "SilkGlC64ShaderRenderTarget";

    private C64 _c64;
    public ISystem System => _c64;

    private readonly GL _gl;
    private readonly IWindow _window;


    private bool _changedAllCharsetCodes = false;

    public Instrumentations Instrumentations { get; } = new();


    private BufferObject<float> _vbo = default!;
    private VertexArrayObject<float, int> _vba = default!;

    private OpenGLHelpers.Shader _shader = default!;

    private BufferObject<TextData> _uboTextData = default!;
    private BufferObject<CharsetData> _uboCharsetData = default!;
    private BufferObject<BitmapData> _uboBitmapData = default!;
    private BufferObject<ColorMapData> _uboColorMapData = default!;
    private BufferObject<ScreenLineData> _uboScreenLineData = default!;
    private BufferObject<SpriteData> _uboSpriteData = default!;
    private BufferObject<SpriteContentData> _uboSpriteContentData = default!;
    private C64GpuPacket _c64GpuPacket;
    private readonly C64SilkNetOpenGlRendererConfig _config;

    public C64SilkNetOpenGlRendererTarget(C64 c64, C64SilkNetOpenGlRendererConfig config, GL gl, IWindow window)
    {
        _c64 = c64;
        _config = config;
        _gl = gl;
        _window = window;

        Init();
    }

    public ValueTask PresentAsync(C64GpuPacket c64GpuPacket, CancellationToken ct = default)
    {
        DrawFrame(c64GpuPacket);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Cleanup();
        return ValueTask.CompletedTask;
    }


    public void Init()
    {
        InitGlAndShader();
        //// Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        //_c64.Vic2.CharsetManager.CharsetAddressChanged += (s, e) => CharsetChangedHandler(_c64, e);
    }

    public void InitGlAndShader()
    {
        _gl.Viewport(_window.FramebufferSize);
        InitShader(_c64);
    }

    public void Cleanup()
    {
        CleanupShader();
    }

    public void CleanupShader()
    {
        _shader?.Dispose();
        _vba?.Dispose();
        _vbo?.Dispose();
        _uboTextData?.Dispose();
        _uboCharsetData?.Dispose();
        _uboBitmapData?.Dispose();
        _uboColorMapData?.Dispose();
        _uboScreenLineData?.Dispose();
        _uboSpriteData?.Dispose();
        _uboSpriteContentData?.Dispose();
    }

    private void InitShader(C64 c64)
    {
#if DEBUG
        _gl.GetInteger(GLEnum.MaxUniformBlockSize, out var maxUniformBlockSize); // 65536
        _gl.GetInteger(GLEnum.MaxGeometryUniformComponents, out var maxGeometryUniformComponents); // 2048
        _gl.GetInteger(GLEnum.MaxFragmentUniformComponents, out var maxFragmentUniformComponents); // 4096
#endif

        // Two triangles that covers entire screen
        float[] vertices =
        {
            -1f, -1f, 0.0f, // Bottom-left vertex
            1f, -1f, 0.0f, // Bottom-right vertex
            -1f, 1f, 0.0f, // Top-left vertex,

            -1f, 1f, 0.0f, // Top-left vertex,
            1f,  1f, 0.0f,// Top-right vertex
            1f, -1f, 0.0f, // Bottom-right vertex
        };

        // Defined VBO/VBA objects for vertex shader
        _vbo = new BufferObject<float>(_gl, vertices, BufferTargetARB.ArrayBuffer);
        _vbo.Bind();

        _vba = new VertexArrayObject<float, int>(_gl, _vbo);
        _vba.Bind();
        _vba.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 3, 0);

        // Define text screen data & create Uniform Buffer Object for fragment shader
        var textData = C64GpuPacketBuilder.BuildTextScreenData(c64);
        _uboTextData = new BufferObject<TextData>(_gl, textData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Define character set & create Uniform Buffer Object for fragment shader
        var charsetData = C64GpuPacketBuilder.BuildCharsetData(c64, fromROM: true);
        _uboCharsetData = new BufferObject<CharsetData>(_gl, charsetData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Define bitmap & create Uniform Buffer Object for fragment shader
        var bitmapData = C64GpuPacketBuilder.BuildBitmapData(c64);
        _uboBitmapData = new BufferObject<BitmapData>(_gl, bitmapData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Screen line data Uniform Buffer Object for fragment shader
        var screenLineData = new ScreenLineData[c64.Vic2.Vic2Screen.VisibleHeight];
        _uboScreenLineData = new BufferObject<ScreenLineData>(_gl, screenLineData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Create Uniform Buffer Object for mapping C64 colors to OpenGl colorCode (Vector4) for fragment shader
        var colorMapData = new ColorMapData[16];
        var colorMapName = ColorMaps.DEFAULT_COLOR_MAP_NAME;
        for (byte colorCode = 0; colorCode < colorMapData.Length; colorCode++)
        {
            var systemColor = ColorMaps.GetSystemColor(colorCode, colorMapName);
            colorMapData[colorCode].Color = new Vector4(systemColor.R / 255f, systemColor.G / 255f, systemColor.B / 255f, 1f);
        }
        _uboColorMapData = new BufferObject<ColorMapData>(_gl, colorMapData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Sprite Uniform Buffer Object for sprite meta data
        var spriteData = new SpriteData[c64.Vic2.SpriteManager.NumberOfSprites];
        _uboSpriteData = new BufferObject<SpriteData>(_gl, spriteData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Sprite Uniform Buffer Object for sprite content data (8 sprites * 3 bytes per row * 21 rows = 504 items)
        var spriteContentData = new SpriteContentData[c64.Vic2.SpriteManager.NumberOfSprites * (Vic2Sprite.DEFAULT_WIDTH / 8) * Vic2Sprite.DEFAULT_HEIGTH];
        _uboSpriteContentData = new BufferObject<SpriteContentData>(_gl, spriteContentData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Init shader with:
        // - Vertex shader to draw triangles covering the entire screen.
        // - Fragment shader does the actual drawing of 2D pixels.
        _shader = new OpenGLHelpers.Shader(_gl, vertexShaderPath: "Commodore64/Render/C64shader.vert", fragmentShaderPath: "Commodore64/Render/C64shader.frag");

        // Bind UBOs (used in fragment shader)
        _shader.BindUBO("ubTextData", _uboTextData, binding_point_index: 0);
        _shader.BindUBO("ubCharsetData", _uboCharsetData, binding_point_index: 1);
        _shader.BindUBO("ubColorMap", _uboColorMapData, binding_point_index: 2);
        _shader.BindUBO("ubScreenLineData", _uboScreenLineData, binding_point_index: 3);
        _shader.BindUBO("ubSpriteData", _uboSpriteData, binding_point_index: 4);
        _shader.BindUBO("ubSpriteContentData", _uboSpriteContentData, binding_point_index: 5);
        _shader.BindUBO("ubBitmapData", _uboBitmapData, binding_point_index: 6);
    }

    public void GenerateFrame()
    {
        _c64GpuPacket = C64GpuPacketBuilder.CreateC64GpuPacket(_c64, _changedAllCharsetCodes, _config.UseFineScrollPerRasterLine);
    }

    public void DrawFrame()
    {
        DrawFrame(_c64GpuPacket);
    }

    public void DrawFrame(C64GpuPacket c64GpuPacket)
    {
        // Clear screen
        //_gl.Enable(EnableCap.DepthTest);
        //_gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        // Update shader uniform buffers that needs to be updated

        if (c64GpuPacket.DisplayMode == Vic2.DispMode.Text)
        {
            // Charset dot-matrix UBO
            if (_changedAllCharsetCodes)
            {
                _uboCharsetData.Update(c64GpuPacket.CharsetData);
                _changedAllCharsetCodes = false;
            }
            // Text screen UBO
            _uboTextData.Update(c64GpuPacket.TextScreenData, 0);
        }
        else if (c64GpuPacket.DisplayMode == Vic2.DispMode.Bitmap)
        {
            // Bitmap dot-matrix UBO
            _uboBitmapData.Update(c64GpuPacket.BitmapData, 0);

            // TODO: Bitmap Color UBO
        }

        // Screen line data UBO
        _uboScreenLineData.Update(c64GpuPacket.ScreenLineData, 0);

        // Sprite meta data UBO
        _uboSpriteData.Update(c64GpuPacket.SpriteData, 0);

        // Sprite content UBO
        if (c64GpuPacket.SpriteContentDataIsDirty)
            _uboSpriteContentData.Update(c64GpuPacket.SpriteContentData, 0);

        // Setup shader for use in rendering
        _shader.Use();
        var windowSize = _window.Size;
        _shader.SetUniform("uWindowSize", new Vector3(windowSize.X, windowSize.Y, 0), skipExistCheck: true);

        var scaleX = (float)windowSize.X / c64GpuPacket.Vic2Screen.VisibleWidth;
        var scaleY = (float)windowSize.Y / c64GpuPacket.Vic2Screen.VisibleHeight;
        _shader.SetUniform("uScale", new Vector2(scaleX, scaleY), skipExistCheck: true);

        _shader.SetUniform("uTextScreenStart", new Vector2(c64GpuPacket.VisibleMainScreenArea.Screen.Start.X, c64GpuPacket.VisibleMainScreenArea.Screen.Start.Y));
        _shader.SetUniform("uTextScreenEnd", new Vector2(c64GpuPacket.VisibleMainScreenArea.Screen.End.X, c64GpuPacket.VisibleMainScreenArea.Screen.End.Y));

        _shader.SetUniform("uDisplayMode", (int)c64GpuPacket.DisplayMode);  // uDisplayMode: 0 = Text, 1 = Bitmap
        _shader.SetUniform("uBitmapMode", (int)c64GpuPacket.BitmapMode);  // uBitmapMode: 0 = Standard, 1 = MultiColor
        _shader.SetUniform("uTextCharacterMode", (int)c64GpuPacket.CharacterMode); // uTextCharacterMode: 0 = Standard, 1 = Extended, 2 = MultiColor

        // Draw triangles covering the entire screen, with the fragment shader doing the actual drawing of 2D pixels.
        _vba.Bind();
        _gl.DrawArrays(GLEnum.Triangles, 0, 6);
    }

    private void CharsetChangedHandler(C64 c64, Vic2CharsetManager.CharsetAddressChangedEventArgs e)
    {
        if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetBaseAddress)
            _changedAllCharsetCodes = true;
        else if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetCharacter && e.CharCode.HasValue)
        {
            // Updating individual characters in the UBO array probably take longer time than just updating the entire array.
            _changedAllCharsetCodes = true;
            //if (!_changedCharsetCodes.Contains(e.CharCode.Value))
            //    _changedCharsetCodes.Add(e.CharCode.Value);
        }
    }

    public void Dispose()
    {
        _gl?.BindBuffer(GLEnum.ArrayBuffer, 0);
        _gl?.BindVertexArray(0);
        _gl?.UseProgram(0);

        _uboCharsetData?.Dispose();
        _uboTextData?.Dispose();
        _uboColorMapData?.Dispose();
        _vbo?.Dispose();
        _vba?.Dispose();
    }
}
