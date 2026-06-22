using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Highbyte.DotNet6502.DebugAdapter;
using Highbyte.DotNet6502.Remoting;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.App.Avalonia.Core.Views;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Highbyte.DotNet6502.Systems.Plugins;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Builder;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public partial class App : Application
{
    // Static handler for exceptions from ReactiveCommands in WASM
    internal static Action<Exception>? WasmExceptionHandler { get; private set; }

    private readonly IConfiguration _configuration;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;
    private readonly Func<string, IConfigurationSection, string?, Task>? _saveCustomConfigSection;
    private readonly IGamepad? _gamepad;
    private readonly IScriptingEngine? _scriptingEngine;
    private readonly Func<string, string?>? _loadScript;
    private readonly Action<string, string>? _saveScript;
    private readonly Action<string>? _deleteScript;
    private readonly Func<Task>? _loadExamples;
    private readonly Func<IHostApp, Task>? _automatedStartupRunner;
    private AvaloniaHostApp _hostApp = default!;
    private IServiceProvider _serviceProvider = default!;

    /// <summary>
    /// Exposes the host app's debuggable interface for external access (e.g., debug adapter integration).
    /// </summary>
    public IDebuggableHostApp HostApp => _hostApp;

    public bool IsHostAppReady => _hostApp != null;

    /// <summary>
    /// Optional async runner invoked from <see cref="ViewModels.MainViewModel.SetDefaultSystemSelectionAsync"/>
    /// when an automated startup has been requested (e.g. via URL query parameters in the Browser host).
    /// Running automation from there guarantees the Avalonia view tree has been loaded and rendered
    /// at least once, so subsequent <c>InvalidateVisual</c> calls actually trigger paint.
    /// </summary>
    public Func<IHostApp, Task>? AutomatedStartupRunner => _automatedStartupRunner;

    /// <summary>
    /// Runtime controller for the external TCP debug server.
    /// Non-null only on Desktop; null on Browser (where TCP is unavailable).
    /// </summary>
    public IExternalDebugController? ExternalDebugController { get; private set; }

    /// <summary>
    /// Runtime controller for the TCP remote control server.
    /// Non-null only on Desktop and Headless; null on Browser.
    /// </summary>
    public IRemoteControlController? RemoteControlController { get; private set; }

    /// <summary>
    /// Static reference to the current App instance (for debug adapter integration).
    /// </summary>
    public new static App? Current { get; private set; }

    /// <summary>
    /// Completes when <see cref="HostApp"/> has been fully initialized and is ready for use.
    /// Awaiting this from a background thread is safe: the TPL guarantees that all writes
    /// made before <c>TrySetResult</c> are visible to the continuation.
    /// </summary>
    public static Task<IDebuggableHostApp> WhenHostAppReadyAsync => s_hostAppReady.Task;
    private static readonly TaskCompletionSource<IDebuggableHostApp> s_hostAppReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Guard to prevent multiple error overlays from being shown simultaneously
    private Panel? _currentErrorOverlay;

    /// <summary>
    /// Avalonia App constructor.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="saveCustomConfigString"></param>
    /// <param name="saveCustomConfigSection"></param>
    /// <param name="gamepad">Optional gamepad provider. Pass null to use a NullAvaloniaGamepad.</param>
    /// <param name="scriptingEngine">Optional Lua scripting engine. Pass null to disable scripting (e.g. in WASM).</param>
    /// <param name="loadScript">Optional callback to load a script's source by file name (browser: from localStorage).</param>
    /// <param name="saveScript">Optional callback to persist a script by file name and content (browser: to localStorage).</param>
    /// <param name="deleteScript">Optional callback to remove a script by file name (browser: from localStorage).</param>
    /// <param name="loadExamples">Optional callback to fetch and seed bundled example scripts (browser-only).</param>
    /// <param name="automatedStartupRunner">
    /// Optional automated-startup delegate invoked from <see cref="ViewModels.MainViewModel.SetDefaultSystemSelectionAsync"/>
    /// after the view tree has been loaded. When non-null, the UI's default system selection is
    /// suppressed and the runner is invoked instead — used by the Browser host for URL-driven
    /// automation, by Desktop for CLI-driven automation, and as a no-op (<c>_ =&gt; Task.CompletedTask</c>)
    /// when a Lua script is responsible for the lifecycle.
    /// </param>
    public App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        Func<string, string, string?, Task>? saveCustomConfigString = null,
        Func<string, IConfigurationSection, string?, Task>? saveCustomConfigSection = null,
        IGamepad? gamepad = null,
        IExternalDebugController? externalDebugController = null,
        IRemoteControlController? remoteControlController = null,
        IScriptingEngine? scriptingEngine = null,
        Func<string, string?>? loadScript = null,
        Action<string, string>? saveScript = null,
        Action<string>? deleteScript = null,
        Func<Task>? loadExamples = null,
        Func<IHostApp, Task>? automatedStartupRunner = null)
    {
        WriteBootstrapLog("App constructor called");

        _configuration = configuration;
        _emulatorConfig = emulatorConfig;
        _loggerFactory = loggerFactory;
        _logStore = logStore;
        _logConfig = logConfig;
        _saveCustomConfigString = saveCustomConfigString;
        _saveCustomConfigSection = saveCustomConfigSection;
        _gamepad = gamepad;
        _scriptingEngine = scriptingEngine;
        _loadScript = loadScript;
        _saveScript = saveScript;
        _deleteScript = deleteScript;
        _loadExamples = loadExamples;
        _automatedStartupRunner = automatedStartupRunner;

        // Set static reference for external access (e.g., debug adapter)
        Current = this;
        ExternalDebugController = externalDebugController;
        RemoteControlController = remoteControlController;

        // Initialize static logger factory for use in Views and other classes where DI is not available
        AppLogger.Factory = loggerFactory;

        try
        {
            _logger = loggerFactory.CreateLogger(nameof(App));

            // Only set up exception handlers if error dialog is enabled
            // When disabled, let exceptions flow naturally to trigger debugger
            if (_emulatorConfig.UseGlobalExceptionHandler)
            {
                WriteBootstrapLog("ShowErrorDialog is enabled");
                SetupGlobalExceptionHandlers();
            }
            else
            {
                WriteBootstrapLog("ShowErrorDialog is disabled");
                _logger.LogInformation("Error dialog is disabled - global exception handlers not configured");
            }

        }
        catch (Exception ex)
        {
            WriteBootstrapLog($"Failed to initialize App. Constructor threw exception: {ex}", LogLevel.Error);
            throw;
        }
    }

    public override void Initialize()
    {
        WriteBootstrapLog("App Initialize called");

        AvaloniaXamlLoader.Load(this);

        // Pre-register an empty NativeMenu on macOS so Avalonia's backend subscribes to
        // Items.CollectionChanged before any window is shown. ApplyMenuContributor then
        // mutates the items of this same object at runtime instead of replacing the menu,
        // which is required for the macOS menu bar and its keyboard shortcuts to work.
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX))
        {
            NativeMenu.SetMenu(this, new NativeMenu());
        }

        WriteBootstrapLog("App Initialize end");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        WriteBootstrapLog("AppOnFrameworkInitializationCompleted called");

        // Initialize ReactiveUI 23.x (required — without this, the first use of
        // ReactiveObject/WhenAnyValue throws TypeInitializationException with
        // "ReactiveUI has not been initialized"). Formerly done by Avalonia.ReactiveUI's
        // UseReactiveUI(), which was deprecated in v12; we replace it with the stock
        // ReactiveUI builder and point the UI scheduler at our Avalonia dispatcher shim.
        //
        // Order matters: WithCoreServices() returns an IAppBuilder (terminal interface),
        // so it must come after the IReactiveUIBuilder-returning methods.
        var rxBuilder = RxAppBuilder.CreateReactiveUIBuilder()
            .WithPlatformServices()
            .WithMainThreadScheduler(AvaloniaDispatcherScheduler.Instance, setRxApp: true);

        // Reactive exception handler (desktop only — in WASM ReactiveUI's handler uses
        // threading APIs that throw PlatformNotSupportedException; exceptions there fall
        // through to Avalonia's UI thread handler instead).
        if (_emulatorConfig.UseGlobalExceptionHandler && !PlatformDetection.IsRunningInWebAssembly())
            rxBuilder = rxBuilder.WithExceptionHandler(Observer.Create<Exception>(OnReactiveUIException));

        rxBuilder.WithCoreServices().BuildApp();

        // TODO: when Avalonia.Diagnostics 12.x ships, re-add the AttachDevTools call
        // (DEBUG-only, rebound to Ctrl+F12 because F12 is used by the emulator Monitor).

        // All startup-critical work runs inside one try/catch: any failure here (no emulator
        // systems, an invalid DefaultEmulator, a plug-in that throws while registering, a
        // ViewModel constructor failing, ...) shows the fatal error screen instead of crashing
        // before any UI appears.
        try
        {
            // Setup DI container first: plug-in shell services (per-system VMs and ISystemConfigurer
            // factories) need to be registered before we build the SystemList from DI.
            WriteBootstrapLog("Calling SetupDependencyInjection");
            SetupDependencyInjection();

            // Initialize the emulator host app (resolves plug-in-provided configurers from DI).
            WriteBootstrapLog("Calling InitializeHostApp");
            InitializeHostApp();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                WriteBootstrapLog("ApplicationLifetime is IClassicDesktopStyleApplicationLifetime");

                // Get MainViewModel from DI and set as DataContext
                // MainWindow.Content (MainView) is created by XAML and inherits DataContext
                WriteBootstrapLog("Initializing MainWindow");
                var mainWindow = new MainWindow();
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                mainWindow.DataContext = mainViewModel;
                desktop.MainWindow = mainWindow;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                WriteBootstrapLog("ApplicationLifetime is ISingleViewApplicationLifetime");

                // Get MainViewModel from DI and set as DataContext
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

                WriteBootstrapLog("Initializing MainView");
                // MainView is created by XAML
                var mainView = new MainView();
                mainView.DataContext = mainViewModel;
                singleViewPlatform.MainView = mainView;
            }
        }
        catch (Exception ex)
        {
            WriteBootstrapLog($"Fatal error during startup: {ex}", LogLevel.Error);
            // Unblock anything awaiting the host app (e.g. automated startup) so it doesn't hang.
            s_hostAppReady.TrySetException(ex);
            ShowFatalStartupError(
                "The emulator could not start.\n\n" + ex.Message, ex);
        }

        // Note: RenderControl registration is now handled by EmulatorView.OnDataContextChanged()
        // when the DataContext is set, following the dependency inversion pattern.

        WriteBootstrapLog("Calling OnFrameworkInitializationCompleted");
        base.OnFrameworkInitializationCompleted();

        WriteBootstrapLog("AppOnFrameworkInitializationCompleted end");
    }

    private static void WriteBootstrapLog(string message, LogLevel logLevel = LogLevel.Information)
    {
        AppLogger.WriteBootstrapLog(message, logLevel, nameof(App));
    }

    private void SetupDependencyInjection()
    {
        var services = new ServiceCollection();

        // Register singletons.
        // AvaloniaHostApp is constructed in InitializeHostApp (which runs *after* this method).
        // Register as a deferred factory so plug-in shell services that consume DI can be
        // registered now; the host is only resolved later, when ViewModels are constructed.
        services.AddSingleton<AvaloniaHostApp>(_ =>
            _hostApp ?? throw new InvalidOperationException(
                "AvaloniaHostApp resolved before InitializeHostApp finished. Reorder boot."));
        services.AddSingleton(_emulatorConfig);
        services.AddSingleton(_configuration);
        services.AddSingleton(_loggerFactory);
        services.AddSingleton(new CustomConfigPersistence(_saveCustomConfigString));
        if (_logStore != null)
            services.AddSingleton(_logStore);
        if (_logConfig != null)
            services.AddSingleton(_logConfig);

        // Register helpers
        services.AddTransient<OverlayDialogHelper>((sp) => new OverlayDialogHelper(this.ApplicationLifetime));

        // Register system-agnostic ViewModels as transient (new instance each time).
        // Per-system ViewModels (C64MenuViewModel, C64InfoViewModel, C64ConfigDialogViewModel)
        // are registered by their plug-in below.
        services.AddTransient<MainViewModel>();
        services.AddTransient<EmulatorViewModel>();
        services.AddTransient<EmulatorPlaceholderViewModel>();
        services.AddTransient<StatisticsViewModel>();

        // Discover and register shell plug-ins. Each plug-in adds its own VMs / configurer.
        // EnabledSystems gates which plug-ins activate; absent config = all discovered plug-ins.
        var enabledSystems = _configuration
            .GetSection("EnabledSystems")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        if (enabledSystems.Length == 0)
            enabledSystems = null;
        var pluginLogger = _loggerFactory.CreateLogger<App>();
        var shellPlugins = SystemPluginDiscovery
            .Discover<ISystemShellPlugin>(enabledSystems, pluginLogger)
            .ToList();
        pluginLogger.LogInformation("Discovered {Count} shell plug-in(s): {Names}",
            shellPlugins.Count,
            string.Join(",", shellPlugins.Select(p => p.SystemName)));
        foreach (var plugin in shellPlugins)
            plugin.RegisterShellServices(services);
        services.AddSingleton<IReadOnlyList<ISystemShellPlugin>>(shellPlugins);

        // Discover and register engine plug-ins (Impl.Avalonia.<System>). Each registers the
        // per-system ISystemConfigurer; InitializeHostApp builds the SystemList from those.
        var enginePlugins = SystemPluginDiscovery
            .Discover<ISystemEnginePlugin>(enabledSystems, pluginLogger)
            .ToList();
        pluginLogger.LogInformation("Discovered {Count} engine plug-in(s): {Names}",
            enginePlugins.Count,
            string.Join(",", enginePlugins.Select(p => p.SystemName)));
        foreach (var plugin in enginePlugins)
            plugin.Register(services, _configuration);

        // Diagnose enabled-but-missing systems and engine/shell plug-in mismatches.
        SystemPluginDiscovery.LogPluginDiagnostics(enabledSystems, enginePlugins, shellPlugins, pluginLogger);

        // Views are NOT registered - XAML creates them!
        // They get their ViewModels through DataContext binding

        _serviceProvider = services.BuildServiceProvider(DotNet6502ServiceProviderOptions.Validated);
    }

    /// <summary>
    /// Get the service provider for dependency injection.
    /// </summary>
    public IServiceProvider? GetServiceProvider() => _serviceProvider;

    private void InitializeHostApp()
    {
        try
        {
            // ----------
            // Get systems
            // ----------
            var systemList = new SystemList();

            // Plug-in-provided systems (C64 + Generic, via Highbyte.DotNet6502.App.Avalonia.Shell.*).
            // Each shell plug-in registers its ISystemConfigurer in DI under RegisterShellServices.
            foreach (var configurer in _serviceProvider!
                .GetServices<ISystemConfigurer>())
            {
                systemList.AddSystem(configurer);
            }

            // Drop any system that declares no configuration variants — it cannot be built or run,
            // and would crash a variant picker. Treated as unavailable, like a missing plug-in.
            // Sync-wait is intentional: this is single-threaded UI bootstrap, before any window.
#pragma warning disable VSTHRD002
            systemList.RemoveSystemsWithNoConfigurationVariants(_logger).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

            // No usable system: a fatal startup error. The caller (OnFrameworkInitializationCompleted)
            // catches this and shows the fatal error screen instead of the main UI.
            if (systemList.Systems.Count == 0)
                throw new InvalidOperationException(
                    "No emulator systems are available.\n\n" +
                    "Check the 'EnabledSystems' setting in appsettings.json, and that the system " +
                    "plug-in assemblies are deployed with the application.");

            // ----------
            // Create AvaloniaHostApp
            // ----------
            _emulatorConfig.Validate(systemList);

            _hostApp = new AvaloniaHostApp(
                systemList,
                _loggerFactory,
                _emulatorConfig,
                _logStore,
                _logConfig,
                _saveCustomConfigString,
                _saveCustomConfigSection,
                _gamepad,
                _loadScript,
                _saveScript,
                _deleteScript,
                _loadExamples);

            // Wire Lua scripting engine (NoScriptingEngine used when null, e.g. in WASM)
            _hostApp.SetScriptingEngine(_scriptingEngine ?? new NoScriptingEngine());

            // Signal waiters (e.g. automated startup on a background thread) that HostApp is ready.
            // TrySetResult guarantees all writes above are visible to awaiters before they resume.
            s_hostAppReady.TrySetResult(_hostApp);
        }
        catch (Exception ex)
        {
            WriteBootstrapLog($"Failed to initialize HostApp: {ex}", LogLevel.Error);
            throw;
        }
    }

    private void SetupGlobalExceptionHandlers()
    {
        _logger.LogInformation("About to initialize exception handlers.");

        // Set up static handler for WASM ReactiveCommand exceptions
        WasmExceptionHandler = (ex) => HandleGlobalException(ex);

        // Set up UI thread exception handler (Avalonia best practice)
        Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;

        // Set up global exception handler for unhandled exceptions in other threads
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // Set up handler for unhandled exceptions in tasks
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // NOTE: The ReactiveUI-specific exception handler is registered later, via the
        // RxAppBuilder chain in OnFrameworkInitializationCompleted (builder must be the one
        // to set it because RxState.DefaultExceptionHandler is read-only in 23.x).

        _logger.LogInformation("Global exception handlers configured successfully for error dialog mode");
    }

    private void OnUIThreadUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled exception on UI thread");

        // Mark as handled to prevent application crash and show error dialog
        e.Handled = true;

        // Handle the exception with error dialog
        HandleErrorDialog(e.Exception, "UI Thread Exception");
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception in AppDomain");
            HandleErrorDialog(exception, "Application Domain Exception");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception");

        // Mark as observed to prevent process termination
        e.SetObserved();

        HandleErrorDialog(e.Exception, "Task Exception");
    }

    private void OnReactiveUIException(Exception exception)
    {
        _logger.LogError(exception, "ReactiveUI unhandled exception");
        HandleErrorDialog(exception, "ReactiveUI Exception");
    }

    private void HandleGlobalException(Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        //Console.WriteLine($"Exception: {exception.Message}");
        //Console.WriteLine($"Stack trace: {exception.StackTrace}");
        HandleErrorDialog(exception, "Unhandled Exception");
    }

    private void HandleErrorDialog(Exception exception, string title)
    {
        if (_emulatorConfig.ShowErrorDialog == false)
        {
            //ShowErrorDialog is disabled - not showing error overlay
            return;
        }

        // Pause emulator if it's running
        if (_hostApp?.EmulatorState == EmulatorState.Running)
        {
            _hostApp.Pause();
        }

        // Show error overlay and properly handle the task to avoid unobserved task exceptions
        _ = Task.Run(async () =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await ShowErrorOverlayAsync(exception, title);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing error overlay");
            }
        });
    }

    private async Task ShowErrorOverlayAsync(Exception exception, string title)
    {
        // Prevent multiple overlays from being shown at the same time
        if (_currentErrorOverlay != null)
        {
            _logger.LogWarning("Error overlay already showing, skipping additional error: {Message}", exception.Message);
            return;
        }

        // Create the ErrorViewModel
        var errorMessage = $"An unexpected error occurred in the application.\n\n" +
                 $"Error: {exception.Message}\n\n" +
                 $"Type: {exception.GetType().Name}";
        var errorViewModel = new ErrorViewModel(_loggerFactory, errorMessage, exception);

        // Create the UserControl
        var errorUserControl = new ErrorUserControl(errorViewModel, _loggerFactory);

        // Set up event handling for responding to user exiting the error dialog
        var taskCompletionSource = new TaskCompletionSource<bool>();
        errorUserControl.CloseRequested += (s, exit) =>
        {
            // Use TrySetResult to prevent InvalidOperationException if called multiple times
            taskCompletionSource.TrySetResult(exit);
        };

        // Create overlay panel
        var overlayDialogHelper = _serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(errorUserControl);
        // Set current overlay to prevent multiple overlays
        _currentErrorOverlay = overlayPanel;

        // Show the overlay
        var mainGrid = overlayDialogHelper.ShowOverlayDialogOnMainView(overlayPanel);

        // Wait for the dialog to close
        try
        {
            var exit = await taskCompletionSource.Task;

            if (exit && !PlatformDetection.IsRunningInWebAssembly())
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    desktopLifetime.Shutdown();
                    return;
                }
                else
                {
                    Environment.Exit(0);
                    return;
                }
            }
        }
        finally
        {
            // Clean up - remove the overlay
            bool removed = mainGrid.Children.Remove(overlayPanel);
            _logger.LogInformation("Error overlay removed: {Removed}", removed);

            // Reset the current overlay reference
            _currentErrorOverlay = null;
        }
    }

    /// <summary>
    /// Shows a fatal startup error as the application's root UI, offering only a "Quit" action.
    /// On browser hosts (which cannot quit a tab) the screen has no actionable button and stays
    /// open. The normal main UI is never created when this is shown.
    /// </summary>
    private void ShowFatalStartupError(string message, Exception? exception = null)
    {
        var errorViewModel = new ErrorViewModel(_loggerFactory, message, exception, fatalStartupError: true);
        var errorUserControl = new ErrorUserControl(errorViewModel, _loggerFactory);

        errorUserControl.CloseRequested += (s, exit) =>
        {
            if (!exit)
                return;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                desktopLifetime.Shutdown();
            else
                Environment.Exit(0);
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "DotNet 6502 Emulator — startup error",
                Content = errorUserControl,
                Width = 560,
                Height = 360,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = errorUserControl;
        }
    }
}
