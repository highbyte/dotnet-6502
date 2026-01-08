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
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Kernel;
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
    /// <param name="wavePlayer">Optional IWavePlayer for audio output. If null, audio will be disabled.</param>
    /// <param name="saveCustomConfigString"></param>
    /// <param name="saveCustomConfigSection"></param>
    public App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
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

    private void HandleGlobalException(Exception exception, string title)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        Console.WriteLine($"Exception: {exception.Message}");
        Console.WriteLine($"Stack trace: {exception.StackTrace}");

        // Pause emulator if it's running
        if (_hostApp?.EmulatorState == EmulatorState.Running)
        {
            _hostApp.Pause();
        }
        _ = ShowErrorOverlay(exception, title);
    }

    private async Task ShowErrorOverlay(Exception exception, string title)
    {
        // Prevent multiple overlays from being shown at the same time
        if (_currentErrorOverlay != null)
        {
            _logger.LogWarning("Error overlay already showing, skipping additional error: {Message}", exception.Message);
            return;
        }

        var mainGrid = GetMainGrid();
        if (mainGrid == null)
            return;

        // Create the ErrorViewModel
        var errorMessage = $"An unexpected error occurred in the application.\n\n" +
                 $"Error: {exception.Message}\n\n" +
                 $"Type: {exception.GetType().Name}";
        var errorViewModel = new ErrorViewModel(_loggerFactory, errorMessage, exception);

        // Create the UserControl
        var errorUserControl = new ErrorUserControl(errorViewModel, _loggerFactory);

        // Create overlay panel
        var overlayPanel = BuildËrrorUserControlOverlayPanel(errorViewModel, errorUserControl);
        _currentErrorOverlay = overlayPanel;

        // Set up event handling for responding to user exiting the error dialog
        var taskCompletionSource = new TaskCompletionSource<bool>();
        errorUserControl.CloseRequested += (s, exit) =>
        {
            // Use TrySetResult to prevent InvalidOperationException if called multiple times
            taskCompletionSource.TrySetResult(exit);
        };

        // Show the overlay
        Grid.SetRowSpan(overlayPanel, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
        Grid.SetColumnSpan(overlayPanel, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
        mainGrid.Children.Add(overlayPanel);

        try
        {
            // Wait for the dialog to close with result
            var exit = await taskCompletionSource.Task;

            if (exit)
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
            mainGrid.Children.Remove(overlayPanel);
            _currentErrorOverlay = null;
        }
    }

    private Panel BuildËrrorUserControlOverlayPanel(ErrorViewModel errorViewModel, ErrorUserControl errorUserControl)
    {
        // Create a custom overlay with better modal behavior
        var overlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // More opaque overlay
            ZIndex = 1000
        };

        // Create a dialog container that looks like a proper modal
        var dialogContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),  // 1A202C, ViewDefaultBg
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 8,
                Blur = 25,
                Color = Color.FromArgb(128, 0, 0, 0)
            }),
            Margin = new Thickness(20), // Add margin from screen edges
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = errorUserControl // Direct child, no ScrollViewer wrapper
        };

        overlay.Children.Add(dialogContainer);

        return overlay;
    }

    private Grid? GetMainGrid()
    {
        // Find the main Grid.
        // We need to find the root Window's content Grid or MainView's Grid
        MainView? mainView = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainView = desktop.MainWindow?.Content as MainView;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            mainView = singleViewPlatform.MainView as MainView;
        }
        else
        {
            mainView = null;
        }

        if (mainView == null)
            return null;


        Grid? mainGrid = null;
        if (mainView?.Content is Grid mainViewGrid)
        {
            mainGrid = mainViewGrid;
        }

        return mainGrid;

        // Try to find the Window by walking up from TopLevel
        //var topLevel = TopLevel.GetTopLevel(this);

        //// Desktop scenario: C64ConfigDialog Window
        //if (topLevel is Window window)
        //{
        //    // Check if this is the C64ConfigDialog window (desktop)
        //    // The dialog's Content is the C64ConfigUserControl, which contains a Grid
        //    if (window is C64ConfigDialog dialog)
        //    {
        //        // Look for the Grid inside this C64ConfigUserControl
        //        // We can use the visual tree to find it
        //        if (this.Content is Grid thisGrid)
        //        {
        //            mainGrid = thisGrid;
        //        }
        //        else
        //        {
        //            // Try to find the first Grid child in this control
        //            mainGrid = this.FindDescendantOfType<Grid>();
        //        }
        //    }
        //    // Or if it's the main window with a Grid content
        //    else if (window.Content is Grid windowGrid)
        //    {
        //        mainGrid = windowGrid;
        //    }
        //}

        //// Browser scenario: Try to find MainView in the visual tree
        //if (mainGrid == null)
        //{
        //    var mainView = this.FindAncestorOfType<MainView>(true);
        //    if (mainView?.Content is Grid mainViewGrid)
        //    {
        //        mainGrid = mainViewGrid;
        //    }
        //}

        //// Last resort: try to find any Grid ancestor by walking the visual tree
        //if (mainGrid == null)
        //{
        //    var current = this.Parent;
        //    while (current != null)
        //    {
        //        if (current is Grid grid)
        //        {
        //            mainGrid = grid;
        //            break;
        //        }
        //        current = (current as Control)?.Parent;
        //    }
        //}


    }
}
