using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using AvaloniaApp = Highbyte.DotNet6502.App.Avalonia.Core.App;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Core.Services;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.Views;

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
            var serviceProvider = (Application.Current as AvaloniaApp)?.GetServiceProvider();
            if (serviceProvider == null)
            {
                Logger.LogError("Could not get service provider");
                e.SetResult(false);
                return;
            }

            var acknowledgmentService = serviceProvider.GetRequiredService<C64AcknowledgmentService>();
            var result = await acknowledgmentService.ShowRomLicenseConsentAsync(this);
            e.SetResult(result);
        });

    private void OnDigitsOnlyTextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Any(ch => !char.IsDigit(ch)))
        {
            e.Handled = true;
        }
    }

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
                "Select C64 ROM files",
                AllowMultiple: true,
                [
                    new AppFilePickerFileType("ROM files", ["*.bin", "*.rom"]),
                    AppFilePickerFileType.AllFiles
                ]));
        if (files.Count > 0)
        {
            var romDataList = new List<(string fileName, byte[] data)>();
            foreach (var file in files)
            {
                try
                {
                    romDataList.Add((file.Name, file.Bytes));
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
