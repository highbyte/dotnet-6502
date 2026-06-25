using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.Views;

public partial class Vic20ConfigDialogView : UserControl
{
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(Vic20ConfigDialogView));

    private Vic20ConfigDialogViewModel? _previousViewModel;
    private Vic20ConfigDialogViewModel? ViewModel => DataContext as Vic20ConfigDialogViewModel;
    private EventHandler<bool>? _configurationChangedHandlers;

    public event EventHandler<bool>? ConfigurationChanged
    {
        add
        {
            _configurationChangedHandlers += value;
            if (ViewModel != null)
                ViewModel.ConfigurationChanged += value;
        }
        remove
        {
            _configurationChangedHandlers -= value;
            if (ViewModel != null)
                ViewModel.ConfigurationChanged -= value;
        }
    }

    public Vic20ConfigDialogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_previousViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                _previousViewModel.ConfigurationChanged -= _configurationChangedHandlers;
            _previousViewModel.RomLicenseAcknowledgementRequested -= OnRomLicenseAcknowledgementRequested;
        }

        if (ViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                ViewModel.ConfigurationChanged += _configurationChangedHandlers;
            ViewModel.RomLicenseAcknowledgementRequested += OnRomLicenseAcknowledgementRequested;
        }

        _previousViewModel = ViewModel;
    }

    private void OnRomLicenseAcknowledgementRequested(object? sender, Vic20RomLicenseAcknowledgementEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
            if (serviceProvider == null)
            {
                Logger.LogError("Could not get service provider");
                e.SetResult(false);
                return;
            }

            var romPromptService = serviceProvider.GetRequiredService<Vic20RomPromptService>();
            var result = await romPromptService.ShowConfigDownloadAcknowledgementAsync(this);
            e.SetResult(result);
        });

    private void LoadRoms_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(LoadRomsAsync);

    private async Task LoadRomsAsync()
    {
        var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
        var filePicker = serviceProvider?.GetService<IAppFilePicker>();
        if (filePicker == null)
            return;

        var files = await filePicker.OpenFilesAsync(
            this,
            new AppFilePickerOpenOptions(
                "Select VIC-20 ROM files",
                AllowMultiple: true,
                [
                    new AppFilePickerFileType("ROM files", ["*.bin", "*.rom"]),
                    AppFilePickerFileType.AllFiles
                ]));
        if (files.Count == 0)
            return;

        var romDataList = new List<(string fileName, byte[] data)>();
        foreach (var file in files)
        {
            try
            {
                romDataList.Add((file.Name, file.Bytes));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to read ROM file {FileName}", file.Name);
            }
        }

        if (romDataList.Count > 0 && ViewModel != null)
            await ViewModel.LoadRomsFromDataAsync(romDataList);
    }
}
