using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.App.Avalonia.Shell.Oric.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric.Views;

public partial class OricConfigDialogView : UserControl
{
    private OricConfigDialogViewModel? _previous;
    private OricConfigDialogViewModel? ViewModel => DataContext as OricConfigDialogViewModel;
    private EventHandler<bool>? _configurationChangedHandlers;

    public event EventHandler<bool>? ConfigurationChanged
    {
        add
        {
            _configurationChangedHandlers += value;
            ViewModel?.ConfigurationChanged += value;
        }
        remove
        {
            _configurationChangedHandlers -= value;
            ViewModel?.ConfigurationChanged -= value;
        }
    }

    public OricConfigDialogView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_previous != null)
        {
            if (_configurationChangedHandlers != null)
                _previous.ConfigurationChanged -= _configurationChangedHandlers;
            _previous.RomLicenseAcknowledgementRequested -= OnAcknowledgementRequested;
        }
        if (ViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                ViewModel.ConfigurationChanged += _configurationChangedHandlers;
            ViewModel.RomLicenseAcknowledgementRequested += OnAcknowledgementRequested;
        }
        _previous = ViewModel;
    }

    private void OnAcknowledgementRequested(object? sender, OricRomLicenseAcknowledgementEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var services = (Application.Current as AvaloniaApp)?.GetServiceProvider();
            var prompt = services?.GetService<OricRomPromptService>();
            e.SetResult(prompt != null && await prompt.ShowAsync(this));
        });

    private void LoadRom_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => SafeAsyncHelper.Execute(LoadRomAsync);

    private async Task LoadRomAsync()
    {
        var services = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        var picker = services?.GetService<IAppFilePicker>();
        if (picker == null || ViewModel == null)
            return;
        var files = await picker.OpenFilesAsync(this, new AppFilePickerOpenOptions(
            "Select Oric Atmos BASIC ROM", false,
            [new AppFilePickerFileType("ROM files", ["*.rom", "*.bin"]), AppFilePickerFileType.AllFiles]));
        if (files.Count == 1)
            await ViewModel.LoadRomFromDataAsync(files[0].Name, files[0].Bytes);
    }
}
