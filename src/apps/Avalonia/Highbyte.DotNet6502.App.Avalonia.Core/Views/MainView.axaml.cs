using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Utils;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Linq;

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

    // C64-specific event handlers
    private async void CopyBasicSource_Click(object? sender, RoutedEventArgs e)
    {
        await CopyBasicSourceCode();
    }

    private async void PasteText_Click(object? sender, RoutedEventArgs e)
    {
        await PasteText();
    }

    private async void ToggleDiskImage_Click(object? sender, RoutedEventArgs e)
    {
        await ToggleDiskImage();
    }

    private void OpenBasicAssistantInfo_Click(object? sender, RoutedEventArgs e)
    {
        // Open the info link for Basic coding assistant
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/highbyte/dotnet-6502/blob/master/doc/SYSTEMS_C64_AI_CODE_COMPLETION.md"));
        }
    }

    private void OpenDiskInfo_Click(object? sender, RoutedEventArgs e)
    {
        // Open the info link for disk functionality
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.Launcher.LaunchUriAsync(new Uri("https://github.com/highbyte/dotnet-6502/blob/master/doc/SYSTEMS_C64_COMPATIBLE_PRG.md"));
        }
    }

    // The actual implementation methods will be connected to the ViewModel commands
    // by updating the ViewModel command initialization to call these methods
    public async Task CopyBasicSourceCode()
    {
        if (App.HostApp?.EmulatorState != Systems.EmulatorState.Running || 
            !IsC64System())
            return;

        try
        {
            var c64 = (C64)App.HostApp.CurrentRunningSystem!;
            var sourceCode = c64.BasicTokenParser.GetBasicText();
            
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                await topLevel.Clipboard?.SetTextAsync(sourceCode.ToLower())!;
            }
        }
        catch (Exception ex)
        {
            // Handle error - could show a dialog or log it
            System.Console.WriteLine($"Error copying Basic source: {ex.Message}");
        }
    }

    public async Task PasteText()
    {
        if (App.HostApp?.EmulatorState != Systems.EmulatorState.Running || 
            !IsC64System())
            return;

        try
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                var text = await topLevel.Clipboard?.GetTextAsync()!;
                if (!string.IsNullOrEmpty(text))
                {
                    var c64 = (C64)App.HostApp.CurrentRunningSystem!;
                    c64.TextPaste.Paste(text);
                }
            }
        }
        catch (Exception ex)
        {
            // Handle error
            System.Console.WriteLine($"Error pasting text: {ex.Message}");
        }
    }

    public async Task ToggleDiskImage()
    {
        if (App.HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || 
            !IsC64System())
            return;

        try
        {
            var c64 = (C64)App.HostApp.CurrentRunningSystem!;
            var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();
            
            if (diskDrive?.IsDisketteInserted == true)
            {
                // Detach current disk image
                diskDrive.RemoveD64DiskImage();
            }
            else
            {
                // Attach new disk image - open file dialog
                await AttachDiskImage();
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error toggling disk image: {ex.Message}");
        }
    }

    private async Task AttachDiskImage()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        var storageProvider = topLevel.StorageProvider;
        if (storageProvider.CanOpen)
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select D64 Disk Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("D64 Disk Images") { Patterns = new[] { "*.d64" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                try
                {
                    await using var stream = await files[0].OpenReadAsync();
                    var fileBuffer = new byte[stream.Length];
                    await stream.ReadAsync(fileBuffer);

                    // Parse the D64 disk image
                    var d64DiskImage = Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.ParseD64File(fileBuffer);
                    
                    // Set the disk image on the running C64's DiskDrive1541
                    var c64 = (C64)App.HostApp!.CurrentRunningSystem!;
                    var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();
                    if (diskDrive != null)
                    {
                        diskDrive.SetD64DiskImage(d64DiskImage);
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error loading disk image: {ex.Message}");
                }
            }
        }
    }

    private bool IsC64System()
    {
        return string.Equals(App.HostApp?.SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);
    }
}
