using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext ??= new MainViewModel();
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
}
