using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64ConfigUserControl : UserControl
{
    // Lazy-initialized logger
    private ILogger? _logger;
    private ILogger Logger => _logger ??= AppLogger.CreateLogger(nameof(C64ConfigUserControl));

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
            ViewModel?.ConfigurationChanged += value;
        }
        remove
        {
            _configurationChangedHandlers -= value;
            // Unsubscribe from ViewModel if available
            ViewModel?.ConfigurationChanged -= value;
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
        if (_previousViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                _previousViewModel.ConfigurationChanged -= _configurationChangedHandlers;
            _previousViewModel.RomLicenseAcknowledgementRequested -= OnRomLicenseAcknowledgementRequested;
        }

        // Subscribe to new ViewModel
        if (ViewModel != null)
        {
            if (_configurationChangedHandlers != null)
                ViewModel.ConfigurationChanged += _configurationChangedHandlers;
            ViewModel.RomLicenseAcknowledgementRequested += OnRomLicenseAcknowledgementRequested;
        }

        _previousViewModel = ViewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnRomLicenseAcknowledgementRequested(object? sender, RomLicenseAcknowledgementEventArgs e)
        => SafeAsyncHelper.Execute(async () =>
        {
            var serviceProvider = (Application.Current as App)?.GetServiceProvider();
            if (serviceProvider == null)
            {
                Logger.LogError("Could not get service provider");
                e.SetResult(false);
                return;
            }

            var romPromptService = serviceProvider.GetRequiredService<C64RomPromptService>();
            var result = await romPromptService.ShowConfigDownloadAcknowledgementAsync(this);
            e.SetResult(result);
        });

    // In Avalonia WASM on macOS, TextBox default key bindings only include Ctrl+C/V, not Meta (CMD).
    // This handler explicitly handles CMD+C/V/X/A so copy/paste works in the browser.
    private void CorsProxyTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if ((e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Control)) == 0) return;

        if (e.Key == Key.C)
        {
            textBox.Copy();
            e.Handled = true;
        }
        else if (e.Key == Key.V)
        {
            textBox.Paste();
            e.Handled = true;
        }
        else if (e.Key == Key.X)
        {
            textBox.Cut();
            e.Handled = true;
        }
        else if (e.Key == Key.A)
        {
            textBox.SelectAll();
            e.Handled = true;
        }
    }

    private void LoadRoms_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(LoadRomsAsync);

    private async Task LoadRomsAsync()
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
                var viewModel = ViewModel;
                if (viewModel != null)
                {
                    await viewModel.LoadRomsFromDataAsync(romDataList);
                }
            }
        }
    }

    private void OpenAIHelpUrl_Click(object? sender, RoutedEventArgs e)
        => SafeAsyncHelper.Execute(OpenAIHelpUrlAsync);

    private Task OpenAIHelpUrlAsync()
    {
        var viewModel = ViewModel;
        if (viewModel == null)
            return Task.CompletedTask;

        if (TopLevel.GetTopLevel(this) is { Launcher: { } launcher })
        {
            return launcher.LaunchUriAsync(new Uri(viewModel.AIHelpUrl));
        }

        return Task.CompletedTask;
    }

}
