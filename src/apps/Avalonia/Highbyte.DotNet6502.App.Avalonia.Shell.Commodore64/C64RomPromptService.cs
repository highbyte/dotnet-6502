using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.Views;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64;

public sealed class C64RomPromptService
{
    private readonly OverlayDialogHelper _overlayDialogHelper;
    private readonly ILogger _logger;

    public C64RomPromptService(
        OverlayDialogHelper overlayDialogHelper,
        ILoggerFactory loggerFactory)
    {
        _overlayDialogHelper = overlayDialogHelper;
        _logger = loggerFactory.CreateLogger(nameof(C64RomPromptService));
    }

    public Task<bool> ShowConfigDownloadAcknowledgementAsync(UserControl owner)
        => ShowDownloadPromptAsync(
            title: "ROM License Acknowledgement",
            leadText: "You are about to download Commodore 64 ROM files from:",
            acknowledgeText: "Do you acknowledge these requirements and wish to proceed?",
            confirmText: "Yes",
            cancelText: "No",
            owner: owner);

    public async Task<bool> RunStartupDownloadWorkflowAsync(
        C64ConfigDialogViewModel configViewModel,
        Func<Task<(bool success, string? errorTitle, string? errorMessage)>> finalizeAfterDownloadAsync)
    {
        var tcs = new TaskCompletionSource<bool>();
        Grid? overlayHost = null;
        Panel? overlayPanel = null;

        void CloseOverlay(bool result)
        {
            if (overlayPanel != null && overlayHost != null)
                overlayHost.Children.Remove(overlayPanel);
            tcs.TrySetResult(result);
        }

        async Task OpenConfigAsync()
        {
            if (overlayPanel != null && overlayHost != null)
                overlayHost.Children.Remove(overlayPanel);

            await ShowStartupConfigOverlayAsync(configViewModel);
            tcs.TrySetResult(false);
        }

        var titleText = new TextBlock
        {
            Text = "Download C64 ROMs",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var leadTextBlock = new TextBlock
        {
            Text = "This C64 startup link needs Commodore 64 ROM files before the emulator can start. The app can download them from:",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap
        };

        var statusTextBlock = new TextBlock
        {
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            IsVisible = false,
            Height = 6
        };

        var errorTextBlock = new TextBlock
        {
            Classes = { "small" },
            TextWrapping = TextWrapping.Wrap
        };

        var errorBorder = new Border
        {
            Classes = { "error-container-small" },
            IsVisible = false,
            Child = errorTextBlock
        };

        var downloadButton = new Button
        {
            Content = "Download ROMs",
            Classes = { "small", "primary" }
        };

        var openConfigButton = new Button
        {
            Content = "Open C64 Config",
            Classes = { "small", "primary" },
            IsVisible = false
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "small", "cancel" }
        };

        void SetBusyState(string statusText)
        {
            statusTextBlock.Text = statusText;
            statusTextBlock.IsVisible = true;
            progressBar.IsVisible = true;
            errorBorder.IsVisible = false;
            downloadButton.IsEnabled = false;
            cancelButton.IsEnabled = false;
            openConfigButton.IsVisible = false;
        }

        void SetFailureState(string title, string leadText, string errorMessage)
        {
            titleText.Text = title;
            leadTextBlock.Text = leadText;
            statusTextBlock.IsVisible = false;
            progressBar.IsVisible = false;
            errorTextBlock.Text = errorMessage;
            errorBorder.IsVisible = true;
            downloadButton.IsVisible = false;
            openConfigButton.IsVisible = true;
            cancelButton.IsEnabled = true;
            cancelButton.Content = "Cancel";
        }

        async Task StartDownloadAsync()
        {
            SetBusyState("Downloading C64 ROMs...");

            try
            {
                if (!await configViewModel.DownloadRomsToByteArrayAsync(requireAcknowledgement: false))
                {
                    SetFailureState(
                        title: "C64 ROM Download Failed",
                        leadText: "The automated C64 startup was cancelled because the ROM download failed.",
                        errorMessage: configViewModel.StatusMessage ?? "Unknown error.");
                    return;
                }

                SetBusyState("Saving downloaded ROM configuration...");

                var finalizeResult = await finalizeAfterDownloadAsync();
                if (!finalizeResult.success)
                {
                    SetFailureState(
                        title: finalizeResult.errorTitle ?? "C64 ROM Configuration Failed",
                        leadText: "The automated C64 startup could not continue. Review the configuration and try again.",
                        errorMessage: finalizeResult.errorMessage ?? "Unknown error.");
                    return;
                }

                CloseOverlay(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure in startup C64 ROM download workflow.");
                SetFailureState(
                    title: "C64 ROM Download Failed",
                    leadText: "The automated C64 startup was cancelled because the ROM download failed.",
                    errorMessage: ex.Message);
            }
        }

        downloadButton.Click += (_, _) => SafeAsyncHelper.Execute(StartDownloadAsync);
        openConfigButton.Click += (_, _) => SafeAsyncHelper.Execute(OpenConfigAsync);
        cancelButton.Click += (_, _) => CloseOverlay(false);

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
        urlButton.Click += (_, _) => SafeAsyncHelper.Execute(() => LaunchUriIfAvailableAsync(null, C64SystemConfig.DEFAULT_KERNAL_ROM_DOWNLOAD_BASE_URL));

        var dialogContent = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                        Padding = new Thickness(8, 6),
                        CornerRadius = new CornerRadius(4, 4, 0, 0),
                        Child = titleText
                    },
                    new StackPanel
                    {
                        Margin = new Thickness(12),
                        Spacing = 8,
                        Children =
                        {
                            leadTextBlock,
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
                                Text = "Do you acknowledge these requirements and want the app to download the ROMs now?",
                                FontSize = 10,
                                TextWrapping = TextWrapping.Wrap,
                                FontWeight = FontWeight.Bold,
                                Margin = new Thickness(0, 8, 0, 0)
                            },
                            progressBar,
                            statusTextBlock,
                            errorBorder,
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 10,
                                Margin = new Thickness(0, 8, 0, 0),
                                Children =
                                {
                                    downloadButton,
                                    openConfigButton,
                                    cancelButton
                                }
                            }
                        }
                    }
                }
            }
        };

        overlayPanel = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { dialogContent }
        };

        try
        {
            overlayHost = _overlayDialogHelper.ShowOverlayDialogOnMainView(overlayPanel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show startup C64 ROM workflow overlay.");
            return false;
        }

        return await tcs.Task;
    }

    private Task<bool> ShowDownloadPromptAsync(
        string title,
        string leadText,
        string acknowledgeText,
        string confirmText,
        string cancelText,
        UserControl? owner)
    {
        var tcs = new TaskCompletionSource<bool>();
        Grid? overlayHost = null;
        Panel? overlayPanel = null;

        void CloseOverlay(bool result)
        {
            if (overlayPanel != null && overlayHost != null)
                overlayHost.Children.Remove(overlayPanel);
            tcs.TrySetResult(result);
        }

        var confirmButton = new Button
        {
            Content = confirmText,
            Classes = { "small", "primary" }
        };
        confirmButton.Click += (_, _) => CloseOverlay(true);

        var cancelButton = new Button
        {
            Content = cancelText,
            Classes = { "small", "cancel" }
        };
        cancelButton.Click += (_, _) => CloseOverlay(false);

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
        urlButton.Click += (_, _) => SafeAsyncHelper.Execute(() => LaunchUriIfAvailableAsync(owner, C64SystemConfig.DEFAULT_KERNAL_ROM_DOWNLOAD_BASE_URL));

        var dialogContent = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                        Padding = new Thickness(8, 6),
                        CornerRadius = new CornerRadius(4, 4, 0, 0),
                        Child = new TextBlock
                        {
                            Text = title,
                            FontSize = 14,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    },
                    new StackPanel
                    {
                        Margin = new Thickness(12),
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = leadText,
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
                                Text = acknowledgeText,
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
                                    confirmButton,
                                    cancelButton
                                }
                            }
                        }
                    }
                }
            }
        };

        overlayPanel = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { dialogContent }
        };

        try
        {
            overlayHost = owner != null
                ? _overlayDialogHelper.ShowOverlayDialog(overlayPanel, owner)
                : _overlayDialogHelper.ShowOverlayDialogOnMainView(overlayPanel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show C64 ROM prompt overlay.");
            return Task.FromResult(false);
        }

        return tcs.Task;
    }

    private async Task LaunchUriIfAvailableAsync(UserControl? owner, string uri)
    {
        if (owner != null)
        {
            if (TopLevel.GetTopLevel(owner) is { Launcher: { } ownerLauncher })
            {
                await ownerLauncher.LaunchUriAsync(new Uri(uri));
                return;
            }
        }

        if (Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Launcher is { } desktopLauncher)
        {
            await desktopLauncher.LaunchUriAsync(new Uri(uri));
            return;
        }

        if (Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView &&
            TopLevel.GetTopLevel(singleView.MainView) is { Launcher: { } launcher })
        {
            await launcher.LaunchUriAsync(new Uri(uri));
        }
    }

    private async Task ShowStartupConfigOverlayAsync(C64ConfigDialogViewModel configViewModel)
    {
        var configControl = new C64ConfigUserControl
        {
            DataContext = configViewModel
        };

        var overlayPanel = _overlayDialogHelper.BuildOverlayDialogPanel(configControl);
        var overlayHost = _overlayDialogHelper.ShowOverlayDialogOnMainView(overlayPanel);
        var tcs = new TaskCompletionSource();

        configControl.ConfigurationChanged += (_, _) => tcs.TrySetResult();

        try
        {
            await tcs.Task;
        }
        finally
        {
            overlayHost.Children.Remove(overlayPanel);
        }
    }
}
