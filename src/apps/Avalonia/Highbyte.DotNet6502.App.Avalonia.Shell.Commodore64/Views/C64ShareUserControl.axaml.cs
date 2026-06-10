using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.Views;

/// <summary>
/// Share-link dialog content shown in an overlay. Binds to <c>C64MenuViewModel</c> (its share
/// properties/commands); raises <see cref="CloseRequested"/> when the user dismisses it.
/// </summary>
public partial class C64ShareUserControl : UserControl
{
    /// <summary>Raised when the user clicks Close so the host can remove the overlay.</summary>
    public event EventHandler? CloseRequested;

    public C64ShareUserControl()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
        => CloseRequested?.Invoke(this, EventArgs.Empty);
}
