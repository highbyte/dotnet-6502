using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AvaloniaAnimation = Avalonia.Animation;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    private bool _isInitialized;

    private MainViewModel? _subscribedViewModel;
    private MonitorDialog? _monitorWindow;
    private Panel? _monitorOverlay;

    // For log auto-scroll
    private ScrollViewer? _logScrollViewer;
    private bool _logAutoScrollEnabled = true;

    // Parameterless constructor - child views created by XAML!
    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        this.AttachedToVisualTree += MainView_AttachedToVisualTree;
        // DataContext will be set from App.axaml.cs via DI
        // Child views (C64MenuView, StatisticsView, EmulatorView) are created by XAML
        // and get their DataContext through XAML bindings

        this.Loaded += OnViewLoaded;

        KeyDown += OnKeyDown;
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (_isInitialized)
                return;
            _isInitialized = true;

            // Start fade-in animation
            await FadeIn();

            if (DataContext is MainViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }

            // TODO: When not having the emulator started, need to set focus to the EmulatorView to capture keyboard input.
            //       When the emulator is running, Focusable should be set back to false.
            //Focus();
            //Focusable = true;
        });

    private async Task FadeIn()
    {
        var animation = new AvaloniaAnimation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(500),
            FillMode = AvaloniaAnimation.FillMode.Forward,
            Children =
            {
                new AvaloniaAnimation.KeyFrame
                {
                    Cue = new AvaloniaAnimation.Cue(0.0),
                    Setters = { new Setter(OpacityProperty, 0.0) }
                },
                new AvaloniaAnimation.KeyFrame
                {
                    Cue = new AvaloniaAnimation.Cue(1.0),
                    Setters = { new Setter(OpacityProperty, 1.0) }
                }
            }
        };

        await animation.RunAsync(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel's property changes
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested -= OnEmulatorOptionsRequested;
        }

        // Subscribe to new ViewModel's property changes
        _subscribedViewModel = DataContext as MainViewModel;
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested += OnEmulatorOptionsRequested;
            // Check immediately in case validation errors are already set
            CheckAndSelectValidationErrorsTab();
            // Listen for log changes
            _subscribedViewModel.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
            // Set up tab selection tracking
            SetupTabSelectionTracking();
        }
    }

    // If scale can change at runtime, listen for property changes
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Listen for changes to ValidationErrors property
        if (e.PropertyName == nameof(MainViewModel.ValidationErrors))
        {
            CheckAndSelectValidationErrorsTab();
        }

        if (e.PropertyName == nameof(MainViewModel.IsMonitorVisible) && DataContext is MainViewModel viewModel)
        {
            if (viewModel.IsMonitorVisible)
            {
                ShowMonitorUI();
            }
            else
            {
                // Ensure UI operations run on UI thread
                Dispatcher.UIThread.Post(() => CloseMonitorUI(), DispatcherPriority.Loaded);
            }
        }
    }

    private void CheckAndSelectValidationErrorsTab()
    {
        if (_subscribedViewModel == null)
            return;

        // Check the actual ValidationErrors collection instead of IsSystemConfigValid
        // to avoid timing issues with reactive property updates
        if (_subscribedViewModel.ValidationErrors == null ||
            _subscribedViewModel.ValidationErrors.Count == 0)
            return;

        // Use Dispatcher to ensure the control is properly initialized
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TabItem>("ConfigStatusTabItem") is TabItem configErrorsTab)
            {
                if (this.FindControl<TabControl>("InformationTabControl") is TabControl tabControl)
                {
                    tabControl.SelectedItem = configErrorsTab;
                }
            }
        });
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        // Initialization complete
    }

    // ComboBox SelectionChanged handlers - invoke commands
    private void OnSystemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedSystem = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedSystem))
            {
                // Fire and forget - let the ReactiveCommand handle scheduling and execution. This works in WebAssembly because we're not subscribing to the observable
                _ = viewModel.SelectSystemCommand.Execute(selectedSystem);
            }
        }
    }

    private void OnSystemVariantSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedVariant = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedVariant))
            {
                // Fire and forget - let the ReactiveCommand handle scheduling and execution
                // This works in WebAssembly because we're not subscribing to the observable
                _ = viewModel.SelectSystemVariantCommand.Execute(selectedVariant);
            }
        }
    }

    private void ShowMonitorUI()
    {
        // Check if running on WASM/Browser platform
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            // For WASM, show usercontrol overlay instead of Window
            ShowMonitorOverlay();
        }
        else
        {
            // For desktop platforms, use the Window dialog
            ShowMonitorWindow();
            //ShowMonitorOverlay();
        }
    }

    private void CloseMonitorUI()
    {
        CloseMonitorWindow();
        CloseMonitorOverlay();
    }

    private void ShowMonitorWindow()
    {
        if (_monitorWindow != null)
        {
            _monitorWindow.Activate();
            return;
        }

        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.MonitorViewModel == null)
            return;

        _monitorWindow = new MonitorDialog
        {
            DataContext = viewModel.MonitorViewModel
        };
        _monitorWindow.Closed += (sender, e) =>
          {
              _monitorWindow = null;
          };

        if (TopLevel.GetTopLevel(this) is Window owner)
            _ = _monitorWindow.ShowDialog(owner);
        else
            _monitorWindow.Show();
    }

    private void CloseMonitorWindow()
    {
        if (_monitorWindow == null)
            return;

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearMonitorViewModel();
        }

        var window = _monitorWindow;
        _monitorWindow = null;

        if (window.IsVisible)
            window.Close();
    }

    private void ShowMonitorOverlay()
    {
        if (_monitorOverlay != null)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.MonitorViewModel == null)
            return;

        var monitorControl = new MonitorUserControl(viewModel.MonitorViewModel)
        {
            MaxHeight = 600  // Limit height in Browser mode to prevent unbounded expansion
        };

        _monitorOverlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000
        };

        var dialogContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),  // 1A202C, ViewDefaultBg
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = monitorControl
        };

        _monitorOverlay.Children.Add(dialogContainer);

        if (Content is Grid mainGrid)
        {
            Grid.SetRowSpan(_monitorOverlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(_monitorOverlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
            mainGrid.Children.Add(_monitorOverlay);
        }
    }

    private void CloseMonitorOverlay()
    {
        Console.WriteLine("MainView: CloseMonitorOverlay called.");
        if (_monitorOverlay == null)
            return;

        Console.WriteLine("MainView: Removing monitor overlay from visual tree.");
        if (Content is Grid mainGrid && mainGrid.Children.Contains(_monitorOverlay))
            mainGrid.Children.Remove(_monitorOverlay);

        _monitorOverlay = null;

        if (DataContext is MainViewModel viewModel)
        {
            Console.WriteLine("MainView: Clearing MonitorViewModel.");
            viewModel.ClearMonitorViewModel();
        }

        // Restore focus to EmulatorView
        Console.WriteLine("MainView: Setting focus to EmulatorView.");
        var emulatorView = this.FindControl<EmulatorView>("EmulatorView");
        emulatorView?.Focus();
        Console.WriteLine("MainView: Focus set to EmulatorView.");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.EmulatorOptionsRequested -= OnEmulatorOptionsRequested;
            _subscribedViewModel.LogMessages.CollectionChanged -= LogMessages_CollectionChanged;
            _subscribedViewModel = null;
        }
    }
    private void MainView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Directly find the LogScrollViewer by name
        _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        _logScrollViewer?.ScrollChanged += LogScrollViewer_ScrollChanged;
    }

    private void LogScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScrollViewer == null)
            return;
        // If user is at the bottom, enable auto-scroll
        double tolerance = 2.0;
        bool isAtBottom = _logScrollViewer.Offset.Y >= _logScrollViewer.Extent.Height - _logScrollViewer.Viewport.Height - tolerance;
        _logAutoScrollEnabled = isAtBottom;
    }

    private void LogMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_logScrollViewer == null)
            return;
        if (_logAutoScrollEnabled)
        {
            // Scroll to bottom after layout/render
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(10); // Small delay to ensure layout
                double maxY = Math.Max(0, _logScrollViewer.Extent.Height - _logScrollViewer.Viewport.Height);
                _logScrollViewer.Offset = new Vector(_logScrollViewer.Offset.X, maxY);
            }, DispatcherPriority.Loaded);
        }
    }

    private void SetupTabSelectionTracking()
    {
        // Find the TabControl and set up selection change tracking
        var tabControl = this.FindControl<TabControl>("InformationTabControl");
        if (tabControl != null)
        {
            // Subscribe to tab selection changes
            tabControl.SelectionChanged += OnTabSelectionChanged;
            // Initialize current tab name
            if (_subscribedViewModel != null && tabControl.SelectedItem is TabItem selectedTab)
            {
                _subscribedViewModel.SelectedTabName = selectedTab.Name ?? "";
            }
        }
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_subscribedViewModel != null && sender is TabControl tabControl && tabControl.SelectedItem is TabItem selectedTab)
        {
            _subscribedViewModel.SelectedTabName = selectedTab.Name ?? "";
        }
    }

    // Public property to access the EmulatorView
    public EmulatorView? GetEmulatorView()
    {
        return this.FindControl<EmulatorView>("EmulatorView");
    }

    /// <summary>
    /// Handle key down events and forward them to the host app
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Prevent keys from being processed by Avalonia's focus system
        //e.Handled = true;
        //HostApp?.OnKeyDown(e.Key, e.KeyModifiers);

        // Check for Ctrl+Shift+D to open the sound debug overlay
        if (e.Key == Key.D && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            ShowSoundDebug();
        }
    }

    private void OpenSoundDebug_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(ShowSoundDebug);

    private async Task ShowSoundDebug()
    {
        // Only allow opening the sound debug overlay when the emulator is uninitialized
        if (_subscribedViewModel.HostApp.EmulatorState != EmulatorState.Uninitialized)
            return;

        await SoundDebugUserControlOverlay();
    }

    private async Task SoundDebugUserControlOverlay()
    {
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            System.Console.WriteLine("Error: Could not get service provider");
            return;
        }

        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var wavePlayerFactory = new WavePlayerFactory(loggerFactory, _subscribedViewModel!.HostApp.EmulatorConfig);
        var wavePlayer = wavePlayerFactory.CreateWavePlayer();

        // Create the UserControl-based config
        var configControl = new DebugSoundUserControl
        {
            DataContext = new DebugSoundViewModel(
                _subscribedViewModel!.HostApp!,
                serviceProvider.GetRequiredService<IConfiguration>(),
                loggerFactory,
                wavePlayer)
        };

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.CloseRequested += (s, closed) =>
        {
            taskCompletionSource.SetResult(closed);
        };

        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the dialog to complete
        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            // Clean up - remove the overlay
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OpenGamepadDebug_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(ShowGamepadDebug);

    private async Task ShowGamepadDebug()
    {
        // Only allow opening the gamepad debug overlay when the emulator is uninitialized
        if (_subscribedViewModel?.HostApp.EmulatorState != EmulatorState.Uninitialized)
            return;

        await GamepadDebugUserControlOverlay();
    }

    private async Task GamepadDebugUserControlOverlay()
    {
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            System.Console.WriteLine("Error: Could not get service provider");
            return;
        }

        if (_subscribedViewModel?.HostApp == null)
            return;

        // Create the UserControl-based config
        var configControl = new DebugGamepadUserControl
        {
            DataContext = new DebugGamepadViewModel(_subscribedViewModel.HostApp)
        };

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.CloseRequested += (s, closed) =>
        {
            taskCompletionSource.SetResult(closed);
        };


        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the dialog to complete
        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            // Clean up - remove the overlay
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OnEmulatorOptionsRequested(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(EmulatorOptionsUserControlOverlay);

    private async Task EmulatorOptionsUserControlOverlay()
    {
        if (_subscribedViewModel?.HostApp == null)
            return;

        // Get IConfiguration from DI
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
            return;

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // Create the UserControl-based config
        var configControl = new EmulatorConfigUserControl
        {
            DataContext = new EmulatorConfigViewModel(_subscribedViewModel.HostApp, configuration)
        };
        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (s, saved) =>
        {
            if (saved)
            {
                // Refresh config-dependent properties in MainViewModel
                _subscribedViewModel?.RefreshConfigProperties();
            }
            taskCompletionSource.SetResult(saved);
        };

        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);

        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the dialog to complete
        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            // Clean up - remove the overlay
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    /// <summary>
    /// Safely executes an async operation with global exception handling for WASM compatibility.
    /// Use this in async void event handlers instead of direct await.
    /// </summary>
    private async void SafeExecuteAsync(Func<Task> asyncAction)
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            App.WasmExceptionHandler?.Invoke(ex);
        }
    }
}
