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

/// <summary>
/// Describes the program a (shared) C64 startup link is about to load and run, for display in the
/// startup acknowledgement dialog. <paramref name="Url"/> is shown only for sources that have one
/// (<c>.prg</c> / <c>.d64</c>).
/// </summary>
public sealed record C64StartupProgramInfo(string Kind, string? Url, bool AudioEnabled);

/// <summary>
/// Shows the themed C64 acknowledgement overlays. Unifies two related consent needs into one
/// consistent dialog family:
/// <list type="bullet">
///   <item>Pre-selection startup acknowledgement for URL-driven (shared) links — what is about to
///   run, plus the ROM-license consent when ROMs are missing (<see cref="RunStartupAcknowledgmentAsync"/>).</item>
///   <item>ROM-license consent on its own, used by the C64 config dialog's download command
///   (<see cref="ShowRomLicenseConsentAsync"/>).</item>
/// </list>
/// The actual ROM download (progress) runs separately after the system is selected
/// (<see cref="RunRomDownloadAsync"/>), since consent has already been given by then.
/// </summary>
public sealed class C64AcknowledgmentService
{
    private readonly OverlayDialogHelper _overlayDialogHelper;
    private readonly ILogger _logger;

    public C64AcknowledgmentService(
        OverlayDialogHelper overlayDialogHelper,
        ILoggerFactory loggerFactory)
    {
        _overlayDialogHelper = overlayDialogHelper;
        _logger = loggerFactory.CreateLogger(nameof(C64AcknowledgmentService));
    }

    /// <summary>
    /// Pre-selection acknowledgement for an automated (URL-driven) C64 startup. Shows what is about
    /// to run and, when <paramref name="romsNeeded"/>, the ROM-license consent (gated by a
    /// checkbox). Invokes <paramref name="unlockAudio"/> on confirmation so audio can play (browser
    /// autoplay policy). Returns <see langword="true"/> when the user confirms.
    /// </summary>
    public Task<bool> RunStartupAcknowledgmentAsync(
        C64StartupProgramInfo programInfo,
        bool romsNeeded,
        Func<Task>? unlockAudio)
        => ShowConsentDialogAsync(
            title: "Start C64 program",
            programInfo: programInfo,
            showRomSection: romsNeeded,
            confirmText: "Start",
            unlockAudio: unlockAudio,
            owner: null);

    /// <summary>
    /// ROM-license consent on its own (no program-start section), used by the C64 config dialog's
    /// ROM download command. Returns <see langword="true"/> when the user acknowledges.
    /// </summary>
    public Task<bool> ShowRomLicenseConsentAsync(UserControl owner)
        => ShowConsentDialogAsync(
            title: "ROM License Acknowledgement",
            programInfo: null,
            showRomSection: true,
            confirmText: "Yes",
            unlockAudio: null,
            owner: owner);

