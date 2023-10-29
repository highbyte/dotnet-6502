using System.Numerics;
using Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Silk.NET.OpenGL;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;

public class C64SilkNetOpenGlRenderer : IRenderer<C64, SilkNetOpenGlRenderContext>, IDisposable
{

    private SilkNetOpenGlRenderContext _silkNetOpenGlRenderContext;
    private GL _gl => _silkNetOpenGlRenderContext.Gl;

    private bool _changedAllCharsetCodes = false;

    public bool HasDetailedStats => true;
    public List<string> DetailedStatNames => new List<string>()
    {
    };

    // Types for Uniform Buffer Objects, must align to 16 bytes.
    public struct TextData
    {
        public uint Character;  // uint = 4 bytes, only using 1 byte
        public uint Color;      // uint = 4 bytes, only using 1 byte
        public uint _;          // unused
        public uint __;         // unused
    }
    public struct CharsetData
    {
        public uint CharLine;   // uint = 4 bytes, only using 1 byte
        public uint _;          // unused
        public uint __;         // unused
        public uint ___;        // unused
    }
    public struct RasterLineData
    {
        public uint BorderColorCode;        // uint = 4 bytes, only using 1 byte
        public uint BackgroundColor0Code;   // uint = 4 bytes, only using 1 byte
        public uint BackgroundColor1Code;   // uint = 4 bytes, only using 1 byte
        public uint BackgroundColor2Code;   // uint = 4 bytes, only using 1 byte

        public uint BackgroundColor3Code;   // uint = 4 bytes, only using 1 byte
        public uint _____;      // unused
        public uint ______;     // unused
        public uint _______;    // unused

        public uint ColMode40;   // 0 = 38 col mode, 1 = 40 col mode
        public uint RowMode25;   // 0 = 24 row mode, 1 = 25 row mode
        public uint ScrollX;     // 0 to 7 horizontal fine scrolling (+1 in 38 col mode)
        public int ScrollY;      // -3 to 4 vertical fine scrolling (+1 in 24 row mode)

    }
    public struct ColorMapData
    {
        public uint ColorCode;  // uint = 4 bytes, only using 1 byte
        public uint __;         // unused
        public uint ___;        // unused
        public uint ____;       // unused
        public Vector4 Color;   // Vector4 = 16 bytes
    }

    private BufferObject<float> _vbo;
    private VertexArrayObject<float, int> _vba;

    private OpenGLHelpers.Shader _shader;

    private BufferObject<TextData> _uboTextData;
    private BufferObject<CharsetData> _uboCharsetData;
    private BufferObject<ColorMapData> _uboColorMapData;
    private BufferObject<RasterLineData> _uboRasterLineData;

    public C64SilkNetOpenGlRenderer()
    {
    }

    public void Init(C64 c64, SilkNetOpenGlRenderContext silkNetOpenGlRenderContext)
    {
        _silkNetOpenGlRenderContext = silkNetOpenGlRenderContext;

        _gl.Viewport(silkNetOpenGlRenderContext.Window.FramebufferSize);

        // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        c64.Vic2.CharsetManager.CharsetAddressChanged += (s, e) => CharsetChangedHandler(c64, e);

        InitShader(c64);
    }

