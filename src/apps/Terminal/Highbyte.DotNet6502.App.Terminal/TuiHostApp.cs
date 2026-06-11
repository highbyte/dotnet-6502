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

// TextView is marked obsolete in favour of the separate gui-cs/Editor add-on (not referenced here).
// A read-only multi-line text box is exactly what the Logs pane needs, so keep using TextView.
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
    private Window _window = default!;
    private FrameView _screenFrame = default!;
    private EmulatorScreenView _screenView = default!;
    // Last screen-frame size applied to fit the running system (incl. the 1-cell border on each side).
    private int _appliedFrameWidth = -1;
    private int _appliedFrameHeight = -1;
    private Button _systemButton = default!;
    private Button _variantButton = default!;
    private Label _statusLabel = default!;
    private TextView _logsView = default!;
    private Button _startButton = default!;
    private Button _pauseButton = default!;
    private Button _stopButton = default!;
    private Button _resetButton = default!;

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
            UpdateStatus();
            RefreshLogs();
            UpdateButtonStates();

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
            Title = "DotNet 6502 — Terminal Host   (Ctrl-Q to quit)",
            BorderStyle = LineStyle.Single,
        };

        // --- Control bar (top) ---
        // System + variant pickers (only changeable while the emulator is stopped).
        _systemButton = new Button { X = 1, Y = 0, Text = "System: - ▸ (F2)" };
        _variantButton = new Button { X = Pos.Right(_systemButton) + 2, Y = 0, Text = "Variant: - ▸ (F3)" };
        _systemButton.Accepting += (_, e) => { e.Handled = true; CycleSystem(); };
        _variantButton.Accepting += (_, e) => { e.Handled = true; CycleVariant(); };

        _startButton = new Button { X = 1, Y = 1, Text = "_Start (F5)" };
        _pauseButton = new Button { X = Pos.Right(_startButton) + 1, Y = 1, Text = "_Pause (F6)" };
        _stopButton = new Button { X = Pos.Right(_pauseButton) + 1, Y = 1, Text = "S_top (F7)" };
        _resetButton = new Button { X = Pos.Right(_stopButton) + 1, Y = 1, Text = "_Reset (F8)" };
        var quitButton = new Button { X = Pos.Right(_resetButton) + 1, Y = 1, Text = "_Quit (F10)" };

        _startButton.Accepting += (_, e) => { e.Handled = true; DoStart(); };
        _pauseButton.Accepting += (_, e) => { e.Handled = true; DoPause(); };
        _stopButton.Accepting += (_, e) => { e.Handled = true; DoStop(); };
        _resetButton.Accepting += (_, e) => { e.Handled = true; DoReset(); };
        quitButton.Accepting += (_, e) => { e.Handled = true; RequestQuit(); };

        // --- Emulator screen (left) ---
        // Width/Height are resized to fit the running system's frame (see ResizeScreenFrameToFit),
        // so the bordered box hugs the screen for any system (C64 ~52, VIC-20 ~32 wide) and the
        // status/logs panes (anchored to its right) reflow to use the reclaimed space.
        _screenFrame = new FrameView
        {
            Title = "Screen",
            X = 0,
            Y = 3,
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
        _screenView.HotkeyPressed += OnHotkey;
        _screenFrame.Add(_screenView);

        // --- Status (right top) ---
        var statusFrame = new FrameView
        {
            Title = "Status",
            X = Pos.Right(_screenFrame),
            Y = 3,
            Width = Dim.Fill(),
            Height = 9,
            BorderStyle = LineStyle.Single,
        };
        _statusLabel = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        statusFrame.Add(_statusLabel);

        // --- Logs (right bottom) ---
        var logsFrame = new FrameView
        {
            Title = "Logs",
            X = Pos.Right(_screenFrame),
            Y = Pos.Bottom(statusFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.Single,
        };
        _logsView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
        };
        logsFrame.Add(_logsView);

        // --- Bottom hint line ---
        var hintLabel = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Text = " F2 System  F3 Variant  F5 Start  F6 Pause  F7 Stop  F8 Reset  F10/Ctrl-Q Quit  Tab Focus",
        };

        _window.Add(_systemButton, _variantButton, _startButton, _pauseButton, _stopButton, _resetButton, quitButton);
        _window.Add(_screenFrame, statusFrame, logsFrame, hintLabel);
    }

    // ----------------------------------------------------------------------
    // Button / hotkey actions
    // ----------------------------------------------------------------------

    private void OnHotkey(Key key)
    {
        var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);
        switch (code)
        {
            case KeyCode.F2: CycleSystem(); break;
            case KeyCode.F3: CycleVariant(); break;
            case KeyCode.F5: DoStart(); break;
            case KeyCode.F6: DoPause(); break;
            case KeyCode.F7: DoStop(); break;
            case KeyCode.F8: DoReset(); break;
            case KeyCode.F10: RequestQuit(); break;
        }
    }

    private void CycleSystem() => Safe(() =>
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
        var next = names[(idx + 1) % names.Count];
        SelectSystem(next).Wait();

        UpdateSystemSelectors();
        UpdateButtonStates();
    });

    private void CycleVariant() => Safe(() =>
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
        var next = variants[(idx + 1) % variants.Count];
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
    });

    private void DoStop() => Safe(() =>
    {
        Stop();
        UpdateButtonStates();
        UpdateStatus();
    });

    private void DoReset() => Safe(() =>
    {
        Reset().Wait();
        UpdateButtonStates();
        _screenView.SetFocus();
    });

    private void RequestQuit()
    {
        Application.RequestStop(_window);
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

        StartEmulatorTimer();
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
        UpdateStatus();
        RefreshLogs();
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
        _systemButton.Text = $"System: {SelectedSystemName} ▸ (F2)";
        _variantButton.Text = $"Variant: {variant} ▸ (F3)";
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
    }

    private void UpdateStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"State : {EmulatorState}");

        var system = CurrentRunningSystem;
        if (system != null)
        {
            sb.AppendLine($"System: {system.Name}");
            sb.AppendLine($"PC    : 0x{system.CPU.PC:X4}");
            sb.AppendLine($"Screen: {system.Screen.RefreshFrequencyHz:0.0} Hz");
        }

        foreach (var (name, stat) in GetStats())
        {
            if (name.EndsWith("OnUpdateFPS", StringComparison.Ordinal))
            {
                sb.AppendLine($"FPS   : {stat.GetDescription()}");
                break;
            }
        }

        _statusLabel.Text = sb.ToString();
    }

    private void RefreshLogs()
    {
        var messages = _logStore.GetLogMessages();
        if (messages.Count == _lastLogCount)
            return;
        _lastLogCount = messages.Count;

        // The store inserts newest-first; show newest at the top.
        _logsView.Text = string.Join('\n', messages);
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
