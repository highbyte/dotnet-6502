using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Impl.Skia.Commodore64.Video;

public class SpriteGen
{
    public static SKColor SpriteImageDrawColor = SKColors.White;
    private readonly C64SkiaPaint _c64SkiaPaint;
    private readonly Vic2 _vic2;

    public SpriteGen(C64SkiaPaint c64SkiaPaint, Vic2 vic2)
    {
        _c64SkiaPaint = c64SkiaPaint;
        _vic2 = vic2;
    }

    public SKImage GenerateSpriteImage(Vic2Sprite sprite)
    {
        using (var surface = SKSurface.Create(new SKImageInfo(Vic2Sprite.DEFAULT_WIDTH, Vic2Sprite.DEFAULT_HEIGTH)))
        {
            var canvas = surface.Canvas;

            if (sprite.Multicolor)
            {
                // Note: The multi-color sprites does not currently have similar optimization as single-color sprites.
                //       The actual image with with 3 colors (the sprite-specific one, and two common multi-color ones) is generated here, and should then be copied directly to screen.
                //       If any of the used colors are changed, the image must be generated again by setting the Sprite Dirty flag (which is different from single-color ones due to optization).
                var spritePaint = _c64SkiaPaint.GetFillPaint(sprite.Color);
                var multiColorPaint0 = _c64SkiaPaint.GetFillPaint(_vic2.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_0));
                var multiColorPaint1 = _c64SkiaPaint.GetFillPaint(_vic2.ReadIOStorage(Vic2Addr.SPRITE_MULTI_COLOR_1));

                foreach (var spriteRow in sprite.Data.Rows)
                {
                    DrawMultiColorSpriteLine(canvas, spritePaint, multiColorPaint0, multiColorPaint1, spriteRow.Bytes);
                    canvas.Translate(0, 1);
                }
            }
            else
            {
                // Note: As optimization, single color sprits (aka hi-res sprites) are pre-generated with drawing the image in one hard-coded color, which is then has
                //       its color transformed when being drawn on screen.
                var spritePaint = new SKPaint
                {
                    Style = SKPaintStyle.StrokeAndFill,
                    Color = SpriteImageDrawColor,
                    StrokeWidth = 1
                };
                foreach (var spriteRow in sprite.Data.Rows)
                {
                    DrawSpriteLine(canvas, spritePaint, spriteRow.Bytes);
                    canvas.Translate(0, 1);
                }
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

    private void DrawSpriteLine(SKCanvas canvas, SKPaint spritePaint, byte[] lineData)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            foreach (var linePart in lineData)
            {
                DrawSpriteLinePart(canvas, spritePaint, linePart);
                canvas.Translate(8, 0);
            }
        }
    }

    private void DrawSpriteLinePart(SKCanvas canvas, SKPaint spritePaint, byte linePart)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            var mask = 0b10000000;
            for (var pixel = 0; pixel < 8; pixel++)
            {
                var pixelSet = (linePart & mask) == mask;
                if (pixelSet)
                    canvas.DrawRect(0, 0, 1, 0, spritePaint);
                mask = mask >> 1;
                canvas.Translate(1, 0);
            }
        }
    }


    private void DrawMultiColorSpriteLine(SKCanvas canvas, SKPaint spritePaint, SKPaint multiColorPaint0, SKPaint multiColorPaint1, byte[] lineData)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            foreach (var linePart in lineData)
            {
                DrawMultiColorSpriteLinePart(canvas, spritePaint, multiColorPaint0, multiColorPaint1, linePart);
                canvas.Translate(8, 0);
            }
        }
    }

    private void DrawMultiColorSpriteLinePart(SKCanvas canvas, SKPaint spritePaint, SKPaint multiColorPaint0, SKPaint multiColorPaint1, byte linePart)
    {
        using (new SKAutoCanvasRestore(canvas))
        {
            var maskMultiColor0 = 0b01000000;
            var maskMultiColor1 = 0b11000000;
            var maskSpriteColor = 0b10000000;
            SKPaint? paint;
            for (var pixel = 0; pixel < 8; pixel += 2)
            {
                paint = linePart switch
                {
                    var p when (p & maskMultiColor1) == maskMultiColor1 => multiColorPaint1,
                    var p when (p & maskMultiColor0) == maskMultiColor0 => multiColorPaint0,
                    var p when (p & maskSpriteColor) == maskSpriteColor => spritePaint,
                    _ => null
                };

                if (paint != null)
                    canvas.DrawRect(0, 0, 2, 1, paint);
                maskMultiColor0 = maskMultiColor0 >> 2;
                maskMultiColor1 = maskMultiColor1 >> 2;
                maskSpriteColor = maskSpriteColor >> 2;
                canvas.Translate(2, 0);
            }
        }
    }
}