    /// <summary>
    /// Builds the consent overlay. Sections are shown dynamically: an optional program-start
    /// section (<paramref name="programInfo"/>) and an optional ROM-license section
    /// (<paramref name="showRomSection"/>). When the ROM section is shown, the confirm button is
    /// disabled until the license checkbox is ticked.
    /// </summary>
    private Task<bool> ShowConsentDialogAsync(
        string title,
        C64StartupProgramInfo? programInfo,
        bool showRomSection,
        string confirmText,
        Func<Task>? unlockAudio,
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
            Classes = { "small", "primary" },
            // When the ROM section is shown, require the license checkbox first.
            IsEnabled = !showRomSection
        };
        confirmButton.Click += (_, _) => SafeAsyncHelper.Execute(async () =>
        {
            if (unlockAudio != null)
            {
                try { await unlockAudio(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Audio unlock callback failed."); }
            }
            CloseOverlay(true);
        });

        var cancelButton = new Button
        {
            Content = "Cancel",
            Classes = { "small", "cancel" }
        };
        cancelButton.Click += (_, _) => CloseOverlay(false);

        var bodyChildren = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 8
        };

        // ── Program-start section ──────────────────────────────────────────────────────────
        if (programInfo != null)
        {
            bodyChildren.Children.Add(new TextBlock
            {
                Text = "This shared link will start:",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });
            bodyChildren.Children.Add(new TextBlock
            {
                Text = programInfo.Kind,
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 0, 0)
            });
            if (!string.IsNullOrEmpty(programInfo.Url))
            {
                bodyChildren.Children.Add(new TextBlock
                {
                    Text = programInfo.Url,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 140, 174)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8, 0, 0, 0)
                });
            }
            if (programInfo.AudioEnabled)
            {
                bodyChildren.Children.Add(new TextBlock
                {
                    Text = "Audio is enabled.",
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
        }

        // ── ROM-license section ────────────────────────────────────────────────────────────
        CheckBox? licenseCheckBox = null;
        if (showRomSection)
        {
            if (programInfo != null)
            {
                bodyChildren.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
                    Margin = new Thickness(0, 8, 0, 4)
                });
            }

            bodyChildren.Children.Add(new TextBlock
            {
                Text = "Commodore 64 ROM files are needed. The app can download them from:",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });
            bodyChildren.Children.Add(BuildRomUrlButton(owner));
            bodyChildren.Children.Add(new TextBlock
            {
                Text = "Please be aware that you probably need to:",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            bodyChildren.Children.Add(new TextBlock
            {
                Text = "• Have a license from Commodore/Cloanto, OR\n• Own a Commodore 64 computer",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 0, 0)
            });
            bodyChildren.Children.Add(new TextBlock
            {
                Text = "to be allowed to download and use these ROM files.",
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });

            licenseCheckBox = new CheckBox
            {
                Content = "I have a license from Commodore/Cloanto, or own a Commodore 64",
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0)
            };
            licenseCheckBox.IsCheckedChanged += (_, _) =>
                confirmButton.IsEnabled = licenseCheckBox.IsChecked == true;
            bodyChildren.Children.Add(licenseCheckBox);
        }

        bodyChildren.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { cancelButton, confirmButton }
        });

        var dialogContent = BuildDialogShell(title, bodyChildren);

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
            _logger.LogError(ex, "Failed to show C64 acknowledgement overlay.");
            return Task.FromResult(false);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Downloads the C64 ROMs (progress only — consent is assumed to have already been given via
    /// <see cref="RunStartupAcknowledgmentAsync"/>), applies and validates the result via
    /// <paramref name="finalizeAfterDownloadAsync"/>, and returns <see langword="true"/> on success.
    /// On failure the user can open the C64 config or cancel.
    /// </summary>
    public async Task<bool> RunRomDownloadAsync(
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
            Text = "Downloading the Commodore 64 ROM files needed to start the emulator…",
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
            openConfigButton.IsVisible = true;
            cancelButton.IsEnabled = true;
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

        openConfigButton.Click += (_, _) => SafeAsyncHelper.Execute(OpenConfigAsync);
        cancelButton.Click += (_, _) => CloseOverlay(false);

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
                                    cancelButton,
                                    openConfigButton
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
            _logger.LogError(ex, "Failed to show startup C64 ROM download overlay.");
            return false;
        }

        // Consent already obtained — start the download immediately.
        SafeAsyncHelper.Execute(StartDownloadAsync);

        return await tcs.Task;
    }

    private Border BuildDialogShell(string title, Control body)
        => new()
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
                        Child = new TextBlock
                        {
                            Text = title,
                            FontSize = 14,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    },
                    body
                }
            }
        };

    private Button BuildRomUrlButton(UserControl? owner)
    {
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
        urlButton.Click += (_, _) => SafeAsyncHelper.Execute(
            () => LaunchUriIfAvailableAsync(owner, C64SystemConfig.DEFAULT_KERNAL_ROM_DOWNLOAD_BASE_URL));
        return urlButton;
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
