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

        public EmulatorRenderer(
            PeriodicAsyncTimer? renderLoopTimer)
        {
            _renderLoopTimer = renderLoopTimer;
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
            DrawCharacter(canvas, "H", 0, 0, s_yellowTextPaint, s_darkBlueBGPaint);
            DrawCharacter(canvas, "I", 1, 0, s_yellowTextPaint, s_darkBlueBGPaint);
            DrawCharacter(canvas, "G", 0, 1, s_yellowTextPaint, s_darkBlueBGPaint);
            DrawCharacter(canvas, "H", 1, 1, s_yellowTextPaint, s_darkBlueBGPaint);
        }

        private void DrawCharacter(SKCanvas canvas, string character, int col, int row, SKPaint textPaint, SKPaint backgroundPaint)
        {
            //var textHeight = textPaint.TextSize;

            var x = col * CellPixels;
            var y = row * CellPixels;
            // Make clipping rectangle for the tile we're drawing, to avoid any accidental spill-over to neighboring tiles.
            var rect = new SKRect(x, y, x + CellPixels, y + CellPixels);
            using (new SKAutoCanvasRestore(canvas))
            {
                canvas.ClipRect(rect, SKClipOperation.Intersect);
                canvas.DrawRect(rect, backgroundPaint);
                canvas.DrawText(character, x + (CellPixels / 2), y + (CellPixels - 2), textPaint);
            }
        }

        private static readonly SKPaint s_yellowTextPaint = new()
        {
            TextSize = 16,
            IsAntialias = true,
            Color = SKColors.Yellow,
            TextAlign = SKTextAlign.Center,
        };

        private static readonly SKPaint s_darkBlueBGPaint = new()
        {
            Color = SKColors.DarkBlue,
            Style = SKPaintStyle.Fill
        };

        public void Dispose()
        {
        }
    }
}
