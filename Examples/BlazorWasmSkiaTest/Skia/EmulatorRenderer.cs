using SkiaSharp;

namespace BlazorWasmSkiaTest.Skia
{
    public class EmulatorRenderer : IDisposable
    {
        private const int GameLoopInterval = 16;    // Number of milliseconds between each invokation of the main game loop
        private const int CellPixels = 16;          // Width & Height of a cell

        private int _screenWidth;
        private int _screenHeight;

        private readonly PeriodicAsyncTimer? _renderLoopTimer;
        private readonly SKTypeface _typeFace;

        public EmulatorRenderer(
            PeriodicAsyncTimer? renderLoopTimer, SKTypeface typeFace)
        {
            _renderLoopTimer = renderLoopTimer;
            _typeFace = typeFace;
            if (_renderLoopTimer != null)
            {
                _renderLoopTimer.IntervalMilliseconds = GameLoopInterval;
                _renderLoopTimer.Elapsed += GameLoopTimerElapsed;
                _renderLoopTimer.Start();
            }

            //_pixelMapper.ViewPortChanged += (s, e) => _imageCache.SetDirty(_gridLayerRenderers);
            //_pixelMapper.SetMapSize(40, 25, CellPixels);
        }

        private void GameLoopTimerElapsed(object? sender, EventArgs e) => GameLoopStep();
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public void GameLoopStep()
        {
            DoGameLoopStep();
        }
        private void DoGameLoopStep()
        {
        }

        public void SetSize(int width, int height)
        {
            _screenWidth = width;
            _screenHeight = height;
        }

        public (int Width, int Height) GetScreenSize() => (_screenWidth, _screenHeight);

        public void SetContext(GRContext context)
        {
        }

        public void Render(SKCanvas canvas)
        {
            //PixelMapper pixelMapper = _pixelMapper.Snapshot();

            using (new SKAutoCanvasRestore(canvas))
            {
                //RenderFrame(canvas, pixelMapper);
                RenderFrame(canvas);
            }
        }

        //private void RenderFrame(SKCanvas canvas, PixelMapper pixelMapper)
        private void RenderFrame(SKCanvas canvas)
        {
            int y = 0;
            DrawCharacter(canvas, "H", 0, y);
            DrawCharacter(canvas, "I", 1, y);
            DrawCharacter(canvas, "G", 2, y);
            DrawCharacter(canvas, "H", 3, y);

            const int fullBlockUnicode = 0x2588;
            string fullBlockString = ((char)fullBlockUnicode).ToString();

            y++;
            DrawCharacter(canvas, " ", 0, y);
            DrawCharacter(canvas, " ", 1, y);
            DrawCharacter(canvas, " ", 2, y);
            DrawCharacter(canvas, " ", 3, y);

            y++;
            DrawCharacter(canvas, fullBlockString, 0, y);
            DrawCharacter(canvas, fullBlockString, 1, y);
            DrawCharacter(canvas, fullBlockString, 2, y);
            DrawCharacter(canvas, fullBlockString, 3, y);

            y++;
            DrawCharacter(canvas, "B", 0, y);
            DrawCharacter(canvas, "Y", 1, y);
            DrawCharacter(canvas, "T", 2, y);
            DrawCharacter(canvas, "E", 3, y);

            y++;
            DrawCharacter(canvas, "A", 0, y);
            DrawCharacter(canvas, "B", 1, y);
            DrawCharacter(canvas, "C", 2, y);
            DrawCharacter(canvas, "D", 3, y);
        }

        private void DrawCharacter(SKCanvas canvas, string character, int col, int row)
        {
            SKPaint textPaint;
            SKPaint backgroundPaint;

            if ((col % 2 == 0 && row % 2 == 0) || (col % 2 == 1 && row % 2 == 1))
            {
                textPaint = s_yellowTextPaint;
                backgroundPaint = s_darkBlueBGPaint;
            }
            else
            {
                textPaint = s_darkBlueTextPaint;
                backgroundPaint = s_yellowBGPaint;
            }

            DrawCharacter(canvas, character, col, row, textPaint, backgroundPaint);
        }
        private void DrawCharacter(SKCanvas canvas, string character, int col, int row, SKPaint textPaint, SKPaint backgroundPaint)
        {
            //var textHeight = textPaint.TextSize;

            textPaint.Typeface = _typeFace;

            var x = col * CellPixels;
            var y = row * CellPixels;
            // Make clipping rectangle for the tile we're drawing, to avoid any accidental spill-over to neighboring tiles.
            var rect = new SKRect(x, y, x + CellPixels, y + CellPixels);
            using (new SKAutoCanvasRestore(canvas))
            {
                canvas.ClipRect(rect, SKClipOperation.Intersect);
                canvas.DrawRect(rect, backgroundPaint);
                canvas.DrawText(character, x, y + (CellPixels - 2), textPaint);
            }
        }

        private static readonly SKPaint s_yellowTextPaint = new()
        {
            TextSize = 16,
            //IsAntialias = true,
            Color = SKColors.Yellow,
            TextAlign = SKTextAlign.Left,
        };

        private static readonly SKPaint s_darkBlueTextPaint = new()
        {
            TextSize = 16,
            //IsAntialias = true,
            Color = SKColors.DarkBlue,
            TextAlign = SKTextAlign.Left,
        };

        private static readonly SKPaint s_darkBlueBGPaint = new()
        {
            Color = SKColors.DarkBlue,
            Style = SKPaintStyle.Fill
        };

        private static readonly SKPaint s_yellowBGPaint = new()
        {
            Color = SKColors.Yellow,
            Style = SKPaintStyle.Fill
        };

        public void Dispose()
        {
        }
    }
}
