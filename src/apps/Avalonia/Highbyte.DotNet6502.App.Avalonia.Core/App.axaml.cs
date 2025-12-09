using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
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
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using ReactiveUI;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public partial class App : Application
{
    private readonly IConfiguration _configuration;
    private readonly EmulatorConfig _emulatorConfig;
    private readonly DotNet6502InMemLogStore _logStore;
    private readonly DotNet6502InMemLoggerConfiguration _logConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Func<string, string, string?, Task>? _saveCustomConfigString;
    private readonly Func<string, IConfigurationSection, string?, Task>? _saveCustomConfigSection;
    private readonly IWavePlayer? _wavePlayer;

    private AvaloniaHostApp _hostApp = default!;
    private IServiceProvider _serviceProvider = default!;

    /// <summary>
    /// Avalonia App constructor.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="wavePlayer">Optional IWavePlayer for audio output. If null, audio will be disabled.</param>
    /// <param name="saveCustomConfigString"></param>
    /// <param name="saveCustomConfigSection"></param>
    public App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        IWavePlayer? wavePlayer = null,
        Func<string, string, string?, Task>? saveCustomConfigString = null,
        Func<string, IConfigurationSection, string?, Task>? saveCustomConfigSection = null)
    {
        Console.WriteLine("App constructor called");

        _configuration = configuration;
        _emulatorConfig = emulatorConfig;
        _loggerFactory = loggerFactory;
        _logStore = logStore;
        _logConfig = logConfig;
        _saveCustomConfigString = saveCustomConfigString;
        _saveCustomConfigSection = saveCustomConfigSection;
        _wavePlayer = wavePlayer;

        try
        {
            _logger = loggerFactory.CreateLogger(typeof(App).Name);

            _logger.LogInformation("About to initialize exception handlers.");

            // Set up global exception handlers
            SetupGlobalExceptionHandlers();

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize App. Constructor threw exception: {ex}");
            throw;
        }
    }

    public override void Initialize()
    {
        Console.WriteLine("App Initialize called");

        AvaloniaXamlLoader.Load(this);

        Console.WriteLine("App Initialize end");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("AppOnFrameworkInitializationCompleted called");

#if DEBUG
        // Rebind DevTools away from F12 (e.g., Ctrl+F12)
        // Only attach DevTools when running as a desktop application
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            this.AttachDevTools(new DevToolsOptions
            {
                Gesture = new KeyGesture(Key.F12, KeyModifiers.Control)
            });
        }
