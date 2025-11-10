using System;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64MenuView : UserControl
{
    // Access ViewModel through DataContext
    private C64MenuViewModel? ViewModel => DataContext as C64MenuViewModel;

    // Parameterless constructor for XAML compatibility
    public C64MenuView()
    {
        InitializeComponent();

        // Subscribe to ViewModel events for UI operations
        this.DataContextChanged += (s, e) =>
        {
            if (ViewModel != null)
            {
                // Subscribe to clipboard and file operation requests
                ViewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
                ViewModel.ClipboardPasteRequested += OnClipboardPasteRequested;
                ViewModel.AttachDiskImageRequested += OnAttachDiskImageRequested;

                // Update section states if needed (not in WebAssembly to avoid crash)
                if (!PlatformDetection.IsRunningInWebAssembly())
                    UpdateSectionStatesIfNeeded();
            }
        };

        // Subscribe to visibility property changes to update section states when view becomes visible
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property == IsVisibleProperty && this.IsVisible && ViewModel != null)
            {
                UpdateSectionStatesIfNeeded();
            }
        };
    }

    // Event handlers for ViewModel requests (pure UI operations)
    private async void OnClipboardCopyRequested(object? sender, string text)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            await topLevel.Clipboard?.SetTextAsync(text)!;
        }
    }

    private async void OnClipboardPasteRequested(object? sender, EventArgs e)
    {
        if (ViewModel != null && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            var text = await topLevel.Clipboard?.GetTextAsync()!;
            ViewModel.ClipboardPasteResult = text;
        }
    }

    private async void OnAttachDiskImageRequested(object? sender, EventArgs e)
    {
        if (ViewModel == null || TopLevel.GetTopLevel(this) is not { } topLevel)
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

                    ViewModel.DiskImageFileResult = fileBuffer;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error reading disk image file: {ex.Message}");
                }
            }
        }
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
        if (ViewModel?.HostApp == null)
            return;

        if (ViewModel.HostApp.CurrentHostSystemConfig is not C64HostConfig c64HostConfig)
            return;

        var renderProviderOptions = ViewModel.HostApp.GetAvailableSystemRenderProviderTypesAndRenderTargetTypeCombinations();

        // Check if running on WASM/Browser platform
        if (PlatformDetection.IsRunningInWebAssembly())
        {
            // For WASM, show usercontrol overlay instead of Window
            await C64ConfigUserControlOverlay();
        }
        else
        {
            // For desktop platforms, use the Window dialog
            await ShowC64ConfigDialog();
            //await C64ConfigUserControlOverlay();
        }
    }

    private async Task ShowC64ConfigDialog()
    {
        // Get C64ConfigDialogViewModel from DI
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

        var dialog = new C64ConfigDialog
        {
            DataContext = new C64ConfigDialogViewModel(ViewModel!.HostApp!, serviceProvider.GetRequiredService<IConfiguration>(), loggerFactory)
        };

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
            ViewModel?.RefreshAllBindings();
        }
    }

    private async Task C64ConfigUserControlOverlay()
    {
        // Get C64ConfigDialogViewModel from DI
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

        // Create the UserControl-based config
        var configControl = new C64ConfigUserControl
        {
            DataContext = new C64ConfigDialogViewModel(ViewModel!.HostApp!, serviceProvider.GetRequiredService<IConfiguration>(), loggerFactory)
        };

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
                    // Re-validate config in HostApp to update ValidationErrors
                    await ViewModel!.HostApp!.ValidateConfigAsync();

                    // Notify C64MenuViewModel of state changes
                    ViewModel?.RefreshAllBindings();
                }
            }
            finally
            {
                // Clean up - remove the overlay
                mainGrid.Children.Remove(overlay);
            }
        }
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

    // File operation event handlers - now delegate to ViewModel commands
    private async void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Basic PRG File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PRG Files") { Patterns = new[] { "*.prg" } },
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

                // Fire and forget - let the ReactiveCommand handle scheduling and execution. This works in WebAssembly because we're not subscribing to the observable
                _ = ViewModel!.LoadBasicFileCommand.Execute(fileBuffer);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error loading Basic .prg: {ex.Message}");
            }
        }
    }

    private async void SaveBasicFile_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanSave)
            return;

        try
        {
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Basic PRG File",
                SuggestedFileName = "program",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PRG Files") { Patterns = new[] { "*.prg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (file != null)
            {
                // Call ViewModel method directly to get the byte array
                var saveData = await ViewModel!.GetBasicProgramAsPrgFileBytes();

                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(saveData);
                System.Console.WriteLine($"Basic program saved to {file.Name}");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error saving Basic .prg: {ex.Message}");
        }
    }

    private async void LoadBinaryFile_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        var storageProvider = topLevel.StorageProvider;
        if (!storageProvider.CanOpen)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load & Start Binary PRG File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PRG Files") { Patterns = new[] { "*.prg" } },
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

                // Fire and forget - let the ReactiveCommand handle scheduling and execution. This works in WebAssembly because we're not subscribing to the observable
                _ = ViewModel!.LoadBinaryFileCommand.Execute(fileBuffer);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error loading binary .prg: {ex.Message}");
            }
        }
    }

    // Section toggle handlers (pure UI functionality)
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

    private CancellationTokenSource _buttonFlashCancellation;
}
