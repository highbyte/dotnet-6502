using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;
using Highbyte.DotNet6502.Impl.Avalonia;
using Highbyte.DotNet6502.Impl.Avalonia.Oric;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.Views;

public partial class OricMenuView : UserControl
{
    private OricMenuViewModel? ViewModel => DataContext as OricMenuViewModel;
    private OricMenuViewModel? _subscribedViewModel;

    public OricMenuView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => UpdateViewModelSubscriptions(ViewModel);
        AttachedToVisualTree += (_, _) => UpdateViewModelSubscriptions(ViewModel);
        DetachedFromVisualTree += (_, _) => UpdateViewModelSubscriptions(null);
    }

    private void UpdateViewModelSubscriptions(OricMenuViewModel? newViewModel)
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
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;

            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(text));
            await clipboard.SetDataAsync(data);
        });

    private void OnClipboardPasteRequested(object? sender, TaskCompletionSource<string?> completion)
        => SafeAsyncHelper.Execute(async () =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                completion.TrySetResult(null);
                return;
            }

            using var data = await clipboard.TryGetDataAsync();
            completion.TrySetResult(data is not null ? await data.TryGetTextAsync() : null);
        });

    private void OpenConfig_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => SafeAsyncHelper.Execute(OpenConfigAsync);

    private async Task OpenConfigAsync()
    {
        if (ViewModel?.HostApp.CurrentHostSystemConfig is not OricHostConfig)
            return;

        if (PlatformDetection.IsRunningInWebAssembly())
        {
            var services = (Application.Current as AvaloniaApp)?.GetServiceProvider();
            var overlayHelper = services?.GetService<OverlayDialogHelper>();
            if (overlayHelper == null)
                return;
            var view = new OricConfigDialogView { DataContext = new OricConfigDialogViewModel(ViewModel.HostApp) };
            var completion = new TaskCompletionSource<bool>();
            view.ConfigurationChanged += (_, saved) => completion.TrySetResult(saved);
            var panel = overlayHelper.BuildOverlayDialogPanel(view);
            var host = overlayHelper.ShowOverlayDialog(panel, this);
            try
            {
                if (await completion.Task)
                    await ViewModel.HostApp.ValidateConfigAsync();
            }
            finally { host.Children.Remove(panel); }
            return;
        }

        var dialog = new OricConfigDialog
        {
            DataContext = new OricConfigDialogViewModel(ViewModel.HostApp)
        };
        var saved = TopLevel.GetTopLevel(this) is Window owner
            ? await dialog.ShowDialog<bool?>(owner)
            : null;
        if (saved == true)
            await ViewModel.HostApp.ValidateConfigAsync();
    }
}
