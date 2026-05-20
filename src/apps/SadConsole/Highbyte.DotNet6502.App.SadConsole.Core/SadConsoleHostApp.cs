using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Impl.NAudio.WavePlayers.SilkNetOpenAL;
using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SadConsole.Components;
using SadConsole.Configuration;
using SadConsole.Input;
using SadRogue.Primitives;
using Console = SadConsole.Console;

namespace Highbyte.DotNet6502.App.SadConsole.Core;

/// <summary>
/// Host app for running Highbyte.DotNet6502 emulator in a SadConsole Window
/// </summary>
public class SadConsoleHostApp : HostApp
{
    // --------------------
    // Injected variables
    // --------------------
    private new readonly ILogger _logger;
    private readonly EmulatorConfig _emulatorConfig;
    public EmulatorConfig EmulatorConfig => _emulatorConfig;

    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<string, ISadConsoleMenuContribution?> _resolveMenuContribution;
    private readonly Func<string, ISadConsoleInfoContribution?> _resolveInfoContribution;

    // System-specific SadConsole glyph/colour transforms are contributed by engine plug-ins
    // (Impl.SadConsole.<System>) via ISadConsoleRenderCustomizationPlugin.
    private readonly IReadOnlyList<ISadConsoleRenderCustomizationPlugin> _renderCustomizationPlugins;
    private readonly Dictionary<string, ISadConsoleMenuContribution?> _menuContributionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISadConsoleInfoContribution?> _infoContributionCache = new(StringComparer.OrdinalIgnoreCase);

    // --------------------
    // Other variables / constants
    // --------------------
    private ScreenObject? _sadConsoleScreen;

    private MenuConsole? _menuConsole;
    public MenuConsole MenuConsole => _menuConsole!;

    private Console? _systemMenuConsole;
    public Console? SystemMenuConsole => _systemMenuConsole;
    private ISadConsoleMenuContribution? _activeSystemMenuContribution;

    private EmulatorConsole? _sadConsoleEmulatorConsole;

    private MonitorConsole? _monitorConsole;
    //private MonitorConsole? _monitorConsole;
    private MonitorStatusConsole? _monitorStatusConsole;
    public event EventHandler<bool>? MonitorStateChange;
    protected virtual void OnMonitorStateChange(bool monitorEnabled)
    {
        var handler = MonitorStateChange;
        handler?.Invoke(this, monitorEnabled);
    }

    private SadConsoleInputHandlerContext _inputHandlerContext = default!;
    private NAudioAudioHandlerContext _audioHandlerContext = default!;
    private InfoConsole? _infoConsole;
    private const int MENU_POSITION_X = 0;
    private const int MENU_POSITION_Y = 0;

    private ErrorDialog? _currentErrorDialog;

    // Set to true once the quit-only startup-error screen is shown (any fatal error while
    // building the main screen). The per-frame update is then skipped.
    private bool _startupErrorScreenActive;

    private int StartupScreenWidth => MenuConsole.CONSOLE_WIDTH + 60;
    private int StartupScreenHeight => MenuConsole.CONSOLE_HEIGHT + 14;

    private const int STATS_UPDATE_EVERY_X_FRAME = 60 * 1;
    private const int DEBUGINFO_UPDATE_EVERY_X_FRAME = 10 * 1;

    private int _statsFrameCount = 0;
    private int _debugInfoFrameCount = 0;

    private const int LOGS_UPDATE_EVERY_X_FRAME = 60 * 1;
    private int _logsFrameCount = 0;
    private DrawImage? _logoDrawImage;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemList"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    public SadConsoleHostApp(
        SystemList systemList,
        ILoggerFactory loggerFactory,

