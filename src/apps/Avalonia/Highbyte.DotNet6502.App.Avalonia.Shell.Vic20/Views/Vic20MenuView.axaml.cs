using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Vic20;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Views;

public partial class Vic20MenuView : UserControl
{
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(Vic20MenuView));
    private Vic20MenuViewModel? ViewModel => DataContext as Vic20MenuViewModel;
    private Vic20MenuViewModel? _subscribedViewModel;
    private CancellationTokenSource? _buttonFlashCancellation;

    public Vic20MenuView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            UpdateViewModelSubscriptions(ViewModel);
        };

        AttachedToVisualTree += (_, _) =>
        {
            if (ViewModel != null)
                UpdateSectionStatesIfNeeded();
        };

        DetachedFromVisualTree += (_, _) => UpdateViewModelSubscriptions(null);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void UpdateViewModelSubscriptions(Vic20MenuViewModel? newViewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, newViewModel))
            return;

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
            _subscribedViewModel.ClipboardPasteRequested -= OnClipboardPasteRequested;
        }

        _subscribedViewModel = newViewModel;

        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
            _subscribedViewModel.ClipboardPasteRequested += OnClipboardPasteRequested;
        }
    }

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

    private void UpdateSectionStatesIfNeeded()
    {
        try
        {
            if (ViewModel == null)
                return;

            if (ViewModel.HasConfigValidationErrors)
            {
                ViewModel.ExpandConfigSectionOnValidationError();

                var vic20ConfigButton = this.FindControl<Button>("Vic20Config");
                if (vic20ConfigButton != null)
                    StartButtonFlash(vic20ConfigButton, Colors.DarkOrange, stopAfterClick: true);
            }
            else
            {
                CancelButtonFlash();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateSectionStatesIfNeeded: Skipping update due to uninitialized state - {ex.Message}");
        }
    }

    private void CancelButtonFlash()
    {
        var cts = _buttonFlashCancellation;
        if (cts == null)
            return;

        _buttonFlashCancellation = null;
        SafeAsyncHelper.Execute(async () =>
        {
            await cts.CancelAsync();
            cts.Dispose();
        });
    }

    private void StartButtonFlash(Button button, Color flashColor, bool stopAfterClick)
        => SafeAsyncHelper.Execute(async () =>
        {
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
            tempHandler = (_, _) =>
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
                    await Task.Delay(700, buttonFlashCancellation.Token);

                    button.Background = originalBrush;
                    await Task.Delay(2000, buttonFlashCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                button.Background = originalBrush;
            }
            finally
            {
                button.Click -= tempHandler;
                buttonFlashCancellation.Dispose();

                if (ReferenceEquals(_buttonFlashCancellation, buttonFlashCancellation))
                    _buttonFlashCancellation = null;
            }
        });

    private void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var selectedFile = await OpenLocalFileAsync(
                "Load Basic PRG File",
                "PRG Files",
                "*.prg");
            if (selectedFile != null)
            {
                try
                {
                    _ = ViewModel!.LoadBasicFileCommand.Execute(selectedFile.Bytes);
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
            var selectedFile = await OpenLocalFileAsync(
                "Load & Start Binary PRG File",
                "PRG Files",
                "*.prg");
            if (selectedFile != null)
            {
                try
                {
                    _ = ViewModel!.LoadBinaryFileCommand.Execute(selectedFile.Bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading binary .prg");
                }
            }
        });

    private async Task<AppPickedFile?> OpenLocalFileAsync(
        string title,
        string fileTypeName,
        params string[] patterns)
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        var filePicker = serviceProvider?.GetService<IAppFilePicker>();
        return filePicker == null
            ? null
            : await filePicker.OpenFileAsync(
                this,
                new AppFilePickerOpenOptions(
                    title,
                    AllowMultiple: false,
                    [
                        new AppFilePickerFileType(fileTypeName, patterns),
                        AppFilePickerFileType.AllFiles
                    ]));
    }

    private void OpenVic20Config_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (ViewModel?.HostApp == null)
                return;

            if (ViewModel.HostApp.CurrentHostSystemConfig is not Vic20HostConfig)
                return;

            if (PlatformDetection.IsRunningInWebAssembly())
                await Vic20ConfigUserControlOverlayAsync();
            else
                await ShowVic20ConfigDialogAsync();
        });

    private async Task ShowVic20ConfigDialogAsync()
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        var dialog = new Vic20ConfigDialog
        {
            DataContext = new Vic20ConfigDialogViewModel(ViewModel!.HostApp)
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
            await ViewModel!.HostApp.ValidateConfigAsync();

        UpdateSectionStatesIfNeeded();
    }

    private async Task Vic20ConfigUserControlOverlayAsync()
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        var configControl = new Vic20ConfigDialogView
        {
            DataContext = new Vic20ConfigDialogViewModel(ViewModel!.HostApp)
        };

        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (_, saved) =>
        {
            taskCompletionSource.TrySetResult(saved);
        };

        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        try
        {
            var result = await taskCompletionSource.Task;
            if (result)
                await ViewModel!.HostApp.ValidateConfigAsync();

            UpdateSectionStatesIfNeeded();
        }
        finally
        {
            mainGrid.Children.Remove(overlayPanel);
        }
    }
}
