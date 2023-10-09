using System.Reflection;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.Impl.Skia.Generic.Video;

public class GenericComputerSkiaRenderer : IRenderer<GenericComputer, SkiaRenderContext>
{
    private Func<SKCanvas> _getSkCanvas = default!;
    private SKPaintMaps _skPaintMaps = default!;

    private const int TextSize = 8;
    private const int TextPixelSize = TextSize;
    private const int BorderWidthFactor = 3;
    private const int BorderPixels = TextPixelSize * BorderWidthFactor;
    private readonly EmulatorScreenConfig _emulatorScreenConfig;

    public GenericComputerSkiaRenderer(EmulatorScreenConfig emulatorScreenConfig)
    {
        _emulatorScreenConfig = emulatorScreenConfig;
    }

    public void Init(GenericComputer genericComputer, SkiaRenderContext skiaRenderContext)
    {
        _getSkCanvas = skiaRenderContext.GetCanvas;

        var typeFace = LoadEmbeddedFont("C64_Pro_Mono-STYLE.ttf");
        _skPaintMaps = new SKPaintMaps(
            textSize: TextSize,
            typeFace: typeFace,
            SKPaintMaps.ColorMap
        );

        InitEmulatorScreenMemory(genericComputer);
    }

    public void Init(ISystem system, IRenderContext renderContext)
    {
        Init((GenericComputer)system, (SkiaRenderContext)renderContext);
    }

    public void Draw(GenericComputer genericComputer)
    {
        var mem = genericComputer.Mem;
        var canvas = _getSkCanvas();

        // Draw border
        var borderColor = mem[_emulatorScreenConfig.ScreenBorderColorAddress];
        var borderPaint = _skPaintMaps.GetSKBackgroundPaint(borderColor);
        canvas.DrawRect(0, 0, genericComputer.TextCols * TextPixelSize + BorderPixels * 2, genericComputer.TextRows * TextPixelSize + BorderPixels * 2, borderPaint);

        // Draw background
        using (new SKAutoCanvasRestore(canvas))
        {
            var bgColor = mem[_emulatorScreenConfig.ScreenBackgroundColorAddress];
            var bgPaint = _skPaintMaps.GetSKBackgroundPaint(bgColor);
            canvas.Translate(BorderPixels, BorderPixels);
            canvas.DrawRect(0, 0, genericComputer.TextCols * TextPixelSize, genericComputer.TextRows * TextPixelSize, bgPaint);
        }

        var screenMemoryAddress = _emulatorScreenConfig.ScreenStartAddress;
        var colorMemoryAddress = _emulatorScreenConfig.ScreenColorStartAddress;
        using (new SKAutoCanvasRestore(canvas))
        {
            canvas.Translate(BorderPixels, BorderPixels);
            // Draw characters
            for (var row = 0; row < genericComputer.TextRows; row++)
            {
                for (var col = 0; col < genericComputer.TextCols; col++)
                {
                    var chr = mem[(ushort)(screenMemoryAddress + row * genericComputer.TextCols + col)];
                    var chrColor = mem[(ushort)(colorMemoryAddress + row * genericComputer.TextCols + col)]; ;
                    var drawText = GetDrawTextFromCharacter(chr);
                    var textPaint = _skPaintMaps.GetSKTextPaint(chrColor);
                    DrawCharacter(canvas, drawText, col, row, textPaint);
                }
            }
        }
    }

    public void Draw(ISystem system)
    {
        Draw((GenericComputer)system);
    }

    private string GetDrawTextFromCharacter(byte chr)
    {
        string representAsString;
        switch (chr)
        {
            case 0x00:  // Uninitialized
            case 0x0a:  // NewLine/CarrigeReturn
            case 0x0d:  // NewLine/CarrigeReturn
                representAsString = " "; // Replace with space
                break;
            case 0xa0:  //160, C64 inverted space
            case 0xe0:  //224, Also C64 inverted space?
                // Unicode for Inverted square in https://style64.org/c64-truetype font
                representAsString = ((char)0x2588).ToString();
                break;
            default:
                // Even though both upper and lowercase characters are used in the 6502 program (and in the font), show all as uppercase for C64 look.
                representAsString = Convert.ToString((char)chr).ToUpper();
                break;
        }
        return representAsString;
    }

    private void DrawCharacter(SKCanvas canvas, string character, int col, int row, SKPaint textPaint)
    {
        //var textHeight = textPaint.TextSize;

        var x = col * TextPixelSize;
        var y = row * TextPixelSize;
        // Make clipping rectangle for the tile we're drawing, to avoid any accidental spill-over to neighboring tiles.
        var rect = new SKRect(x, y, x + TextPixelSize, y + TextPixelSize);
        using (new SKAutoCanvasRestore(canvas))
        {
            canvas.ClipRect(rect, SKClipOperation.Intersect);
            //canvas.DrawText(character, x, y + (TextPixelSize - 2), textPaint);
            canvas.DrawText(character, x, y + TextPixelSize, textPaint);
        }
    }

    // private async Task<SKTypeface> LoadFont(string fontUrl)
    // {
    //     using (Stream file = await HttpClient!.GetStreamAsync(fontUrl))
    //     using (var memoryStream = new MemoryStream())
    //     {
    //         await file.CopyToAsync(memoryStream);
    //         //byte[] bytes = memoryStream.ToArray();
    //         var typeFace = SKTypeface.FromStream(memoryStream);
    //         if (typeFace == null)
    //             throw new ArgumentException($"Cannot load font as a Skia TypeFace. Url: {fontUrl}", nameof(fontUrl));
    //         return typeFace;
    //     }
    // }

    private SKTypeface LoadEmbeddedFont(string fullFontName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var resourceName = $"{"Highbyte.DotNet6502.Impl.Skia.Resources.Fonts"}.{fullFontName}";
        using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
        {
            if (resourceStream == null)
                throw new ArgumentException($"Cannot load font from embedded resource. Resource: {resourceName}", nameof(fullFontName));

            var typeFace = SKTypeface.FromStream(resourceStream) ?? throw new ArgumentException($"Cannot load font as a Skia TypeFace from embedded resource. Resource: {resourceName}", nameof(fullFontName));
            return typeFace;
        }
    }

    /// <summary>
    /// Set emulator screen memory initial state
    /// </summary>
    private void InitEmulatorScreenMemory(GenericComputer system)
    {
        var emulatorMem = system.Mem;

        // Common bg and border color for entire screen, controlled by specific address
        emulatorMem[_emulatorScreenConfig.ScreenBorderColorAddress] = _emulatorScreenConfig.DefaultBorderColor;
        emulatorMem[_emulatorScreenConfig.ScreenBackgroundColorAddress] = _emulatorScreenConfig.DefaultBgColor;

        var currentScreenAddress = _emulatorScreenConfig.ScreenStartAddress;
        var currentColorAddress = _emulatorScreenConfig.ScreenColorStartAddress;
        for (var row = 0; row < _emulatorScreenConfig.Rows; row++)
        {
            for (var col = 0; col < _emulatorScreenConfig.Cols; col++)
            {
                emulatorMem[currentScreenAddress++] = 0x20;    // 32 (0x20) = space
                emulatorMem[currentColorAddress++] = _emulatorScreenConfig.DefaultFgColor;
            }
        }
    }
}