        EmulatorConfig emulatorConfig,
        //IWindow window,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        Func<string, ISadConsoleMenuContribution?> resolveMenuContribution,
        Func<string, ISadConsoleInfoContribution?> resolveInfoContribution,
        IReadOnlyList<ISadConsoleRenderCustomizationPlugin> renderCustomizationPlugins)
        : base("SadConsole", systemList, loggerFactory)
    {
        //_window = window;
        _emulatorConfig = emulatorConfig;
        _logStore = logStore;
        _logConfig = logConfig;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(typeof(SadConsoleHostApp).Name);
        _resolveMenuContribution = resolveMenuContribution;
        _resolveInfoContribution = resolveInfoContribution;
        _renderCustomizationPlugins = renderCustomizationPlugins;
    }

    private void ConfigureFontsFromEmbeddedResources(FontConfig config, GameHost host)
    {
        // Use the default SadConsole built-in font if no UI font specified
        var uiFont = _emulatorConfig.UIFont;
        if (string.IsNullOrEmpty(uiFont))
        {
            config.UseBuiltinFont();
        }
        else
        {
            // Load custom UI font from embedded resources
            var font = EmbeddedResourceHelper.LoadFontFromEmbeddedResource(uiFont);
            var fontKey = Path.GetFileNameWithoutExtension(uiFont);
            host.Fonts[fontKey] = font;
            host.DefaultFont = font;
        }

        // Load emulator fonts from embedded resources
        var emulatorFonts = GetHostSystemConfigs()
            .OfType<ISadConsoleHostConfig>()
            .Where(x => !string.IsNullOrEmpty(x.Font))
            .Select(x => x.Font!)
            .Distinct()
            .ToArray();

        foreach (var fontPath in emulatorFonts)
        {
            var font = EmbeddedResourceHelper.LoadFontFromEmbeddedResource(fontPath);
            var fontKey = Path.GetFileNameWithoutExtension(fontPath);
            host.Fonts[fontKey] = font;
        }
    }

    public void Run()
    {
        _inputHandlerContext = CreateInputHandlerContext();
        _audioHandlerContext = CreateAudioHandlerContext();

        // Set up global exception handlers
        SetupGlobalExceptionHandlers();

        // ----------
        // Main SadConsole screen
        // ----------

        // CreateMainSadConsoleScreen catches any fatal startup error and returns a quit-only
        // error screen instead of the normal main screen.
        var builder = new Builder()
            .SetWindowSizeInCells(StartupScreenWidth, StartupScreenHeight)
            .ConfigureFonts(ConfigureFontsFromEmbeddedResources)
            .SetStartingScreen(CreateMainSadConsoleScreen)
            .IsStartingScreenFocused(false) // Let the object focused in the create method remain.
            .AddFrameUpdateEvent(UpdateSadConsole);

        // Start SadConsole window
        Game.Create(builder);

        Settings.WindowTitle = "DotNet 6502 Emulator + SadConsole (with NAudio)";
        Settings.ResizeMode = Settings.WindowResizeOptions.None;

        try
        {
            Game.Instance.Run();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in SadConsole Game.Instance.Run()");
            HandleGlobalException(ex);
        }
        finally
        {
            // Continues here after SadConsole window is closed
            Game.Instance.Dispose();
        }
    }

    private void SetupGlobalExceptionHandlers()
    {
        // Set up global exception handler for unhandled exceptions in other threads
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                _logger.LogError(exception, "Unhandled exception in AppDomain");
                HandleGlobalException(exception);
            }
        };

        // Set up handler for unhandled exceptions in tasks
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            _logger.LogError(e.Exception, "Unobserved task exception");
            HandleGlobalException(e.Exception);
            e.SetObserved(); // Prevent the process from terminating
        };
    }

    private void HandleGlobalException(Exception exception)
    {
        try
        {
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
            System.Console.WriteLine($"Exception: {exception.Message}");
            System.Console.WriteLine($"Stack trace: {exception.StackTrace}");

            // Pause emulator if it's running
            if (EmulatorState == EmulatorState.Running)
            {
                Pause();
            }

            // Show error dialog
            ShowErrorDialog(exception);
        }
        catch (Exception ex)
        {
            // Last resort: log the exception and exit
            _logger.LogCritical(ex, "Failed to handle global exception properly");
            Environment.Exit(1);
        }
    }

    private void ShowErrorDialog(Exception exception)
    {
        if (_currentErrorDialog != null && _currentErrorDialog.IsVisible)
        {
            // Dialog already showing, don't show another one
            return;
        }

        var errorMessage = "An unexpected error occurred in the application.";

        _currentErrorDialog = new ErrorDialog(errorMessage, exception);
        _currentErrorDialog.Closed += (sender, e) =>
        {
            if (_currentErrorDialog != null)
            {
                if (_currentErrorDialog.UserChoice == ErrorDialog.ErrorDialogResult.Exit)
                {
                    // User chose to exit - close the window which will exit the game loop
                    Settings.DoFinalDraw = false;
                    Game.Instance.MonoGameInstance.Exit();
                }
                else
                {
                    // User chose to continue - unpause emulator if it was paused
                    if (EmulatorState == EmulatorState.Paused)
                    {
                        _ = Start(); // Fire and forget
                    }
                }
                _currentErrorDialog = null;
            }
        };

        // Add the dialog to the screen and show it
        if (_sadConsoleScreen != null)
        {
            _sadConsoleScreen.Children.Add(_currentErrorDialog);
            _currentErrorDialog.Show();
            _currentErrorDialog.IsFocused = true;
        }
    }

    private void OnBeforeRender(double delta)
    {
    }
    private void OnAfterRender(double delta)
    {
        // Push logs to info console (logs should be updated on screen even if emulator is not running)
        _logsFrameCount++;
        if (_logsFrameCount >= LOGS_UPDATE_EVERY_X_FRAME)
        {
            _logsFrameCount = 0;
            GetInfoConsoleOrThrow().UpdateLogs();
        }
    }

    private EmulatorConsole GetEmulatorConsoleOrThrow()
    {
        return _sadConsoleEmulatorConsole ?? throw new DotNet6502Exception("SadConsole emulator console is not initialized.");
    }

    private ScreenObject GetSadConsoleScreenOrThrow()
    {
        return _sadConsoleScreen ?? throw new DotNet6502Exception("SadConsole screen is not initialized.");
    }

    private MenuConsole GetMenuConsoleOrThrow()
    {
        return _menuConsole ?? throw new DotNet6502Exception("SadConsole menu console is not initialized.");
    }

    private MonitorConsole GetMonitorConsoleOrThrow()
    {
        return _monitorConsole ?? throw new DotNet6502Exception("SadConsole monitor console is not initialized.");
    }

    private MonitorStatusConsole GetMonitorStatusConsoleOrThrow()
    {
        return _monitorStatusConsole ?? throw new DotNet6502Exception("SadConsole monitor status console is not initialized.");
    }

    private InfoConsole GetInfoConsoleOrThrow()
    {
        return _infoConsole ?? throw new DotNet6502Exception("SadConsole info console is not initialized.");
    }

    private DrawImage GetLogoDrawImageOrThrow()
    {
        return _logoDrawImage ?? throw new DotNet6502Exception("SadConsole logo image is not initialized.");
    }

    private ISystem GetCurrentRunningSystemOrThrow()
    {
        return CurrentRunningSystem ?? throw new DotNet6502Exception("No system is currently running.");
    }

    private ISadConsoleMenuContribution? GetMenuContribution(string systemName)
    {
        if (!_menuContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = _resolveMenuContribution(systemName);
            _menuContributionCache[systemName] = contribution;
        }

        return contribution;
    }

    private ISadConsoleInfoContribution? GetInfoContribution(string systemName)
    {
        if (!_infoContributionCache.TryGetValue(systemName, out var contribution))
        {
            contribution = _resolveInfoContribution(systemName);
            _infoContributionCache[systemName] = contribution;
        }

        return contribution;
    }

    internal ISadConsoleInfoContribution? GetActiveInfoContribution()
    {
        var systemName = CurrentRunningSystem?.Name ?? SelectedSystemName;
        if (string.IsNullOrWhiteSpace(systemName))
            return null;

        return GetInfoContribution(systemName);
    }

    /// <summary>
    /// A screen showing a quit-only error dialog; the only action is to close the application.
    /// Used when a fatal error occurs while building the main screen.
    /// </summary>
    private IScreenObject CreateStartupErrorScreen(string message)
    {
        _startupErrorScreenActive = true;
        return BuildErrorScreen(message);
    }

    /// <summary>Builds a screen containing a single quit-only error dialog.</summary>
    private static IScreenObject BuildErrorScreen(string message)
    {
        var screen = new ScreenObject();
        var dialog = new ErrorDialog(message, exception: null, fatalStartupError: true);
        dialog.Closed += (s, e) =>
        {
            Settings.DoFinalDraw = false;
            Game.Instance.MonoGameInstance.Exit();
        };
        screen.Children.Add(dialog);
        dialog.Show();
        dialog.IsFocused = true;
        return screen;
    }

    /// <summary>
    /// Runs a minimal SadConsole game that shows only a quit-only error dialog. Used when startup
    /// fails before a full <see cref="SadConsoleHostApp"/> can run (bad appsettings.json, a plug-in
    /// discovery or DI failure, ...). It needs only SadConsole itself — no host app, system list or
    /// plug-ins. If even this minimal UI cannot be created, the error is written to the console —
    /// that is the last-resort fallback when no UI is possible.
    /// </summary>
    /// <param name="logger">Optional — may be null if startup failed before logging was set up.</param>
    public static void RunStartupErrorOnly(string message, ILogger? logger = null)
    {
        try
        {
            var builder = new Builder()
                .SetWindowSizeInCells(80, 25)
                .ConfigureFonts((fontConfig, host) => fontConfig.UseBuiltinFont())
                .SetStartingScreen(_ => BuildErrorScreen(message));

            Game.Create(builder);
            Settings.WindowTitle = "DotNet 6502 Emulator - Startup Error";
            Game.Instance.Run();
        }
        catch (Exception ex)
        {
            // The UI itself could not be shown — the console is the only remaining fallback.
            logger?.LogCritical(ex, "Could not display the startup error UI. Original error: {Message}", message);
            System.Console.Error.WriteLine($"FATAL: could not display the startup error UI: {ex}");
            System.Console.Error.WriteLine($"Original startup error: {message}");
        }
    }

    /// <summary>
    /// Builds the SadConsole starting screen. Any fatal error (no emulator systems, an invalid
    /// DefaultEmulator, a console that fails to build, ...) is caught and a quit-only error
    /// screen is shown instead — the app always reaches a window.
    /// </summary>
    private IScreenObject CreateMainSadConsoleScreen(GameHost gameHost)
    {
        try
        {
            return BuildMainSadConsoleScreen();
        }
        catch (Exception ex)
        {
            var rootEx = ex is AggregateException agg ? agg.InnerException ?? agg : ex;
            _logger.LogCritical(rootEx, "Fatal error during startup.");
            return CreateStartupErrorScreen("The emulator could not start.\n\n" + rootEx.Message);
        }
    }

    private IScreenObject BuildMainSadConsoleScreen()
    {
        if (AvailableSystemNames.Count == 0)
            throw new DotNet6502Exception(
                "No emulator systems are available.\n" +
                "Check the 'EnabledSystems' setting in appsettings.json, and that the system " +
                "plug-in assemblies are deployed with the application.");

        // Trigger SelectSystem which sets current system to default system. A selected system is required for the setup code below.
        SelectSystem(_emulatorConfig.DefaultEmulator).Wait();

        InitTargetRenderers(); // New rendering pipeline

        SetContexts(() => _inputHandlerContext);

        InitInputHandlerContext();
        _audioHandlerContext.Init();

        // Audio pipeline configuration: register the NAudio host audio target.
        SetAudioConfig(atp =>
            atp.AddAudioTargetType<NAudioCommandTarget>(
                () => new NAudioCommandTarget(_audioHandlerContext, _loggerFactory)));

        //ScreenSurface screen = new(gameInstance.ScreenCellsX, gameInstance.ScreenCellsY);
        //return screen;
        _sadConsoleScreen = new ScreenObject();

        _menuConsole = new MenuConsole(this, _loggerFactory);
        _menuConsole.Position = (MENU_POSITION_X, MENU_POSITION_Y);
        _sadConsoleScreen.Children.Add(_menuConsole);

        // Info console
        _infoConsole = new InfoConsole(this, _logStore, _logConfig);
        _infoConsole.Position = (15, 30); // Temporary position while invisible. Will be moved after a system is started and info is enabled.
        _sadConsoleScreen.Children.Add(_infoConsole);

        // Monitor status console
        _monitorStatusConsole = new MonitorStatusConsole(this);
        _monitorStatusConsole.Position = (_menuConsole.Position.X + _menuConsole.Width, _menuConsole.Position.Y + _menuConsole.Height + 1); // Temporary position while invisible. Will be moved after a system is started and monitor is enabled.
        _sadConsoleScreen.Children.Add(_monitorStatusConsole);

        // Monitor console
        _monitorConsole = new MonitorConsole(this, _emulatorConfig.Monitor, _monitorStatusConsole.Refresh);
        _monitorConsole.IsVisible = false;
        _monitorConsole.Position = (_menuConsole.Position.X + _menuConsole.Width, _menuConsole.Position.Y); // Temporary position while invisible. Will be moved after a system is started and monitor is enabled.
        MonitorStateChange += (s, monitorEnabled) =>
        {
            //_inputHandlerContext.ListenForKeyboardInput(enabled: !monitorEnabled);

            if (monitorEnabled)
            {
                // Monitor console
                // Position monitor to the right of the emulator console
                _monitorConsole.UsePixelPositioning = true;
                // Note: _sadConsoleEmulatorConsole has already changed to UsePixelPositioning = true, so its Position.X is in pixels (not Width though).
                var emulatorMaxX = _sadConsoleEmulatorConsole!.Position.X + ((int)(_sadConsoleEmulatorConsole.Width * _sadConsoleEmulatorConsole.Font.GlyphWidth * CommonHostSystemConfig.DefaultFontSize.GetFontSizeScaleFactor()));
                var infoConsoleMax = _infoConsole != null && _infoConsole.IsVisible ? _infoConsole.Position.X + _infoConsole.WidthPixels : 0;
                _monitorConsole.Position = new Point(Math.Max(emulatorMaxX, infoConsoleMax), 0);

                // Monitor status console
                _monitorStatusConsole.UsePixelPositioning = true;
                _monitorStatusConsole.Position = new Point(_monitorConsole.Position.X, _monitorConsole.Position.Y + (_monitorConsole.Height * _monitorConsole.Font.GlyphHeight));

                _sadConsoleEmulatorConsole.IsFocused = false;
                _sadConsoleEmulatorConsole.UseKeyboard = true;
            }
            else
            {
                _sadConsoleEmulatorConsole!.IsFocused = true;
                _sadConsoleEmulatorConsole.UseKeyboard = false;
            }

            // Resize main window to fit menu, emulator, monitor and other visible consoles
            Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        };
        _sadConsoleScreen.Children.Add(_monitorConsole);

        // Trigger SelectSystem call again to update system-specific UI stuff.
        SelectSystem(_emulatorConfig.DefaultEmulator).Wait();

        // Logo - load from embedded resources
        _logoDrawImage = EmbeddedResourceHelper.CreateDrawImageFromEmbeddedResource("Resources/Images/logo.png");
        _logoDrawImage.PositionMode = DrawImage.PositionModes.Pixels;
        //int logoWidthAndHeight = 256; // Pixels
        //var logoX = (MenuConsole.CONSOLE_WIDTH * _menuConsole.Font.GlyphWidth) + ((StartupScreenWidth - MenuConsole.CONSOLE_WIDTH) * _menuConsole.Font.GlyphWidth - logoWidthAndHeight) / 2;
        //var logoY = ((MenuConsole.CONSOLE_HEIGHT * _menuConsole.Font.GlyphHeight) - logoWidthAndHeight) / 2;
        var logoX = (MenuConsole.CONSOLE_WIDTH * _menuConsole.Font.GlyphWidth) + 20;
        var logoY = 20;
        _logoDrawImage.PositionOffset = new Point(logoX, logoY);
        _sadConsoleScreen.SadComponents.Add(_logoDrawImage);

        //_sadConsoleScreen.IsFocused = true;
        _menuConsole.IsFocused = true;

        // Make sure all systems have a supported render target selected in their config  
        ApplySupportedRenderTargetToSystemConfigs().Wait();

        return _sadConsoleScreen;
    }

    private void InitTargetRenderers()
    {
        // New rendering pipeline configuration
        base.SetRenderConfig(
            (RenderTargetProvider rtp) =>
            {
                // Single, system-agnostic SadConsole render target. The only per-system variance is
                // an optional glyph/colour transform, contributed by engine plug-ins via
                // ISadConsoleRenderCustomizationPlugin — keeping system-specific code out of here.
                rtp.AddRenderTargetType<SadConsoleCommandTarget>(() => new SadConsoleCommandTarget(
                    GetEmulatorConsoleOrThrow(),
                    offsetX: EmulatorConsole.USE_CONSOLE_BORDER ? 1 : 0,
                    offsetY: EmulatorConsole.USE_CONSOLE_BORDER ? 1 : 0,
                    transformCharacterAndColor: ResolveCharacterAndColorTransform(CurrentRunningSystem)));
            },
            () =>
            {
                // HostFrameCallback
                var renderloop = new SadConsoleRenderLoop(
                    OnBeforeRender,
                    OnAfterRender,
                    shouldEmitEmulationFrame: () => EmulatorState != EmulatorState.Uninitialized);
                return renderloop;
            });
    }

    // Asks the discovered engine plug-ins for a system-specific SadConsole glyph/colour transform
    // for the running system. Returns null when no plug-in handles it (e.g. the Generic computer).
    private Func<int, Color, Color, (int transformedCharacter, Color transformedFgColor, Color transformedBgColor)>?
        ResolveCharacterAndColorTransform(ISystem? system)
    {
        if (system == null)
            return null;
        foreach (var plugin in _renderCustomizationPlugins)
        {
            var transform = plugin.GetCharacterAndColorTransform(system);
            if (transform != null)
                return transform;
        }
        return null;
    }

    public override void OnAfterSelectedSystemChanged()
    {
        // Hack for when selecting a system during initialization triggers this event.
        if (_menuConsole == null)
            return;

        // Set the default font size configured for the system
        _menuConsole.SetEmulatorFontSize(CommonHostSystemConfig.DefaultFontSize);

        // Clear any old system specific menu console
        if (_systemMenuConsole != null)
        {
            if (_sadConsoleScreen!.Children.Contains(_systemMenuConsole))
                _sadConsoleScreen.Children.Remove(_systemMenuConsole);
            _systemMenuConsole = null;
        }
        _activeSystemMenuContribution = GetMenuContribution(SelectedSystemName);
        if (_activeSystemMenuContribution != null)
        {
            _systemMenuConsole = _activeSystemMenuContribution.Console;
            _systemMenuConsole.Position = (MENU_POSITION_X, _menuConsole.Height);
            if (!_sadConsoleScreen!.Children.Contains(_systemMenuConsole))
                _sadConsoleScreen.Children.Add(_systemMenuConsole);
        }

        GetInfoConsoleOrThrow().ShowSelectedSystemInfoHelp();
    }

    public override bool OnBeforeStart(ISystem systemAboutToBeStarted)
    {
        // Check if we was uninitialized (not started yet) before starting the system. Otherwise it would be just paused, and we don't want to recreate the emulator console then.
        if (EmulatorState == EmulatorState.Uninitialized)
        {
            // Create emulator console
            if (_sadConsoleEmulatorConsole != null)
            {
                var sadConsoleScreen = GetSadConsoleScreenOrThrow();
                if (sadConsoleScreen.Children.Contains(_sadConsoleEmulatorConsole))
                    sadConsoleScreen.Children.Remove(_sadConsoleEmulatorConsole);
            }

            IFont font;
            if (!string.IsNullOrEmpty(CommonHostSystemConfig.Font))
            {
                var fontKey = Path.GetFileNameWithoutExtension(CommonHostSystemConfig.Font);
                if (Game.Instance.Fonts.ContainsKey(fontKey))
                {
                    font = Game.Instance.Fonts[fontKey];
                }
                else
                {
                    // If font not found in registry, load it from embedded resources
                    font = EmbeddedResourceHelper.LoadFontFromEmbeddedResource(CommonHostSystemConfig.Font);
                    Game.Instance.Fonts[fontKey] = font;
                }
            }
            else
            {
                font = Game.Instance.DefaultFont;
            }
            _sadConsoleEmulatorConsole = EmulatorConsole.Create(systemAboutToBeStarted, font, CommonHostSystemConfig.DefaultFontSize, SadConsoleUISettings.CreateEmulatorConsoleDrawBoxBorderParameters(font.SolidGlyphIndex));
            _sadConsoleEmulatorConsole.UsePixelPositioning = true;
            var menuConsole = GetMenuConsoleOrThrow();
            _sadConsoleEmulatorConsole.Position = new Point((menuConsole.Position.X * menuConsole.Font.GlyphWidth) + (menuConsole.Width * menuConsole.Font.GlyphWidth), 0);
            _sadConsoleEmulatorConsole.IsFocused = true;
            GetSadConsoleScreenOrThrow().Children.Add(_sadConsoleEmulatorConsole);

            // Resize main window to fit menu, emulator, and other consoles
            Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        }
        return true;
    }

    public override void OnAfterStart(EmulatorState emulatorStateBeforeStart)
    {
        // Init monitor for current system started if this system was not started before
        if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
        {
            GetMonitorConsoleOrThrow().Init();
        }

        SetEmulatorConsoleFocus();

        var infoConsole = GetInfoConsoleOrThrow();
        if (infoConsole.IsVisible)
        {
            // Enable instrumentations if info console is visible
            GetCurrentRunningSystemOrThrow().InstrumentationEnabled = true;

            if (emulatorStateBeforeStart == EmulatorState.Uninitialized)
            {
                // Enable info again to calculate its position to be below emulator console
                EnableInfo();
            }
        }
    }

    public override void OnBeforeStop()
    {
        // Disable monitor if it is visible
        if (GetMonitorConsoleOrThrow().IsVisible)
            DisableMonitor();
        // Clear stats in info console if it is visible (logs will still be shown)
        if (GetInfoConsoleOrThrow().IsVisible)
            ClearInfoStats();
    }
    public override void OnAfterStop()
    {
        // Remove the console containing the running system
        if (_sadConsoleEmulatorConsole != null)
        {
            var sadConsoleScreen = GetSadConsoleScreenOrThrow();
            if (sadConsoleScreen.Children.Contains(_sadConsoleEmulatorConsole))
                sadConsoleScreen.Children.Remove(_sadConsoleEmulatorConsole);
            _sadConsoleEmulatorConsole.Dispose();
            _sadConsoleEmulatorConsole = null;
        }
    }

    public override void OnAfterClose()
    {
        // Dispose Monitor/Instrumentations panel
        //_monitor.Cleanup();

        // Cleanup contexts
        _inputHandlerContext?.Cleanup();
        _audioHandlerContext?.Cleanup();
    }

    /// <summary>
    /// Runs on every Update Frame event.
    /// Runs the emulator logic for one frame.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="gameHost"></param>
    private void UpdateSadConsole(object? sender, GameHost gameHost)
    {
        // The startup-error screen has no emulator/monitor consoles — nothing to update per frame.
        if (_startupErrorScreenActive)
            return;

        try
        {
            // Handle UI-specific keyboard inputs such as toggle monitor, info, etc.
            HandleUIKeyboardInput().Wait();

            // RunEmulatorOneFrame() will first handle input, then emulator in run for one frame.
            RunEmulatorOneFrame();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateSadConsole frame update");
            HandleGlobalException(ex);
        }
    }

    public override void OnBeforeRunEmulatorOneFrame(out bool shouldRun, out bool shouldReceiveInput)
    {
        shouldRun = false;
        shouldReceiveInput = false;

        // Don't update emulator state when monitor is enabled/visible
        if (GetMonitorConsoleOrThrow().IsVisible)
            return;

        shouldRun = true;

        // Only receive input to emulator if it has focus
        if (GetEmulatorConsoleOrThrow().IsFocused)
            shouldReceiveInput = true;
    }

    public override void OnAfterRunEmulatorOneFrame(ExecEvaluatorTriggerResult execEvaluatorTriggerResult)
    {
        // Push stats to info console
        var currentRunningSystem = GetCurrentRunningSystemOrThrow();
        if (currentRunningSystem.InstrumentationEnabled)
        {
            var infoConsole = GetInfoConsoleOrThrow();
            _statsFrameCount++;
            if (_statsFrameCount >= STATS_UPDATE_EVERY_X_FRAME)
            {
                _statsFrameCount = 0;
                infoConsole.UpdateStats();
            }
            _debugInfoFrameCount++;
            if (_debugInfoFrameCount >= DEBUGINFO_UPDATE_EVERY_X_FRAME)
            {
                _debugInfoFrameCount = 0;
                infoConsole.UpdateSystemDebugInfo();
            }
        }

        // Show monitor if we encounter breakpoint or other break
        if (execEvaluatorTriggerResult.Triggered)
            EnableMonitor(execEvaluatorTriggerResult);
    }

    private SadConsoleInputHandlerContext CreateInputHandlerContext()
    {
        return new SadConsoleInputHandlerContext(_loggerFactory);
    }

    private NAudioAudioHandlerContext CreateAudioHandlerContext()
    {
        // Output to NAudio built-in output (Windows only)
        //var wavePlayer = new WaveOutEvent
        //{
        //    NumberOfBuffers = 2,
        //    DesiredLatency = 100,
        //}

        // Output to OpenAL (cross platform) instead of via NAudio built-in output (Windows only)
        var wavePlayer = new SilkNetOpenALWavePlayer()
        {
            NumberOfBuffers = 2,
            DesiredLatency = 40
        };

        return new NAudioAudioHandlerContext(
            wavePlayer,
            initialVolumePercent: 20, 
            _loggerFactory);
    }

    public ISadConsoleHostConfig CommonHostSystemConfig => (ISadConsoleHostConfig)CurrentHostSystemConfig;

    private int CalculateWindowWidthPixels()
    {
        var menuConsole = GetMenuConsoleOrThrow();
        var infoConsole = GetInfoConsoleOrThrow();
        var menuConsoleWidthPixels = menuConsole.WidthPixels;
        var emulatorConsoleWidthPixels = Math.Max((_sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.WidthPixels : 0)
                                            , (infoConsole.IsVisible ? infoConsole.WidthPixels : 0));
        var monitorConsoleWidthPixels = (_monitorConsole != null && _monitorConsole.IsVisible ? _monitorConsole.WidthPixels : 0);
        var widthPixels = menuConsoleWidthPixels + emulatorConsoleWidthPixels + monitorConsoleWidthPixels;
        return widthPixels;
    }

    private int CalculateWindowHeightPixels()
    {
        var menuConsole = GetMenuConsoleOrThrow();
        var infoConsole = GetInfoConsoleOrThrow();
        var menuConsoleHeightPixels = menuConsole.HeightPixels + (_systemMenuConsole != null ? _systemMenuConsole.HeightPixels : 0);
        var emulatorConsoleHeightPixels = (_sadConsoleEmulatorConsole != null ? _sadConsoleEmulatorConsole.HeightPixels + (infoConsole.IsVisible ? infoConsole.HeightPixels : 0) : 0);
        var monitorConsoleHeightPixels = (_monitorConsole != null && _monitorConsole.IsVisible ? _monitorConsole.HeightPixels + GetMonitorStatusConsoleOrThrow().HeightPixels : 0);

        // Calculate Max of the variables above
        var heightPixels = new int[] { menuConsoleHeightPixels, emulatorConsoleHeightPixels, monitorConsoleHeightPixels }.Max();
        return heightPixels;
    }

    public void ToggleMonitor()
    {
        // Only be able to toggle monitor if emulator is running or paused
        if (EmulatorState == EmulatorState.Uninitialized)
            return;

        if (GetMonitorConsoleOrThrow().IsVisible)
        {
            DisableMonitor();
        }
        else
        {
            EnableMonitor();
        }
    }

    public void DisableMonitor()
    {
        GetMonitorConsoleOrThrow().Disable();
        GetMonitorStatusConsoleOrThrow().Disable();
        OnMonitorStateChange(monitorEnabled: false);
    }

    public void EnableMonitor(ExecEvaluatorTriggerResult? execEvaluatorTriggerResult = null)
    {
        GetMonitorConsoleOrThrow().Enable(execEvaluatorTriggerResult);
        GetMonitorStatusConsoleOrThrow().Enable();
        OnMonitorStateChange(monitorEnabled: true);
    }

    public void ToggleInfo()
    {
        if (GetInfoConsoleOrThrow().IsVisible)
        {
            DisableInfo();
        }
        else
        {
            EnableInfo();
        }
    }

    public void DisableInfo()
    {
        var infoConsole = GetInfoConsoleOrThrow();
        infoConsole.Disable();
        if (CurrentRunningSystem != null)
            CurrentRunningSystem!.InstrumentationEnabled = false;

        // Enable logo when info console is disabled (as it shouldn't be covered by the info console)
        var sadConsoleScreen = GetSadConsoleScreenOrThrow();
        var logoDrawImage = GetLogoDrawImageOrThrow();
        if (!sadConsoleScreen.SadComponents.Contains(logoDrawImage))
            sadConsoleScreen.SadComponents.Add(logoDrawImage);

        // Resize main window to fit menu, emulator, monitor and other visible consoles
        Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());
        //OnStatsStateChange(statsEnabled: false);
    }

    public void EnableInfo()
    {
        if (EmulatorState != EmulatorState.Uninitialized)
            CurrentRunningSystem!.InstrumentationEnabled = true;

        var infoConsole = GetInfoConsoleOrThrow();
        var menuConsole = GetMenuConsoleOrThrow();
        infoConsole.Enable();
        // Assume _sadConsoleEmulatorConsole has enabled pixel positioning
        infoConsole.UsePixelPositioning = true;
        if (_sadConsoleEmulatorConsole != null && _sadConsoleEmulatorConsole.IsVisible)
        {
            //_infoConsole.Position = (_sadConsoleEmulatorConsole.Position.X, _sadConsoleEmulatorConsole.Position.Y + (_sadConsoleEmulatorConsole.Height * (int)(_sadConsoleEmulatorConsole.Font.GlyphHeight * _emulatorConfig.FontSizeScaleFactor)));
            infoConsole.Position = (_sadConsoleEmulatorConsole.Position.X, _sadConsoleEmulatorConsole.Position.Y + _sadConsoleEmulatorConsole.HeightPixels);
        }
        else
        {
            infoConsole.Position = (menuConsole.Position.X + menuConsole.WidthPixels, menuConsole.Position.Y);
        }

        if (GetMonitorConsoleOrThrow().IsVisible)
            EnableMonitor(); // Re-enable to trigger calculation of monitor console position

        // Resize main window to fit menu, emulator, monitor and other visible consoles
        Game.Instance.ResizeWindow(CalculateWindowWidthPixels(), CalculateWindowHeightPixels());

        // Remove logo when info console is enabled (as it may partially cover the logo)
        var sadConsoleScreen = GetSadConsoleScreenOrThrow();
        var logoDrawImage = GetLogoDrawImageOrThrow();
        if (sadConsoleScreen.SadComponents.Contains(logoDrawImage))
            sadConsoleScreen.SadComponents.Remove(logoDrawImage);

        //OnStatsStateChange(statsEnabled: true);
    }
    public void ClearInfoStats()
    {
        var infoConsole = GetInfoConsoleOrThrow();
        infoConsole.ClearStats();
        infoConsole.ClearSystemDebugInfo();
    }

    public void SetVolumePercent(float volumePercent)
    {
        _audioHandlerContext.SetMasterVolumePercent(masterVolumePercent: volumePercent);
    }

    public void SetEmulatorConsoleFocus()
    {
        if (_sadConsoleEmulatorConsole != null)
            _sadConsoleEmulatorConsole.IsFocused = true;
    }

    private async Task HandleUIKeyboardInput()
    {
        var keyboard = GameHost.Instance.Keyboard;

        // if (keyboard.IsKeyReleased(Keys.F8))
        // {
        //     keyboard.Clear();
        //     throw new Exception("Test unhandled exception from F8 key");
        // }

        // Toggle logs with F10 - disabled for now as logs are always updated in info console        
        //if (keyboard.IsKeyReleased(Keys.F10))
        //    ToggleLogs();

        if (keyboard.IsKeyReleased(Keys.F11))
        {
            keyboard.Clear();
            ToggleInfo();
        }

        if (keyboard.IsKeyReleased(Keys.F12) && (EmulatorState == EmulatorState.Running || EmulatorState == EmulatorState.Paused))
        {
            keyboard.Clear();
            ToggleMonitor();
        }

        if (keyboard.IsKeyReleased(Keys.F9) && EmulatorState == EmulatorState.Running)
        {
            keyboard.Clear();
            if (_activeSystemMenuContribution != null)
                await _activeSystemMenuContribution.HandleKeyReleased(Keys.F9);
        }
    }
}
