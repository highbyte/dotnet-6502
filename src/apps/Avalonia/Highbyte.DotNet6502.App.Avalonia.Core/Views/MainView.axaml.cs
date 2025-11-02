using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Layout;
using System.Threading.Tasks;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    private bool _isInitialized;

    private AvaloniaHostApp? HostApp => (DataContext as MainViewModel)?.HostApp; // Access HostApp through ViewModel

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
    }

    private async void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (_isInitialized)
            return;
        _isInitialized = true;

        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous ViewModel's property changes
        if (_subscribedViewModel != null)
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // Subscribe to new ViewModel's property changes
        _subscribedViewModel = DataContext as MainViewModel;
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            // Check immediately in case validation errors are already set
            CheckAndSelectValidationErrorsTab();
            // Listen for log changes
            _subscribedViewModel.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
        }
    }

    // If scale can change at runtime, listen for property changes
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Listen for changes to HasValidationErrors property
        if (e.PropertyName == nameof(MainViewModel.ValidationErrors))
        {
            CheckAndSelectValidationErrorsTab();
        }

        if (e.PropertyName == nameof(MainViewModel.IsMonitorVisible) && DataContext is MainViewModel viewModel)
        {
            if (viewModel.IsMonitorVisible)
                ShowMonitorUI();
            else
                CloseMonitorUI();
        }
    }

    private void CheckAndSelectValidationErrorsTab()
    {
        if (_subscribedViewModel == null || _subscribedViewModel.EmulatorStateFlags.IsSystemConfigValid)
            return;

        // Use Dispatcher to ensure the control is properly initialized
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<TabItem>("ConfigErrorsTabItem") is TabItem configErrorsTab)
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

    // Keep the same selection handler pattern
    private async void OnSystemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HostApp == null)
            return;

        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedSystem = e.AddedItems[0]?.ToString();

            if (!string.IsNullOrEmpty(selectedSystem) && HostApp.SelectedSystemName != selectedSystem)
            {
                try
                {
                    await HostApp.SelectSystem(selectedSystem);
                }
                catch (Exception)
                {
                    // Handle exception if needed - the UI will reflect the actual state from HostApp
                }
            }
        }
    }

    private async void OnSystemVariantSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (HostApp == null)
            return;
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedVariant = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedVariant) && HostApp.SelectedSystemConfigurationVariant != selectedVariant)
            {
                try
                {
                    await HostApp.SelectSystemConfigurationVariant(selectedVariant);
                }
                catch (Exception)
                {
                    // Handle exception if needed - the UI will reflect the actual state from HostApp
                }
            }
        }
    }

    // Emulator Control Event Handlers
    private async void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                await HostApp.Start();
            }
            catch (Exception)
            {
                // Handle exception if needed
            }
        }
    }

    private void PauseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                HostApp.Pause();
            }
            catch (Exception)
            {
                // Handle exception if needed
            }
        }
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                HostApp.Stop();
            }
            catch (Exception)
            {
                // Handle exception if needed
            }
        }
    }

    private async void ResetButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                await HostApp.Reset();
            }
            catch (Exception)
            {
                // Handle exception if needed
            }
        }
    }

    private void MonitorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp == null)
            return;

        try
        {
            HostApp.ToggleMonitor();
        }
        catch (Exception ex)
        {
        }
    }

    private void ShowMonitorUI()
    {
        if (HostApp?.Monitor == null)
            return;

        if (PlatformDetection.IsRunningInWebAssembly())
        {
            ShowMonitorOverlay();
        }
        else
        {
            ShowMonitorWindow();
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

        if (HostApp?.Monitor == null)
            return;

        _monitorWindow = new MonitorDialog(HostApp, HostApp.Monitor);
        _monitorWindow.Closed += MonitorWindowClosed;

        if (TopLevel.GetTopLevel(this) is Window owner)
            _ = _monitorWindow.ShowDialog(owner);
        else
            _monitorWindow.Show();
    }

    private void MonitorWindowClosed(object? sender, EventArgs e)
    {
        if (_monitorWindow != null)
        {
            _monitorWindow.Closed -= MonitorWindowClosed;
            _monitorWindow = null;
        }

        if (HostApp?.Monitor?.IsVisible == true)
            HostApp.DisableMonitor();
    }

    private void CloseMonitorWindow()
    {
        if (_monitorWindow == null)
            return;

        var window = _monitorWindow;
        _monitorWindow = null;
        window.Closed -= MonitorWindowClosed;

        if (window.IsVisible)
            window.Close();
    }

    private void ShowMonitorOverlay()
    {
        if (_monitorOverlay != null)
            return;

        if (HostApp?.Monitor == null)
            return;

        var monitorControl = new MonitorUserControl(HostApp, HostApp.Monitor)
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
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
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
        if (_monitorOverlay == null)
            return;

        if (Content is Grid mainGrid && mainGrid.Children.Contains(_monitorOverlay))
            mainGrid.Children.Remove(_monitorOverlay);

        _monitorOverlay = null;

        // Restore focus to EmulatorView after closing overlay in Browser mode
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            Dispatcher.UIThread.Post(() =>
            {
                var emulatorView = this.FindControl<EmulatorView>("EmulatorView");
                emulatorView?.Focus();
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel.LogMessages.CollectionChanged -= LogMessages_CollectionChanged;
            _subscribedViewModel = null;
        }
    }
    private void MainView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Directly find the LogScrollViewer by name
        _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        if (_logScrollViewer != null)
        {
            _logScrollViewer.ScrollChanged += LogScrollViewer_ScrollChanged;
        }
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

    private async void StatsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                HostApp.ToggleStatisticsPanel();
            }
            catch (Exception ex)
            {
                // Handle exception if needed
            }
        }
    }

    // Public property to access the EmulatorView
    public EmulatorView? GetEmulatorView()
    {
        return this.FindControl<EmulatorView>("EmulatorView");
    }
}
