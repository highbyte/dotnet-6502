using Avalonia;
using Avalonia.Controls;
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

    public OricMenuView() => AvaloniaXamlLoader.Load(this);

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
