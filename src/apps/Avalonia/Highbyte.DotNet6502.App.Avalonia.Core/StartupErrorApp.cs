using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

/// <summary>
/// A minimal, stand-alone Avalonia application that shows nothing but a fatal startup-error
/// message and a Quit button.
/// <para>
/// Used when the normal app cannot even be started — e.g. a malformed <c>appsettings.json</c>,
/// a plug-in/DI failure — so the error is still shown in a window instead of only on the console.
/// It deliberately depends on as little as possible: no DI, no ViewModels, no ReactiveUI, no
/// XAML — just plain controls and the Fluent theme. Failures *inside* the normal app are handled
/// separately by <see cref="App.ShowFatalStartupError"/>.
/// </para>
/// </summary>
public sealed class StartupErrorApp : Application
{
    private readonly string _message;

    /// <summary>Parameterless constructor for the Avalonia designer / tooling.</summary>
    public StartupErrorApp() : this("Unknown startup error.") { }

    public StartupErrorApp(string message) => _message = message;

    public override void Initialize()
    {
        // A control theme is required for Button/TextBox/ScrollViewer to render.
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var content = BuildContent();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "DotNet 6502 Emulator — startup error",
                Content = content,
                Width = 620,
                Height = 400,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = content;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Control BuildContent()
    {
        var title = new TextBlock
        {
            Text = "The emulator could not start",
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 12),
        };

        var messageBox = new TextBox
        {
            Text = _message,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
        };

        var scroller = new ScrollViewer
        {
            Content = messageBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 12),
        };

        var panel = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        // A browser tab cannot quit itself, so only desktop gets a Quit action; on browser the
        // window simply stays open (non-closeable), matching App.ShowFatalStartupError.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var quitButton = new Button
            {
                Content = "Quit",
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(24, 6),
            };
            quitButton.Click += (_, _) => desktop.Shutdown();
            DockPanel.SetDock(quitButton, Dock.Bottom);
            panel.Children.Add(quitButton);
        }

        panel.Children.Add(scroller); // last child fills the remaining space

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
            Child = panel,
        };
    }
}
