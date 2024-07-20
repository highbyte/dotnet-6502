using System.Data;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.JSInterop.BlazorWebAudioSync;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Instrumentation.Stats;
using Highbyte.DotNet6502.Systems;
using Toolbelt.Blazor.Gamepad;

namespace Highbyte.DotNet6502.App.WASM.Emulator.Skia
{
    public class SkiaWASMHostApp : HostApp<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext>
    {
        // --------------------
        // Injected variables
        // --------------------
        private readonly ILogger _logger;
        private readonly EmulatorConfig _emulatorConfig;
        private readonly Func<SKCanvas> _getCanvas;
        private readonly Func<GRContext> _getGrContext;
        private readonly Func<AudioContextSync> _getAudioContext;

        public EmulatorConfig EmulatorConfig => _emulatorConfig;

        private readonly bool _defaultAudioEnabled;
        private readonly float _defaultAudioVolumePercent;
        private readonly ILoggerFactory _loggerFactory;

        // --------------------
        // Other variables / constants
        // --------------------
        private SkiaRenderContext _renderContext = default!;
        private AspNetInputHandlerContext _inputHandlerContext = default!;
        private WASMAudioHandlerContext _audioHandlerContext = default!;

        private readonly IJSRuntime _jsRuntime;
        private PeriodicAsyncTimer? _updateTimer;

        private WasmMonitor _monitor = default!;
        public WasmMonitor Monitor => _monitor;

        // Delegates for changing the state of Stats, Debug, and Monitor UI panels
        private readonly Action<string> _updateStats;
        private readonly Action<string> _updateDebug;
        private readonly Func<bool, Task> _setMonitorState;
        private readonly Func<Task> _toggleDebugStatsState;

        private const int STATS_EVERY_X_FRAME = 60 * 1;
        private int _statsFrameCount = 0;

        private const int DEBUGMESSAGE_EVERY_X_FRAME = 1;
        private int _debugFrameCount = 0;

        public bool Initialized { get; private set; } = false;


        public SkiaWASMHostApp(
            SystemList<SkiaRenderContext, AspNetInputHandlerContext, WASMAudioHandlerContext> systemList,
            ILoggerFactory loggerFactory,
            EmulatorConfig emulatorConfig,


            Func<SKCanvas> getCanvas,
            Func<GRContext> getGrContext,
            Func<AudioContextSync> getAudioContext,
            Action<string> updateStats,
            Action<string> updateDebug,
            Func<bool, Task> setMonitorState,
            Func<Task> toggleDebugStatsState
            ) : base("SilkNet", systemList, emulatorConfig.HostSystemConfigs, loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger(typeof(SkiaWASMHostApp).Name);
            _emulatorConfig = emulatorConfig;
            _emulatorConfig.CurrentDrawScale = _emulatorConfig.DefaultDrawScale;

            _getCanvas = getCanvas;
            _getGrContext = getGrContext;
            _getAudioContext = getAudioContext;

            _updateStats = updateStats;
            _updateDebug = updateDebug;
            _setMonitorState = setMonitorState;
            _toggleDebugStatsState = toggleDebugStatsState;

            _defaultAudioEnabled = false;
            _defaultAudioVolumePercent = 20.0f;
        }

        /// <summary>
        /// Call Init once from Blazor SKGLView "OnAfterRenderAsync" event.
        /// </summary>
        public void Init(GamepadList gamepadList, IJSRuntime jsRuntime)
        {
            if (Initialized)
                throw new InvalidOperationException("Init can only be called once.");

            _renderContext = new SkiaRenderContext(_getCanvas, _getGrContext);
            _inputHandlerContext = new AspNetInputHandlerContext(_loggerFactory, gamepadList);
            _audioHandlerContext = new WASMAudioHandlerContext(_getAudioContext, jsRuntime, _defaultAudioVolumePercent);

            base.InitContexts(() => _renderContext, () => _inputHandlerContext, () => _audioHandlerContext);

            Initialized = true;
        }

        public override void OnAfterSelectSystem()
        {
        }

        public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
        {
            return true;
        }

        public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
        {
            // Setup and start timer for current system started
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Dispose();
            }
            _updateTimer = CreateUpdateTimerForSystem(CurrentSystemRunner!.System);
            _updateTimer!.Start();

            // Init monitor for current system started if this system was not started before
            if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
                _monitor = new WasmMonitor(_jsRuntime, CurrentSystemRunner, _emulatorConfig, _setMonitorState);
        }

        public override void OnAfterStop()
        {
            // TODO: Disable debug window?
            //_debugVisible = false;

            _monitor.Disable();
        }

        public override void OnAfterClose()
        {
            // Cleanup contexts
            _renderContext?.Cleanup();
            _inputHandlerContext?.Cleanup();
            _audioHandlerContext?.Cleanup();
        }


        private PeriodicAsyncTimer CreateUpdateTimerForSystem(ISystem system)
        {
            // Number of milliseconds between each invokation of the main loop. 60 fps -> (1/60) * 1000  -> approx 16.6667ms
            double updateIntervalMS = (1 / system.Screen.RefreshFrequencyHz) * 1000;
            var updateTimer = new PeriodicAsyncTimer();
            updateTimer.IntervalMilliseconds = updateIntervalMS;
            updateTimer.Elapsed += UpdateTimerElapsed;
            return updateTimer;
        }

