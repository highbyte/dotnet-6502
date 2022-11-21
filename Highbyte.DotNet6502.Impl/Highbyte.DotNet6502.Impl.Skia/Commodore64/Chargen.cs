using Highbyte.DotNet6502.Systems.Commodore64.Video;
using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64;

public class Chargen
{
    public static SKColor CharacterImageDrawColor = SKColors.White;
    public SKImage GenerateChargenImage(GRContext grContext, byte[] characterSet, int charactersPerRow = 16)
    {
        if (characterSet.Length != Vic2.CHARACTERSET_SIZE)
            throw new ArgumentException($"Character set size must be {Vic2.CHARACTERSET_SIZE} bytes.", nameof(characterSet));

        int rows = (characterSet.Length / Vic2.CHARACTERSET_ONE_CHARACTER_BYTES) / charactersPerRow;    // Each character is defined by 8 bytes in the character set.

        using (var surface = SKSurface.Create(grContext, true, new SKImageInfo(charactersPerRow * 8, rows * 8)))
        {
            var canvas = surface.Canvas;

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = CharacterImageDrawColor
            };

            int charCode = 0;
            int index = 0;
            int row = 0;
            int col = 0;
            while (index < characterSet.Length)
            {
                // Loop 8 lines for one character
                byte[] charcterLines = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    charcterLines[i] = characterSet[index++];
                }
                DrawOneCharacter(canvas, paint, charcterLines);
                canvas.Translate(8, 0);
                col++;
                if (col == charactersPerRow)
                {
                    col = 0;
                    row++;
                    canvas.Translate(- charactersPerRow * 8, 8);
                }
                charCode++;
            }

            var image = surface.Snapshot();
            return image;
        }
    }

    public void DumpChargenFileToImageFile(GRContext grContext, byte[] characterSet,  string saveImageFile, int charactersPerRow = 16)
    {
        var image = GenerateChargenImage(grContext, characterSet, charactersPerRow);
        DumpChargenFileToImageFile(image, saveImageFile);
    }

    public void DumpChargenFileToImageFile(SKImage image, string saveImageFile)
    {
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(Path.Combine(System.Environment.CurrentDirectory, saveImageFile)))
        {
            data.SaveTo(stream);
        }
    }

    private void DrawOneCharacter(SKCanvas canvas, SKPaint paint, byte[] dataRows)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            if (dataRows.Length != 8)
                throw new Exception("A character in chargen must consist of 8 bytes.");
            foreach (var row in dataRows)
            {
                DrawCharacterLine(canvas, paint, row);
                canvas.Translate(0, 1);
            }
        }
    }

    private void DrawCharacterLine(SKCanvas canvas, SKPaint paint, byte dataRow)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            int mask = 0b10000000;
            for (int pixel = 0; pixel < 8; pixel++)
            {
                bool pixelSet = (dataRow & mask) == mask;
                if (pixelSet)
                {
                    canvas.DrawRect(0, 0, 1, 0, paint);
                }
                mask = mask >> 1;
                canvas.Translate(1, 0);
            }
        }
    }
}
