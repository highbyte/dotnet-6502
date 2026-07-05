using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class AboutUserControl : UserControl
{
    private AboutViewModel? _subscribedViewModel;

    /// <summary>Raised when the dialog should be closed so the host can remove the overlay.</summary>
    public event EventHandler? DialogClosed;

    public AboutUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        if (DataContext is AboutViewModel vm)
        {
            _subscribedViewModel = vm;
            vm.RequestClose += OnRequestClose;
            vm.UpdateStarted += OnUpdateStarted;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribedViewModel != null)
        {
            _subscribedViewModel.RequestClose -= OnRequestClose;
            _subscribedViewModel.UpdateStarted -= OnUpdateStarted;
            _subscribedViewModel = null;
        }
    }

    private void OnRequestClose(object? sender, EventArgs e) => DialogClosed?.Invoke(this, EventArgs.Empty);

    // The self-update helper is now waiting for this app to exit before it upgrades and relaunches,
    // so quit the app.
    private void OnUpdateStarted(object? sender, EventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnCopyCommandClick(object? sender, RoutedEventArgs e)
    {
        var command = (DataContext as AboutViewModel)?.SuggestedCommand;
        if (string.IsNullOrEmpty(command))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
            return;

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateText(command));
        SafeAsyncHelper.Execute(() => clipboard.SetDataAsync(dataTransfer));
    }

    private void OnReleaseNotesClick(object? sender, RoutedEventArgs e)
    {
        var url = (DataContext as AboutViewModel)?.ReleaseNotesUrl;
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher != null)
            SafeAsyncHelper.Execute(() => launcher.LaunchUriAsync(uri));
    }
}
