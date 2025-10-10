using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using System.Runtime.InteropServices;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext ??= new MainViewModel();
    }

    // Keep the same selection handler pattern used in MainWindow
    private void OnSystemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedSystem = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedSystem) && viewModel.SelectSystemCommand.CanExecute(selectedSystem))
            {
                viewModel.SelectSystemCommand.Execute(selectedSystem);
            }
        }
    }

    private void OnSystemVariantSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.AddedItems.Count > 0)
        {
            var selectedVariant = e.AddedItems[0]?.ToString();
            if (!string.IsNullOrEmpty(selectedVariant) && viewModel.SelectSystemVariantCommand.CanExecute(selectedVariant))
            {
                viewModel.SelectSystemVariantCommand.Execute(selectedVariant);
            }
        }
    }

    private async void OpenC64Config_Click(object? sender, RoutedEventArgs e)
    {
        if (App.HostApp == null)
            return;

        if (App.HostApp.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
            return;

        var renderProviderOptions = App.HostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();

        // Check if running on WASM/Browser platform
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            // For WASM, use ContentDialog instead of Window
            await ShowC64ConfigContentDialog(c64HostConfig, renderProviderOptions);
        }
        else
        {
            // For desktop platforms, use the Window dialog
            await ShowC64ConfigDialog(c64HostConfig, renderProviderOptions);
            //await ShowC64ConfigContentDialog(c64HostConfig, renderProviderOptions);
        }
    }

    private async Task ShowC64ConfigDialog(C64HostConfig c64HostConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderProviderOptions)
    {
        var dialog = new C64ConfigDialog(App.HostApp!, c64HostConfig, renderProviderOptions);

        bool? result;
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            result = await dialog.ShowDialog<bool?>(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource<bool?>();
            dialog.Closed += (_, _) => tcs.TrySetResult(dialog.DialogResultValue);
            dialog.Show();
            result = await tcs.Task;
        }

        if (result == true && DataContext is MainViewModel viewModel)
        {
            viewModel.ForceStateRefresh();
        }
    }

    private async Task ShowC64ConfigContentDialog(C64HostConfig c64HostConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderProviderOptions)
    {
        // Create the UserControl-based config
        var configControl = new C64ConfigUserControl(App.HostApp!, c64HostConfig, renderProviderOptions);

        // Create a custom overlay with better modal behavior
        var overlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // More opaque overlay
            ZIndex = 1000
        };

        // Create a dialog container that looks like a proper modal
        // No ScrollViewer needed since the two-column layout is designed to fit without scrolling
        var dialogContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)), // Dark gray background
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
            Child = configControl // Direct child, no ScrollViewer wrapper
        };

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (s, saved) =>
        {
            taskCompletionSource.SetResult(saved);
        };

        overlay.Children.Add(dialogContainer);

        // Get the main Grid and add overlay
        if (this.Content is Grid mainGrid)
        {
            Grid.SetRowSpan(overlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(overlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
            mainGrid.Children.Add(overlay);

            try
            {
                // Wait for the configuration to complete
                var result = await taskCompletionSource.Task;

                if (result && DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.ForceStateRefresh();
                }
            }
            finally
            {
                // Clean up - remove the overlay
                mainGrid.Children.Remove(overlay);
            }
        }
    }
}
