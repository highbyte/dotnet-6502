using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64MenuView : UserControl
{
    // Lazy-initialized logger
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(C64MenuView));

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
    private void OnClipboardCopyRequested(object? sender, string text)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel && topLevel.Clipboard is { } clipboard)
            {
                using var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(text));
                await clipboard.SetDataAsync(data);
            }
        });

    private void OnClipboardPasteRequested(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (ViewModel != null
                && TopLevel.GetTopLevel(this) is { } topLevel
                && topLevel.Clipboard is { } clipboard)
            {
                using var data = await clipboard.TryGetDataAsync();
                if (data is not null)
                    ViewModel.ClipboardPasteResult = await data.TryGetTextAsync();
            }
        });

    private void OnAttachDiskImageRequested(object? sender, EventArgs e)
        => SafeAsyncHelper.Execute(async () =>
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
                        Logger.LogError(ex, "Error reading disk image file");
                    }
                }
            }
        });

    private void UpdateSectionStatesIfNeeded()
    {
        // If there are validation errors, expand config section and collapse others.
        // Section state now lives on the ViewModel; XAML IsVisible is bound to it.
        try
        {
            if (ViewModel != null && ViewModel.HasConfigValidationErrors)
            {
                ViewModel.ExpandConfigSectionOnValidationError();

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

    private void StartButtonFlash(Button button, Color flashColor, bool stopAfterClick)
        => SafeAsyncHelper.Execute(async () =>
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
        });

    private void OpenC64Config_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
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
        });

    private async Task ShowC64ConfigDialog()
    {
        // Get C64ConfigDialogViewModel from DI
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
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
            Logger.LogError("Could not get service provider");
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

        // Set up event handling for configuration completion
        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (s, saved) =>
        {
            taskCompletionSource.SetResult(saved);
        };

        // Show user control in overlay dialog
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Wait for the configuration to complete
        try
        {
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
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OpenBasicAssistantInfo_Click(object? sender, RoutedEventArgs e)
    {
        // Open the info link for Basic coding assistant
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.Launcher.LaunchUriAsync(new Uri("https://highbyte.github.io/dotnet-6502/docs/systems/c64/code-completion/"));
        }
    }

    private void OpenDiskInfo_Click(object? sender, RoutedEventArgs e)
    {
        // Open the info link for disk functionality
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.Launcher.LaunchUriAsync(new Uri("https://highbyte.github.io/dotnet-6502/docs/systems/c64/compatible-programs/"));
        }
    }

    // File operation event handlers - now delegate to ViewModel commands
    private void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
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
                    Logger.LogError(ex, "Error loading Basic .prg");
                }
            }
        });

    private void SaveBasicFile_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
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
                    Logger.LogInformation("Basic program saved to {FileName}", file.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving Basic .prg");
            }
        });

    private void LoadBinaryFile_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
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
                    Logger.LogError(ex, "Error loading binary .prg");
                }
            }
        });

    private CancellationTokenSource _buttonFlashCancellation;
}
