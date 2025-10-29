using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems.Commodore64;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64MenuView : UserControl
{
    // Access ViewModel and HostApp through DataContext
    private C64MenuViewModel? ViewModel => DataContext as C64MenuViewModel;
    private AvaloniaHostApp? HostApp => ViewModel?.HostApp;

    // Parameterless constructor for XAML compatibility
    public C64MenuView()
    {
        InitializeComponent();

        // Subscribe to visibility property changes to update section states when view becomes visible
        this.PropertyChanged += (s, e) =>
        {
            // When the IsVisible property changes to true, update section states if needed.
            // Useful when C64 is not default system and view becomes visible later. Also seem to cover Browser/WASM when C64 is default, because of timing issue during startup.
            if (e.Property == IsVisibleProperty && this.IsVisible && ViewModel != null)
            {
                UpdateSectionStatesIfNeeded();
            }
        };

        this.DataContextChanged += (s, e) =>
        {
            // Useful when C64 is the default system and will be Visible on startup (thus PropertyChanged IsVisible will not happen).
            // This is run during startup.

            // !!! Note!!!
            // UpdateSectionStatesIfNeeded() is not working from here in Browser/WASM, it crashes the app.
            // It's because there is no current system yet selected at this point (but it is when running in Desktop).
            // The code in UpdateSectionStatesIfNeeded depends on HasConfigValidationErrors property which depends on _avaloniaHostApp.CurrentHostSystemConfig which throws an exception.
            // Same exception occurs in Loaded event handler.
            // Seems the PropertyChanged event handler handles all scenarios correctly for Browser, even when starting up with C64 as default system.

            if (ViewModel != null)
            {

                // Dont run if running in WebAssembly/Browser to avoid crash
                if (!PlatformDetection.IsRunningInWebAssembly())
                    UpdateSectionStatesIfNeeded();
            }
        };
    }

    private void UpdateSectionStatesIfNeeded()
    {
        // If there are validation errors, expand config section and collapse others
        try
        {
            if (ViewModel != null && ViewModel.HasConfigValidationErrors)
            {
                // Collapse Disk Section
                var diskHeaderButton = this.FindControl<Button>("DiskSectionHeader");
                var diskContentBorder = this.FindControl<Border>("DiskSectionContent");
                if (diskHeaderButton != null && diskContentBorder != null)
                {
                    SetSectionState(diskHeaderButton, diskContentBorder, expanded: false);
                }

                // Collapse Load/Save Section
                var loadSaveHeaderButton = this.FindControl<Button>("LoadSaveSectionHeader");
                var loadSaveContentBorder = this.FindControl<Border>("LoadSaveSectionContent");
                if (loadSaveHeaderButton != null && loadSaveContentBorder != null)
                {
                    SetSectionState(loadSaveHeaderButton, loadSaveContentBorder, expanded: false);
                }

                // Expand Config Section
                var configHeaderButton = this.FindControl<Button>("ConfigSectionHeader");
                var configContentBorder = this.FindControl<Border>("ConfigSectionContent");
                if (configHeaderButton != null && configContentBorder != null)
                {
                    SetSectionState(configHeaderButton, configContentBorder, expanded: true);
                }

                var c64ConfigButton = this.FindControl<Button>("C64Config");
                if (c64ConfigButton != null)
                    StartButtonFlash(c64ConfigButton, Colors.DarkOrange, stopAfterClick: true);
            }
        }
        catch (Exception ex)
        {
            // Silently ignore exceptions during initialization when CurrentHostSystemConfig is not yet ready
            // This can happen in Browser mode when the async system selection has not completed yet
            System.Diagnostics.Debug.WriteLine($"UpdateSectionStatesIfNeeded: Skipping update due to uninitialized state - {ex.Message}");
        }
    }


    private async void StartButtonFlash(Button button, Color flashColor, bool stopAfterClick)
    {
        _buttonFlashCancellation = new CancellationTokenSource();
        var originalBrush = button.Background;
        var flashBrush = new SolidColorBrush(flashColor);


        EventHandler<RoutedEventArgs>? tempHandler = null;
        tempHandler = (s, e) =>
        {
            _buttonFlashCancellation?.Cancel();
            button.Click -= tempHandler;
        };
        if (stopAfterClick)
        {
            // Add the temporary handler
            button.Click += tempHandler;
        }

        try
        {
            while (!_buttonFlashCancellation.Token.IsCancellationRequested)
            {
                button.Background = flashBrush;
                await Task.Delay(700, _buttonFlashCancellation.Token); // Match delay with flash duration to be at least as long as BrushTransition Duration (otherwise abrupt change may occur)

                button.Background = originalBrush;
                await Task.Delay(2000, _buttonFlashCancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Animation was cancelled, restore original background
            button.Background = originalBrush;
        }
        finally
        {
            // Clean up the handler in case animation completed naturally
            button.Click -= tempHandler;
        }
    }

    private async void OpenC64Config_Click(object? sender, RoutedEventArgs e)
    {
        if (HostApp == null)
            return;

        if (HostApp.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
            return;

        var renderProviderOptions = HostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();

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
        var dialog = new C64ConfigDialog(HostApp!, c64HostConfig, renderProviderOptions);

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

        if (result == true)
        {
            // Notify C64MenuViewModel of state changes
            ViewModel?.NotifyEmulatorStateChanged();

            // Notify MainViewModel to refresh validation errors
            NotifyMainViewModelOfConfigChange();
        }
    }

    private async Task ShowC64ConfigContentDialog(C64HostConfig c64HostConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderProviderOptions)
    {
        // Create the UserControl-based config
        var configControl = new C64ConfigUserControl(HostApp!, c64HostConfig, renderProviderOptions);

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
        // Find the root MainView's Grid by walking up the visual tree
        var root = this.GetVisualRoot();
        Grid? mainGrid = null;

        if (root is Window window && window.Content is Grid contentGrid)
        {
            mainGrid = contentGrid;
        }

        if (mainGrid == null)
        {
            mainGrid = this.FindAncestorOfType<MainView>(true)?.Content as Grid;
        }

        if (mainGrid != null)
        {
            Grid.SetRowSpan(overlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(overlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
            mainGrid.Children.Add(overlay);

            try
            {
                // Wait for the configuration to complete
                var result = await taskCompletionSource.Task;

                if (result)
                {
                    // Notify C64MenuViewModel of state changes
                    ViewModel?.NotifyEmulatorStateChanged();

                    // Notify MainViewModel to refresh validation errors
                    NotifyMainViewModelOfConfigChange();
                }
            }
            finally
            {
                // Clean up - remove the overlay
                mainGrid.Children.Remove(overlay);
            }
        }
    }

    /// <summary>
    /// Helper method to notify MainViewModel to refresh validation errors after configuration changes
    /// </summary>
    private void NotifyMainViewModelOfConfigChange()
    {
        try
        {
            // Find the parent MainView
            var mainView = this.FindAncestorOfType<MainView>(true);
            if (mainView?.DataContext is MainViewModel mainViewModel)
            {
                // Force a full state refresh to update validation errors
                mainViewModel.ForceStateRefresh();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error notifying MainViewModel of config change: {ex.Message}");
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
        if (HostApp?.EmulatorState != Systems.EmulatorState.Running ||
            !IsC64System())
            return;

        try
        {
            var c64 = (C64)HostApp.CurrentRunningSystem!;
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
        if (HostApp?.EmulatorState != Systems.EmulatorState.Running ||
       !IsC64System())
            return;

        try
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                var text = await topLevel.Clipboard?.GetTextAsync()!;
                if (!string.IsNullOrEmpty(text))
                {
                    var c64 = (C64)HostApp.CurrentRunningSystem!;
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
        if (HostApp?.EmulatorState == Systems.EmulatorState.Uninitialized || !IsC64System())
            return;

        try
        {
            var c64 = (C64)HostApp.CurrentRunningSystem!;
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
            ViewModel?.NotifyDiskImageStateChanged();
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
                    var c64 = (C64)HostApp!.CurrentRunningSystem!;
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
        return string.Equals(HostApp?.SelectedSystemName, C64.SystemName, StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleDiskSection_Click(object? sender, RoutedEventArgs e)
    {
        ToggleSection("DiskSectionHeader", "DiskSectionContent",
                  new[] { ("LoadSaveSectionHeader", "LoadSaveSectionContent"),
                         ("ConfigSectionHeader", "ConfigSectionContent") });
    }

    private void ToggleLoadSaveSection_Click(object? sender, RoutedEventArgs e)
    {
        ToggleSection("LoadSaveSectionHeader", "LoadSaveSectionContent",
                new[] { ("DiskSectionHeader", "DiskSectionContent"),
                    ("ConfigSectionHeader", "ConfigSectionContent") });
    }

    private void ToggleConfigSection_Click(object? sender, RoutedEventArgs e)
    {
        ToggleSection("ConfigSectionHeader", "ConfigSectionContent",
                  new[] { ("DiskSectionHeader", "DiskSectionContent"),
                      ("LoadSaveSectionHeader", "LoadSaveSectionContent") });
    }

    /// <summary>
    /// Toggles a collapsible section and collapses other specified sections if this one is being expanded.
    /// The arrow character (▼/▶) at the start of the button content is automatically toggled.
    /// </summary>
    /// <param name="headerButtonName">Name of the header button control</param>
    /// <param name="contentBorderName">Name of the content border control</param>
    /// <param name="otherSectionsToCollapse">Array of tuples containing (headerName, contentName) for sections to collapse</param>
    private void ToggleSection(
        string headerButtonName,
        string contentBorderName,
        (string headerName, string contentName)[] otherSectionsToCollapse)
    {
        var headerButton = this.FindControl<Button>(headerButtonName);
        var contentBorder = this.FindControl<Border>(contentBorderName);

        if (headerButton != null && contentBorder != null)
        {
            bool newExpandedState = !contentBorder.IsVisible;
            SetSectionState(headerButton, contentBorder, newExpandedState);

            // Collapse other sections if this section is being expanded
            if (newExpandedState)
            {
                foreach (var (headerName, contentName) in otherSectionsToCollapse)
                {
                    var otherHeaderButton = this.FindControl<Button>(headerName);
                    var otherContentBorder = this.FindControl<Border>(contentName);

                    if (otherHeaderButton != null && otherContentBorder != null)
                    {
                        SetSectionState(otherHeaderButton, otherContentBorder, expanded: false);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sets the state of a collapsible section (expanded or collapsed).
    /// Updates both the visibility of the content and the arrow character in the header button.
    /// </summary>
    /// <param name="headerButton">The header button control</param>
    /// <param name="contentBorder">The content border control</param>
    /// <param name="expanded">True to expand the section, false to collapse it</param>
    private void SetSectionState(Button headerButton, Border contentBorder, bool expanded)
    {
        contentBorder.IsVisible = expanded;

        if (headerButton.Content is string content)
        {
            if (expanded)
            {
                // Expand: change ▶ to ▼
                headerButton.Content = content.Replace("▶", "▼");
            }
            else
            {
                // Collapse: change ▼ to ▶
                headerButton.Content = content.Replace("▼", "▶");
            }
        }
    }
    // File operation event handlers
    private async void LoadPreloadedDisk_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel!.LoadPreloadedDiskImage();
    }

    private async void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
            return;
        await ViewModel.LoadBasicFile(storageProvider);
    }

    private async void SaveBasicFile_Click(object? sender, RoutedEventArgs e)
    {
       if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanSave)
            return;
        await ViewModel.SaveBasicFile(storageProvider);
    }

    private async void LoadBinaryFile_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
            return;
        await ViewModel.LoadBinaryFile(storageProvider);
    }

    private async void LoadAssemblyExample_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel!.LoadAssemblyExample();
    }

    private async void LoadBasicExample_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel!.LoadBasicExample();
    }

    private CancellationTokenSource _buttonFlashCancellation;
}
