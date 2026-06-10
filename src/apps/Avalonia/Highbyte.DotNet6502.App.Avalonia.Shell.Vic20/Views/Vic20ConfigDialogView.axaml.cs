using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core;
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
        if (TopLevel.GetTopLevel(this)?.StorageProvider == null)
            return;
        var storageProvider = TopLevel.GetTopLevel(this)!.StorageProvider;

        var options = new FilePickerOpenOptions
        {
            Title = "Select VIC-20 ROM files",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("ROM files")
                {
                    Patterns = new[] { "*.bin", "*.rom" }
                },
                FilePickerFileTypes.All
            }
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files == null || files.Count == 0)
            return;

        var romDataList = new List<(string fileName, byte[] data)>();
        foreach (var file in files)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                var data = new byte[stream.Length];
                await stream.ReadExactlyAsync(data);
                romDataList.Add((file.Name, data));
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
