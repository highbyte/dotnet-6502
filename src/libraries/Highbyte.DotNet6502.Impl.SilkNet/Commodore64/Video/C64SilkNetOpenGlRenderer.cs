using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Highbyte.DotNet6502.Impl.SilkNet.OpenGLHelpers;
using Highbyte.DotNet6502.Instrumentation;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Video;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2ScreenLayouts;

namespace Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video;

public class C64SilkNetOpenGlRenderer : IRenderer<C64, SilkNetOpenGlRenderContext>, IDisposable
{

    private SilkNetOpenGlRenderContext _silkNetOpenGlRenderContext = default!;
    private GL _gl => _silkNetOpenGlRenderContext.Gl;

    private bool _changedAllCharsetCodes = false;

    public Instrumentations Instrumentations { get; } = new();

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
    public struct BitmapData
    {
        //public fixed uint Lines[8];   // Note: Could not get uint array inside struct to work in UBO from .NET (even with C# unsafe fixed arrays).
        public uint Line0;
        public uint Line1;
        public uint Line2;
        public uint Line3;
        public uint Line4;
        public uint Line5;
        public uint Line6;
        public uint Line7;

        public uint BackgroundColorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte. From text screen ram low nybble.
        public uint ForegroundColorCode;   // C64 color value 0-15. uint = 4 bytes, only using 1 byte. From text screen ram high nybble.
        public uint ColorRAMColorCode;     // C64 color value 0-15. uint = 4 bytes, only using 1 byte. From color RAM (low nybble).
        public uint __;          // unused 

    }

    public struct ColorMapData
    {
        public uint ColorCode;  // uint = 4 bytes, only using 1 byte
        public uint __;         // unused
        public uint ___;        // unused
        public uint ____;       // unused
        public Vector4 Color;   // Vector4 = 16 bytes
    }
    public struct ScreenLineData
    {
        public uint BorderColorCode;        // uint = 4 bytes, only using 1 byte
        public uint BackgroundColor0Code;   // uint = 4 bytes, only using 1 byte
        public uint BackgroundColor1Code;   // uint = 4 bytes, only using 1 byte
        public uint BackgroundColor2Code;   // uint = 4 bytes, only using 1 byte

        public uint BackgroundColor3Code;   // uint = 4 bytes, only using 1 byte
        public uint SpriteMultiColor0;      // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint SpriteMultiColor1;      // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint _______;    // unused

        public uint Sprite0ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint Sprite1ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint Sprite2ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint Sprite3ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 

        public uint Sprite4ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint Sprite5ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint Sprite6ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 
        public uint Sprite7ColorCode;       // C64 color value 0-15. uint = 4 bytes, only using 1 byte. 

        public uint ColMode40;   // 0 = 38 col mode, 1 = 40 col mode
        public uint RowMode25;   // 0 = 24 row mode, 1 = 25 row mode
        public uint ScrollX;     // 0 to 7 horizontal fine scrolling (+1 in 38 col mode)
        public int ScrollY;      // -3 to 4 vertical fine scrolling (+1 in 24 row mode)
    }
    public struct SpriteData
    {
        public uint Visible;    // 0 = not visible, 1 = visible 
        public int X;           // int = 4 bytes, only using 2 bytes
        public int Y;           // int = 4 bytes, only using 2 bytes
        public uint Color;      // uint = 4 bytes, only using 1 byte

        public uint DoubleWidth; // 0 = Normal width, 1 = Double width
        public uint DoubleHeight;// 0 = Normal height, 1 = Double height
        public uint PriorityOverForeground; // 0 = No priority, 1 = Priority over foreground
        public uint MultiColor;  // 0 = Single color mode, 1 = MultiColor mode
    }
    public struct SpriteContentData
    {
        public uint Content;  // 3 bytes (24 pixels) per row * 21 rows = 63 items.
        public uint __;       // unused
        public uint ___;      // unused 
        public uint ____;     // unused  
    }

