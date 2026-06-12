using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Highbyte.DotNet6502.Impl.Terminal;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Highbyte.DotNet6502.Utils;
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

    // Resolves the optional per-system menu control (contributed by a shell plugin) for a given
    // system name. Returns null for systems with no contribution. Results are cached per system.
    private readonly Func<string, ITerminalMenuContribution?> _resolveMenuContribution;
    private readonly Dictionary<string, ITerminalMenuContribution?> _menuContributionCache = new(StringComparer.OrdinalIgnoreCase);
    private FrameView _controlsFrame = default!;       // left controls column (focus target when entering UI mode)
    private FrameView _systemMenuFrame = default!;     // container in the controls column for the active contribution
    private ITerminalMenuContribution? _activeMenuContribution;

    // Resolves the optional per-system info panel (contributed by a shell plugin); shown in the Info tab.
    private readonly Func<string, ITerminalInfoContribution?> _resolveInfoContribution;
    private readonly Dictionary<string, ITerminalInfoContribution?> _infoContributionCache = new(StringComparer.OrdinalIgnoreCase);
    private ITerminalInfoContribution? _activeInfoContribution;

    private TerminalInputHandlerContext _inputContext = default!;
    private TerminalRenderLoop? _renderLoop;

    // UI
    private const int DefaultScreenFrameWidth = 52; // until a system frame size is known (fits a C64, border included)
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

    // Tabbed area (Info | Config | Logs) below the Stats box.
    // Enum order must match the TabStripView label order (the strip maps tab index <-> InfoTab).
    private enum InfoTab { Info, Config, Logs }
    private InfoTab _activeInfoTab = InfoTab.Info;
    private Scheme? _uiScheme;                     // main UI scheme, reused by dialogs for a consistent look
    private FrameView _tabsFrame = default!;       // container for the tab strip + per-tab content
    private TabStripView _infoTabStrip = default!;
    private ListView _logsListView = default!;    // scrollable log list (like the other host apps)
    private List<string> _logRows = new();        // backing rows for the log list (for the detail popup)
    private Label _configLabel = default!;
    private Label _infoLabel = default!;           // Info-tab fallback shown when the system has no info panel
    private Button _startButton = default!;
    private Button _pauseButton = default!;
    private Button _stopButton = default!;
    private Button _resetButton = default!;
    private Button _monitorButton = default!;
    private Button _statsButton = default!;
    private Button _quitButton = default!;
    private Label _hintLabel = default!;           // bottom hint line; flips to a command menu in host mode

    // Whether the right "Stats" box shows instrumentation (toggled by the Stats button / host menu).
    private bool _statsEnabled;

    // Host-command "leader key" support. Terminals/window managers grab most keys (F10 = menu,
    // F11 = fullscreen, Ctrl/Alt/Shift = emulator), so host actions hang off a single reliable
    // leader key (default F9, configurable via EmulatorConfig.LeaderKey). Pressing it enters host
    // mode; the next keystroke selects a command (see OnGlobalKeyDown). This keeps every other key
    // free for the emulated system.
    private KeyCode _leaderKeyCode = KeyCode.F9;
    private bool _hostModeActive;

    // Focus modes. While the emulator screen has focus ("Emulator mode") every key goes to the
    // machine and Terminal.Gui's Tab/arrow navigation is unavailable; moving focus to any host control
    // ("UI mode") restores native keyboard navigation across all controls. The leader key (F9, then
    // Tab) toggles between the two, since it is the one key that reaches the host even while running.
    // The current mode is shown in the hint line and by dimming the host-UI borders in Emulator mode.
    private Scheme? _dimBorderScheme;               // greyed panel scheme (incl. border) shown in Emulator mode
    private readonly List<View> _uiPanels = new();  // host-UI frames whose scheme recolors by focus mode

    // Machine-code monitor: created lazily on first open while a system runs (bound to the current
    // SystemRunner) and discarded on stop. While the monitor dialog is open, _monitorActive halts the
    // background emulator loop so the monitor has exclusive access to the CPU/memory.
    private TuiMonitor? _monitor;
    private volatile bool _monitorActive;

    // Emulator loop runs off the Terminal.Gui UI thread so emulation can stay at native refresh
    // while terminal repaint remains independently throttled.
    private readonly object _emulatorLoopSync = new();
    private CancellationTokenSource? _emulatorLoopCts;
    private Task? _emulatorLoopTask;

    // Timers (Terminal.Gui timeout tokens)
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
        DotNet6502InMemLogStore logStore,
        Func<string, ITerminalMenuContribution?>? resolveMenuContribution = null,
        Func<string, ITerminalInfoContribution?>? resolveInfoContribution = null)
        : base("Terminal", systemList, loggerFactory, useStatsNamePrefix: false)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(nameof(TuiHostApp));
        _emulatorConfig = emulatorConfig;
        _logStore = logStore;
        _resolveMenuContribution = resolveMenuContribution ?? (_ => null);
        _resolveInfoContribution = resolveInfoContribution ?? (_ => null);
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
        // quit the host (Quit is a leader-key command), and the emulator maps Esc to RUN/STOP, so remove
        // that binding. The log-entry popup still closes on Esc via its own key handler.
        Application.KeyBindings.Remove(Key.Esc);
        // Host commands hang off a single leader key (default F9), handled app-wide so it works
        // regardless of which control has focus. Kept off F1–F8 (the emulated systems use those) and
        // off F10/F11 (terminals bind those to their own menu/fullscreen). See OnGlobalKeyDown.
        _leaderKeyCode = ParseLeaderKey(_emulatorConfig.LeaderKey);
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
            RefreshSystemMenu();
            RefreshSystemInfo();
            // Default to the Info tab, but surface a bad config immediately by opening the Config
            // tab when the startup config is invalid.
            SelectInfoTab(IsCurrentConfigValid() ? InfoTab.Info : InfoTab.Config);

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
            StopEmulatorLoop();
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
            Title = $"DotNet 6502 — Terminal Host   ({LeaderKeyName} = Menu)",
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
        _controlsFrame = controlsFrame;

        // Layout (mirrors the other host apps' menu column):
        //   System: <name>
        //   Variant: <name>
        //   Status: <state>
        //
        //   Start    Pause
        //   Reset    Stop
        //   Monitor  Stats
        //   Quit
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
        // Quit always available via a button (not just the leader-key Q command), since terminals
        // offer no clickable window close control and Esc is reserved for the emulator (RUN/STOP).
        _quitButton = new Button { X = 0, Y = 7, Text = "Quit" };

        _startButton.Accepting += (_, e) => { e.Handled = true; DoStart(); };
        _pauseButton.Accepting += (_, e) => { e.Handled = true; DoPause(); };
        _stopButton.Accepting += (_, e) => { e.Handled = true; DoStop(); };
        _resetButton.Accepting += (_, e) => { e.Handled = true; DoReset(); };
        _statsButton.Accepting += (_, e) => { e.Handled = true; ToggleStats(); };
        // Monitor button and the leader-key Monitor command both open the machine-code monitor
        // (enabled once a system is running).
        _monitorButton.Accepting += (_, e) => { e.Handled = true; DoMonitor(); };
        _quitButton.Accepting += (_, e) => { e.Handled = true; Application.RequestStop(_window); };

        // Disable Terminal.Gui's default button drop-shadow: in this dense column the shadows of
        // adjacent buttons are mostly overlapped by neighbours, leaving stray black stripes.
        foreach (var button in new[]
                 {
                     _systemButton, _variantButton, _startButton, _pauseButton,
                     _resetButton, _stopButton, _monitorButton, _statsButton, _quitButton,
                 })
        {
            button.ShadowStyle = ShadowStyles.None;
        }

        controlsFrame.Add(
            _systemButton, _variantButton, _leftStatusLabel,
            _startButton, _pauseButton, _resetButton, _stopButton, _monitorButton, _statsButton,
            _quitButton);

        // Per-system menu area (below the standard controls). A discovered system plugin may
        // contribute a system-specific control here (see ITerminalMenuContribution); systems without
        // one leave this frame hidden. Filled/cleared by RefreshSystemMenu on system change.
        _systemMenuFrame = new FrameView
        {
            Title = string.Empty,
            X = 0,
            Y = 8, // below the Monitor/Stats button row (Y=6)
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.Single,
            Visible = false,
        };
        controlsFrame.Add(_systemMenuFrame);

        // --- Emulator screen (middle) ---
        // Width/Height are resized to fit the running system's frame (see ResizeScreenFrameToFit), so
        // the box hugs the screen for any system (C64 ~52, VIC-20 ~32 wide) and the status/logs column
        // (anchored to its right) follows. The container is intentionally borderless: the emulated
        // systems draw their own coloured screen border, so a titled FrameView box around it would be
        // a redundant second border — and dropping it reclaims 2 rows so the C64 fits a default
        // terminal (see VerticalBorderRows for trimming the emulated border itself).
        _screenFrame = new FrameView
        {
            Title = string.Empty,
            X = Pos.Right(controlsFrame),
            Y = 0,
            Width = DefaultScreenFrameWidth,
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.None,
        };
        _screenView = new EmulatorScreenView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            VerticalBorderRows = _emulatorConfig.VerticalBorderRows,
            HorizontalBorderColumns = _emulatorConfig.HorizontalBorderColumns,
        };
        _screenView.EmulatorKeyPressed += key => _inputContext.OnKeyDown(key);
        // Keep the emulator screen out of the Tab ring: in UI mode, Tab then cycles only the host
        // controls and never lands back on the screen and re-traps the keyboard. Focusing the screen
        // ("Emulator mode") stays an explicit action (Start/Resume, or the F9-Tab toggle). The screen
        // can still be focused via SetFocus()/mouse — NoStop only removes it from Tab navigation.
        _screenView.TabStop = TabBehavior.NoStop;
        // Recompute the mode indicator + border colours whenever focus enters or leaves the screen
        // (covers the F9-Tab toggle, Start/Resume forcing focus, and mouse clicks).
        _screenView.HasFocusChanged += (_, _) => UpdateFocusModeVisuals();
        _screenFrame.Add(_screenView);

        // --- Stats / Logs column (right), same width as the controls column ---
        // The Stats box content is toggled by the Stats button (see ToggleStats / UpdateStatsBox).
        var statsFrame = new FrameView
        {
            Title = "Stats",
            X = Pos.Right(_screenFrame),
            Y = 0,
            // Fill the remaining width up to the window's right border (anchored at the screen frame's
            // right edge). The screen frame is resized to hug the running system, so this column soaks
            // up whatever horizontal space is left instead of leaving a fixed-width gap.
            Width = Dim.Fill(),
            Height = 12,
            BorderStyle = LineStyle.Single,
        };
        _statsLabel = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        statsFrame.Add(_statsLabel);

        // --- Tabbed area below Stats: Logs | Config | Info ---
        // Terminal.Gui 2.4.5 has no TabView, so this is a small hand-rolled tab strip (TabStripView):
        // all titles fit on one line (Y=0), with a content area (Y=1+) where only the active tab's
        // view is visible. The narrow side column can't fit stock Buttons' chrome for 3+ tabs.
        _tabsFrame = new FrameView
        {
            Title = string.Empty,
            X = Pos.Right(_screenFrame),
            Y = Pos.Bottom(statsFrame),
            Width = Dim.Fill(), // fill to the window's right border, like the Stats box above
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.Single,
        };

        _infoTabStrip = new TabStripView("Info", "Config", "Logs") { X = 0, Y = 0 };
        _infoTabStrip.TabSelected += index => SelectInfoTab((InfoTab)index);

        // Logs: a scrollable ListView (one row per message), like the other host apps' log list.
        // (ListView is lighter than TextView for frequently-rebuilt logs and inherits the UI scheme.)
        _logsListView = new ListView { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() };
        _logsListView.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        // Enter on a log row opens a popup with the full (word-wrapped) entry — the narrow pane clips
        // long lines, and a detail popup is the common TUI way to read the whole entry.
        _logsListView.Accepting += (_, e) => { e.Handled = true; ShowSelectedLogEntry(); };
        // Config: short read-only text — Label (inherits the surrounding scheme, like Stats).
        _configLabel = new Label { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(), Text = "", Visible = false };
        // Info: the active system's info panel is shown here (added/removed by RefreshSystemInfo).
        // This label is only the fallback for systems that contribute no info panel.
        _infoLabel = new Label
        {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
            Text = "No system-specific info.", Visible = false,
        };
        _tabsFrame.Add(_infoTabStrip, _logsListView, _configLabel, _infoLabel);

        // --- Bottom hint line ---
        // Doubles as the host-command menu: shows the leader-key hint normally, and the available
        // commands while host mode is active (see UpdateHintLine / OnGlobalKeyDown).
        _hintLabel = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
        };
        UpdateHintLine();

        _window.Add(controlsFrame, _screenFrame, statsFrame, _tabsFrame, _hintLabel);

        // Host-UI panels whose scheme (border + contents) reflects the focus mode: dimmed in Emulator
        // mode (keyboard goes to the machine), normal in UI mode. The screen frame is excluded — it is
        // the active area in Emulator mode and is borderless anyway.
        _uiPanels.AddRange(new View[] { controlsFrame, _systemMenuFrame, statsFrame, _tabsFrame });

        // Tab-group structure for keyboard navigation. Terminal.Gui scopes focus per frame: Tab cannot
        // cross from one frame into another (verified empirically), so each area is a TabGroup and
        // F6/Shift-F6 (NextTabGroup/PrevTabGroup) move *between* areas while Tab/Shift-Tab cycle the
        // controls *within* the focused area. The areas: the Controls column, the per-system menu (e.g.
        // the C64 menu), and the tabbed pane.
        controlsFrame.TabStop = TabBehavior.TabGroup;
        _tabsFrame.TabStop = TabBehavior.TabGroup;
        // The per-system menu is a sub-frame of the Controls column but a *separate* focus area (Tab
        // can't reach it from the Controls buttons — only F6 can). Keep it a TabGroup so F6 includes it.
        // Its contribution view also needs CanFocus=true (set in RefreshSystemMenu) so F6 lands on the
        // first button rather than the empty frame title.
        _systemMenuFrame.CanFocus = true;
        _systemMenuFrame.TabStop = TabBehavior.TabGroup;
        // Stats holds no focusable controls — keep it out of the F6 ring so it isn't an empty stop.
        statsFrame.TabStop = TabBehavior.NoStop;
        // Exclude the emulator screen (and its borderless frame) from F6: it is entered deliberately via
        // Start/Resume or F9-Tab, not by cycling areas. Without this, F6 dumps focus onto the screen and
        // strands the keyboard (no UI element focused, so F6 can't get back).
        _screenFrame.TabStop = TabBehavior.NoStop;

        // Make focus/hover indication a background change (keeping text readable) instead of the
        // theme default, which flips button text to near-black — unreadable on the dark window.
        // Applied window-wide so buttons and the tab strip stay consistent (children inherit it).
        ApplyReadableFocusScheme(_window);

        // Apply the initial mode visuals (UI mode at startup: nothing has forced focus to the screen).
        UpdateFocusModeVisuals();
    }

    /// <summary>
    /// Derive a scheme from <paramref name="view"/>'s current one where the focus/hover/active roles
    /// indicate selection with a distinct background and bright text, rather than the theme default
    /// that renders focused/hovered button text near-black (illegible on the dark window background).
    /// Caches the derived scheme in <see cref="_uiScheme"/> so dialogs can reuse the same look.
    /// </summary>
    private void ApplyReadableFocusScheme(View view)
    {
        var baseScheme = view.GetScheme();
        if (baseScheme is null)
            return;

        var selected = new global::Terminal.Gui.Drawing.Attribute(new Color(0xFF, 0xFF, 0xFF), new Color(0x2C, 0x5A, 0xA0));
        // Editable text fields (e.g. the ROM directory/file inputs in the C64 config dialog) default
        // to a light-gray background with white text — poor contrast. Use a dark-gray background with
        // light text instead, in keeping with the dark UI.
        var editable = new global::Terminal.Gui.Drawing.Attribute(new Color(0xE6, 0xE6, 0xE6), new Color(0x3A, 0x3A, 0x3A));
        // Panel/window background. The default theme's Normal role uses a *named* ANSI color, which
        // each terminal resolves through its own 16-color palette: macOS Terminal renders it as the
        // intended dark grey/blue, but Windows Terminal's default palette maps it to (near-)black.
        // Pin it to explicit RGB (like the emulator screen cells) so panels look identical on every
        // terminal regardless of palette. HotNormal keeps the same background with an accent text
        // colour for hotkey letters; Disabled dims the text on the same background.
        var normal = new global::Terminal.Gui.Drawing.Attribute(new Color(0xCC, 0xCC, 0xCC), new Color(0x1E, 0x24, 0x33));
        var hotNormal = new global::Terminal.Gui.Drawing.Attribute(new Color(0x6F, 0xA8, 0xFF), new Color(0x1E, 0x24, 0x33));
        var disabled = new global::Terminal.Gui.Drawing.Attribute(new Color(0x77, 0x7C, 0x88), new Color(0x1E, 0x24, 0x33));
        _uiScheme = baseScheme with
        {
            Normal = normal,
            HotNormal = hotNormal,
            Disabled = disabled,
            Focus = selected,
            HotFocus = selected,
            Active = selected,
            HotActive = selected,
            Highlight = selected,
            Editable = editable,
            ReadOnly = editable,
        };
        // Dimmed variant for host-UI panels while in Emulator mode: border line, title and panel text
        // use the grey "disabled" attribute so the panels read as inactive (keyboard goes to the
        // emulator). Terminal.Gui 2.4.5 ties the border colour to the panel's own scheme (the rendering
        // BorderView is internal), so this dims the whole panel rather than the border lines alone.
        _dimBorderScheme = _uiScheme with { Normal = disabled, HotNormal = disabled };
        view.SetScheme(_uiScheme);
    }

    /// <summary>
    /// Apply the main UI scheme (dark panels, light text, readable focus) to a view — used so modal
    /// dialogs match the main window instead of Terminal.Gui's low-contrast default "Dialog" theme.
    /// </summary>
    public void ApplyUiScheme(View view)
    {
        if (_uiScheme != null)
            view.SetScheme(_uiScheme);
    }

    // ----------------------------------------------------------------------
    // Button / hotkey actions
    // ----------------------------------------------------------------------

    /// <summary>
    /// Application-wide key handler so host commands work no matter which control is focused. A single
    /// "leader" key (default F9, configurable) enters host mode; the next keystroke selects a command.
    /// This keeps F1–F8 free for the emulated systems (e.g. the C64) and avoids F10/F11, which
    /// terminals bind to their own menu/fullscreen. Unhandled keys fall through to the focused view
    /// (so the emulator still receives F1–F8 and typing).
    /// </summary>
    private void OnGlobalKeyDown(object? sender, Key key)
    {
        // While a modal dialog (or any other top-level) is the running top, host commands must not
        // fire — every key has to reach the dialog so its fields and buttons work (e.g. typing into a
        // config text field, or Esc closing only the dialog).
        if (!ReferenceEquals(Application.TopRunnableView, _window))
            return;

        var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);

        // Host mode is armed: this keystroke is a command (or a cancel). Handle it before anything
        // else so a command letter never leaks through to the emulator.
        if (_hostModeActive)
        {
            HandleHostCommand(code);
            key.Handled = true;
            return;
        }

        var stopped = EmulatorState == EmulatorState.Uninitialized;

        if (!stopped && IsEmulatorScreenFocused() && IsEmulatorGlobalInputKey(key, code))
        {
            _inputContext.OnKeyDown(key);
            key.Handled = true;
            return;
        }

        // The leader key arms host mode; the next keystroke (HandleHostCommand) is the actual command.
        if (code == _leaderKeyCode)
        {
            _hostModeActive = true;
            UpdateHintLine();
            key.Handled = true;
            return;
        }

        // Not the leader — let it reach the focused view (e.g. the emulator).
    }

    /// <summary>
    /// Dispatch the second keystroke of a leader-key sequence to a host command, then leave host mode.
    /// Unknown keys (including the leader key again, or Esc) simply cancel without doing anything.
    /// </summary>
    private void HandleHostCommand(KeyCode code)
    {
        _hostModeActive = false;
        UpdateHintLine();

        var stopped = EmulatorState == EmulatorState.Uninitialized;
        switch (code)
        {
            case KeyCode.S: DoStartStopToggle(); break;
            case KeyCode.M: DoMonitor(); break;
            case KeyCode.T: ToggleStats(); break;
            case KeyCode.Q: Application.RequestStop(_window); break;
            // Tab and F6 both toggle the keyboard between the emulator and the host UI. F6 is accepted
            // because it is the "move between areas" key in the UI, so reaching for it to leave the
            // running emulator is a natural instinct (it can't be used directly there — while the screen
            // is focused F6 goes to the emulated machine, so the leader key is the way out).
            case KeyCode.Tab:
            case KeyCode.F6: ToggleFocusMode(); break;

            // System/Variant cycling is only meaningful while the emulator is stopped.
            case KeyCode.Y when stopped: CycleSystem(+1); break;
            case KeyCode.V when stopped: CycleVariant(+1); break;

            default: break; // unknown command (or Esc / leader again): cancel, no-op
        }
    }

    /// <summary>
    /// Toggle between Emulator mode (screen focused, keys go to the machine) and UI mode (focus on the
    /// host controls, so native Tab/arrow navigation works). The leader-key Tab command, plus a focus
    /// change, are the only ways to switch; the resulting focus change drives <see
    /// cref="UpdateFocusModeVisuals"/> via the screen's HasFocusChanged hook.
    /// </summary>
    private void ToggleFocusMode()
    {
        if (_screenView.HasFocus)
            FocusHostUi();
        else
            _screenView.SetFocus();
    }

    /// <summary>
    /// Move focus to the host UI's Controls column. Focuses the *frame* (not a specific button) so
    /// Terminal.Gui lands on the first enabled control: focusing a fixed button fails silently when
    /// that button is disabled for the current state (e.g. System/Start are disabled while running),
    /// which previously made F9-Tab do nothing mid-run. From here Tab/F6/arrows reach every control.
    /// </summary>
    private void FocusHostUi() => _controlsFrame.SetFocus();

    /// <summary>True while the emulator screen holds focus (keys go to the emulated machine).</summary>
    private bool InEmulatorMode => _screenView is not null && _screenView.HasFocus;

    /// <summary>
    /// Reflect the current focus mode: update the hint-line indicator and dim the host-UI panels in
    /// Emulator mode (so it is obvious the keyboard is going to the machine, not the UI).
    /// </summary>
    private void UpdateFocusModeVisuals()
    {
        var panelScheme = InEmulatorMode ? _dimBorderScheme : _uiScheme;
        if (panelScheme is not null)
        {
            foreach (var panel in _uiPanels)
            {
                panel.SetScheme(panelScheme);
                panel.SetNeedsDraw();
            }
        }
        UpdateHintLine();
    }

    /// <summary>
    /// Bottom hint line: shows the focus-mode indicator plus the leader-key reminder normally, and the
    /// command menu while host mode is armed (so the available commands are visible after the leader key).
    /// </summary>
    private void UpdateHintLine()
    {
        if (_hintLabel is null)
            return;

        // Normal: a mode indicator + a short reminder, so it's obvious which mode is active and that
        // host mode is *not* armed. The full command list appears only once the leader key arms it.
        // In UI mode also advertise the navigation keys (Tab within an area, F6 between areas).
        if (_hostModeActive)
            _hintLabel.Text =
                $" HOST: S Start/Stop  M Monitor  T Stats  Q Quit  Y System  V Variant  Tab/F6 Emu⇄UI   (Esc cancels)";
        else if (InEmulatorMode)
            _hintLabel.Text = $" [EMULATOR]  {LeaderKeyName} Menu";
        else
            _hintLabel.Text = $" [UI NAV]  {LeaderKeyName} Menu   Tab move · F6 area";
    }

    /// <summary>Display name of the configured leader key (e.g. "F9").</summary>
    private string LeaderKeyName => _leaderKeyCode.ToString();

    /// <summary>
    /// Parse the configured leader key name (e.g. "F9") into a <see cref="KeyCode"/>. Falls back to F9
    /// on an empty or unrecognised value so the host always has a working command key.
    /// </summary>
    private KeyCode ParseLeaderKey(string? configured)
    {
        const KeyCode fallback = KeyCode.F9;
        if (string.IsNullOrWhiteSpace(configured))
            return fallback;

        if (Enum.TryParse<KeyCode>(configured.Trim(), ignoreCase: true, out var parsed))
            return parsed;

        _logger.LogWarning(
            "Unrecognised LeaderKey '{Configured}' in config; falling back to {Fallback}.",
            configured, fallback);
        return fallback;
    }

    private bool IsEmulatorScreenFocused()
        => ReferenceEquals(Application.Navigation?.GetFocused(), _screenView);

    private static bool IsEmulatorGlobalInputKey(Key key, KeyCode code)
        // Tab is otherwise used by Terminal.Gui focus navigation before the screen view sees it.
        // Ctrl/Alt/Shift chords are also forwarded here so Terminal.Gui command bindings do not
        // consume emulator combinations that terminals are able to report.
        => code == KeyCode.Tab || key.IsCtrl || key.IsAlt || key.IsShift;

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
        RefreshSystemMenu();
        RefreshSystemInfo();
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
        // The machine isn't consuming input while paused — hand the keyboard back to the host UI
        // (switches to UI mode: panels un-dim and Tab/arrow navigation works).
        FocusHostUi();
    });

    /// <summary>The Start/Stop command toggles between Start/Resume and Stop depending on the current state.</summary>
    private void DoStartStopToggle()
    {
        if (EmulatorState == EmulatorState.Running)
            DoStop();
        else
            DoStart();
    }


    /// <summary>
    /// Monitor (leader-key Monitor command / Monitor button): open the machine-code monitor against the running (or paused)
    /// system. Needs a built SystemRunner, so it is unavailable while the emulator is uninitialized.
    /// </summary>
    private void DoMonitor() => Safe(() =>
    {
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            _logger.LogInformation("Start a system before opening the monitor.");
            return;
        }

        EnsureMonitor();
        OpenMonitor(null);
    });

    /// <summary>Create the monitor (bound to the current SystemRunner) on first use, with a help banner.</summary>
    private void EnsureMonitor()
    {
        if (_monitor != null)
            return;

        var runner = CurrentSystemRunner;
        if (runner == null)
            return;

        // Resolve relative load/save paths against the app directory unless configured otherwise.
        _emulatorConfig.Monitor.DefaultDirectory ??= Environment.CurrentDirectory;

        _monitor = new TuiMonitor(runner, _emulatorConfig.Monitor, PickMonitorFile);
        _monitor.ShowDescription();
        _monitor.WriteOutput("");
        _monitor.WriteOutput("Type '?' for help, or '<command> -?' for help on a command.");
        _monitor.WriteOutput("Examples:  d   d c000   m c000   r   z   g");
        _monitor.WriteOutput("");
    }

    /// <summary>
    /// Show the monitor dialog (optionally with a break-trigger reason). Halts emulation while open,
    /// then resumes the previous run state on close (a 'g' command continues; Esc leaves it paused
    /// in effect until the dialog reopens — the background loop simply resumes where it left off).
    /// </summary>
    private void OpenMonitor(ExecEvaluatorTriggerResult? trigger)
    {
        if (_monitor == null)
            return;

        _monitor.Reset(); // re-anchor disassembly to PC
        if (trigger != null)
            _monitor.ShowInfoAfterBreakTriggerEnabled(trigger);
        _monitor.ShowCurrentInstruction();

        _monitorActive = true; // pause the background emulator loop while the dialog is open
        UpdateButtonStates();
        try
        {
            MonitorDialog.Show(this, _monitor);
        }
        finally
        {
            _monitorActive = false;
            UpdateButtonStates();
            if (EmulatorState != EmulatorState.Uninitialized)
                _screenView.SetFocus();
        }
    }

    /// <summary>
    /// Open a modal file picker for the monitor's picker-based load commands ('l' / 'lb'), rooted at
    /// the monitor's default directory. Returns the chosen path, or null if cancelled. Runs nested
    /// inside the monitor window's run loop (the picker becomes the running top while open).
    /// </summary>
    private string? PickMonitorFile(string title)
    {
        using var picker = new OpenDialog
        {
            Title = title,
            OpenMode = OpenMode.File,
            AllowsMultipleSelection = false,
        };
        ApplyUiScheme(picker);

        var startDir = PathHelper.ExpandOSEnvironmentVariables(_emulatorConfig.Monitor.DefaultDirectory ?? string.Empty);
        if (Directory.Exists(startDir))
            picker.Path = startDir.EndsWith(Path.DirectorySeparatorChar) ? startDir : startDir + Path.DirectorySeparatorChar;

        Application.Run(picker);

        if (picker.Canceled || picker.FilePaths.Count == 0)
            return null;
        return picker.FilePaths[0];
    }

    private void DoStop() => Safe(() =>
    {
        Stop();
        UpdateButtonStates();
        UpdateLeftStatus();
        UpdateStatsBox();
        // Emulator no longer running — return focus to the host UI (UI mode: panels un-dim, Tab works).
        FocusHostUi();
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
            ApplyScreenBorderTrim();
        }

        ApplyInstrumentationEnabled();
        UpdateLeftStatus();
    }

    /// <summary>
    /// Tell the screen view how thick the running system's cosmetic border is (in cells), so it crops
    /// it down to the configured VerticalBorderRows/HorizontalBorderColumns. Read from the system's own
    /// screen geometry (exact and constant for the run) rather than detected from rendered pixels —
    /// which wobbles while the system boots, briefly resizing the screen. Systems with no border (or no
    /// known character size) are shown untrimmed.
    /// </summary>
    private void ApplyScreenBorderTrim()
    {
        var screen = CurrentRunningSystem?.Screen;
        if (screen is not { HasBorder: true } || screen is not ITextMode textMode
            || textMode.CharacterWidth <= 0 || textMode.CharacterHeight <= 0)
        {
            _screenView.SetBorderThickness(0, 0);
            return;
        }

        var borderColumns = screen.VisibleLeftRightBorderWidth / textMode.CharacterWidth;
        var borderRows = screen.VisibleTopBottomBorderHeight / textMode.CharacterHeight;
        _screenView.SetBorderThickness(borderColumns, borderRows);
    }

    public override void OnAfterEmulatorStateChange()
    {
        if (_selfTestMode)
            return;
        if (EmulatorState == EmulatorState.Running)
            StartEmulatorLoop();
    }

    public override void OnAfterPause() => StopEmulatorLoop();

    public override void OnAfterStop()
    {
        StopEmulatorLoop();
        _screenView?.SetRenderTarget(null);
        // The monitor is bound to the now-discarded SystemRunner; drop it so the next run rebuilds it.
        _monitor = null;
        _monitorActive = false;
        if (!_selfTestMode)
            ResetScreenFrameSize();
    }

    /// <summary>
    /// Halt emulation while the monitor dialog owns the CPU. The background emulator loop calls this at
    /// the start of every frame; returning shouldRun=false makes it idle (still timing frames) until
    /// the monitor closes, so the monitor's step/go commands run without a racing emulation thread.
    /// </summary>
    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = !_monitorActive;
        shouldReceiveInput = !_monitorActive;
    }

    /// <summary>
    /// After each emulated frame, open the monitor if execution hit a breakpoint or other break
    /// trigger (mirrors the other host apps). Runs on the background emulator thread, so the actual
    /// dialog open is marshalled to the Terminal.Gui UI thread.
    /// </summary>
    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        if (_selfTestMode || _monitorActive || !execEvaluatorTriggerResult.Triggered)
            return;

        // Stop the loop immediately (this thread), then open the dialog on the UI thread.
        _monitorActive = true;
        Application.Invoke(() => OpenMonitor(execEvaluatorTriggerResult));
    }

    /// <summary>
    /// The host config changed (e.g. the C64 config dialog applied new ROM paths). Re-evaluate the
    /// Start button (now enabled/disabled by config validity) and refresh the Config tab.
    /// </summary>
    public override void OnAfterHostSystemConfigUpdated() => Safe(() =>
    {
        if (_selfTestMode)
            return;
        UpdateButtonStates();
        if (_activeInfoTab == InfoTab.Config)
            UpdateConfigView();
    });

    public override void QuitApplication()
    {
        // Called by base.Close(); ensure the Terminal.Gui loop stops.
        Application.Invoke(() => Application.RequestStop(_window));
    }

    // ----------------------------------------------------------------------
    // Emulator loop + UI timers. Display/status callbacks run on the Terminal.Gui main loop;
    // emulation runs on a background fixed-rate loop.
    // ----------------------------------------------------------------------

    private void StartEmulatorLoop()
    {
        StopEmulatorLoop();

        var hz = CurrentRunningSystem?.Screen.RefreshFrequencyHz ?? 50.0;
        var cts = new CancellationTokenSource();
        var task = Task.Run(() => RunEmulatorLoopAsync(hz, cts.Token));

        lock (_emulatorLoopSync)
        {
            _emulatorLoopCts = cts;
            _emulatorLoopTask = task;
        }
    }

    private void StopEmulatorLoop()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_emulatorLoopSync)
        {
            cts = _emulatorLoopCts;
            task = _emulatorLoopTask;
            _emulatorLoopCts = null;
            _emulatorLoopTask = null;
        }

        if (cts == null)
            return;

        cts.Cancel();
        try
        {
            if (task != null && !task.Wait(TimeSpan.FromSeconds(1)))
                _logger.LogWarning("Timed out waiting for terminal emulator loop to stop.");
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task RunEmulatorLoopAsync(double refreshHz, CancellationToken ct)
    {
        var intervalTicks = Math.Max(1L, (long)Math.Round(Stopwatch.Frequency / refreshHz));
        var nextFrameTicks = Stopwatch.GetTimestamp();

        while (!ct.IsCancellationRequested)
        {
            var now = Stopwatch.GetTimestamp();
            var ticksUntilNextFrame = nextFrameTicks - now;
            if (ticksUntilNextFrame > 0)
            {
                var delay = TimeSpan.FromSeconds(ticksUntilNextFrame / (double)Stopwatch.Frequency);
                await Task.Delay(delay, ct);
                continue;
            }

            try
            {
                RunEmulatorOneFrame();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception running emulator frame.");
            }

            nextFrameTicks += intervalTicks;

            // If the host falls more than one frame behind, skip catch-up frames rather than running
            // a long burst that would make input and UI refresh feel stalled.
            var behindTicks = Stopwatch.GetTimestamp() - nextFrameTicks;
            if (behindTicks > intervalTicks)
                nextFrameTicks = Stopwatch.GetTimestamp() + intervalTicks;
        }
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
    /// Resizes the (borderless) "Screen" box to fit the running system's rendered frame, so it hugs
    /// the emulator screen (no dead space) for any system/variant. The status/logs panes anchored to
    /// its right reflow automatically. The box is borderless, so its size equals the painted cell size
    /// (which already has the border trim from <see cref="EmulatorScreenView.VerticalBorderRows"/> /
    /// <see cref="EmulatorScreenView.HorizontalBorderColumns"/> applied).
    /// </summary>
    private void ResizeScreenFrameToFit(int frameCellWidth, int frameCellHeight)
    {
        if (frameCellWidth <= 0 || frameCellHeight <= 0)
            return;

        var targetWidth = frameCellWidth;
        var targetHeight = frameCellHeight;
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

        // Start (or resume) when not already running — but never when the current config is invalid
        // (e.g. ROM files missing): the system can't be built, so disable Start and surface why in
        // the Config tab.
        _startButton.Enabled = !running && IsCurrentConfigValid();
        _pauseButton.Enabled = running;
        _stopButton.Enabled = !uninitialized;
        _resetButton.Enabled = !uninitialized;
        // Monitor needs a built SystemRunner, so it is available whenever a system is running/paused.
        _monitorButton.Enabled = !uninitialized;
        _statsButton.Text = _statsEnabled ? "Stats*" : "Stats";

        // Let the active system's menu contribution dim/enable its own controls for this state.
        _activeMenuContribution?.RefreshControlStates();
    }

    /// <summary>True if the selected system's current host config is valid (so it can be started).</summary>
    private bool IsCurrentConfigValid() => CurrentHostSystemConfig?.IsValid(out List<string> _) ?? false;

    /// <summary>Updates the "Status: &lt;state&gt;" line in the controls column.</summary>
    private void UpdateLeftStatus()
    {
        _leftStatusLabel.Text = $"Status: {EmulatorState}";
    }

    /// <summary>
    /// Updates the right "Stats" box. When stats are toggled on (Stats button / leader-key command) and a system is
    /// running, shows the host instrumentation list (FPS, per-frame timings, …) like the other host
    /// apps; otherwise a short hint.
    /// </summary>
    private void UpdateStatsBox()
    {
        if (!_statsEnabled)
        {
            _statsLabel.Text = $"(stats off — press Stats button or {LeaderKeyName} then T)";
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
    // Per-system menu contribution (controls column, below the standard controls)
    // ----------------------------------------------------------------------

    /// <summary>Resolve (and cache) the menu contribution for a system; null if it has none.</summary>
    private ITerminalMenuContribution? GetMenuContribution(string systemName)
    {
        if (!_menuContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = _resolveMenuContribution(systemName);
            _menuContributionCache[systemName] = contribution;
        }
        return contribution;
    }

    /// <summary>
    /// Show the selected system's menu contribution (if any) in the controls column, or hide the
    /// area when the system contributes none. Called at startup and whenever the system changes.
    /// </summary>
    private void RefreshSystemMenu() => Safe(() =>
    {
        if (_systemMenuFrame == null)
            return;

        var next = GetMenuContribution(SelectedSystemName);
        if (ReferenceEquals(next, _activeMenuContribution))
            return;

        if (_activeMenuContribution != null)
            _systemMenuFrame.Remove(_activeMenuContribution.View);

        _activeMenuContribution = next;

        if (next != null)
        {
            next.View.X = 0;
            next.View.Y = 0;
            next.View.Width = Dim.Fill();
            next.View.Height = Dim.Fill();
            // A contribution's root is a plain View (CanFocus defaults to false), which blocks focus
            // from descending to its buttons — so Tab would never reach the per-system menu. Make it
            // focus-permeable (CanFocus) but not a stop itself (NoStop), so its buttons join the
            // Controls column's tab ring and Tab cycles through them with the standard host buttons.
            next.View.CanFocus = true;
            next.View.TabStop = TabBehavior.NoStop;
            _systemMenuFrame.Add(next.View);
            _systemMenuFrame.Title = next.MenuTitle;
            _systemMenuFrame.Height = next.MenuRowCount + 2; // + top/bottom border
            _systemMenuFrame.Visible = true;
            next.RefreshControlStates(); // set initial enabled/disabled state for the current emulator state
        }
        else
        {
            _systemMenuFrame.Title = string.Empty;
            _systemMenuFrame.Visible = false;
        }

        _window.SetNeedsLayout();
    });

    // ----------------------------------------------------------------------
    // Tabbed area (Logs | Config | Info)
    // ----------------------------------------------------------------------

    private void SelectInfoTab(InfoTab tab) => Safe(() =>
    {
        _activeInfoTab = tab;

        _logsListView.Visible = tab == InfoTab.Logs;
        _configLabel.Visible = tab == InfoTab.Config;

        // Info tab: show the active system's info panel if it has one, otherwise the fallback label.
        var infoActive = tab == InfoTab.Info;
        if (_activeInfoContribution != null)
            _activeInfoContribution.View.Visible = infoActive;
        _infoLabel.Visible = infoActive && _activeInfoContribution == null;

        // Keep the strip's highlighted tab in sync (no-op when already active, e.g. when the strip
        // itself raised the change).
        _infoTabStrip.SetActive((int)tab);

        if (tab == InfoTab.Config)
            UpdateConfigView();

        _window.SetNeedsLayout();
    });

    /// <summary>Resolve (and cache) the info panel for a system; null if it has none.</summary>
    private ITerminalInfoContribution? GetInfoContribution(string systemName)
    {
        if (!_infoContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = _resolveInfoContribution(systemName);
            _infoContributionCache[systemName] = contribution;
        }
        return contribution;
    }

    /// <summary>
    /// Swap the Info-tab content to the selected system's info panel (a shell-plugin contribution),
    /// or fall back to a generic label when the system contributes none. Called at startup and on
    /// system change.
    /// </summary>
    private void RefreshSystemInfo() => Safe(() =>
    {
        if (_tabsFrame == null)
            return;

        var next = GetInfoContribution(SelectedSystemName);
        if (ReferenceEquals(next, _activeInfoContribution))
            return;

        if (_activeInfoContribution != null)
            _tabsFrame.Remove(_activeInfoContribution.View);

        _activeInfoContribution = next;

        if (next != null)
        {
            next.View.X = 0;
            next.View.Y = 1; // below the tab strip
            next.View.Width = Dim.Fill();
            next.View.Height = Dim.Fill();
            _tabsFrame.Add(next.View);
        }

        // Re-apply visibility for the currently active tab.
        SelectInfoTab(_activeInfoTab);
    });

    /// <summary>Config-status tab — config validity and the list of validation errors (if any).</summary>
    private void UpdateConfigView()
    {
        var sb = new StringBuilder();

        var hostConfig = CurrentHostSystemConfig;
        if (hostConfig == null)
        {
            sb.AppendLine("Config: (no system)");
        }
        else
        {
            var valid = hostConfig.IsValid(out var errors);
            sb.AppendLine($"Config: {(valid ? "valid" : "invalid")}");
            if (!valid)
            {
                sb.AppendLine();
                sb.AppendLine("Validation errors:");
                foreach (var error in errors)
                    sb.AppendLine($"  - {error}");
            }
        }

        _configLabel.Text = sb.ToString();
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
        ApplyUiScheme(dialog); // match the main UI instead of the low-contrast default dialog theme

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
