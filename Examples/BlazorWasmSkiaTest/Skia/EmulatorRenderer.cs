using BlazorWasmSkiaTest.Helpers;
using SkiaSharp;

namespace BlazorWasmSkiaTest.Skia
{
    public class EmulatorRenderer : IDisposable
    {
        private const int GameLoopInterval = 16;    // Number of milliseconds between each invokation of the main game loop
        private const int BorderWidthFactor = 3;    // The factor applied to _textSizePixels to get the width and height of the border

        private readonly int _textSizePixels;       // Size of each charatcter. Font assumed to be monospaced.
        private readonly int _borderPixels;         // Number of pixels used for border on top/bottom/left/right

        private int _screenWidth;
        private int _screenHeight;

        private readonly PeriodicAsyncTimer? _renderLoopTimer;
        private readonly SKPaintMaps _sKPaintMaps;
        private readonly EmulatorHelper _emulatorHelper;

        public EmulatorRenderer(
            PeriodicAsyncTimer? renderLoopTimer,
            SKPaintMaps sKPaintMaps,
            int textSize,
            EmulatorHelper emulatorHelper)
        {
            _renderLoopTimer = renderLoopTimer;
            _sKPaintMaps = sKPaintMaps;
            _emulatorHelper = emulatorHelper;

            _textSizePixels = textSize;
            _borderPixels = textSize * BorderWidthFactor;

            if (_renderLoopTimer != null)
            {
                _renderLoopTimer.IntervalMilliseconds = GameLoopInterval;
                _renderLoopTimer.Elapsed += GameLoopTimerElapsed;
                _renderLoopTimer.Start();
            }
        }

        private void GameLoopTimerElapsed(object? sender, EventArgs e) => GameLoopStep();

        public void SetSize(int width, int height)
        {
            _screenWidth = width;
            _screenHeight = height;
        }

        public (int Width, int Height) GetScreenSize() => (_screenWidth, _screenHeight);

        public void SetContext(GRContext context)
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public void GameLoopStep()
        {
            DoGameLoopStep();
        }
        private void DoGameLoopStep()
        {
            _emulatorHelper.GenerateRandomNumber();
            _emulatorHelper.ExecuteEmulator();
        }

        public void Render(SKCanvas canvas)
        {
            using (new SKAutoCanvasRestore(canvas))
            {
                RenderFrame(canvas);
            }
        }

        private void RenderFrame(SKCanvas canvas)
        {
            // Draw border
            var borderColor = _emulatorHelper.GetBorderColor();
            var borderPaint = _sKPaintMaps.GetSKBackgroundPaint(borderColor);
            canvas.DrawRect(0, 0, _emulatorHelper.MaxCols * _textSizePixels + _borderPixels * 2, _emulatorHelper.MaxRows * _textSizePixels + _borderPixels * 2, borderPaint);

            // Draw background
            using (new SKAutoCanvasRestore(canvas))
            {
                var bgColor = _emulatorHelper.GetBackgroundColor();
                var bgPaint = _sKPaintMaps.GetSKBackgroundPaint(bgColor);
                canvas.Translate(_borderPixels, _borderPixels);
                canvas.DrawRect(0, 0, _emulatorHelper.MaxCols * _textSizePixels, _emulatorHelper.MaxRows * _textSizePixels, bgPaint);
            }

            using (new SKAutoCanvasRestore(canvas))
            {
                canvas.Translate(_borderPixels, _borderPixels);
                // Draw characters
                for (var row = 0; row < _emulatorHelper.MaxRows; row++)
                {
                    for (var col = 0; col < _emulatorHelper.MaxCols; col++)
                    {
                        var chr = _emulatorHelper.GetScreenCharacter(col, row);
                        var chrColor = _emulatorHelper.GetScreenCharacterForegroundColor(col, row);
                        var drawText = GetDrawTextFromCharacter(chr);
                        var textPaint = _sKPaintMaps.GetSKTextPaint(chrColor);
                        DrawCharacter(canvas, drawText, col, row, textPaint);
                    }
                }
            }
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
                    representAsString = ((char)0x2588).ToString(); // Unicode for Inverted square in https://style64.org/c64-truetype font
                    break;
                default:
                    representAsString = Convert.ToString((char)chr);
                    break;
            }
            return representAsString;
        }

        private void DrawCharacter(SKCanvas canvas, string character, int col, int row, SKPaint textPaint)
        {
            //var textHeight = textPaint.TextSize;

            var x = col * _textSizePixels;
            var y = row * _textSizePixels;
            // Make clipping rectangle for the tile we're drawing, to avoid any accidental spill-over to neighboring tiles.
            var rect = new SKRect(x, y, x + _textSizePixels, y + _textSizePixels);
            using (new SKAutoCanvasRestore(canvas))
            {
                canvas.ClipRect(rect, SKClipOperation.Intersect);
                canvas.DrawText(character, x, y + (_textSizePixels - 2), textPaint);
            }
        }

        public void Dispose()
        {
        }
    }
}
