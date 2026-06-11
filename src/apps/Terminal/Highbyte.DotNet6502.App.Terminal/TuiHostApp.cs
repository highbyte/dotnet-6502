using System.Collections.ObjectModel;
using System.Text;
using Highbyte.DotNet6502.Impl.Terminal;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// Terminal.Gui 2.4.5 marks the static Application API (Init/Run/Shutdown/AddTimeout/Invoke/
// RequestStop) obsolete in favour of a not-yet-stable instance-based IApplication. Keep using the
// static API for now (it is fully functional); migrate when the instance API settles.
#pragma warning disable CS0618

namespace Highbyte.DotNet6502.App.Terminal;

/// <summary>
/// Host app for running the Highbyte.DotNet6502 emulator inside a real terminal, using Terminal.Gui
/// for the window/control chrome and a custom <see cref="EmulatorScreenView"/> for the emulator
/// screen. Text mode only; no audio.
/// </summary>
public class TuiHostApp : HostApp
{
    private new readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly DotNet6502InMemLogStore _logStore;

    private TerminalInputHandlerContext _inputContext = default!;
    private TerminalRenderLoop? _renderLoop;

    // UI
    private const int DefaultScreenFrameWidth = 52; // until a system frame size is known (fits a C64)
    private const int SideColumnWidth = 30;         // controls column (left) and status/logs column (right)
    private Window _window = default!;
    private FrameView _screenFrame = default!;
    private EmulatorScreenView _screenView = default!;
    // Last screen-frame size applied to fit the running system (incl. the 1-cell border on each side).
    private int _appliedFrameWidth = -1;
    private int _appliedFrameHeight = -1;
    private Button _systemButton = default!;
    private Button _variantButton = default!;
    private Label _leftStatusLabel = default!;   // "Status: <state>" in the controls column
    private Label _statsLabel = default!;         // instrumentation list in the right "Stats" box

    // Tabbed area (Logs | Config | Info) below the Stats box.
    private enum InfoTab { Logs, Config, Info }
    private InfoTab _activeInfoTab = InfoTab.Logs;
    private TabStripView _infoTabStrip = default!;
    private ListView _logsListView = default!;    // scrollable log list (like the other host apps)
    private List<string> _logRows = new();        // backing rows for the log list (for the detail popup)
    private Label _configLabel = default!;
    private Label _infoLabel = default!;
    private Button _startButton = default!;
    private Button _pauseButton = default!;
    private Button _stopButton = default!;
    private Button _resetButton = default!;
    private Button _monitorButton = default!;
    private Button _statsButton = default!;

    // Whether the right "Stats" box shows instrumentation (toggled by the Stats button / F11).
    private bool _statsEnabled;

    // Timers (Terminal.Gui timeout tokens)
    private object? _emulatorTimerToken;
    private object? _displayTimerToken;
    private object? _statusTimerToken;

    private int _lastLogCount = -1;

    // When true, the Terminal.Gui UI is not created (headless self-test that drives the emulator +
    // render target directly and dumps the rendered screen as text). Set by RunSelfTest.
    private bool _selfTestMode;

