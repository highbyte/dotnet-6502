using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    // Access HostApp through ViewModel
    private AvaloniaHostApp? HostApp => (DataContext as MainViewModel)?.HostApp;

    // Parameterless constructor - child views created by XAML!
    public MainView()
    {
        InitializeComponent();
        // DataContext will be set from App.axaml.cs via DI
        // Child views (C64MenuView, StatisticsView, EmulatorView) are created by XAML
        // and get their DataContext through XAML bindings

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

    private async void MonitorButton_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp != null)
        {
            try
            {
                // TODO
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
