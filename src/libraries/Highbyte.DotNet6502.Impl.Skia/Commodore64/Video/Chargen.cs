using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class CharGen
{
    public static SKColor CharacterImageDrawColor = SKColors.White;
    public static SKColor CharacterImageDrawMultiColorBG1 = SKColors.Blue;
    public static SKColor CharacterImageDrawMultiColorBG2 = SKColors.Red;

    private static readonly SKPaint s_paint;
    private static readonly SKPaint s_paintMultiColorBG1;
    private static readonly SKPaint s_paintMultiColorBG2;

    static CharGen()
    {
        s_paint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = CharacterImageDrawColor,
            StrokeWidth = 1
        };
        s_paintMultiColorBG1 = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = CharacterImageDrawMultiColorBG1,
            StrokeWidth = 1
        };
        s_paintMultiColorBG2 = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            Color = CharacterImageDrawMultiColorBG2,
            StrokeWidth = 1
        };
    }

    public Dictionary<int, SKImage> GenerateChargenImages(byte[] characterSet, bool multiColor = false)
    {
        if (characterSet.Length != Vic2CharsetManager.CHARACTERSET_SIZE)
            throw new ArgumentException($"Character set size must be {Vic2CharsetManager.CHARACTERSET_SIZE} bytes.", nameof(characterSet));

        Dictionary<int, SKImage> images = new();
        for (int charCode = 0; charCode < Vic2CharsetManager.CHARACTERSET_NUMBER_OF_CHARCTERS; charCode++)
        {
            var image = GenerateChargenImageForOneCharacter(characterSet, charCode, multiColor);
            images.Add(charCode, image);
        }
        return images;
    }

    public SKImage GenerateChargenImageForOneCharacter(byte[] characterSet, int charCode, bool multiColor = false)
    {
        using (var surface = SKSurface.Create(new SKImageInfo(8, 8)))
        {
            var canvas = surface.Canvas;

            var characterLines = new byte[8];
            for (int line = 0; line < 8; line++)
                characterLines[line] = characterSet[(charCode * 8) + line];

            DrawOneCharacter(canvas, s_paint, s_paintMultiColorBG1, s_paintMultiColorBG2, characterLines, multiColor);

            var image = surface.Snapshot();
            return image;
        }
    }

    public SKImage GenerateChargenImageTotal(byte[] characterSet, int charactersPerRow = 16, bool multiColor = false)
    {
        if (characterSet.Length != Vic2CharsetManager.CHARACTERSET_SIZE)
            throw new ArgumentException($"Character set size must be {Vic2CharsetManager.CHARACTERSET_SIZE} bytes.", nameof(characterSet));

        var rows = characterSet.Length / Vic2CharsetManager.CHARACTERSET_ONE_CHARACTER_BYTES / charactersPerRow;    // Each character is defined by 8 bytes in the character set.

        using (var surface = SKSurface.Create(new SKImageInfo(charactersPerRow * 8, rows * 8)))
        {
            var canvas = surface.Canvas;

            var charCode = 0;
            var index = 0;
            var row = 0;
            var col = 0;
            while (index < characterSet.Length)
            {
                // Loop 8 lines for one character
                var characterLines = new byte[8];
                for (var i = 0; i < 8; i++)
                {
                    characterLines[i] = characterSet[index++];
                }
                DrawOneCharacter(canvas, s_paint, s_paintMultiColorBG1, s_paintMultiColorBG2, characterLines, multiColor);
                canvas.Translate(8, 0);
                col++;
                if (col == charactersPerRow)
                {
                    col = 0;
                    row++;
                    canvas.Translate(-charactersPerRow * 8, 8);
                }
                charCode++;
            }

            var image = surface.Snapshot();
            return image;
        }
    }

    private void DrawOneCharacter(SKCanvas canvas, SKPaint paint, SKPaint paintMultiColorBG1, SKPaint paintMultiColorBG2, byte[] dataRows, bool multiColor)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            if (dataRows.Length != 8)
                throw new Exception("A character in chargen must consist of 8 bytes.");
            foreach (var row in dataRows)
            {
                DrawCharacterLine(canvas, paint, paintMultiColorBG1, paintMultiColorBG2, row, multiColor);
                canvas.Translate(0, 1);
            }
        }
    }

    private void DrawCharacterLine(SKCanvas canvas, SKPaint paint, SKPaint paintMultiColorBG1, SKPaint paintMultiColorBG2, byte dataRow, bool multiColor)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            if (multiColor)
            {
                var mask = 0b11000000;
                for (var pixel = 0; pixel < 4; pixel++)
                {
                    var pixelPair = (dataRow & mask) >> (6 - pixel * 2);
                    if (pixelPair != 0)
                    {
                        var pixelPairPaint = pixelPair switch
                        {
                            0b01 => paintMultiColorBG1,
                            0b10 => paintMultiColorBG2,
                            0b11 => paint,
                            _ => throw new Exception("Invalid pixel pair value.")
                        };
                        canvas.DrawRect(0, 0, 2, 0, pixelPairPaint);
                    }
                    mask = mask >> 2;
                    canvas.Translate(2, 0);
                }
            }
            else
            {
                var mask = 0b10000000;
                for (var pixel = 0; pixel < 8; pixel++)
                {
                    var pixelSet = (dataRow & mask) == mask;
                    if (pixelSet)
                        canvas.DrawRect(0, 0, 1, 0, paint);
                    mask = mask >> 1;
                    canvas.Translate(1, 0);
                }
            }
        }
    }

    public void DumpChargenImagesToOneFile(Dictionary<int, SKImage> images, string saveImageFile, int charactersPerRow = 16)
    {
        SKImage totalImage = BuildTotalImage(images, charactersPerRow);
        DumpImageToFile(totalImage, saveImageFile);
    }

    private SKImage BuildTotalImage(Dictionary<int, SKImage> images, int charactersPerRow)
    {
        var rows = images.Keys.Count / charactersPerRow;

        using (var surface = SKSurface.Create(new SKImageInfo(charactersPerRow * 8, rows * 8)))
        {
            var canvas = surface.Canvas;

            int col = 0;
            int row = 0;
            // Loop every totalImage in images
            foreach (var charCode in images.Keys.OrderBy(x => x))
            {
                var image = images[charCode];
                canvas.DrawImage(image, 0, 0);
                canvas.Translate(8, 0);

                col++;
                if (col == charactersPerRow)
                {
                    col = 0;
                    row++;
                    canvas.Translate(-charactersPerRow * 8, 8);
                }
            }
            var totalImage = surface.Snapshot();
            return totalImage;
        }
    }

    public void DumpChargenFileToImageFile(byte[] characterSet, string saveImageFile, bool multiColor, int charactersPerRow = 16)
    {
        var totalImage = GenerateChargenImageTotal(characterSet, charactersPerRow, multiColor);
        DumpImageToFile(totalImage, saveImageFile);
    }

    public void DumpImageToFile(SKImage image, string saveImageFile)
    {
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(Path.Combine(Environment.CurrentDirectory, saveImageFile)))
        {
            data.SaveTo(stream);
        }
    }

}
