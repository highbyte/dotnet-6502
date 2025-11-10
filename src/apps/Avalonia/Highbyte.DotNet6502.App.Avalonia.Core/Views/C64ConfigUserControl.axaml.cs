using System;
using System.Collections.Generic;
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

    private async void OnRomLicenseAcknowledgementRequested(object? sender, RomLicenseAcknowledgementEventArgs e)
    {
        var result = await ShowRomLicenseOverlay();
        e.SetResult(result);
    }

    private async System.Threading.Tasks.Task<bool> ShowRomLicenseOverlay()
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

        // Find the main Grid - the C64ConfigUserControl itself is already in an overlay,
        // so we need to find the root Window's content Grid or MainView's Grid
        Grid? mainGrid = null;

        // Try to find the Window by walking up from TopLevel
        var topLevel = TopLevel.GetTopLevel(this);
        
        // Desktop scenario: C64ConfigDialog Window
        if (topLevel is Window window)
        {
            // Check if this is the C64ConfigDialog window (desktop)
            // The dialog's Content is the C64ConfigUserControl, which contains a Grid
            if (window is C64ConfigDialog dialog)
            {
                // Look for the Grid inside this C64ConfigUserControl
                // We can use the visual tree to find it
                if (this.Content is Grid thisGrid)
                {
                    mainGrid = thisGrid;
                }
                else
                {
                    // Try to find the first Grid child in this control
                    mainGrid = this.FindDescendantOfType<Grid>();
                }
            }
            // Or if it's the main window with a Grid content
            else if (window.Content is Grid windowGrid)
            {
                mainGrid = windowGrid;
            }
        }

        // Browser scenario: Try to find MainView in the visual tree
        if (mainGrid == null)
        {
            var mainView = this.FindAncestorOfType<MainView>(true);
            if (mainView?.Content is Grid mainViewGrid)
            {
                mainGrid = mainViewGrid;
            }
        }

        // Last resort: try to find any Grid ancestor by walking the visual tree
        if (mainGrid == null)
        {
            var current = this.Parent;
            while (current != null)
            {
                if (current is Grid grid)
                {
                    mainGrid = grid;
                    break;
                }
                current = (current as Control)?.Parent;
            }
        }

        if (mainGrid == null)
        {
            // Fallback: if we can't find any grid, default to Yes
            return true;
        }

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
            Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)), // Match the dark theme
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
        var overlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { dialogContent }
        };

        // Span all rows and columns in the main grid
        Grid.SetRowSpan(overlay, mainGrid.RowDefinitions.Count > 0 ? mainGrid.RowDefinitions.Count : 1);
        Grid.SetColumnSpan(overlay, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);

        // Add overlay to main grid
        mainGrid.Children.Add(overlay);
        
        // Store reference for cleanup
        _overlayHost = mainGrid;
        _currentOverlay = overlay;

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
