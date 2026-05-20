using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.Views;

public partial class C64InfoView : UserControl
{
    // Access ViewModel through DataContext
    private C64InfoViewModel? ViewModel => DataContext as C64InfoViewModel;

    // Parameterless constructor for XAML compatibility
    public C64InfoView()
    {
        InitializeComponent();

        // Subscribe to ViewModel events for UI operations
        this.DataContextChanged += (s, e) =>
        {
            if (ViewModel != null)
            {
            }
        };

    }

    // Event handlers for ViewModel requests (pure UI operations)

    private void OpenKeyboardDoc_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
            _ = topLevel.Launcher.LaunchUriAsync(
                new Uri("https://highbyte.github.io/dotnet-6502/docs/systems/c64/keyboard/"));
    }
}
