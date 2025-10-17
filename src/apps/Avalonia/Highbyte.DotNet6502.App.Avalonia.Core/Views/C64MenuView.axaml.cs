using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;
using Highbyte.DotNet6502.Systems.Commodore64.Render.CustomPayload;
using Highbyte.DotNet6502.Systems.Commodore64.Render.Rasterizer;
using Highbyte.DotNet6502.Systems.Commodore64.Render.VideoCommands;
using Microsoft.Extensions.Logging;
using Avalonia.VisualTree;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64MenuView : UserControl
{
    public C64MenuView()
    {
        InitializeComponent();
    }

    private void UpdateDiskToggleButtonStyle()
    {
        var diskToggleButton = this.FindControl<Button>("DiskToggleButton");
        if (diskToggleButton != null && DataContext is ViewModels.C64MenuViewModel viewModel)
        {
            // Clear existing classes
            diskToggleButton.Classes.Clear();

            // Add the appropriate class based on disk attachment state
            if (viewModel.IsDiskImageAttached)
            {
                diskToggleButton.Classes.Add("warning");
            }
            else
            {
                diskToggleButton.Classes.Add("secondary");
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

        if (result == true && DataContext is C64MenuViewModel viewModel)
        {
            viewModel.NotifyEmulatorStateChanged();
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

        // Get the parent main Grid and add overlay
        if (this.GetVisualRoot() is Window window && window.Content is Grid mainGrid)
        {
            Grid.SetRowSpan(overlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(overlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
            mainGrid.Children.Add(overlay);

            try
            {
                // Wait for the configuration to complete
                var result = await taskCompletionSource.Task;

                if (result && DataContext is C64MenuViewModel c64ViewModel)
                {
                    c64ViewModel.NotifyEmulatorStateChanged();
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

    // Core C64 functionality methods
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

            // Notify the ViewModel that the disk image state has changed
            if (DataContext is ViewModels.C64MenuViewModel viewModel)
            {
                viewModel.NotifyDiskImageStateChanged();
            }

            // Update button styling
            UpdateDiskToggleButtonStyle();
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
                    await stream.ReadExactlyAsync(fileBuffer);

                    // Parse the D64 disk image
                    var d64DiskImage = Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.ParseD64File(fileBuffer);

                    // Set the disk image on the running C64's DiskDrive1541
                    var c64 = (C64)App.HostApp!.CurrentRunningSystem!;
                    var diskDrive = c64.IECBus?.Devices?.OfType<Systems.Commodore64.TimerAndPeripheral.DiskDrive.DiskDrive1541>().FirstOrDefault();
                    diskDrive?.SetD64DiskImage(d64DiskImage);
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

    private void ToggleDiskSection_Click(object? sender, RoutedEventArgs e)
    {
        var headerButton = this.FindControl<Button>("DiskSectionHeader");
        var contentBorder = this.FindControl<Border>("DiskSectionContent");

        if (headerButton != null && contentBorder != null)
        {
            contentBorder.IsVisible = !contentBorder.IsVisible;
            headerButton.Content = contentBorder.IsVisible ? "▼ Disk Drive & .D64 images" : "▶ Disk Drive & .D64 images";
        }
    }

    private void ToggleLoadSaveSection_Click(object? sender, RoutedEventArgs e)
    {
        var headerButton = this.FindControl<Button>("LoadSaveSectionHeader");
        var contentBorder = this.FindControl<Border>("LoadSaveSectionContent");

        if (headerButton != null && contentBorder != null)
        {
            contentBorder.IsVisible = !contentBorder.IsVisible;
            headerButton.Content = contentBorder.IsVisible ? "▼ Load/Save" : "▶ Load/Save";
        }
    }

    private void ToggleConfigSection_Click(object? sender, RoutedEventArgs e)
    {
        var headerButton = this.FindControl<Button>("ConfigSectionHeader");
        var contentBorder = this.FindControl<Border>("ConfigSectionContent");

        if (headerButton != null && contentBorder != null)
        {
            contentBorder.IsVisible = !contentBorder.IsVisible;
            headerButton.Content = contentBorder.IsVisible ? "▼ Configuration" : "▶ Configuration";
        }
    }

    // File operation event handlers
    private async void LoadPreloadedDisk_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not C64MenuViewModel viewModel)
            return;

        string selectedPreloadedDisk = viewModel.SelectedPreloadedDisk;
        if (string.IsNullOrEmpty(selectedPreloadedDisk) || !_preloadedD64Images.ContainsKey(selectedPreloadedDisk))
            return;

        var diskInfo = _preloadedD64Images[selectedPreloadedDisk];
        _isLoadingPreloadedDisk = true;
        _latestPreloadedDiskError = "";

        System.Console.WriteLine($"Starting to load preloaded disk: {diskInfo.DisplayName}");

        try
        {
            // Initialize D64AutoDownloadAndRun if not already done
            if (_d64AutoDownloadAndRun == null)
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                var c64HostConfig = App.HostApp!.CurrentHostSystemConfig as C64HostConfig;
                _d64AutoDownloadAndRun = new D64AutoDownloadAndRun(
                    loggerFactory,
                    httpClient,
                    App.HostApp!,
                    corsProxyUrl: PlatformDetection.IsRunningInWebAssembly() ? c64HostConfig.CorsProxyURL: null);
            }

            await _d64AutoDownloadAndRun.DownloadAndRunDiskImage(
                diskInfo,
                stateHasChangedCallback: async () =>
                {
                    viewModel.NotifyEmulatorStateChanged();
                    await Task.CompletedTask;
                },
                setConfigCallback: async (diskInfo) =>
                {
                    if (App.HostApp?.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
                        return;

                    var c64SystemConfig = c64HostConfig.SystemConfig;

                    // Apply keyboard joystick settings to config object while emulator is stopped
                    c64SystemConfig.KeyboardJoystickEnabled = diskInfo.KeyboardJoystickEnabled;
                    c64SystemConfig.KeyboardJoystick = diskInfo.KeyboardJoystickNumber;

                    // Apply renderer setting to config object while emulator is stopped
                    // TODO: If/when a optimized RenderType for use without bitmap graphics is available, set rendererProviderType appropriately here.
                    //Type rendererProviderType = diskInfo.RequiresBitmap ? typeof(Vic2Rasterizer) : typeof(C64VideoCommandStream);
                    Type rendererProviderType = typeof(Vic2Rasterizer);
                    c64HostConfig.SystemConfig.SetRenderProviderType(rendererProviderType);

                    // Apply audio enabled setting to config object while emulator is stopped
                    c64SystemConfig.AudioEnabled = diskInfo.AudioEnabled;

                    // Apply C64 variant setting to config object while emulator is stopped
                    await App.HostApp.SelectSystemConfigurationVariant(diskInfo.C64Variant);

                    App.HostApp.UpdateHostSystemConfig(c64HostConfig);
                });
        }
        catch (Exception ex)
        {
            _latestPreloadedDiskError = $"Error downloading or running disk image: {ex.Message}";
            System.Console.WriteLine($"LoadPreloadedDisk_Click error: {_latestPreloadedDiskError}");
        }
        finally
        {
            _isLoadingPreloadedDisk = false;
            System.Console.WriteLine($"Finished loading preloaded disk. Loading state: {_isLoadingPreloadedDisk}");
            if (!string.IsNullOrEmpty(_latestPreloadedDiskError))
                System.Console.WriteLine($"Final error state: {_latestPreloadedDiskError}");
            viewModel.NotifyEmulatorStateChanged();
        }
    }

    private void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement Basic file loading functionality
        System.Console.WriteLine("LoadBasicFile_Click - Not implemented yet");
    }

    private void SaveBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement Basic file saving functionality
        System.Console.WriteLine("SaveBasicFile_Click - Not implemented yet");
    }

    private void LoadBinaryFile_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement binary file loading functionality
        System.Console.WriteLine("LoadBinaryFile_Click - Not implemented yet");
    }

    private void LoadAssemblyExample_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement assembly example loading functionality
        System.Console.WriteLine("LoadAssemblyExample_Click - Not implemented yet");
    }

    private void LoadBasicExample_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement Basic example loading functionality
        System.Console.WriteLine("LoadBasicExample_Click - Not implemented yet");
    }

    // Private fields for preloaded disk functionality
    private readonly Dictionary<string, D64DownloadDiskInfo> _preloadedD64Images = new()
    {
        {"bubblebobble", new D64DownloadDiskInfo("Bubble Bobble", "https://csdb.dk/release/download.php?id=191127", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false, directLoadPRGName: "*")}, // Note: Bubble Bobble is not a bitmap game, but somehow this version fails to initialize the custom charset in text mode correctly in SkiaSharp renderer.
        {"digiloi", new D64DownloadDiskInfo("Digiloi", "https://csdb.dk/release/download.php?id=213381", keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"elite", new D64DownloadDiskInfo("Elite", "https://csdb.dk/release/download.php?id=70413", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false,directLoadPRGName: "*", c64Variant: "C64PAL")},
        {"lastninja", new D64DownloadDiskInfo("Last Ninja", "https://csdb.dk/release/download.php?id=101848", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, requiresBitmap: true, audioEnabled: false, directLoadPRGName: "*")},
        {"minizork", new D64DownloadDiskInfo("Mini Zork", "https://csdb.dk/release/download.php?id=42919", audioEnabled: false, directLoadPRGName: "*")},
        {"montezuma", new D64DownloadDiskInfo("Montezuma's Revenge", "https://csdb.dk/release/download.php?id=128101", downloadType: DownloadType.ZIP, keyboardJoystickEnabled: true, keyboardJoystickNumber: 2, audioEnabled: true, directLoadPRGName: "*")},
        {"rallyspeedway", new D64DownloadDiskInfo("Rally Speedway", "https://csdb.dk/release/download.php?id=219614", keyboardJoystickEnabled: true, keyboardJoystickNumber: 1, audioEnabled: true, directLoadPRGName: "*")}
    };

    // Fields for preloaded disk loading functionality
    private string _latestPreloadedDiskError = "";
    private bool _isLoadingPreloadedDisk = false;
    private D64AutoDownloadAndRun? _d64AutoDownloadAndRun;
}