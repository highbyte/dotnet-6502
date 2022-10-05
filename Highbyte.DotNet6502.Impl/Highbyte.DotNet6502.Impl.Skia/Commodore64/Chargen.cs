using SkiaSharp;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64
{
    public class Chargen
    {
        public SKImage GenerateChargenImage(GRContext grContext, string chargenFile, int charctersPerRow = 16)
        {
            var chargenBytes = File.ReadAllBytes(chargenFile);

            int rows = (chargenBytes.Length / 8) / charctersPerRow;

            using (var surface = SKSurface.Create(grContext, true, new SKImageInfo(charctersPerRow * 8, rows * 8)))
            {
                var canvas = surface.Canvas;

                var paint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = SKColors.White
                };

                int charCode = 0;
                int index = 0;
                int row = 0;
                int col = 0;
                while (index < chargenBytes.Length)
                {
                    // Loop 8 lines for one character
                    byte[] charcterLines = new byte[8];
                    for (int i = 0; i < 8; i++)
                    {
                        charcterLines[i] = chargenBytes[index++];
                    }
                    DrawOneCharacter(canvas, paint, charcterLines);
                    canvas.Translate(8, 0);
                    col++;
                    if (col == charctersPerRow)
                    {
                        col = 0;
                        row++;
                        canvas.Translate(- charctersPerRow * 8, 8);
                    }
                    charCode++;
                }

                var image = surface.Snapshot();
                return image;
            }
        }

        public void DumpChargenFileToImageFile(GRContext grContext, string chargenFile, string saveImageFile, int charctersPerRow = 16)
        {
            var image = GenerateChargenImage(grContext, chargenFile, charctersPerRow);
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
}
