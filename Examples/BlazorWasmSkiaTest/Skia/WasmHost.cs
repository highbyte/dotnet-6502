using BlazorWasmSkiaTest.Instrumentation.Stats;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using SkiaSharp;

namespace BlazorWasmSkiaTest.Skia
{
    public class WasmHost : IDisposable
    {
        private int _screenWidth;
        private int _screenHeight;
        private readonly SystemRunner _systemRunner;
        private readonly PeriodicAsyncTimer? _updateTimer;

        private SKCanvas _skCanvas;
        private SkiaRenderContext _skiaRenderContext;
        public AspNetInputHandlerContext InputHandlerContext { get; private set; }
        private readonly ISystem _system;
        private readonly Action<string> _updateStats;
        private readonly float _scale;

        private readonly ElapsedMillisecondsTimedStat _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASM-InputTime");
        private readonly ElapsedMillisecondsTimedStat _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
        private readonly ElapsedMillisecondsTimedStat _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASMSkiaSharp-RenderTime");
        private readonly PerSecondTimedStat _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnUpdateFPS");
        private readonly PerSecondTimedStat _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnRenderFPS");

        private const int STATS_EVERY_X_FRAME = 60 * 1;
        private int _statsFrameCount = 0;

        public WasmHost(
            ISystem system,
            Func<ISystem, SkiaRenderContext, AspNetInputHandlerContext, SystemRunner> getSystemRunner,
            Action<string> updateStats,
            float scale = 1.0f
            )
        {
            _system = system;
            _skiaRenderContext = new SkiaRenderContext(GetCanvas);
            InputHandlerContext = new AspNetInputHandlerContext();
            _systemRunner = getSystemRunner(_system, _skiaRenderContext, InputHandlerContext);
            _updateStats = updateStats;
            _scale = scale;

            var screen = (IScreen)system;
            // Number of milliseconds between each invokation of the main game loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
            double updateIntervalMS = (1 / screen.RefreshFrequencyHz) * 1000;
            _updateTimer = new PeriodicAsyncTimer();
            _updateTimer.IntervalMilliseconds = updateIntervalMS;
            _updateTimer.Elapsed += UpdateTimerElapsed;
            _updateTimer.Start();


        }

        private void UpdateTimerElapsed(object? sender, EventArgs e) => EmulatorRunOneFrame();

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
        private void EmulatorRunOneFrame()
        {
            _updateFps.Update();

            //_emulatorHelper.GenerateRandomNumber();
            using (_inputTime.Measure())
            {
                _systemRunner.ProcessInput();
            }
            using (_systemTime.Measure())
            {
                _systemRunner.RunEmulatorOneFrame();
            }


            _statsFrameCount++;
            if (_statsFrameCount >= STATS_EVERY_X_FRAME)
            {
                _statsFrameCount = 0;
                var statsString = GetStats();
                _updateStats(statsString);
            }
        }

        public void Render(SKCanvas canvas)
        {
            _renderFps.Update();

            _skCanvas = canvas;
            _skCanvas.Scale(_scale);
            using (_renderTime.Measure())
            {
                _systemRunner.Draw();
                //using (new SKAutoCanvasRestore(skCanvas))
                //{
                //    _systemRunner.Draw(skCanvas);
                //}
            }
        }

        private SKCanvas GetCanvas()
        {
            return _skCanvas;
        }

        private string GetStats()
        {
            var strings = new List<string>();
            foreach ((string name, IStat stat) in InstrumentationBag.Stats.OrderBy(i => i.Name))
            {
                if (stat.ShouldShow())
                {
                    string line = name + ": " + stat.GetDescription();
                    strings.Add(line);
                }
            };
            var stats = string.Join(" - ", strings);
            return stats;
        }

        public void Dispose()
        {
        }
    }
}
