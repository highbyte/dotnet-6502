using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64ConfigUserControl : UserControl
{
    private C64ConfigDialogViewModel? _previousViewModel;
    private C64ConfigDialogViewModel? ViewModel => DataContext as C64ConfigDialogViewModel;
    private EventHandler<bool>? _configurationChangedHandlers;

    public event EventHandler<bool>? ConfigurationChanged
    {
        add
        {
            // Note: Special code to handle creating C64ConfigUserControl directly (used in Browser) or via C64ConfigDialog Window (used in Desktop app)
            _configurationChangedHandlers += value;
            // If ViewModel is already available, subscribe immediately
            if (ViewModel != null)
            {
                ViewModel.ConfigurationChanged += value;
            }
        }
        remove
        {
            _configurationChangedHandlers -= value;
            // Unsubscribe from ViewModel if available
            if (ViewModel != null)
            {
                ViewModel.ConfigurationChanged -= value;
            }
        }
    }

    public C64ConfigUserControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Note: Special code to handle creating C64ConfigUserControl directly (used in Browser) or via C64ConfigDialog Window (used in Desktop app)
        // Unsubscribe from previous ViewModel
        if (_previousViewModel != null && _configurationChangedHandlers != null)
        {
            _previousViewModel.ConfigurationChanged -= _configurationChangedHandlers;
        }

        // Subscribe to new ViewModel
        if (ViewModel != null && _configurationChangedHandlers != null)
        {
            ViewModel.ConfigurationChanged += _configurationChangedHandlers;
        }

        _previousViewModel = ViewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
                await ViewModel.LoadRomsFromDataAsync(romDataList);
            }
        }
    }
}
