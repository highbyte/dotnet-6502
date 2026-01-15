using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class C64ConfigUserControl : UserControl
{
    private C64ConfigDialogViewModel? _previousViewModel;
    private C64ConfigDialogViewModel? ViewModel => DataContext as C64ConfigDialogViewModel;
    private EventHandler<bool>? _configurationChangedHandlers;
    private Panel? _overlayHost;
    private Control? _currentOverlay;

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
            var result = await ShowRomLicenseOverlay();
            e.SetResult(result);
        });

    private async Task<bool> ShowRomLicenseOverlay()
    {
        var tcs = new TaskCompletionSource<bool>();

        var yesButton = new Button
        {
            Content = "Yes",
            Width = 40,
            Classes = { "small", "primary" }
        };
        yesButton.Click += (_, _) =>
        {
            CloseOverlay();
            tcs.TrySetResult(true);
        };

        var noButton = new Button
        {
            Content = "No",
            Width = 40,
            Classes = { "small", "cancel" }
        };
        noButton.Click += (_, _) =>
        {
            CloseOverlay();
            tcs.TrySetResult(false);
        };

        // Create clickable URL button
        var urlButton = new Button
        {
            Content = C64SystemConfig.DEFAULT_KERNAL_ROM_DOWNLOAD_BASE_URL,
            Classes = { "link" },
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        urlButton.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is { } tl)
            {
                tl.Launcher.LaunchUriAsync(new Uri(C64SystemConfig.DEFAULT_KERNAL_ROM_DOWNLOAD_BASE_URL));
            }
        };

        var dialogContent = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),  // 1A202C, ViewDefaultBg
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            MaxWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Children =
                {
                    // Header
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                        Padding = new Thickness(5, 5),
                        CornerRadius = new CornerRadius(4, 4, 0, 0),
                        Child = new TextBlock
                        {
                            Text = "ROM License Acknowledgement",
                            FontSize = 14,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    },
                    // Content
                    new StackPanel
                    {
                        Margin = new Thickness(10),
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "You are about to download Commodore 64 ROM files from:",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap
                            },
                            urlButton,
                            new TextBlock
                            {
                                Text = "Please be aware that you probably need to:",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 8, 0, 0)
                            },
                            new TextBlock
                            {
                                Text = "• Have a license from Commodore/Cloanto, OR\n• Own a Commodore 64 computer",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(8, 0, 0, 0)
                            },
                            new TextBlock
                            {
                                Text = "to be allowed to download and use these ROM files.",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = "Do you acknowledge these requirements and wish to proceed?",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap,
                                FontWeight = FontWeight.Bold,
                                Margin = new Thickness(0, 8, 0, 0)
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 10,
                                Margin = new Thickness(0, 8, 0, 0),
                                Children =
                                {
                                    yesButton,
                                    noButton
                                }
                            }
                        }
                    }
                }
            }
        };

        // Create overlay panel (like in C64MenuView)
        var overlayPanel = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { dialogContent }
        };

        // Show user control in overlay dialog
        var serviceProvider = (Application.Current as App)?.GetServiceProvider();
        if (serviceProvider == null)
        {
            System.Console.WriteLine("Error: Could not get service provider");
            return false;
        }
        var overlayDialogHelper = serviceProvider.GetRequiredService<OverlayDialogHelper>();
        var mainGrid = overlayDialogHelper.ShowOverlayDialog(overlayPanel, this);

        // Store reference for cleanup
        _overlayHost = mainGrid;
        _currentOverlay = overlayPanel;

        return await tcs.Task;
    }

    private void CloseOverlay()
    {
        if (_currentOverlay != null && _overlayHost != null)
        {
            _overlayHost.Children.Remove(_currentOverlay);
            _currentOverlay = null;
            _overlayHost = null;
        }
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

    private void OpenAIHelpUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        if (TopLevel.GetTopLevel(this) is { } tl)
        {
            tl.Launcher.LaunchUriAsync(new Uri(ViewModel.AIHelpUrl));
        }
    }
}
