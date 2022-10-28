using System.Threading;
using BlazorWasmSkiaTest.Instrumentation.Stats;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Monitor;
using Highbyte.DotNet6502.Systems;
using Microsoft.AspNetCore.Components.Web;
using SkiaSharp;

namespace BlazorWasmSkiaTest.Skia
{
    public class WasmHost : IDisposable
    {
        public bool Initialized { get; private set; }

        private SystemRunner _systemRunner;

        private SKCanvas _skCanvas;
        private GRContext _grContext;

        private PeriodicAsyncTimer? _updateTimer;

        public AspNetInputHandlerContext InputHandlerContext { get; private set; }
        private readonly ISystem _system;
        private readonly Func<ISystem, SkiaRenderContext, AspNetInputHandlerContext, SystemRunner> _getSystemRunner;
        private readonly Action<string> _updateStats;
        private readonly Action<string> _updateDebugMessage;
        private readonly Func<bool, Task> _setMonitorState;
        private readonly MonitorConfig _monitorConfig;
        private readonly Func<Task> _toggleDebugStatsState;
        private readonly float _scale;

        public WasmMonitor Monitor { get; private set; }

        private readonly ElapsedMillisecondsTimedStat _inputTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASM-InputTime");
        private readonly ElapsedMillisecondsTimedStat _systemTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("Emulator-SystemTime");
        private readonly ElapsedMillisecondsTimedStat _renderTime = InstrumentationBag.Add<ElapsedMillisecondsTimedStat>("WASMSkiaSharp-RenderTime");
        private readonly PerSecondTimedStat _updateFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnUpdateFPS");
        private readonly PerSecondTimedStat _renderFps = InstrumentationBag.Add<PerSecondTimedStat>("WASMSkiaSharp-OnRenderFPS");

        private const int STATS_EVERY_X_FRAME = 60 * 1;
        private int _statsFrameCount = 0;

        private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
        private int _debugFrameCount = 0;

        public WasmHost(
            ISystem system,
            Func<ISystem, SkiaRenderContext, AspNetInputHandlerContext, SystemRunner> getSystemRunner,
            Action<string> updateStats,
            Action<string> updateDebugMessage,
            Func<bool, Task> setMonitorState,
            MonitorConfig monitorConfig,
            Func<Task> toggleDebugStatsState,
            float scale = 1.0f)
        {
            _system = system;
            _getSystemRunner = getSystemRunner;
            _updateStats = updateStats;
            _updateDebugMessage = updateDebugMessage;
            _setMonitorState = setMonitorState;
            _monitorConfig = monitorConfig;
            _toggleDebugStatsState = toggleDebugStatsState;
            _scale = scale;

            Initialized = false;
        }

        public void Init(SKCanvas canvas, GRContext grContext)
        {
            _skCanvas = canvas;
            _grContext = grContext;

            var skiaRenderContext = new SkiaRenderContext(GetCanvas, GetGRContext);
            InputHandlerContext = new AspNetInputHandlerContext();
            _systemRunner = _getSystemRunner(_system, skiaRenderContext, InputHandlerContext);

            Monitor = new WasmMonitor(_systemRunner, _monitorConfig, _setMonitorState);

            var screen = (IScreen)_system;
            // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
            double updateIntervalMS = (1 / screen.RefreshFrequencyHz) * 1000;
            _updateTimer = new PeriodicAsyncTimer();
            _updateTimer.IntervalMilliseconds = updateIntervalMS;
            _updateTimer.Elapsed += UpdateTimerElapsed;
            _updateTimer.Start();

            Initialized = true;
        }

        private void UpdateTimerElapsed(object? sender, EventArgs e) => EmulatorRunOneFrame();


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        private void EmulatorRunOneFrame()
        {
            if (Monitor.Visible)
                return;

            _updateFps.Update();

            _debugFrameCount++;
            if (_debugFrameCount >= DEBUGMESSAGE_EVERY_X_FRAME)
            {
                _debugFrameCount = 0;
                var debugString = GetDebugMessage();
                _updateDebugMessage(debugString);
            }

            //_emulatorHelper.GenerateRandomNumber();
            using (_inputTime.Measure())
            {
                _systemRunner.ProcessInput();
            }

            bool cont;
            using (_systemTime.Measure())
            {
                cont = _systemRunner.RunEmulatorOneFrame();
            }

            _statsFrameCount++;
            if (_statsFrameCount >= STATS_EVERY_X_FRAME)
            {
                _statsFrameCount = 0;
                var statsString = GetStats();
                _updateStats(statsString);
            }

            // Show monitor if we encounter breakpoint or other break
            if (!cont)
                Monitor.Enable();
        }

        public void Render(SKCanvas canvas, GRContext grContext)
        {
            //if (Monitor.Visible)
            //    return;

            _renderFps.Update();

            _grContext = grContext;
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

        private GRContext GetGRContext()
        {
            return _grContext;
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

        private string GetDebugMessage()
        {
            string msg = "DEBUG: ";
            msg += _systemRunner.InputHandler.GetDebugMessage();
            return msg;
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Enable / Disable emulator functions such as monitor and stats/debug
        /// </summary>
        /// <param name="e"></param>
        public void OnKeyDown(KeyboardEventArgs e)
        {
            var key = e.Key;

            //if ((key == "§" || key == "~") && e.ShiftKey)
            //{
            //    // TODO: Show/hide stats & debug panel
            //    _toggleDebugStatsState();

            //}
            //else 

            if (key == "§" || key == "~")
            {
                if (Monitor.Visible)
                {
                    Monitor.Disable();
                }
                else
                {
                    Monitor.Enable();
                }
            }
        }

        /// <summary>
        /// Enable / Disable emulator functions such as monitor and stats/debug
        /// </summary>
        /// <param name="e"></param>
        public void OnKeyPress(KeyboardEventArgs e)
        {
            var key = e.Key;

            if ((key == "½" || key == "¬") && e.ShiftKey)   // Shift-"§" (Swedish) or Shift-"~" (Us)
            {
                // TODO: Show/hide stats & debug panel
                _toggleDebugStatsState();
            }
        }
    }
}
