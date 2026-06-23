using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Commodore64;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.Views;

public partial class C64MenuView : UserControl
{
    // Lazy-initialized logger
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(C64MenuView));

    // Access ViewModel through DataContext
    private C64MenuViewModel? ViewModel => DataContext as C64MenuViewModel;
    private C64MenuViewModel? _subscribedViewModel;

    // Parameterless constructor for XAML compatibility
    public C64MenuView()
    {
        InitializeComponent();

        // Subscribe to ViewModel events for UI operations
        this.DataContextChanged += (s, e) =>
        {
            UpdateViewModelSubscriptions(ViewModel);

            // NOTE: Do NOT call UpdateSectionStatesIfNeeded() here.
            // DataContextChanged fires before the view is in the visual tree. If
            // AttachedToVisualTree fires shortly after (which it always does in the
            // plugin architecture), a second StartButtonFlash call would capture the
            // button's background while it's already orange, making both "on" and "off"
            // states orange so the animation appears stuck. AttachedToVisualTree is
            // the single, reliable trigger used below.
        };

        // Subscribe to visibility property changes to update section states when view becomes visible.
        // NOTE: In the plugin architecture the ContentControl (not C64MenuView itself) has the
        // IsVisible binding, so C64MenuView.IsVisible never changes and this handler never fires.
        // The AttachedToVisualTree handler below is the reliable equivalent trigger in that case.
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property == IsVisibleProperty && this.IsVisible && ViewModel != null)
            {
                UpdateSectionStatesIfNeeded();
            }
        };

        // In the plugin architecture (ContentControl + ViewLocator), C64MenuView is created lazily
        // when the C64 system is selected and added to the visual tree at that point.
        // AttachedToVisualTree fires after DataContext is set and after the view is in the visual
        // tree — the equivalent of the old IsVisible false→true transition in the pre-plugin code.
        //
        // No WASM exclusion here. The old code only excluded DataContextChanged (which fired too
        // early in WASM before async initialization completed). The IsVisibleProperty handler —
        // which was the working WASM trigger — had no WASM guard, and AttachedToVisualTree fires
        // at the same point in the lifecycle. UpdateSectionStatesIfNeeded is protected by a
        // try-catch and all inner null checks, so it is safe to call from WASM.
        this.AttachedToVisualTree += (s, e) =>
        {
            if (ViewModel != null)
                UpdateSectionStatesIfNeeded();
        };

        this.DetachedFromVisualTree += (s, e) => UpdateViewModelSubscriptions(null);
    }

    private void UpdateViewModelSubscriptions(C64MenuViewModel? newViewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, newViewModel))
            return;

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
            _subscribedViewModel.ClipboardPasteRequested -= OnClipboardPasteRequested;
            _subscribedViewModel.AttachDiskImageRequested -= OnAttachDiskImageRequested;
            _subscribedViewModel.AttachCartridgeImageRequested -= OnAttachCartridgeImageRequested;
            _subscribedViewModel.ConfirmCartridgeReplaceRequested -= OnConfirmCartridgeReplaceRequested;
        }

        _subscribedViewModel = newViewModel;

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
            _subscribedViewModel.ClipboardPasteRequested += OnClipboardPasteRequested;
            _subscribedViewModel.AttachDiskImageRequested += OnAttachDiskImageRequested;
            _subscribedViewModel.AttachCartridgeImageRequested += OnAttachCartridgeImageRequested;
            _subscribedViewModel.ConfirmCartridgeReplaceRequested += OnConfirmCartridgeReplaceRequested;
        }
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

    private void OnAttachCartridgeImageRequested(
        object? sender,
        TaskCompletionSource<SelectedBinaryFile?> tcs)
        => SafeAsyncHelper.Execute(async () =>
        {
            try
            {
                if (TopLevel.GetTopLevel(this) is not { } topLevel ||
                    !topLevel.StorageProvider.CanOpen)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select C64 CRT Cartridge Image",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("C64 CRT Cartridge Images") { Patterns = ["*.crt"] },
                        new FilePickerFileType("All Files") { Patterns = ["*"] },
                    ],
                });

                if (files.Count == 0)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                await using var stream = await files[0].OpenReadAsync();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                tcs.TrySetResult(new SelectedBinaryFile(files[0].Name, buffer.ToArray()));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading CRT cartridge image");
                tcs.TrySetResult(null);
                await ShowMessageOverlayAsync(
                    "Could Not Read Cartridge Image",
                    string.IsNullOrWhiteSpace(ex.Message)
                        ? "The selected CRT cartridge image could not be read."
                        : ex.Message);
            }
        });

    private void OnConfirmCartridgeReplaceRequested(
        object? sender,
        CartridgeReplaceConfirmationEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
            e.Completion.TrySetResult(await ShowConfirmationOverlayAsync(
                "Replace Cartridge",
                $"Replace the currently attached cartridge '{e.CartridgeName}'?")));

    private void OnClipboardPasteRequested(object? sender, TaskCompletionSource<string?> tcs)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel && topLevel.Clipboard is { } clipboard)
            {
                using var data = await clipboard.TryGetDataAsync();
                tcs.TrySetResult(data is not null ? await data.TryGetTextAsync() : null);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        });

    private void OnAttachDiskImageRequested(object? sender, TaskCompletionSource<byte[]?> tcs)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (TopLevel.GetTopLevel(this) is not { } topLevel)
            {
                tcs.TrySetResult(null);
                return;
            }

            var storageProvider = topLevel.StorageProvider;
            if (!storageProvider.CanOpen)
            {
                tcs.TrySetResult(null);
                return;
            }

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
                    tcs.TrySetResult(fileBuffer);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error reading disk image file");
                    tcs.TrySetResult(null);
                }
            }
            else
            {
                tcs.TrySetResult(null);
            }
        });

    private void UpdateSectionStatesIfNeeded()
    {
        // Expand/flash on validation errors; cancel flash when config becomes valid.
        // Section state lives on the ViewModel; XAML IsVisible is bound to it.
        try
        {
            if (ViewModel == null)
                return;

            if (ViewModel.HasConfigValidationErrors)
            {
                ViewModel.ExpandConfigSectionOnValidationError();

                var c64ConfigButton = this.FindControl<Button>("C64Config");
                if (c64ConfigButton != null)
                    StartButtonFlash(c64ConfigButton, Colors.DarkOrange, stopAfterClick: true);
            }
            else
            {
                // Config is valid (or has just become valid) — stop any ongoing flash.
                CancelButtonFlash();
            }
        }
        catch (Exception ex)
        {
            // Silently ignore exceptions during initialization when CurrentHostSystemConfig is not yet ready
            // This can happen in Browser mode when the async system selection has not completed yet
            System.Diagnostics.Debug.WriteLine($"UpdateSectionStatesIfNeeded: Skipping update due to uninitialized state - {ex.Message}");
        }
    }

    /// <summary>
    /// Cancels any ongoing button flash animation without starting a new one.
    /// CancellationTokenSource.Dispose() is safe to call multiple times, so even if
    /// StartButtonFlash's finally block also disposes it, there is no harm.
    /// </summary>
    private void CancelButtonFlash()
    {
        var cts = _buttonFlashCancellation;
        if (cts == null) return;
        _buttonFlashCancellation = null;
        SafeAsyncHelper.Execute(async () =>
        {
            await cts.CancelAsync();
            cts.Dispose(); // safe; StartButtonFlash's catch/finally may also dispose it
        });
    }

    private void StartButtonFlash(Button button, Color flashColor, bool stopAfterClick)
        => SafeAsyncHelper.Execute(async () =>
        {
            // Capture the field into a local before awaiting so the finally block's
            // null-clear cannot cause a NullReferenceException on the Dispose() line
            // if another concurrent call races through the finally while we're awaiting.
            var existingCancellation = _buttonFlashCancellation;
            if (existingCancellation != null)
            {
                _buttonFlashCancellation = null;
                await existingCancellation.CancelAsync();
                existingCancellation.Dispose();
            }

            var buttonFlashCancellation = new CancellationTokenSource();
            _buttonFlashCancellation = buttonFlashCancellation;
            var originalBrush = button.Background;
            var flashBrush = new SolidColorBrush(flashColor);

            EventHandler<RoutedEventArgs>? tempHandler = null;
            tempHandler = (s, e) =>
            {
                SafeAsyncHelper.Execute(async () =>
                {
                    await buttonFlashCancellation.CancelAsync();
                    button.Click -= tempHandler;
                });
            };
            if (stopAfterClick)
                button.Click += tempHandler;

            try
            {
                while (!buttonFlashCancellation.Token.IsCancellationRequested)
                {
                    button.Background = flashBrush;
                    await Task.Delay(700, buttonFlashCancellation.Token); // Match delay with flash duration to be at least as long as BrushTransition Duration (otherwise abrupt change may occur)

                    button.Background = originalBrush;
                    await Task.Delay(2000, buttonFlashCancellation.Token);
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
                buttonFlashCancellation.Dispose();

                if (ReferenceEquals(_buttonFlashCancellation, buttonFlashCancellation))
                    _buttonFlashCancellation = null;
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
                await C64ConfigUserControlOverlayAsync();
            }
            else
            {
                // For desktop platforms, use the Window dialog
                await ShowC64ConfigDialogAsync();
                //await C64ConfigUserControlOverlay();
            }
        });

    private async Task ShowC64ConfigDialogAsync()
    {
        // Get C64ConfigDialogViewModel from DI
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
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
            // Re-validate config in HostApp so the Config Status tab (ValidationErrors)
            // and HasConfigValidationErrors both reflect the saved state.
            await ViewModel!.HostApp!.ValidateConfigAsync();

            // Notify C64MenuViewModel of state changes (refreshes all bindings).
            ViewModel?.RefreshAllBindings();

            // Update flash state: stops the flash if config is now valid,
            // or keeps it going (restarting from a clean original-brush capture)
            // if the config is still invalid.
            UpdateSectionStatesIfNeeded();
        }
    }

    private async Task C64ConfigUserControlOverlayAsync()
    {
        // Get C64ConfigDialogViewModel from DI
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
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

    private void OpenShare_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(ShowShareOverlayAsync);

    private async Task ShowShareOverlayAsync()
    {
        if (ViewModel == null)
            return;

        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        // Rebuild the link from current state before showing it.
        ViewModel.RefreshShareLink();

        var shareControl = new C64ShareUserControl
        {
            DataContext = ViewModel
        };

        var taskCompletionSource = new TaskCompletionSource();
        shareControl.CloseRequested += (_, _) => taskCompletionSource.TrySetResult();

        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(shareControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        try
        {
            await taskCompletionSource.Task;
        }
        finally
        {
            mainGrid.Children.Remove(overlayPanel);
        }
    }

    private void OpenDiskInfo_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(() => LaunchUriIfAvailableAsync("https://highbyte.github.io/dotnet-6502/docs/systems/c64/compatible-programs/"));

    private void OpenCartridgeInfo_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(() => ShowMessageOverlayAsync(
            "C64 Cartridge Images",
            "Attach generic 8K, generic 16K, Ultimax, Magic Desk, Ocean, Epyx FastLoad, or Action Replay .crt images. Freezer cartridges expose a Freeze button while attached. Other cartridge hardware types are rejected until their banking or device behavior is implemented."));

    private async Task<bool> ShowConfirmationOverlayAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();
        Panel? overlayPanel = null;
        Grid? hostGrid = null;

        void Close(bool result)
        {
            if (overlayPanel != null && hostGrid != null)
                hostGrid.Children.Remove(overlayPanel);
            tcs.TrySetResult(result);
        }

        var cancelButton = new Button { Content = "Cancel", Classes = { "small", "cancel" } };
        cancelButton.Click += (_, _) => Close(false);
        var replaceButton = new Button { Content = "Replace", Classes = { "small", "danger" } };
        replaceButton.Click += (_, _) => Close(true);

        overlayPanel = BuildMessageOverlay(
            title,
            message,
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children = { cancelButton, replaceButton },
            });
        hostGrid = ShowOverlay(overlayPanel);
        if (hostGrid == null)
            return false;
        return await tcs.Task;
    }

    private async Task ShowMessageOverlayAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();
        Panel? overlayPanel = null;
        Grid? hostGrid = null;
        var closeButton = new Button { Content = "OK", Classes = { "small", "primary" } };
        closeButton.Click += (_, _) =>
        {
            if (overlayPanel != null && hostGrid != null)
                hostGrid.Children.Remove(overlayPanel);
            tcs.TrySetResult();
        };
        overlayPanel = BuildMessageOverlay(title, message, closeButton);
        hostGrid = ShowOverlay(overlayPanel);
        if (hostGrid != null)
            await tcs.Task;
    }

    private static Panel BuildMessageOverlay(string title, string message, Control actions)
        => new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children =
            {
                new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    MaxWidth = 420,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.Bold },
                            new TextBlock { Text = message, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                            actions,
                        },
                    },
                },
            },
        };

    private Grid? ShowOverlay(Panel overlayPanel)
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider for cartridge dialog");
            return null;
        }
        var helper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        return helper.ShowOverlayDialog(overlayPanel, this);
    }

    private Task LaunchUriIfAvailableAsync(string uri)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            return topLevel.Launcher.LaunchUriAsync(new Uri(uri));
        }

        return Task.CompletedTask;
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
                    var saveData = await ViewModel!.GetBasicProgramAsPrgFileBytesAsync();

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

    private CancellationTokenSource? _buttonFlashCancellation;
}
