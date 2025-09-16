//using System.Reflection;
//using Highbyte.DotNet6502.Systems;
//using Highbyte.DotNet6502.Systems.Generic;
//using Highbyte.DotNet6502.Systems.Generic.Config;
//using Highbyte.DotNet6502.Systems.Instrumentation;

//namespace Highbyte.DotNet6502.Impl.Skia.Generic.Video;

//// TODO: Remove?  Is this completely replace in new render pipeline with a VideoCommand render target + custom adjustment?
//public class GenericComputerSkiaRenderer_LEGACY
//{
//    private readonly GenericComputer _genericComputer;
//    public ISystem System => _genericComputer;
//    private readonly SkiaRenderContext _skiaRenderContext;
//    private Func<SKCanvas> _getSkCanvas = default!;
//    private SKPaintMaps _skPaintMaps = default!;

//    private const int TextSize = 8;
//    private const int TextPixelSize = TextSize;
//    private const int BorderWidthFactor = 3;
//    private const int BorderPixels = TextPixelSize * BorderWidthFactor;
//    private readonly EmulatorScreenConfig _emulatorScreenConfig;

//    public Instrumentations Instrumentations { get; } = new();

//    public GenericComputerSkiaRenderer_LEGACY(GenericComputer genericComputer, SkiaRenderContext skiaRenderContext)
//    {
//        _genericComputer = genericComputer;
//        _skiaRenderContext = skiaRenderContext;
//        _emulatorScreenConfig = genericComputer.GenericComputerConfig.Memory.Screen;
//    }

//    public void Init()
//    {
//        _getSkCanvas = _skiaRenderContext.GetCanvas;

//        var typeFace = LoadEmbeddedFont("C64_Pro_Mono-STYLE.ttf");
//        _skPaintMaps = new SKPaintMaps(
//            textSize: TextSize,
//            typeFace: typeFace,
//            SKPaintMaps.ColorMap
//        );
//    }

//    public void Cleanup()
//    {
//    }

//    public void GenerateFrame()
//    {
//        // All drawing is done in DrawFrame with commands against Skia Canvas, so nothing to do here.
//    }

//    public void DrawFrame()
//    {
//        var mem = _genericComputer.Mem;
//        var canvas = _getSkCanvas();

//        // Draw border
//        var borderColor = mem[_emulatorScreenConfig.ScreenBorderColorAddress];
//        var borderPaint = _skPaintMaps.GetSKBackgroundPaint(borderColor);
//        canvas.DrawRect(0, 0, _genericComputer.TextCols * TextPixelSize + BorderPixels * 2, _genericComputer.TextRows * TextPixelSize + BorderPixels * 2, borderPaint);

//        // Draw background
//        using (new SKAutoCanvasRestore(canvas))
//        {
//            var bgColor = mem[_emulatorScreenConfig.ScreenBackgroundColorAddress];
//            var bgPaint = _skPaintMaps.GetSKBackgroundPaint(bgColor);
//            canvas.Translate(BorderPixels, BorderPixels);
//            canvas.DrawRect(0, 0, _genericComputer.TextCols * TextPixelSize, _genericComputer.TextRows * TextPixelSize, bgPaint);
//        }

//        var screenMemoryAddress = _emulatorScreenConfig.ScreenStartAddress;
//        var colorMemoryAddress = _emulatorScreenConfig.ScreenColorStartAddress;
//        using (new SKAutoCanvasRestore(canvas))
//        {
//            canvas.Translate(BorderPixels, BorderPixels);
//            // Draw characters
//            for (var row = 0; row < _genericComputer.TextRows; row++)
//            {
//                for (var col = 0; col < _genericComputer.TextCols; col++)
//                {
//                    var chr = mem[(ushort)(screenMemoryAddress + row * _genericComputer.TextCols + col)];
//                    var chrColor = mem[(ushort)(colorMemoryAddress + row * _genericComputer.TextCols + col)]; ;
//                    var drawText = GetDrawTextFromCharacter(chr);
//                    var textPaint = _skPaintMaps.GetSKTextPaint(chrColor);
//                    DrawCharacter(canvas, drawText, col, row, textPaint);
//                }
//            }
//        }

//        if (_skiaRenderContext.Flush)
//            canvas.Flush();
//    }

//    private string GetDrawTextFromCharacter(byte chr)
//    {
//        string representAsString;
//        switch (chr)
//        {
//            case 0x00:  // Uninitialized
//            case 0x0a:  // NewLine/CarrigeReturn
//            case 0x0d:  // NewLine/CarrigeReturn
//                representAsString = " "; // Replace with space
//                break;
//            case 0xa0:  //160, C64 inverted space
//            case 0xe0:  //224, Also C64 inverted space?
//                // Unicode for Inverted square in https://style64.org/c64-truetype font
//                representAsString = ((char)0x2588).ToString();
//                break;
//            default:
//                // Even though both upper and lowercase characters are used in the 6502 program (and in the font), show all as uppercase for C64 look.
//                representAsString = Convert.ToString((char)chr).ToUpper();
//                break;
//        }
//        return representAsString;
//    }

//    private void DrawCharacter(SKCanvas canvas, string character, int col, int row, SKPaint textPaint)
//    {
//        //var textHeight = textPaint.TextSize;

//        var x = col * TextPixelSize;
//        var y = row * TextPixelSize;
//        // Make clipping rectangle for the tile we're drawing, to avoid any accidental spill-over to neighboring tiles.
//        var rect = new SKRect(x, y, x + TextPixelSize, y + TextPixelSize);
//        using (new SKAutoCanvasRestore(canvas))
//        {
//            canvas.ClipRect(rect, SKClipOperation.Intersect);
//            //canvas.DrawText(character, x, y + (TextPixelSize - 2), textPaint);
//            canvas.DrawText(character, x, y + TextPixelSize, textPaint);
//        }
//    }

//    // private async Task<SKTypeface> LoadFont(string fontUrl)
//    // {
//    //     using (Stream file = await HttpClient!.GetStreamAsync(fontUrl))
//    //     using (var memoryStream = new MemoryStream())
//    //     {
//    //         await file.CopyToAsync(memoryStream);
//    //         //byte[] bytes = memoryStream.ToArray();
//    //         var typeFace = SKTypeface.FromStream(memoryStream);
//    //         if (typeFace == null)
//    //             throw new ArgumentException($"Cannot load font as a Skia TypeFace. Url: {fontUrl}", nameof(fontUrl));
//    //         return typeFace;
//    //     }
//    // }

//    private SKTypeface LoadEmbeddedFont(string fullFontName)
//    {
//        var assembly = Assembly.GetExecutingAssembly();

//        var resourceName = $"{"Highbyte.DotNet6502.Impl.Skia.Resources.Fonts"}.{fullFontName}";
//        using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
//        {
//            if (resourceStream == null)
//                throw new ArgumentException($"Cannot load font from embedded resource. Resource: {resourceName}", nameof(fullFontName));

//            var typeFace = SKTypeface.FromStream(resourceStream) ?? throw new ArgumentException($"Cannot load font as a Skia TypeFace from embedded resource. Resource: {resourceName}", nameof(fullFontName));
//            return typeFace;
//        }
//    }
//}