        private void UpdateTimerElapsed(object? sender, EventArgs e) => RunEmulatorOneFrame();

        public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
        {
            shouldRun = false;
            shouldReceiveInput = false;
            // Don't update emulator state when monitor is visible
            if (_monitor.Visible)
                return;

            shouldRun = true;
            shouldReceiveInput = true;
        }

        public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
        {
            // Push debug info to debug UI
            _debugFrameCount++;
            if (_debugFrameCount >= DEBUGMESSAGE_EVERY_X_FRAME)
            {
                _debugFrameCount = 0;
                var debugString = GetDebugMessagesHtmlString();
                _updateDebug(debugString);
            }

            // Push stats to stats UI
            if (CurrentRunningSystem!.InstrumentationEnabled)
            {
                _statsFrameCount++;
                if (_statsFrameCount >= STATS_EVERY_X_FRAME)
                {
                    _statsFrameCount = 0;
                    _updateStats(GetStatsHtmlString());
                }
            }

            // Show monitor if we encounter breakpoint or other break
            if (execEvaluatorTriggerResult.Triggered)
                _monitor.Enable(execEvaluatorTriggerResult);
        }

        /// <summary>
        /// Called from ASP.NET Blazor SKGLView "OnPaintSurface" event to render one frame.
        /// </summary>
        /// <param name="args"></param>
        public void Render()
        {
            // Draw emulator on screen
            base.DrawFrame();
        }

        public override void OnBeforeDrawFrame(bool emulatorWillBeRendered)
        {
            if (emulatorWillBeRendered)
            {
                // TODO: Shouldn't scale be able to set once we start the emulator (OnBeforeStart method?) instead of every frame?
                _getCanvas().Scale((float)_emulatorConfig.CurrentDrawScale);
            }
        }

        public override void OnAfterDrawFrame(bool emulatorRendered)
        {
            if (emulatorRendered)
            {
            }
        }

        public void SetVolumePercent(float volumePercent)
        {
            _audioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
        }

        private string GetStatsHtmlString()
        {
            string stats = "";

            var allStats = GetStats();
            foreach ((string name, IStat stat) in allStats.OrderBy(i => i.name))
            {
                if (stat.ShouldShow())
                {
                    if (stats != "")
                        stats += "<br />";
                    stats += $"{BuildHtmlString(name, "header")}: {BuildHtmlString(stat.GetDescription(), "value")} ";
                }
            }
            return stats;
        }

        private string GetDebugMessagesHtmlString()
        {
            string debugMessages = "";

            var inputDebugInfo = CurrentSystemRunner!.InputHandler.GetDebugInfo();
            var inputStatsOneString = string.Join(" # ", inputDebugInfo);
            debugMessages += $"{BuildHtmlString("INPUT", "header")}: {BuildHtmlString(inputStatsOneString, "value")} ";
            //foreach (var message in inputDebugInfo)
            //{
            //    if (debugMessages != "")
            //        debugMessages += "<br />";
            //    debugMessages += $"{BuildHtmlString("DEBUG INPUT", "header")}: {BuildHtmlString(message, "value")} ";
            //}

            var audioDebugInfo = CurrentSystemRunner!.AudioHandler.GetDebugInfo();
            foreach (var message in audioDebugInfo)
            {
                if (debugMessages != "")
                    debugMessages += "<br />";
                debugMessages += $"{BuildHtmlString("AUDIO", "header")}: {BuildHtmlString(message, "value")} ";
            }

            return debugMessages;
        }

        private string BuildHtmlString(string message, string cssClass, bool startNewLine = false)
        {
            string html = "";
            if (startNewLine)
                html += "<br />";
            html += $@"<span class=""{cssClass}"">{HttpUtility.HtmlEncode(message)}</span>";
            return html;
        }

        /// <summary>
        /// Receive Key Down event in emulator canvas.
        /// Also check for special non-emulator functions such as monitor and stats/debug
        /// </summary>
        /// <param name="e"></param>
        public void OnKeyDown(KeyboardEventArgs e)
        {
            // Send key press to emulator
            _inputHandlerContext.KeyDown(e);

            // Check for other emulator functions
            var key = e.Key;
            if (key == "F11")
            {
                _toggleDebugStatsState();

            }
            else if (key == "F12")
            {
                ToggleMonitor();
            }
        }

        /// <summary>
        /// Receive Key Up event in emulator canvas.
        /// Also check for special non-emulator functions such as monitor and stats/debug
        /// </summary>
        /// <param name="e"></param>
        public void OnKeyUp(KeyboardEventArgs e)
        {
            // Send key press to emulator
            _inputHandlerContext.KeyUp(e);

            // Check for other emulator functions
            var key = e.Key;
            if (key == "F11")
            {
                _toggleDebugStatsState();

            }
            else if (key == "F12")
            {
                ToggleMonitor();
            }
        }

        /// <summary>
        /// Receive Focus on emulator canvas.
        /// </summary>
        /// <param name="e"></param>
        public void OnFocus(FocusEventArgs e)
        {
            _inputHandlerContext.OnFocus(e);
        }

        public void ToggleMonitor()
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
}