    private void InitShader(C64 c64)
    {
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
        var textData = BuildTextScreenData(c64);
        _uboTextData = new BufferObject<TextData>(_gl, textData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Define character set & create Uniform Buffer Object for fragment shader
        var charsetData = BuildCharsetData(c64, fromROM: true);
        _uboCharsetData = new BufferObject<CharsetData>(_gl, charsetData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Rasterline data Uniform Buffer Object for fragment shader
        var rasterLineData = new RasterLineData[c64.Vic2.Vic2Screen.VisibleHeight];
        _uboRasterLineData = new BufferObject<RasterLineData>(_gl, rasterLineData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Create Uniform Buffer Object for mapping C64 colors to OpenGl colorCode (Vector4) for fragment shader
        var colorMapData = new ColorMapData[16];
        var colorMapName = ColorMaps.DEFAULT_COLOR_MAP_NAME;
        for (byte colorCode = 0; colorCode < colorMapData.Length; colorCode++)
        {
            var systemColor = ColorMaps.GetSystemColor(colorCode, colorMapName);
            colorMapData[colorCode].Color = new Vector4(systemColor.R / 255f, systemColor.G / 255f, systemColor.B / 255f, 1f);
        }
        _uboColorMapData = new BufferObject<ColorMapData>(_gl, colorMapData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Init shader with:
        // - Vertex shader to draw triangles covering the entire screen.
        // - Fragment shader does the actual drawing of 2D pixels.
        _shader = new OpenGLHelpers.Shader(_gl, vertexShaderPath: "Commodore64/Video/C64shader.vert", fragmentShaderPath: "Commodore64/Video/C64shader.frag");

        // Bind UBOs (used in fragment shader)
        _shader.BindUBO("ubTextData", _uboTextData, binding_point_index: 0);
        _shader.BindUBO("ubCharsetData", _uboCharsetData, binding_point_index: 1);
        _shader.BindUBO("ubColorMap", _uboColorMapData, binding_point_index: 2);
        _shader.BindUBO("ubRasterLineData", _uboRasterLineData, binding_point_index: 3);
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SilkNetOpenGlRenderContext)renderContext);
    }

    public void Draw(C64 c64, Dictionary<string, double> detailedStats)
    {
        foreach (var detailedStatName in DetailedStatNames)
        {
            detailedStats[detailedStatName] = 0;
        }

        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;
        var vic2ScreenLayouts = vic2.ScreenLayouts;

        // Visible screen area
        var visibileLayout = vic2ScreenLayouts.GetLayout(LayoutType.Visible);
        // Clip main screen area with consideration to possible 38 column and 24 row mode
        var visibleClippedScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized);
        // Main screen draw area for characters, without consideration to 38 column mode or 24 row mode.
        var visibleMainScreenArea = vic2ScreenLayouts.GetLayout(LayoutType.VisibleNormalized, for24RowMode: false, for38ColMode: false);

        var characterMode = vic2.CharacterMode;

        // Clear screen
        //_gl.Enable(EnableCap.DepthTest);
        //_gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit));


        // Update shader uniform buffers that needs to be updated

        // Charset UBO
        if (_changedAllCharsetCodes)
        {
            var charsetData = BuildCharsetData(c64, fromROM: false);
            _uboCharsetData.Update(charsetData);
            _changedAllCharsetCodes = false;
        }
        // Text screen UBO
        var textScreenData = BuildTextScreenData(c64);
        _uboTextData.Update(textScreenData, 0);

        // Raster line data UBO
        var rasterLineData = new RasterLineData[c64.Vic2.Vic2Screen.VisibleHeight];
        foreach (var c64ScreenLine in c64.Vic2.ScreenLineBorderColor.Keys)
        {
            if (c64ScreenLine < visibileLayout.TopBorder.Start.Y || c64ScreenLine > visibileLayout.BottomBorder.End.Y)
                continue;
            var canvasYPos = (ushort)(c64ScreenLine - visibileLayout.TopBorder.Start.Y);
            var borderColor = c64.Vic2.ScreenLineBorderColor[c64ScreenLine];
            rasterLineData[canvasYPos].BorderColorCode = borderColor;
        }
        foreach (var c64ScreenLine in c64.Vic2.ScreenLineBackgroundColor.Keys)
        {
            if (c64ScreenLine < visibileLayout.Screen.Start.Y || c64ScreenLine > visibileLayout.Screen.End.Y)
                continue;
            var canvasYPos = (ushort)(c64ScreenLine - visibileLayout.TopBorder.Start.Y);
            var bgColor0 = c64.Vic2.ScreenLineBackgroundColor[c64ScreenLine];
            rasterLineData[canvasYPos].BackgroundColor0Code = bgColor0;
        }
        for (int i = 0; i < rasterLineData.Length; i++)
        {
            rasterLineData[i].BackgroundColor1Code = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_1);
            rasterLineData[i].BackgroundColor2Code = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_2);
            rasterLineData[i].BackgroundColor3Code = c64.ReadIOStorage(Vic2Addr.BACKGROUND_COLOR_3);

            rasterLineData[i].ColMode40 = vic2.Is38ColumnDisplayEnabled ? 0u : 1u;
            rasterLineData[i].RowMode25 = vic2.Is24RowDisplayEnabled ? 0u : 1u;
            rasterLineData[i].ScrollX = (uint)vic2.GetScrollX();
            rasterLineData[i].ScrollY = vic2.GetScrollY();
        }

        _uboRasterLineData.Update(rasterLineData, 0);

        // Setup shader for use in rendering
        _shader.Use();
        var windowSize = _silkNetOpenGlRenderContext.Window.Size;
        _shader.SetUniform("uWindowSize", new Vector3(windowSize.X, windowSize.Y, 0), skipExistCheck: true);

        float scaleX = (float)windowSize.X / vic2Screen.VisibleWidth;
        float scaleY = (float)windowSize.Y / vic2Screen.VisibleHeight;
        _shader.SetUniform("uScale", new Vector2(scaleX, scaleY), skipExistCheck: true);

        _shader.SetUniform("uTextScreenStart", new Vector2(visibleMainScreenArea.Screen.Start.X, visibleMainScreenArea.Screen.Start.Y));
        _shader.SetUniform("uTextScreenEnd", new Vector2(visibleMainScreenArea.Screen.End.X, visibleMainScreenArea.Screen.End.Y));

        _shader.SetUniform("uTextCharacterMode", (int)characterMode);

        // Draw triangles covering the entire screen, with the fragment shader doing the actual drawing of 2D pixels.
        _vba.Bind();
        _gl.DrawArrays(GLEnum.Triangles, 0, 6);
    }

    public void Draw(ISystem system, Dictionary<string, double> detailedStats)
    {
        Draw((C64)system, detailedStats);
    }

    private TextData[] BuildTextScreenData(C64 c64)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;

        var screenAddress = vic2.VideoMatrixBaseAddress;
        var colorAddress = Vic2Addr.COLOR_RAM_START;

        // 40 columns, 25 rows = 1024 items
        var textData = new TextData[vic2Screen.TextCols * vic2Screen.TextRows];
        for (int i = 0; i < textData.Length; i++)
        {
            textData[i].Character = vic2Mem[(ushort)(screenAddress + i)];
            textData[i].Color = c64.ReadIOStorage((ushort)(colorAddress + i));
        }
        return textData;
    }

    private CharsetData[] BuildCharsetData(C64 c64, bool fromROM)
    {
        var charsetManager = c64.Vic2.CharsetManager;
        // A character is defined by 8 bytes (1 line per byte), 256 total characters = 2048 items
        var charsetData = new CharsetData[Vic2CharsetManager.CHARACTERSET_SIZE];

        if (fromROM)
        {
            var charsets = c64.ROMData[C64Config.CHARGEN_ROM_NAME];
            for (int i = 0; i < charsetData.Length; i++)
            {
                charsetData[i].CharLine = charsets[(ushort)(i)];
            };
        }
        else
        {
            for (int i = 0; i < charsetData.Length; i++)
            {
                charsetData[i].CharLine = c64.Vic2.Vic2Mem[(ushort)(charsetManager.CharacterSetAddressInVIC2Bank + i)];
            };
        }

        return charsetData;
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
