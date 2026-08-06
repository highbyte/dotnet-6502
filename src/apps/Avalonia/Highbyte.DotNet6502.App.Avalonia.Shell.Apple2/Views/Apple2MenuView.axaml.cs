using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Apple2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Apple2.Views;

public partial class Apple2MenuView : UserControl
{
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(Apple2MenuView));

    private Apple2MenuViewModel? ViewModel => DataContext as Apple2MenuViewModel;

    private Apple2MenuViewModel? _subscribedViewModel;

    public Apple2MenuView()
    {
        InitializeComponent();

        // Keep clipboard-event subscriptions in sync with the current DataContext.
        this.DataContextChanged += (s, e) => UpdateViewModelSubscriptions(ViewModel);
        this.AttachedToVisualTree += (s, e) => UpdateViewModelSubscriptions(ViewModel);
        this.DetachedFromVisualTree += (s, e) => UpdateViewModelSubscriptions(null);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void UpdateViewModelSubscriptions(Apple2MenuViewModel? newViewModel)
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

    private void OpenDskImage_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var selectedFile = await OpenLocalFileAsync(
                "Open DOS 3.3 disk image",
                "Disk Images",
                "*.dsk", "*.do");
            if (selectedFile != null)
            {
                try
                {
                    _ = ViewModel!.AttachDskImageCommand.Execute(selectedFile.Bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error opening .dsk image");
                }
            }
        });

    /// <summary>One button both ways: eject what is in the drive, or ask for a disk to insert.</summary>
    private void ToggleDiskImage_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (ViewModel?.HasInsertedDisk == true)
            {
                _ = ViewModel.EjectDiskCommand.Execute();
                return;
            }

            var selectedFile = await OpenLocalFileAsync(
                "Insert DOS 3.3 disk image in drive 1",
                "Disk Images",
                "*.dsk", "*.do");
            if (selectedFile != null)
            {
                try
                {
                    _ = ViewModel!.InsertDiskCommand.Execute(selectedFile.Bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error inserting .dsk image");
                }
            }
        });

    private void LoadBasicFile_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var selectedFile = await OpenLocalFileAsync(
                "Load tokenized Applesoft Basic file",
                "Basic Files",
                "*.bas");
            if (selectedFile != null)
            {
                try
                {
                    // Fire and forget - let the ReactiveCommand handle scheduling and execution. This works in WebAssembly because we're not subscribing to the observable
                    _ = ViewModel!.LoadBasicFileCommand.Execute(selectedFile.Bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading Basic file");
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
                    Title = "Save tokenized Applesoft Basic file",
                    SuggestedFileName = "program.bas",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Basic Files") { Patterns = new[] { "*.bas" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    }
                });

                if (file != null)
                {
                    // Call ViewModel method directly to get the byte array
                    var saveData = await ViewModel!.GetBasicProgramAsFileBytesAsync();

                    await using var stream = await file.OpenWriteAsync();
                    await stream.WriteAsync(saveData);
                    Logger.LogInformation("Basic program saved to {FileName}", file.Name);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error saving Basic file");
            }
        });

    private void LoadBinaryFile_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var selectedFile = await OpenLocalFileAsync(
                "Load & start DOS 3.3 binary (B) file",
                "Binary Files",
                "*.bin", "*.b");
            if (selectedFile != null)
            {
                try
                {
                    // Fire and forget - let the ReactiveCommand handle scheduling and execution. This works in WebAssembly because we're not subscribing to the observable
                    _ = ViewModel!.LoadBinaryFileCommand.Execute(selectedFile.Bytes);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading binary file");
                }
            }
        });

    private async Task<SelectedBinaryFile?> OpenLocalFileAsync(
        string title,
        string fileTypeName,
        params string[] patterns)
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        var filePicker = serviceProvider?.GetService<IAppFilePicker>();
        var file = filePicker == null
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
        if (file == null)
            return null;
        return new SelectedBinaryFile(file.Name, file.Bytes);
    }

    private sealed record SelectedBinaryFile(string Name, byte[] Bytes);

    private void OpenApple2Config_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (ViewModel?.HostApp == null)
                return;

            if (ViewModel.HostApp.CurrentHostSystemConfig is not Apple2HostConfig)
                return;

            // The browser host has no separate windows, so the same view is shown as an overlay.
            if (PlatformDetection.IsRunningInWebAssembly())
                await Apple2ConfigUserControlOverlayAsync();
            else
                await ShowApple2ConfigDialogAsync();
        });

    private async Task ShowApple2ConfigDialogAsync()
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        var dialog = new Apple2ConfigDialog
        {
            DataContext = new Apple2ConfigDialogViewModel(ViewModel!.HostApp)
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
    }

    private async Task Apple2ConfigUserControlOverlayAsync()
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            Logger.LogError("Could not get service provider");
            return;
        }

        var configControl = new Apple2ConfigDialogView
        {
            DataContext = new Apple2ConfigDialogViewModel(ViewModel!.HostApp)
        };

        var taskCompletionSource = new TaskCompletionSource<bool>();
        configControl.ConfigurationChanged += (_, saved) => taskCompletionSource.TrySetResult(saved);

        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var overlayPanel = overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        try
        {
            if (await taskCompletionSource.Task)
                await ViewModel!.HostApp.ValidateConfigAsync();
        }
        finally
        {
            mainGrid.Children.Remove(overlayPanel);
        }
    }
}
