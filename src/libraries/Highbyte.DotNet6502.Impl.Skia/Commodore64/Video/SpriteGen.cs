using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class SpriteGen
{
    public static SKColor SpriteImageDrawColor = SKColors.White;

    public SKImage GenerateSpriteImage(GRContext grContext, Vic2Sprite sprite)
    {
        using (var surface = SKSurface.Create(grContext, true, new SKImageInfo(Vic2Sprite.DEFAULT_WIDTH, Vic2Sprite.DEFAULT_HEIGTH)))
        {
            var canvas = surface.Canvas;

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SpriteImageDrawColor
            };

            foreach (var spriteRow in sprite.Data.Rows)
            {
                DrawSpriteLine(canvas, paint, spriteRow.Bytes);
                canvas.Translate(0, 1);
            }

            var image = surface.Snapshot();
            return image;
        }
    }

    public void DumpSpriteToImageFile(SKImage image, string saveImageFile)
    {
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.OpenWrite(Path.Combine(Environment.CurrentDirectory, saveImageFile)))
        {
            data.SaveTo(stream);
        }
    }

    private void DrawSpriteLine(SKCanvas canvas, SKPaint paint, byte[] lineData)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            foreach (var linePart in lineData)
            {
                DrawSpriteLinePart(canvas, paint, linePart);
                canvas.Translate(8, 0);
            }
        }
    }

    private void DrawSpriteLinePart(SKCanvas canvas, SKPaint paint, byte linePart)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            var mask = 0b10000000;
            for (var pixel = 0; pixel < 8; pixel++)
            {
                var pixelSet = (linePart & mask) == mask;
                if (pixelSet)
                    canvas.DrawRect(0, 0, 1, 0, paint);
                mask = mask >> 1;
                canvas.Translate(1, 0);
            }
        }
    }
}