    public TuiHostApp(
        SystemList systemList,
        ILoggerFactory loggerFactory,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore)
        : base("Terminal", systemList, loggerFactory, useStatsNamePrefix: false)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(TuiHostApp));
        _emulatorConfig = emulatorConfig;
        _logStore = logStore;
    }

    public void Run()
    {
        _inputContext = new TerminalInputHandlerContext(_loggerFactory);

        // Rendering pipeline: a single, system-agnostic terminal render target consuming the
        // system's video-command stream, driven by a manual-invalidation render loop.
        SetRenderConfig(
            rtp => rtp.AddRenderTargetType<TerminalRenderTarget>(() => new TerminalRenderTarget()),
            () =>
            {
                _renderLoop = new TerminalRenderLoop();
                return _renderLoop;
            });

        // No SetAudioConfig() — terminals have no audio output.

        SetContexts(() => _inputContext);
        InitInputHandlerContext();

        Application.Init();
        // Terminal.Gui binds Esc → Quit at the application level by default. We don't want Esc to
        // quit the host (F10 is the only quit key), and the emulator maps Esc to RUN/STOP, so remove
        // that binding. The log-entry popup still closes on Esc via its own key handler.
        Application.KeyBindings.Remove(Key.Esc);
        // Global hotkeys (F9–F12): handled app-wide so they work regardless of which control has
        // focus. Kept off F1–F8, which the emulated systems (e.g. the C64) use.
        Application.KeyDown += OnGlobalKeyDown;
        try
        {
            BuildUi();

            if (AvailableSystemNames.Count == 0)
                throw new DotNet6502Exception(
                    "No emulator systems are available. Check appsettings.json and that the system " +
                    "core assemblies are deployed.");

            SelectSystem(_emulatorConfig.DefaultEmulator).Wait();
            ApplySupportedRenderTargetToSystemConfigs().Wait();
            UpdateSystemSelectors();
            UpdateLeftStatus();
            UpdateStatsBox();
            RefreshLogs();
            UpdateButtonStates();
            PopulateInfoView();
            SelectInfoTab(InfoTab.Logs);

            // Display refresh timer (throttled, independent of emulator frame rate).
            var displayIntervalMs = Math.Max(1000.0 / Math.Max(_emulatorConfig.DisplayRefreshHz, 1), 1);
            _displayTimerToken = Application.AddTimeout(
                TimeSpan.FromMilliseconds(displayIntervalMs), OnDisplayTick);

            // Status / log refresh timer (a few times per second).
            _statusTimerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(250), OnStatusTick);

            Application.Run(_window);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error running terminal host.");
            // Best effort: surface to console after Terminal.Gui shuts down.
            Console.Error.WriteLine($"FATAL: {ex.Message}");
        }
        finally
        {
            Application.KeyDown -= OnGlobalKeyDown;
            RemoveTimer(ref _emulatorTimerToken);
            RemoveTimer(ref _displayTimerToken);
            RemoveTimer(ref _statusTimerToken);
            Application.Shutdown();
            Close();
        }
    }

    /// <summary>
    /// Headless self-test: wires the render pipeline, boots the default system, runs it for a number
    /// of emulated frames, then returns the rendered screen as plain text (one line per cell row).
    /// Does not create the Terminal.Gui UI, so it can run without a TTY (CI, smoke test).
    /// </summary>
    public string RunSelfTest(int frames = 200, string? systemName = null)
    {
        _selfTestMode = true;

        _inputContext = new TerminalInputHandlerContext(_loggerFactory);
        SetRenderConfig(
            rtp => rtp.AddRenderTargetType<TerminalRenderTarget>(() => new TerminalRenderTarget()),
            () => { _renderLoop = new TerminalRenderLoop(); return _renderLoop; });
        SetContexts(() => _inputContext);
        InitInputHandlerContext();

        SelectSystem(systemName ?? _emulatorConfig.DefaultEmulator).Wait();
        ApplySupportedRenderTargetToSystemConfigs().Wait();
        Start().Wait();

        var target = GetRenderTarget<TerminalRenderTarget>()
            ?? throw new DotNet6502Exception("Self-test: no terminal render target was created.");
        var coordinator = GetRenderCoordinator()
            ?? throw new DotNet6502Exception("Self-test: no render coordinator (manual-invalidation expected).");

        for (var i = 0; i < frames; i++)
        {
            RunEmulatorOneFrame();
            coordinator.FlushIfDirtyAsync().GetAwaiter().GetResult();
        }

        var buffer = new TerminalRenderTarget.ScreenCell[1, 1];
        var (width, height) = target.Snapshot(ref buffer);

        var sb = new StringBuilder();
        sb.AppendLine($"Rendered frame: {width} x {height} cells");
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                sb.Append(buffer[y, x].Rune);
            sb.AppendLine();
        }

        Stop();
        Close();
        return sb.ToString();
    }

    // ----------------------------------------------------------------------
    // UI construction
    // ----------------------------------------------------------------------

    private void BuildUi()
    {
        _window = new Window
        {
            Title = "DotNet 6502 — Terminal Host   (F10 = Quit)",
            BorderStyle = LineStyle.Single,
        };

        // --- Controls column (left), like the other host apps' menu column ---
        // Buttons stack vertically; hotkeys are shown in the bottom hint line, so labels stay short.
        var controlsFrame = new FrameView
        {
            Title = "Controls",
            X = 0,
            Y = 0,
            Width = SideColumnWidth,
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.Single,
        };

        // Layout (mirrors the other host apps' menu column):
        //   System: <name>
        //   Variant: <name>
        //   Status: <state>
        //
        //   Start    Pause
        //   Reset    Stop
        //   Monitor  Stats
        const int col2X = 12; // X of the right button in each pair

        // System + variant pickers (only changeable while the emulator is stopped).
        _systemButton = new Button { X = 0, Y = 0, Text = "System: -" };
        _variantButton = new Button { X = 0, Y = 1, Text = "Variant: -" };
        _systemButton.Accepting += (_, e) => { e.Handled = true; CycleSystem(+1); };
        _variantButton.Accepting += (_, e) => { e.Handled = true; CycleVariant(+1); };

        _leftStatusLabel = new Label { X = 0, Y = 2, Text = "Status: -" };

        _startButton = new Button { X = 0, Y = 4, Text = "Start" };
        _pauseButton = new Button { X = col2X, Y = 4, Text = "Pause" };
        _resetButton = new Button { X = 0, Y = 5, Text = "Reset" };
        _stopButton = new Button { X = col2X, Y = 5, Text = "Stop" };
        _monitorButton = new Button { X = 0, Y = 6, Text = "Monitor", Enabled = false };
        _statsButton = new Button { X = col2X, Y = 6, Text = "Stats" };

        _startButton.Accepting += (_, e) => { e.Handled = true; DoStart(); };
        _pauseButton.Accepting += (_, e) => { e.Handled = true; DoPause(); };
        _stopButton.Accepting += (_, e) => { e.Handled = true; DoStop(); };
        _resetButton.Accepting += (_, e) => { e.Handled = true; DoReset(); };
        _statsButton.Accepting += (_, e) => { e.Handled = true; ToggleStats(); };
        // Monitor button is disabled for now (placeholder); F12 / this button call DoMonitor.
        _monitorButton.Accepting += (_, e) => { e.Handled = true; DoMonitor(); };

        // Disable Terminal.Gui's default button drop-shadow: in this dense column the shadows of
        // adjacent buttons are mostly overlapped by neighbours, leaving stray black stripes.
        foreach (var button in new[]
                 {
                     _systemButton, _variantButton, _startButton, _pauseButton,
                     _resetButton, _stopButton, _monitorButton, _statsButton,
                 })
        {
            button.ShadowStyle = ShadowStyles.None;
        }

        controlsFrame.Add(
            _systemButton, _variantButton, _leftStatusLabel,
            _startButton, _pauseButton, _resetButton, _stopButton, _monitorButton, _statsButton);

        // --- Emulator screen (middle) ---
        // Width/Height are resized to fit the running system's frame (see ResizeScreenFrameToFit),
        // so the bordered box hugs the screen for any system (C64 ~52, VIC-20 ~32 wide) and the
        // status/logs column (anchored to its right) follows.
        _screenFrame = new FrameView
        {
            Title = "Screen",
            X = Pos.Right(controlsFrame),
            Y = 0,
            Width = DefaultScreenFrameWidth,
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.Single,
        };
        _screenView = new EmulatorScreenView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _screenView.EmulatorKeyPressed += key => _inputContext.OnKeyDown(key);
        _screenFrame.Add(_screenView);

        // --- Stats / Logs column (right), same width as the controls column ---
        // The Stats box content is toggled by the Stats button (see ToggleStats / UpdateStatsBox).
        var statsFrame = new FrameView
        {
            Title = "Stats",
            X = Pos.Right(_screenFrame),
            Y = 0,
            Width = SideColumnWidth,
            Height = 12,
            BorderStyle = LineStyle.Single,
        };
        _statsLabel = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        statsFrame.Add(_statsLabel);

        // --- Tabbed area below Stats: Logs | Config | Info ---
        // Terminal.Gui 2.4.5 has no TabView, so this is a small hand-rolled tab strip (TabStripView):
        // all titles fit on one line (Y=0), with a content area (Y=1+) where only the active tab's
        // view is visible. The narrow side column can't fit stock Buttons' chrome for 3+ tabs.
        var tabsFrame = new FrameView
        {
            Title = string.Empty,
            X = Pos.Right(_screenFrame),
            Y = Pos.Bottom(statsFrame),
            Width = SideColumnWidth,
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.Single,
        };

        _infoTabStrip = new TabStripView("Logs", "Config", "Info") { X = 0, Y = 0 };
        _infoTabStrip.TabSelected += index => SelectInfoTab((InfoTab)index);

        // Logs: a scrollable ListView (one row per message), like the other host apps' log list.
        // (ListView is lighter than TextView for frequently-rebuilt logs and inherits the UI scheme.)
        _logsListView = new ListView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        _logsListView.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        // Enter on a log row opens a popup with the full (word-wrapped) entry — the narrow pane clips
        // long lines, and a detail popup is the common TUI way to read the whole entry.
        _logsListView.Accepting += (_, e) => { e.Handled = true; ShowSelectedLogEntry(); };
        // Config / Info: short read-only text — Labels (which inherit the surrounding scheme, like Stats).
        _configLabel = new Label { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(), Text = "", Visible = false };
        _infoLabel = new Label { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(), Text = "", Visible = false };
        tabsFrame.Add(_infoTabStrip, _logsListView, _configLabel, _infoLabel);

        // --- Bottom hint line ---
        var hintLabel = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Text = " 9 System  0 Variant   F9 Start/Stop  F10 Quit  F11 Stats  F12 Monitor   Tab Focus",
        };

        _window.Add(controlsFrame, _screenFrame, statsFrame, tabsFrame, hintLabel);

        // Make focus/hover indication a background change (keeping text readable) instead of the
        // theme default, which flips button text to near-black — unreadable on the dark window.
        // Applied window-wide so buttons and the tab strip stay consistent (children inherit it).
        ApplyReadableFocusScheme(_window);
    }

    /// <summary>
    /// Derive a scheme from <paramref name="view"/>'s current one where the focus/hover/active roles
    /// indicate selection with a distinct background and bright text, rather than the theme default
    /// that renders focused/hovered button text near-black (illegible on the dark window background).
    /// </summary>
    private static void ApplyReadableFocusScheme(View view)
    {
        var baseScheme = view.GetScheme();
        if (baseScheme is null)
            return;

        var selected = new global::Terminal.Gui.Drawing.Attribute(new Color(0xFF, 0xFF, 0xFF), new Color(0x2C, 0x5A, 0xA0));
        view.SetScheme(baseScheme with
        {
            Focus = selected,
            HotFocus = selected,
            Active = selected,
            HotActive = selected,
            Highlight = selected,
        });
    }

    // ----------------------------------------------------------------------
    // Button / hotkey actions
    // ----------------------------------------------------------------------

    /// <summary>
    /// Application-wide key handler so host hotkeys work no matter which control is focused. Only
    /// F9–F12 are used — F1–F8 are reserved for the emulated systems (e.g. the C64). Unhandled keys
    /// fall through to the focused view (so the emulator still receives F1–F8 and typing).
    /// System/Variant/Pause/Stop have no hotkey (their buttons remain) to stay within F9–F12.
    /// </summary>
    private void OnGlobalKeyDown(object? sender, Key key)
    {
        var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);
        var stopped = EmulatorState == EmulatorState.Uninitialized;
        switch (code)
        {
            // Running-time actions: only plain F9–F12 are conflict-free while the emulator runs
            // (Ctrl = the C64/VIC-20 Commodore key, Shift/Tab/Alt are also emulator keys).
            case KeyCode.F9: DoStartStopToggle(); break;
            case KeyCode.F10: Application.RequestStop(_window); break; // quit
            case KeyCode.F11: ToggleStats(); break;
            case KeyCode.F12: DoMonitor(); break;

            // System/Variant cycling: only meaningful while stopped, so we intercept 9/0 only then
            // (chosen for being next to F9). While running, 9/0 fall through to the emulator.
            case KeyCode.D9 when stopped: CycleSystem(+1); break;
            case KeyCode.D0 when stopped: CycleVariant(+1); break;

            default: return; // not a host hotkey — let it reach the focused view (e.g. the emulator)
        }
        key.Handled = true;
    }

    private void CycleSystem(int direction) => Safe(() =>
    {
        if (EmulatorState != EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Stop the emulator before changing system.");
            return;
        }

        var names = AvailableSystemNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count <= 1)
            return;

        var idx = names.IndexOf(SelectedSystemName);
        var next = names[((idx + direction) % names.Count + names.Count) % names.Count];
        SelectSystem(next).Wait();

        UpdateSystemSelectors();
        UpdateButtonStates();
    });

    private void CycleVariant(int direction) => Safe(() =>
    {
        if (EmulatorState != EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Stop the emulator before changing variant.");
            return;
        }

        var variants = AllSelectedSystemConfigurationVariants;
        if (variants.Count <= 1)
            return;

        var idx = variants.IndexOf(SelectedSystemConfigurationVariant);
        var next = variants[((idx + direction) % variants.Count + variants.Count) % variants.Count];
        SelectSystemConfigurationVariant(next).Wait();

        UpdateSystemSelectors();
    });

    private void DoStart() => Safe(() =>
    {
        if (EmulatorState == EmulatorState.Running)
            return;
        Start().Wait();
        UpdateButtonStates();
        _screenView.SetFocus();
    });

    private void DoPause() => Safe(() =>
    {
        Pause();
        UpdateButtonStates();
        UpdateLeftStatus();
    });

    /// <summary>F9 toggles between Start/Resume and Stop depending on the current state.</summary>
    private void DoStartStopToggle()
    {
        if (EmulatorState == EmulatorState.Running)
            DoStop();
        else
            DoStart();
    }


    /// <summary>Monitor (F12) — placeholder; the machine-code monitor is not implemented yet.</summary>
    private void DoMonitor() => _logger.LogInformation("Monitor is not implemented yet.");

    private void DoStop() => Safe(() =>
    {
        Stop();
        UpdateButtonStates();
        UpdateLeftStatus();
        UpdateStatsBox();
    });

    private void DoReset() => Safe(() =>
    {
        Reset().Wait();
        UpdateButtonStates();
        UpdateLeftStatus();
        _screenView.SetFocus();
    });

    private void ToggleStats() => Safe(() =>
    {
        _statsEnabled = !_statsEnabled;
        ApplyInstrumentationEnabled();
        UpdateButtonStates();
        UpdateStatsBox();
    });

    /// <summary>Turns the running system's detailed instrumentation on/off to match the Stats toggle.</summary>
    private void ApplyInstrumentationEnabled()
    {
        if (CurrentRunningSystem != null)
            CurrentRunningSystem.InstrumentationEnabled = _statsEnabled;
    }

    // ----------------------------------------------------------------------
    // HostApp lifecycle overrides
    // ----------------------------------------------------------------------

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        if (_selfTestMode)
            return; // self-test drives frames manually and has no UI/timers

        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        {
            // Bind the screen view to the freshly created render target for this run.
            _screenView.SetRenderTarget(GetRenderTarget<TerminalRenderTarget>());
        }

        ApplyInstrumentationEnabled();
        StartEmulatorTimer();
        UpdateLeftStatus();
    }

    public override void OnAfterPause() => RemoveTimer(ref _emulatorTimerToken);

    public override void OnAfterStop()
    {
        RemoveTimer(ref _emulatorTimerToken);
        _screenView?.SetRenderTarget(null);
        if (!_selfTestMode)
            ResetScreenFrameSize();
    }

    public override void QuitApplication()
    {
        // Called by base.Close(); ensure the Terminal.Gui loop stops.
        Application.Invoke(() => Application.RequestStop(_window));
    }

    // ----------------------------------------------------------------------
    // Timers (all callbacks run on the Terminal.Gui main loop / UI thread)
    // ----------------------------------------------------------------------

    private void StartEmulatorTimer()
    {
        RemoveTimer(ref _emulatorTimerToken);
        var hz = CurrentRunningSystem?.Screen.RefreshFrequencyHz ?? 50.0;
        var intervalMs = Math.Max(1000.0 / hz, 1);
        _emulatorTimerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(intervalMs), OnEmulatorTick);
    }

    private bool OnEmulatorTick()
    {
        try
        {
            RunEmulatorOneFrame();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception running emulator frame.");
        }
        return true; // keep the timer
    }

    private bool OnDisplayTick()
    {
        try
        {
            if (_renderLoop != null && _renderLoop.ConsumeRedrawRequested())
            {
                var coordinator = GetRenderCoordinator();
                if (coordinator != null)
                    coordinator.FlushIfDirtyAsync().GetAwaiter().GetResult();
                _screenView.RefreshFromRenderTarget();
                ResizeScreenFrameToFit(_screenView.FrameWidth, _screenView.FrameHeight);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in display refresh.");
        }
        return true;
    }

    private bool OnStatusTick()
    {
        UpdateLeftStatus();
        UpdateStatsBox();
        RefreshLogs();
        if (_activeInfoTab == InfoTab.Config)
            UpdateConfigView();
        return true;
    }

    // ----------------------------------------------------------------------
    // UI updates
    // ----------------------------------------------------------------------

    /// <summary>
    /// Resizes the bordered "Screen" box to fit the running system's rendered frame, so it hugs the
    /// emulator screen (no dead space) for any system/variant. The status/logs panes anchored to its
    /// right reflow automatically. A 1-cell FrameView border on each side is added to the cell size.
    /// </summary>
    private void ResizeScreenFrameToFit(int frameCellWidth, int frameCellHeight)
    {
        if (frameCellWidth <= 0 || frameCellHeight <= 0)
            return;

        var targetWidth = frameCellWidth + 2;
        var targetHeight = frameCellHeight + 2;
        if (targetWidth == _appliedFrameWidth && targetHeight == _appliedFrameHeight)
            return;

        _appliedFrameWidth = targetWidth;
        _appliedFrameHeight = targetHeight;
        _screenFrame.Width = targetWidth;
        _screenFrame.Height = targetHeight;
        _screenFrame.SetNeedsLayout();
        _window.SetNeedsLayout();
    }

    /// <summary>Restores the screen box to its default size (no system running).</summary>
    private void ResetScreenFrameSize()
    {
        _appliedFrameWidth = -1;
        _appliedFrameHeight = -1;
        _screenFrame.Width = DefaultScreenFrameWidth;
        _screenFrame.Height = Dim.Fill(1);
        _screenFrame.SetNeedsLayout();
        _window.SetNeedsLayout();
    }

    private void UpdateSystemSelectors()
    {
        var variant = string.IsNullOrEmpty(SelectedSystemConfigurationVariant)
            ? "-"
            : SelectedSystemConfigurationVariant;
        _systemButton.Text = $"System: {SelectedSystemName} ▸";
        _variantButton.Text = $"Variant: {variant} ▸";
    }

    private void UpdateButtonStates()
    {
        var running = EmulatorState == EmulatorState.Running;
        var uninitialized = EmulatorState == EmulatorState.Uninitialized;

        // System/variant can only be changed while the emulator is fully stopped.
        _systemButton.Enabled = uninitialized && AvailableSystemNames.Count > 1;
        _variantButton.Enabled = uninitialized && AllSelectedSystemConfigurationVariants.Count > 1;

        _startButton.Enabled = !running;       // Start (resume) when not already running
        _pauseButton.Enabled = running;
        _stopButton.Enabled = !uninitialized;
        _resetButton.Enabled = !uninitialized;
        // Monitor stays disabled for now (planned feature).
        _statsButton.Text = _statsEnabled ? "Stats*" : "Stats";
    }

    /// <summary>Updates the "Status: &lt;state&gt;" line in the controls column.</summary>
    private void UpdateLeftStatus()
    {
        _leftStatusLabel.Text = $"Status: {EmulatorState}";
    }

    /// <summary>
    /// Updates the right "Stats" box. When stats are toggled on (Stats button / F11) and a system is
    /// running, shows the host instrumentation list (FPS, per-frame timings, …) like the other host
    /// apps; otherwise a short hint.
    /// </summary>
    private void UpdateStatsBox()
    {
        if (!_statsEnabled)
        {
            _statsLabel.Text = "(stats off — press Stats / F11)";
            return;
        }

        if (EmulatorState == EmulatorState.Uninitialized)
        {
            _statsLabel.Text = "(start a system to see stats)";
            return;
        }

        var stats = GetStats()
            .Where(s => s.stat.ShouldShow())
            .OrderBy(s => s.name, StringComparer.Ordinal)
            .ToList();

        if (stats.Count == 0)
        {
            _statsLabel.Text = "(no stats yet)";
            return;
        }

        var sb = new StringBuilder();
        foreach (var (name, stat) in stats)
            sb.AppendLine($"{name}: {stat.GetDescription()}");
        _statsLabel.Text = sb.ToString();
    }

    private void RefreshLogs()
    {
        var messages = _logStore.GetLogMessages();
        if (messages.Count == _lastLogCount)
            return;
        _lastLogCount = messages.Count;

        // The store inserts newest-first; show newest at the top (one row per message), as the
        // other host apps do.
        _logRows = messages.Select(m => m.TrimEnd('\r', '\n')).ToList();
        _logsListView.SetSource(new ObservableCollection<string>(_logRows));
    }

    // ----------------------------------------------------------------------
    // Tabbed area (Logs | Config | Info)
    // ----------------------------------------------------------------------

    private void SelectInfoTab(InfoTab tab) => Safe(() =>
    {
        _activeInfoTab = tab;

        _logsListView.Visible = tab == InfoTab.Logs;
        _configLabel.Visible = tab == InfoTab.Config;
        _infoLabel.Visible = tab == InfoTab.Info;

        // Keep the strip's highlighted tab in sync (no-op when already active, e.g. when the strip
        // itself raised the change).
        _infoTabStrip.SetActive((int)tab);

        if (tab == InfoTab.Config)
            UpdateConfigView();

        _window.SetNeedsLayout();
    });

    /// <summary>Config-status tab — current system/variant, audio, render provider, config validity.</summary>
    private void UpdateConfigView()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"System : {SelectedSystemName}");
        sb.AppendLine($"Variant: {SelectedSystemConfigurationVariant}");
        sb.AppendLine($"State  : {EmulatorState}");

        var hostConfig = CurrentHostSystemConfig;
        if (hostConfig != null)
        {
            sb.AppendLine($"Audio  : {(hostConfig.AudioSupported ? "supported" : "none")}");
            sb.AppendLine($"Render : {hostConfig.SystemConfig.RenderProviderType?.Name ?? "-"}");
            var valid = hostConfig.IsValid(out var errors);
            sb.AppendLine($"Config : {(valid ? "valid" : "invalid")}");
            foreach (var error in errors)
                sb.AppendLine($"  - {error}");
        }

        _configLabel.Text = sb.ToString();
    }

    /// <summary>General-info tab — static app/system/key overview.</summary>
    private void PopulateInfoView()
    {
        var sb = new StringBuilder();
        sb.AppendLine("DotNet 6502 — Terminal Host");
        sb.AppendLine();
        sb.AppendLine("Systems:");
        foreach (var name in AvailableSystemNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"  - {name}");
        sb.AppendLine();
        sb.AppendLine("Text mode only; no audio.");
        sb.AppendLine();
        sb.AppendLine("When stopped:");
        sb.AppendLine(" 9     cycle system");
        sb.AppendLine(" 0     cycle variant");
        sb.AppendLine();
        sb.AppendLine("Keys (global):");
        sb.AppendLine(" F9    start / stop");
        sb.AppendLine(" F10   quit");
        sb.AppendLine(" F11   stats");
        sb.AppendLine(" F12   monitor");
        sb.AppendLine(" Tab   move focus");
        sb.AppendLine();
        sb.AppendLine("F1-F8 go to the emulator.");
        sb.AppendLine("Pause / Reset: buttons.");
        sb.AppendLine();
        sb.AppendLine("Logs: Enter on a row");
        sb.AppendLine("  shows the full entry.");
        sb.AppendLine();
        sb.AppendLine("Tabs: focus strip, then");
        sb.AppendLine("  Left/Right to switch.");

        _infoLabel.Text = sb.ToString();
    }

    // ----------------------------------------------------------------------
    // Log entry detail popup (Enter on a Logs row)
    // ----------------------------------------------------------------------

    private void ShowSelectedLogEntry() => Safe(() =>
    {
        if (_logRows.Count == 0)
            return;
        var index = _logsListView.SelectedItem ?? 0;
        if (index < 0 || index >= _logRows.Count)
            index = 0;
        ShowLogEntryPopup(_logRows[index]);
    });

    private void ShowLogEntryPopup(string message)
    {
        var dialogWidth = Math.Clamp(_window.Frame.Width - 4, 24, 84);
        var dialogHeight = Math.Clamp(_window.Frame.Height - 4, 6, 24);
        var wrapWidth = dialogWidth - 4; // dialog border + a little padding

        var dialog = new Dialog
        {
            Title = "Log entry  (Esc to close)",
            Width = dialogWidth,
            Height = dialogHeight,
        };

        // Word-wrapped lines in a scrollable list (vertical scroll only; no TextView).
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(1) };
        list.SetSource(new ObservableCollection<string>(WrapText(message, wrapWidth)));
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        var closeButton = new Button { Text = "Close", ShadowStyle = ShadowStyles.None };
        closeButton.Accepting += (_, e) => { e.Handled = true; Application.RequestStop(dialog); };

        dialog.KeyDown += (_, key) =>
        {
            if ((key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask)) == KeyCode.Esc)
            {
                key.Handled = true;
                Application.RequestStop(dialog);
            }
        };

        dialog.Add(list);
        dialog.AddButton(closeButton);

        try { Application.Run(dialog); }
        finally { dialog.Dispose(); }
    }

    /// <summary>Greedy word-wrap to <paramref name="width"/> columns, hard-splitting over-long tokens.</summary>
    private static List<string> WrapText(string text, int width)
    {
        if (width < 1)
            width = 1;

        var lines = new List<string>();
        foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n'))
        {
            if (rawLine.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = new StringBuilder();
            foreach (var word in rawLine.Split(' '))
            {
                var w = word;
                while (w.Length > width)
                {
                    if (current.Length > 0) { lines.Add(current.ToString()); current.Clear(); }
                    lines.Add(w[..width]);
                    w = w[width..];
                }

                if (current.Length == 0)
                    current.Append(w);
                else if (current.Length + 1 + w.Length <= width)
                    current.Append(' ').Append(w);
                else { lines.Add(current.ToString()); current.Clear(); current.Append(w); }
            }

            if (current.Length > 0)
                lines.Add(current.ToString());
        }

        return lines;
    }

    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            var root = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
            _logger.LogError(root, "Action failed: {Message}", root.Message);
        }
    }

    private static void RemoveTimer(ref object? token)
    {
        if (token != null)
        {
            Application.RemoveTimeout(token);
            token = null;
        }
    }
}
