using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.App.Avalonia.Core.Views;
using Highbyte.DotNet6502.Impl.Avalonia.Input;
using Highbyte.DotNet6502.Impl.NAudio;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;

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
    private AvaloniaHostApp _hostApp = default!;
    private IServiceProvider _serviceProvider = default!;

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
    public App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        Func<string, string, string?, Task>? saveCustomConfigString = null,
        Func<string, IConfigurationSection, string?, Task>? saveCustomConfigSection = null,
        IGamepad? gamepad = null)
    {
        AppLogger.WriteBootstrapLog("App constructor called");

        _configuration = configuration;
        _emulatorConfig = emulatorConfig;
        _loggerFactory = loggerFactory;
        _logStore = logStore;
        _logConfig = logConfig;
        _saveCustomConfigString = saveCustomConfigString;
        _saveCustomConfigSection = saveCustomConfigSection;
        _gamepad = gamepad;

        // Initialize static logger factory for use in Views and other classes where DI is not available
        AppLogger.Factory = loggerFactory;

        try
        {
            _logger = loggerFactory.CreateLogger(typeof(App).Name);

            // Only set up exception handlers if error dialog is enabled
            // When disabled, let exceptions flow naturally to trigger debugger
            if (_emulatorConfig.UseGlobalExceptionHandler)
            {
                AppLogger.WriteBootstrapLog("ShowErrorDialog is enabled");
                SetupGlobalExceptionHandlers();
            }
            else
            {
                AppLogger.WriteBootstrapLog("ShowErrorDialog is disabled");
                _logger.LogInformation("Error dialog is disabled - global exception handlers not configured");
            }

        }
        catch (Exception ex)
        {
            AppLogger.WriteBootstrapLog($"Failed to initialize App. Constructor threw exception: {ex}", LogLevel.Error);
            throw;
        }
    }

    public override void Initialize()
    {
        AppLogger.WriteBootstrapLog("App Initialize called");

        AvaloniaXamlLoader.Load(this);

        AppLogger.WriteBootstrapLog("App Initialize end");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppLogger.WriteBootstrapLog("AppOnFrameworkInitializationCompleted called");

#if DEBUG
        // Rebind Avalonia built-in DevTools away from F12 because it's used by the emulator Monitor
        // Instead use Ctrl+F12, and only attach DevTools when running as a desktop application.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            this.AttachDevTools(new DevToolsOptions
            {
                Gesture = new KeyGesture(Key.F12, KeyModifiers.Control)
            });
        }
#endif

        // Initialize the emulator host app
        AppLogger.WriteBootstrapLog("Calling InitializeHostApp");
        InitializeHostApp();

        // Setup DI container
        AppLogger.WriteBootstrapLog("Calling SetupDependencyInjection");
        SetupDependencyInjection();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppLogger.WriteBootstrapLog("ApplicationLifetime is IClassicDesktopStyleApplicationLifetime");

            DisableAvaloniaDataAnnotationValidation();

            var mainWindow = new MainWindow();

            // Get MainViewModel from DI and set as DataContext
            // MainWindow.Content (MainView) is created by XAML and inherits DataContext
            AppLogger.WriteBootstrapLog("Getting mainViewModel");
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            AppLogger.WriteBootstrapLog("Setting mainWindow.DataContext = mainViewModel");
            mainWindow.DataContext = mainViewModel;

            AppLogger.WriteBootstrapLog("Setting desktop.MainWindow = mainWindow");
            desktop.MainWindow = mainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            AppLogger.WriteBootstrapLog("ApplicationLifetime is ISingleViewApplicationLifetime");
            // MainView is created by XAML
            var mainView = new MainView();

            // Get MainViewModel from DI and set as DataContext
            AppLogger.WriteBootstrapLog("Getting mainViewModel");
            try
            {
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

                AppLogger.WriteBootstrapLog("Setting mainView.DataContext = mainViewModel");
                mainView.DataContext = mainViewModel;

                AppLogger.WriteBootstrapLog("Setting singleViewPlatform.MainView = mainView");
                singleViewPlatform.MainView = mainView;
            }
            catch (Exception ex)
            {
                AppLogger.WriteBootstrapLog($"Fatal error setting DataContext: {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                AppLogger.WriteBootstrapLog($"Stack trace: {ex.StackTrace}", LogLevel.Error);
                if (ex.InnerException != null)
                {
                    AppLogger.WriteBootstrapLog($"Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", LogLevel.Error);
                }
                throw;
            }
        }

        // Note: RenderControl registration is now handled by EmulatorView.OnDataContextChanged()
        // when the DataContext is set, following the dependency inversion pattern.

        AppLogger.WriteBootstrapLog("Calling OnFrameworkInitializationCompleted");
        base.OnFrameworkInitializationCompleted();

        AppLogger.WriteBootstrapLog("AppOnFrameworkInitializationCompleted end");
    }

    private void SetupDependencyInjection()
    {
        var services = new ServiceCollection();

        // Register singletons
        services.AddSingleton(_hostApp);
        services.AddSingleton(_emulatorConfig);
        services.AddSingleton(_configuration);
        services.AddSingleton(_loggerFactory);
        if (_logStore != null)
            services.AddSingleton(_logStore);
        if (_logConfig != null)
            services.AddSingleton(_logConfig);

        // Register helpers
        services.AddTransient<OverlayDialogHelper>((sp) => new OverlayDialogHelper(this.ApplicationLifetime));

        // Register ViewModels as transient (new instance each time)
        services.AddTransient<MainViewModel>();
        services.AddTransient<EmulatorViewModel>();
        services.AddTransient<EmulatorPlaceholderViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<C64MenuViewModel>();
        services.AddTransient<C64ConfigDialogViewModel>();
        services.AddTransient<C64InfoViewModel>();

        // Views are NOT registered - XAML creates them!
        // They get their ViewModels through DataContext binding

        _serviceProvider = services.BuildServiceProvider();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
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
            var systemList = new SystemList<AvaloniaInputHandlerContext, NAudioAudioHandlerContext>();

            var c64Setup = new C64Setup(_loggerFactory, _configuration, _saveCustomConfigString);
            systemList.AddSystem(c64Setup);

            var genericComputerSetup = new GenericComputerSetup(_loggerFactory, _configuration, _emulatorConfig, _saveCustomConfigString);
            systemList.AddSystem(genericComputerSetup);

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
                _gamepad);

        }
        catch (Exception ex)
        {
            AppLogger.WriteBootstrapLog($"Failed to initialize HostApp: {ex}", LogLevel.Error);
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

        // Set up ReactiveUI exception handler ONLY on desktop platforms
        // In WebAssembly, ReactiveUI's exception handling uses threading which causes PlatformNotSupportedException
        // Instead, let exceptions bubble up to Avalonia's UI thread handler which works correctly in WASM
        if (!PlatformDetection.IsRunningInWebAssembly())
        {
            try
            {
                RxApp.DefaultExceptionHandler = Observer.Create<Exception>(OnReactiveUIException);
                _logger.LogInformation("ReactiveUI exception handler configured successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure ReactiveUI exception handler");
            }
        }
        else
        {
            _logger.LogInformation("Skipping ReactiveUI exception handler in WebAssembly - exceptions will be caught by ReactiveCommandHelper");
        }

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
                    await ShowErrorOverlay(exception, title);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing error overlay");
            }
        });
    }

    private async Task ShowErrorOverlay(Exception exception, string title)
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
}