    private TextData[] _textScreenData;
    private CharsetData[] _charsetData;
    private BitmapData[] _bitmapData;

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
    private readonly C64SilkNetOpenGlRendererConfig _config;

    public C64SilkNetOpenGlRenderer(C64SilkNetOpenGlRendererConfig config)
    {
        _config = config;
    }

    public unsafe void Init(C64 c64, SilkNetOpenGlRenderContext silkNetOpenGlRenderContext)
    {
        _silkNetOpenGlRenderContext = silkNetOpenGlRenderContext;

        // Listen to event when the VIC2 charset address is changed to recreate a image for the charset
        c64.Vic2.CharsetManager.CharsetAddressChanged += (s, e) => CharsetChangedHandler(c64, e);

        // Init working memory for data sent to shader
        _textScreenData = new TextData[1000 + 24]; // Text screen is 1000 bytes. Pad to 1024 to align with 16 bytes
        _charsetData = new CharsetData[Vic2CharsetManager.CHARACTERSET_SIZE]; // Size of character set is 2048 (256 * 8 bytes per cha). No need for padding.
        _bitmapData = new BitmapData[(Vic2BitmapManager.BITMAP_SIZE / 8) + 24]; // 1000 "characters" as bitmap, pad to 1024 to align with 16 bytes

        // Hack for Silk.NET WASM experiment.
        _gl.Viewport(silkNetOpenGlRenderContext.Window.FramebufferSize);

        if (silkNetOpenGlRenderContext.Window.API.API == ContextAPI.OpenGL)
        {
#if DEBUG
            _gl.Enable(GLEnum.DebugOutput);
            _gl.Enable(GLEnum.DebugOutputSynchronous);
            _gl.DebugMessageCallback(OnDebug, null);
#endif
        }

        // Init openGL shaders
        InitShader(c64);
    }

    private void OnDebug
        (GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userparam)
    {
        var msg = $"{Marshal.PtrToStringAnsi(message)}";
        var logMsg = $"|{severity.ToString().Substring(13)}| {type.ToString().Substring(9)}/{id}: {msg}";

        Console.WriteLine(logMsg);
        Debug.WriteLine(logMsg);
    }

