using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext ??= new MainViewModel();
        Focusable = false;
    }

    private void MainView_Loaded(object? sender, RoutedEventArgs e)
    {
        // Initialization complete
    }

    // Keep the same selection handler pattern used in MainWindow
    private async void OnSystemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedSystem = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedSystem) && App.HostApp != null)
            {
                try
                {
                    await App.HostApp.SelectSystem(selectedSystem);
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
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedVariant = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedVariant) && App.HostApp != null)
            {
                try
                {
                    await App.HostApp.SelectSystemConfigurationVariant(selectedVariant);
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
        if (App.HostApp != null)
        {
            try
            {
                await App.HostApp.Start();
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
        if (App.HostApp != null)
        {
            try
            {
                App.HostApp.Pause();
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
        if (App.HostApp != null)
        {
            try
            {
                App.HostApp.Stop();
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
        if (App.HostApp != null)
        {
            try
            {
                await App.HostApp.Reset();
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
            App.HostApp.EmulatorView.Focus();
        }, global::Avalonia.Threading.DispatcherPriority.Loaded);
    }
}
