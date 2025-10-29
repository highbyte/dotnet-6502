using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core.SystemSetup;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64ConfigUserControl : UserControl
{
    private readonly C64ConfigDialogViewModel _viewModel;
    /// <summary>
    /// Gets the ViewModel for direct access when used in ContentDialog
    /// </summary>
    public C64ConfigDialogViewModel ViewModel => _viewModel;

    public event EventHandler<bool>? ConfigurationChanged;

    public C64ConfigUserControl()
    {
        InitializeComponent();
    }

    public C64ConfigUserControl(
        AvaloniaHostApp hostApp,
        C64HostConfig originalConfig,
        List<(System.Type renderProviderType, System.Type renderTargetType)> renderCombinations)
    {
        InitializeComponent();
        _viewModel = new C64ConfigDialogViewModel(hostApp, originalConfig, renderCombinations);
        DataContext = _viewModel;
    }


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void DownloadRomsToByteArray_Click(object? sender, RoutedEventArgs e)
    {
        await _viewModel.AutoDownloadRomsToByteArrayAsync();
    }

    private async void LoadRoms_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider == null)
            return;
        var storageProvider = TopLevel.GetTopLevel(this)!.StorageProvider;

        var options = new FilePickerOpenOptions
        {
            Title = "Select C64 ROM files",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("ROM files")
                {
                    Patterns = new[] { "*.bin", "*.rom" }
                },
                FilePickerFileTypes.All
            }
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files != null && files.Count > 0)
        {
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
                    // Log error but continue with other files
                    System.Diagnostics.Debug.WriteLine($"Failed to read file {file.Name}: {ex.Message}");
                }
            }

            if (romDataList.Count > 0)
            {
                await _viewModel.LoadRomsFromDataAsync(romDataList);
            }
        }
    }

    private void ClearRoms_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.UnloadRoms();
    }

    private async void DownloadRomsToFiles_Click(object? sender, RoutedEventArgs e)
    {
        await _viewModel.AutoDownloadROMsToFilesAsync();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, false);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (await _viewModel.TryApplyChanges())
        {
            ConfigurationChanged?.Invoke(this, true);
        }
    }
}
