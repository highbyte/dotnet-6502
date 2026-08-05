using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core;
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

    public Apple2MenuView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

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
