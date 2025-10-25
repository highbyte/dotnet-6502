using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    // Access HostApp through ViewModel
    private AvaloniaHostApp? HostApp => (DataContext as MainViewModel)?.HostApp;

    private AvaloniaHostApp? _subscribedHostApp;
    private MonitorDialog? _monitorWindow;
    private Panel? _monitorOverlay;

    // Parameterless constructor - child views created by XAML!
    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // DataContext will be set from App.axaml.cs via DI
        // Child views (C64MenuView, StatisticsView, EmulatorView) are created by XAML
        // and get their DataContext through XAML bindings

    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedHostApp != null)
            _subscribedHostApp.MonitorVisibilityChanged -= OnMonitorVisibilityChanged;

        _subscribedHostApp = HostApp;

        if (_subscribedHostApp != null)
            _subscribedHostApp.MonitorVisibilityChanged += OnMonitorVisibilityChanged;
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
                    viewModel.OnSystemSelectionCompleted();
                    viewModel.ForceStateRefresh();
                }
                catch (Exception)
                {
                    // Handle exception if needed - the UI will reflect the actual state from HostApp
                    viewModel.ForceStateRefresh();
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
                    viewModel.ForceStateRefresh();
                }
                catch (Exception)
                {
                    // Handle exception if needed - the UI will reflect the actual state from HostApp
                    viewModel.ForceStateRefresh();
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
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }

                FocusEmulator();
            }
            catch (Exception)
            {
                // Handle exception if needed
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
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
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
            }
            catch (Exception)
            {
                // Handle exception if needed
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
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
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
            }
            catch (Exception)
            {
                // Handle exception if needed
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
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
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
            }
            catch (Exception)
            {
                // Handle exception if needed
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
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
        catch (Exception)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ForceStateRefresh();
            }
        }
    }

    private void OnMonitorVisibilityChanged(object? sender, bool isVisible)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (isVisible)
                ShowMonitorUI();
            else
                CloseMonitorUI();
        });
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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_subscribedHostApp != null)
        {
            _subscribedHostApp.MonitorVisibilityChanged -= OnMonitorVisibilityChanged;
            _subscribedHostApp = null;
        }

        CloseMonitorUI();
    }

    private async void StatsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                HostApp.ToggleStatisticsPanel();
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
            }
            catch (Exception)
            {
                // Handle exception if needed
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ForceStateRefresh();
                }
            }
        }
    }

    // Public property to access the EmulatorView
    public EmulatorView? GetEmulatorView()
    {
        return this.FindControl<EmulatorView>("EmulatorView");
    }

    private void FocusEmulator()
    {
        // Use Dispatcher to ensure focus is set after the UI has finished processing
        global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
           {
               // First, ensure the main window has focus
               if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
               {
                   var mainWindow = desktop.MainWindow;
                   if (mainWindow != null)
                   {
                       mainWindow.Activate();
                       mainWindow.Focus();
                   }
               }

               // Then focus the emulator view
               HostApp?.EmulatorView?.Focus();
           }, global::Avalonia.Threading.DispatcherPriority.Loaded);
    }
}
