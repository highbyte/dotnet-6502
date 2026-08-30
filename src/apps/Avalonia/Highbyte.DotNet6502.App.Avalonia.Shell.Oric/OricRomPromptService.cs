using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Highbyte.DotNet6502.App.Avalonia.Core;
using Highbyte.DotNet6502.Systems.Oric.Config;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Oric;

/// <summary>Explicit legal acknowledgement shown before fetching the non-redistributable ROM.</summary>
public sealed class OricRomPromptService(OverlayDialogHelper overlayDialogHelper)
{
    public Task<bool> ShowAsync(UserControl owner)
    {
        var completion = new TaskCompletionSource<bool>();
        Grid? host = null;
        Panel? overlay = null;

        void Close(bool result)
        {
            if (host != null && overlay != null)
                host.Children.Remove(overlay);
            completion.TrySetResult(result);
        }

        var confirm = new Button { Content = "I own or license the ROM — download", Classes = { "small", "primary" } };
        var cancel = new Button { Content = "Cancel", Classes = { "small", "cancel" } };
        AutomationProperties.SetAutomationId(confirm, "OricRomDownloadConfirmButton");
        AutomationProperties.SetAutomationId(cancel, "OricRomDownloadCancelButton");
        confirm.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);

        var content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(74, 85, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Oric ROM licence acknowledgement", FontSize = 16, FontWeight = FontWeight.Bold },
                    new TextBlock
                    {
                        Text = "The Atmos BASIC ROM is copyrighted firmware. The emulator does not grant a licence or verify that the download site is authorized to redistribute it.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Continue only if you own an Oric Atmos or otherwise have permission to possess and use this ROM.",
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeight.Bold
                    },
                    new TextBlock { Text = OricSystemConfig.RomSourceInfoUrl, TextWrapping = TextWrapping.Wrap, FontSize = 10 },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancel, confirm }
                    }
                }
            }
        };
        overlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { content }
        };
        host = overlayDialogHelper.ShowOverlayDialog(overlay, owner);
        return completion.Task;
    }
}
