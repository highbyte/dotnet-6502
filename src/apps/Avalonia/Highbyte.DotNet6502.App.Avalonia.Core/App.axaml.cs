using System;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.Input;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.App.Avalonia.Core.Views;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Logging.InMem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly Func<HttpClient>? _getHttpClient;
    private readonly Func<string, Task<string>>? _getCustomConfigJson;
    private readonly Func<string, string, Task>? _saveCustomConfigJson;

    public static AvaloniaHostApp? HostApp { get; protected internal set; }

    /// <summary>
    /// Used from Desktop app where resources are loaded from local file system and logs are stored in memory.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="logStore"></param>
    /// <param name="logConfig"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="loadResourcesFromHttp"></param>
    /// <param name="getHttpClient"></param>
    /// <param name="getCustomConfigJson"></param>
    /// <param name="saveCustomConfigJson"></param>
    public App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        DotNet6502InMemLogStore logStore,
        DotNet6502InMemLoggerConfiguration logConfig,
        ILoggerFactory loggerFactory,
        bool loadResourcesFromHttp = false,
        Func<HttpClient>? getHttpClient = null,
        Func<string, Task<string>>? getCustomConfigJson = null,
        Func<string, string, Task>? saveCustomConfigJson = null) : this(configuration, emulatorConfig, loggerFactory)
    {
        _logStore = logStore;
        _logConfig = logConfig;
    }

    /// <summary>
    /// Used from Browser app where resources are loaded from HTTP and logs are written to browser F12 console.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="emulatorConfig"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="getHttpClient"></param>
    /// <param name="getCustomConfigJson"></param>
    /// <param name="saveCustomConfigJson"></param>
    public App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        ILoggerFactory loggerFactory,
        Func<HttpClient> getHttpClient,
        Func<string, Task<string>> getCustomConfigJson,
        Func<string, string, Task> saveCustomConfigJson) : this(configuration, emulatorConfig, loggerFactory)
    {
        _getHttpClient = getHttpClient;
        _getCustomConfigJson = getCustomConfigJson;
        _saveCustomConfigJson = saveCustomConfigJson;
    }

    private App(
        IConfiguration configuration,
        EmulatorConfig emulatorConfig,
        ILoggerFactory loggerFactory)
    {
        Console.WriteLine("App constructor called");

        try
        {
            _configuration = configuration;
            _emulatorConfig = emulatorConfig;
            _loggerFactory = loggerFactory;
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

    // Allow other assemblies (e.g., Web) to set HostApp safely without exposing a public setter on the property
    public static void SetHostApp(AvaloniaHostApp hostApp)
    {
        HostApp = hostApp;
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

        // Initialize the emulator host app
        InitializeHostApp();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();

        Console.WriteLine("AppOnFrameworkInitializationCompleted end");

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

    private void InitializeHostApp()
    {
        try
        {
            HttpClient? httpClient = _getHttpClient != null ? _getHttpClient() : null;
            Func<string, Task<string>>? getCustomConfigJson = _getCustomConfigJson ?? null;
            Func<string, string, Task>? saveCustomConfigJson = _saveCustomConfigJson ?? null;

            // ----------
            // Get systems
            // ----------
            var systemList = new SystemList<AvaloniaInputHandlerContext, NullAudioHandlerContext>();

            var c64Setup = new C64Setup(_loggerFactory, _configuration, httpClient, getCustomConfigJson, saveCustomConfigJson);
            systemList.AddSystem(c64Setup);

            var genericComputerSetup = new GenericComputerSetup(_loggerFactory, _configuration, httpClient, getCustomConfigJson, saveCustomConfigJson);
            systemList.AddSystem(genericComputerSetup);

            // ----------
            // Create AvaloniaHostApp
            // ----------
            _emulatorConfig.Validate(systemList);

            HostApp = new AvaloniaHostApp(
                systemList,
                _loggerFactory,
                _emulatorConfig,
                _logStore,
                _logConfig);

            // Select default system but don't start emulation yet
            // In WebAssembly, DON'T auto-select the system - let the user select it manually from the UI
            // This avoids race conditions and runtime errors during WASM initialization
            if (!PlatformDetection.IsRunningInWebAssembly())
            {
                // Desktop: can safely use .Wait() during initialization
                HostApp.SelectSystem(_emulatorConfig.DefaultEmulator).Wait();
            }
            else
            {
                // WebAssembly: Can't Wait() during initialization - must be async
                HostApp.SelectSystem(_emulatorConfig.DefaultEmulator);
            }
            _logger.LogInformation($"Default system '{_emulatorConfig.DefaultEmulator}' selected during initialization");

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
        if (HostApp?.EmulatorState == EmulatorState.Running)
        {
            HostApp.Pause();
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
                    if (HostApp?.EmulatorState == EmulatorState.Paused)
                    {
                        _ = HostApp.Start(); // Fire and forget
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