#endif

        // Initialize the emulator host app
        Console.WriteLine("Calling InitializeHostApp");
        InitializeHostApp();

        // Setup DI container
        Console.WriteLine("Calling SetupDependencyInjection");
        SetupDependencyInjection();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("ApplicationLifetime is IClassicDesktopStyleApplicationLifetime");

            DisableAvaloniaDataAnnotationValidation();

            var mainWindow = new MainWindow();

            // Get MainViewModel from DI and set as DataContext
            // MainWindow.Content (MainView) is created by XAML and inherits DataContext
            Console.WriteLine("Getting mainViewModel");
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            Console.WriteLine("Setting mainWindow.DataContext = mainViewModel");
            mainWindow.DataContext = mainViewModel;

            Console.WriteLine("Setting desktop.MainWindow = mainWindow");
            desktop.MainWindow = mainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            Console.WriteLine("ApplicationLifetime is ISingleViewApplicationLifetime");
            // MainView is created by XAML
            var mainView = new MainView();

            // Get MainViewModel from DI and set as DataContext
            Console.WriteLine("Getting mainViewModel");
            try
            {
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

                Console.WriteLine("Setting mainView.DataContext = mainViewModel");
                mainView.DataContext = mainViewModel;

                Console.WriteLine("Setting singleViewPlatform.MainView = mainView");
                singleViewPlatform.MainView = mainView;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error setting DataContext: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        // Note: RenderControl registration is now handled by EmulatorView.OnDataContextChanged()
        // when the DataContext is set, following the dependency inversion pattern.

        Console.WriteLine("Calling OnFrameworkInitializationCompleted");
        base.OnFrameworkInitializationCompleted();

        Console.WriteLine("AppOnFrameworkInitializationCompleted end");
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

        if (_wavePlayer != null)
            services.AddSingleton<IWavePlayer>(_wavePlayer);

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
                _wavePlayer,
                _saveCustomConfigString,
                _saveCustomConfigSection);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize HostApp: {ex}");
            throw;
        }
    }

    private void SetupGlobalExceptionHandlers()
    {
        // Only set up exception handlers if error dialog is enabled
        // When disabled, let exceptions flow naturally to trigger debugger
        if (_emulatorConfig.ShowErrorDialog)
        {
            // Set up UI thread exception handler (Avalonia best practice)
            Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;

            // Set up global exception handler for unhandled exceptions in other threads
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            // Set up handler for unhandled exceptions in tasks
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Set up ReactiveUI exception handler only if not running in WebAssembly
            // WebAssembly/AOT has issues with Observer.Create<Exception> due to runtime limitations
            if (!PlatformDetection.IsRunningInWebAssembly())
            {
                try
                {
                    RxApp.DefaultExceptionHandler = Observer.Create<Exception>(OnReactiveUIException);
                    _logger.LogInformation("ReactiveUI exception handler configured successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to configure ReactiveUI exception handler (this may be expected in WebAssembly environments)");
                }
            }
            else
            {
                _logger.LogInformation("Skipping ReactiveUI exception handler setup in WebAssembly environment");
            }

            _logger.LogInformation("Global exception handlers configured successfully for error dialog mode");
        }
        else
        {
            _logger.LogInformation("Global exception handlers disabled - exceptions will trigger debugger or cause application exit");
        }
    }

    private void OnUIThreadUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled exception on UI thread");

        // Mark as handled to prevent application crash and show error dialog
        e.Handled = true;

        // Handle the exception with error dialog
        HandleGlobalException(e.Exception, "UI Thread Exception");
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception in AppDomain");
            HandleGlobalException(exception, "Application Domain Exception");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception");

        // Mark as observed to prevent process termination
        e.SetObserved();

        HandleGlobalException(e.Exception, "Task Exception");
    }

    private void OnReactiveUIException(Exception exception)
    {
        _logger.LogError(exception, "ReactiveUI unhandled exception");
        HandleGlobalException(exception, "ReactiveUI Exception");
    }

    private void HandleGlobalException(Exception exception, string title = "Unhandled Exception")
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        System.Console.WriteLine($"Exception: {exception.Message}");
        System.Console.WriteLine($"Stack trace: {exception.StackTrace}");

        // Pause emulator if it's running
        if (_hostApp?.EmulatorState == EmulatorState.Running)
        {
            _hostApp.Pause();
        }

        // Show error dialog - ensure it runs on UI thread
        ShowErrorDialog(exception, title);
    }

    private void ShowErrorDialog(Exception exception, string title = "Unhandled Exception")
    {
        // Ensure we're on the UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowErrorDialog(exception, title));
            return;
        }

        // Run on UI thread
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var errorMessage = $"An unexpected error occurred in the application.\n\n" +
                                 $"Error: {exception.Message}\n\n" +
                                 $"Type: {exception.GetType().Name}";

                var errorDialog = new ErrorDialog(errorMessage, exception);

                global::Avalonia.Controls.Window? parentWindow = null;
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    parentWindow = desktop.MainWindow;
                }

                ErrorDialog.ErrorDialogResult result;
                if (parentWindow != null)
                {
                    result = await errorDialog.ShowDialog<ErrorDialog.ErrorDialogResult>(parentWindow);
                }
                else
                {
                    // Show as non-modal dialog and wait for it to close
                    var tcs = new TaskCompletionSource<ErrorDialog.ErrorDialogResult>();
                    errorDialog.Closed += (_, _) =>
                    {
                        tcs.SetResult(errorDialog.UserChoice);
                    };
                    errorDialog.Show();
                    result = await tcs.Task;
                }

                if (result == ErrorDialog.ErrorDialogResult.Exit)
                {
                    // User chose to exit - close the application
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                    {
                        desktopLifetime.Shutdown();
                    }
                    else
                    {
                        Environment.Exit(0);
                    }
                }
                else
                {
                    // User chose to continue - unpause emulator if it was paused
                    if (_hostApp?.EmulatorState == EmulatorState.Paused)
                    {
                        _ = _hostApp.Start(); // Fire and forget
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show error dialog");
                // Fallback: just exit
                Environment.Exit(1);
            }
        });
    }
}