    private void InitShader(C64 c64)
    {
#if DEBUG
        _gl.GetInteger(GLEnum.MaxUniformBlockSize, out int maxUniformBlockSize); // 65536 (same in WebGL)
        _gl.GetInteger(GLEnum.MaxGeometryUniformComponents, out int maxGeometryUniformComponents); // 2048 (same in WebGL)
        _gl.GetInteger(GLEnum.MaxFragmentUniformComponents, out int maxFragmentUniformComponents); // 4096 (0 in WebGL)
        _gl.GetInteger(GLEnum.UniformBufferOffsetAlignment, out int uniformBufferOffsetAlignment); // 256 (same in WebGL)
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
        BuildTextScreenData(c64, ref _textScreenData);
        _uboTextData = new BufferObject<TextData>(_gl, _textScreenData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Define character set & create Uniform Buffer Object for fragment shader
        BuildCharsetData(c64, fromROM: true, ref _charsetData);
        _uboCharsetData = new BufferObject<CharsetData>(_gl, _charsetData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Define bitmap & create Uniform Buffer Object for fragment shader
        BuildBitmapData(c64, ref _bitmapData);
        _uboBitmapData = new BufferObject<BitmapData>(_gl, _bitmapData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Screen line data Uniform Buffer Object for fragment shader
        //var screenLineData = new ScreenLineData[c64.Vic2.Vic2Screen.VisibleHeight];
        var screenLineData = new ScreenLineData[312 + 8]; // 312 is maximum used by any VIC2 chip (PAL?).  Set to 320 to be aligned with 16 items..
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
        var spriteData = new SpriteData[Vic2SpriteManager.NUMBERS_OF_SPRITES + 8]; // 8 sprites. Add 8 to align with 16 items.
        _uboSpriteData = new BufferObject<SpriteData>(_gl, spriteData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Sprite Uniform Buffer Object for sprite content data (8 sprites * 3 bytes per row * 21 rows = 504 items) + 8 = 512 to align with 16 items
        const int spriteContentDataSize = (Vic2SpriteManager.NUMBERS_OF_SPRITES * (Vic2Sprite.DEFAULT_WIDTH / 8) * Vic2Sprite.DEFAULT_HEIGTH) + 8;
        var spriteContentData = new SpriteContentData[spriteContentDataSize];
        _uboSpriteContentData = new BufferObject<SpriteContentData>(_gl, spriteContentData, BufferTargetARB.UniformBuffer, BufferUsageARB.StaticDraw);

        // Init shader with:
        // - Vertex shader to draw triangles covering the entire screen.
        // - Fragment shader does the actual drawing of 2D pixels.
        var vertexShaderPath = _config.VertexShaderPath;
        var fragmentShaderPath = _config.FragmentShaderPath;
        Type? shaderEmbeddedResourceType = _config.ShaderEmbeddedResourceType;
        if (_config.UseTestShader)
        {
            fragmentShaderPath = _config.UseTestShader ? "Highbyte.DotNet6502.Impl.SilkNet.Commodore64.Video.TestShader.frag" : _config.FragmentShaderPath;
            shaderEmbeddedResourceType = typeof(C64SilkNetOpenGlRendererConfig);
        }
        if (shaderEmbeddedResourceType == null)
        {
            _shader = new OpenGLHelpers.Shader(_gl, vertexShaderPath: vertexShaderPath, fragmentShaderPath: fragmentShaderPath);
        }
        else
        {
            _shader = new OpenGLHelpers.Shader(_gl, shaderEmbeddedResourceType, vertexShaderPath: vertexShaderPath, fragmentShaderPath: fragmentShaderPath);
        }

        // Bind UBOs (used in fragment shader)

        if (!_config.UseTestShader)
        {
            uint i = 0;
            _shader.BindUBO("ubTextData", _uboTextData, binding_point_index: i++);
            _shader.BindUBO("ubCharsetData", _uboCharsetData, binding_point_index: i++);
            _shader.BindUBO("ubColorMap", _uboColorMapData, binding_point_index: i++);
            _shader.BindUBO("ubScreenLineData", _uboScreenLineData, binding_point_index: i++);
            _shader.BindUBO("ubSpriteData", _uboSpriteData, binding_point_index: i++);
            _shader.BindUBO("ubSpriteContentData", _uboSpriteContentData, binding_point_index: i++);
            _shader.BindUBO("ubBitmapData", _uboBitmapData, binding_point_index: i++);
        }
        else
        {
            uint i = 0;
            _shader.BindUBO("ubSpriteData", _uboSpriteData, binding_point_index: i++);
            _shader.BindUBO("ubTextData", _uboTextData, binding_point_index: i++);
            _shader.BindUBO("ubCharsetData", _uboCharsetData, binding_point_index: i++);
            _shader.BindUBO("ubColorMap", _uboColorMapData, binding_point_index: i++);
            _shader.BindUBO("ubScreenLineData", _uboScreenLineData, binding_point_index: i++);
            _shader.BindUBO("ubSpriteContentData", _uboSpriteContentData, binding_point_index: i++);
            //_shader.BindUBO("ubBitmapData", _uboBitmapData, binding_point_index: i++);
        }
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((C64)system, (SilkNetOpenGlRenderContext)renderContext);
    }

    public void Draw(C64 c64)
    {
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

        var displayMode = vic2.DisplayMode;
        var characterMode = vic2.CharacterMode;
        var bitmapMode = vic2.BitmapMode;

        // Clear screen
        //_gl.Enable(EnableCap.DepthTest);
        //_gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit));

        // Update shader uniform buffers that needs to be updated

        if (displayMode == Vic2.DispMode.Text)
        {
            // Charset dot-matrix UBO
            if (_changedAllCharsetCodes)
            {
                BuildCharsetData(c64, fromROM: false, ref _charsetData);
                _uboCharsetData.Update(_charsetData);
                _changedAllCharsetCodes = false;
            }
            // Text screen UBO
            BuildTextScreenData(c64, ref _textScreenData);
            _uboTextData.Update(_textScreenData, 0);
        }
        else if (displayMode == Vic2.DispMode.Bitmap)
        {
            // Bitmap dot-matrix UBO
            BuildBitmapData(c64, ref _bitmapData);
            _uboBitmapData.Update(_bitmapData, 0);

            // TODO: Bitmap Color UBO
        }

        // Screen line data UBO
        //var screenLineData = new ScreenLineData[c64.Vic2.Vic2Screen.VisibleHeight];
        var screenLineData = new ScreenLineData[312 + 8]; // 312 is maximum used by any VIC2 chip (PAL?).  Set to 320 to be aligned with 16 bytes.
        foreach (var c64ScreenLine in c64.Vic2.ScreenLineIORegisterValues.Keys)
        {
            if (c64ScreenLine < visibileLayout.TopBorder.Start.Y || c64ScreenLine > visibileLayout.BottomBorder.End.Y)
                continue;
            var canvasYPos = (ushort)(c64ScreenLine - visibileLayout.TopBorder.Start.Y);
            var screenLineIORegisters = c64.Vic2.ScreenLineIORegisterValues[c64ScreenLine];
            screenLineData[canvasYPos].BorderColorCode = screenLineIORegisters.BorderColor;
            screenLineData[canvasYPos].BackgroundColor0Code = screenLineIORegisters.BackgroundColor0;
            screenLineData[canvasYPos].BackgroundColor1Code = screenLineIORegisters.BackgroundColor1;
            screenLineData[canvasYPos].BackgroundColor2Code = screenLineIORegisters.BackgroundColor2;
            screenLineData[canvasYPos].SpriteMultiColor0 = screenLineIORegisters.SpriteMultiColor0;
            screenLineData[canvasYPos].SpriteMultiColor1 = screenLineIORegisters.SpriteMultiColor1;
            screenLineData[canvasYPos].Sprite0ColorCode = screenLineIORegisters.Sprite0Color;
            screenLineData[canvasYPos].Sprite1ColorCode = screenLineIORegisters.Sprite1Color;
            screenLineData[canvasYPos].Sprite2ColorCode = screenLineIORegisters.Sprite2Color;
            screenLineData[canvasYPos].Sprite3ColorCode = screenLineIORegisters.Sprite3Color;
            screenLineData[canvasYPos].Sprite4ColorCode = screenLineIORegisters.Sprite4Color;
            screenLineData[canvasYPos].Sprite5ColorCode = screenLineIORegisters.Sprite5Color;
            screenLineData[canvasYPos].Sprite6ColorCode = screenLineIORegisters.Sprite6Color;
            screenLineData[canvasYPos].Sprite7ColorCode = screenLineIORegisters.Sprite7Color;
            screenLineData[canvasYPos].ColMode40 = screenLineIORegisters.ColMode40 ? 1u : 0u;
            screenLineData[canvasYPos].RowMode25 = screenLineIORegisters.RowMode25 ? 1u : 0u;

            if (_config.UseFineScrollPerRasterLine)
            {
                screenLineData[canvasYPos].ScrollX = (uint)screenLineIORegisters.ScrollX;
                screenLineData[canvasYPos].ScrollY = screenLineIORegisters.ScrollY;
            }
            else
            {
                screenLineData[canvasYPos].ScrollX = (uint)vic2.GetScrollX();
                screenLineData[canvasYPos].ScrollY = vic2.GetScrollY();
            }
        }

        _uboScreenLineData.Update(screenLineData, 0);

        // Sprite meta data UBO
        var spriteData = new SpriteData[Vic2SpriteManager.NUMBERS_OF_SPRITES + 8];  // 8 sprites.Add 8 to align with 16 items.
        foreach (var sprite in c64.Vic2.SpriteManager.Sprites)
        {
            int si = sprite.SpriteNumber;
            spriteData[si].Visible = sprite.Visible ? 1u : 0u;
            spriteData[si].X = (sprite.X + visibleMainScreenArea.Screen.Start.X - Vic2SpriteManager.SCREEN_OFFSET_X);
            spriteData[si].Y = (sprite.Y + visibleMainScreenArea.Screen.Start.Y - Vic2SpriteManager.SCREEN_OFFSET_Y);
            spriteData[si].Color = (uint)sprite.Color;
            spriteData[si].DoubleWidth = sprite.DoubleWidth ? 1u : 0u;
            spriteData[si].DoubleHeight = sprite.DoubleHeight ? 1u : 0u;
            spriteData[si].PriorityOverForeground = sprite.PriorityOverForeground ? 1u : 0u;
            spriteData[si].MultiColor = sprite.Multicolor ? 1u : 0u;
        }
        _uboSpriteData.Update(spriteData, 0);

        // Sprite content UBO
        if (c64.Vic2.SpriteManager.Sprites.Any(s => s.IsDirty))
        {
            // TODO: Is best approach to upload all sprite content if any sprite is dirty? To minimize the number of separate UBO updates?
            const int spriteContentDataSize = (Vic2SpriteManager.NUMBERS_OF_SPRITES * (Vic2Sprite.DEFAULT_WIDTH / 8) * Vic2Sprite.DEFAULT_HEIGTH) + 8;
            var spriteContentData = new SpriteContentData[spriteContentDataSize];
            int uboIndex = 0;
            foreach (var sprite in c64.Vic2.SpriteManager.Sprites)
            {
                var spriteEmulatorData = sprite.Data;
                foreach (var row in sprite.Data.Rows)
                {
                    foreach (var rowByte in row.Bytes)
                    {
                        spriteContentData[uboIndex++] = new SpriteContentData()
                        {
                            Content = rowByte
                        };
                    }
                }
                if (sprite.IsDirty)
                    sprite.ClearDirty();
            }
            _uboSpriteContentData.Update(spriteContentData, 0);
        }

        // Setup shader for use in rendering
        _shader.Use();

        var windowSize = _silkNetOpenGlRenderContext.Window.Size;
        //var windowSize = _silkNetOpenGlRenderContext.Window.FramebufferSize; // TEST
        //var windowSize = new Silk.NET.Maths.Vector2D<int>(836, 470); // TEST - HACK FOR WASM Silk.NET EXPERIMENT

        _shader.SetUniform("uWindowSize", new Vector3(windowSize.X, windowSize.Y, 0), skipExistCheck: true);
        float scaleX = (float)windowSize.X / vic2Screen.VisibleWidth;
        float scaleY = (float)windowSize.Y / vic2Screen.VisibleHeight;
        _shader.SetUniform("uScale", new Vector2(scaleX, scaleY), skipExistCheck: true);

        _shader.SetUniform("uTextScreenStart", new Vector2(visibleMainScreenArea.Screen.Start.X, visibleMainScreenArea.Screen.Start.Y));
        _shader.SetUniform("uTextScreenEnd", new Vector2(visibleMainScreenArea.Screen.End.X, visibleMainScreenArea.Screen.End.Y));
        _shader.SetUniform("uDisplayMode", (int)displayMode);  // uDisplayMode: 0 = Text, 1 = Bitmap
        _shader.SetUniform("uBitmapMode", (int)bitmapMode);  // uBitmapMode: 0 = Standard, 1 = MultiColor
        _shader.SetUniform("uTextCharacterMode", (int)characterMode); // uTextCharacterMode: 0 = Standard, 1 = Extended, 2 = MultiColor

        // Draw triangles covering the entire screen, with the fragment shader doing the actual drawing of 2D pixels.
        _vba.Bind();
        _gl.DrawArrays(GLEnum.Triangles, 0, 6);
    }

    public void Draw(ISystem system)
    {
        Draw((C64)system);
    }

    private void BuildTextScreenData(C64 c64, ref TextData[] textScreenData)
    {
        var vic2 = c64.Vic2;
        var vic2Mem = vic2.Vic2Mem;
        var vic2Screen = vic2.Vic2Screen;

        var videoMatrixBaseAddress = vic2.VideoMatrixBaseAddress;
        var colorAddress = Vic2Addr.COLOR_RAM_START;
        var numberOfChars = vic2Screen.TextCols * vic2Screen.TextRows;
        for (int i = 0; i < numberOfChars; i++)
        {
            textScreenData[i].Character = vic2Mem[(ushort)(videoMatrixBaseAddress + i)];
            textScreenData[i].Color = c64.ReadIOStorage((ushort)(colorAddress + i));
        }
    }

    private void BuildCharsetData(C64 c64, bool fromROM, ref CharsetData[] charsetData)
    {
        var charsetManager = c64.Vic2.CharsetManager;
        // A character is defined by 8 bytes (1 line per byte), 256 total characters = 2048 items
        var numberOfChars = Vic2CharsetManager.CHARACTERSET_SIZE;
        if (fromROM)
        {
            var charsets = c64.ROMData[C64Config.CHARGEN_ROM_NAME];
            for (int i = 0; i < numberOfChars; i++)
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
    }

    private void CharsetChangedHandler(C64 c64, Vic2CharsetManager.CharsetAddressChangedEventArgs e)
    {
        if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetBaseAddress)
        {
            _changedAllCharsetCodes = true;
        }
        else if (e.ChangeType == Vic2CharsetManager.CharsetAddressChangedEventArgs.CharsetChangeType.CharacterSetCharacter && e.CharCode.HasValue)
        {
            // Updating individual characters in the UBO array probably take longer time than just updating the entire array.
            _changedAllCharsetCodes = true;
            //if (!_changedCharsetCodes.Contains(e.CharCode.Value))
            //    _changedCharsetCodes.Add(e.CharCode.Value);
        }
    }

    private void BuildBitmapData(C64 c64, ref BitmapData[] bitmapData)
    {
        var bitmapManager = c64.Vic2.BitmapManager;

        var vic2Mem = c64.Vic2.Vic2Mem;
        var videoMatrixBaseAddress = c64.Vic2.VideoMatrixBaseAddress;
        var colorAddress = Vic2Addr.COLOR_RAM_START;

        // 1000 (40x25) "chars", that each contains 8 bytes (lines) where each line is 8 pixels.
        const int numberOfChars = Vic2BitmapManager.BITMAP_SIZE / 8;
        for (int c = 0; c < numberOfChars; c++)
        {
            int charOffset = bitmapManager.BitmapAddressInVIC2Bank + (c * 8);
            for (int line = 0; line < 8; line++)
            {
                var charLine = vic2Mem[(ushort)(charOffset + line)];
                //_bitmapData[charPos].Lines[lineIndex] = charLine;
                switch (line)
                {
                    case 0:
                        bitmapData[c].Line0 = charLine; break;
                    case 1:
                        bitmapData[c].Line1 = charLine; break;
                    case 2:
                        bitmapData[c].Line2 = charLine; break;
                    case 3:
                        bitmapData[c].Line3 = charLine; break;
                    case 4:
                        bitmapData[c].Line4 = charLine; break;
                    case 5:
                        bitmapData[c].Line5 = charLine; break;
                    case 6:
                        bitmapData[c].Line6 = charLine; break;
                    case 7:
                        bitmapData[c].Line7 = charLine; break;
                }
            }

            bitmapData[c].BackgroundColorCode = (uint)(vic2Mem[(ushort)(videoMatrixBaseAddress + c)] & 0b00001111);
            bitmapData[c].ForegroundColorCode = (uint)(vic2Mem[(ushort)(videoMatrixBaseAddress + c)] & 0b11110000) >> 4;
            bitmapData[c].ColorRAMColorCode = c64.ReadIOStorage((ushort)(colorAddress + c));
        };
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
