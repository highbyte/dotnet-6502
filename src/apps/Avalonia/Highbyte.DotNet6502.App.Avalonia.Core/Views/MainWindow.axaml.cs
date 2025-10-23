using Avalonia.Controls;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Highbyte.DotNet6502 Emulator - Avalonia";
        // Remove fixed Width since SizeToContent="Width" will handle it
        Height = 800;
        MinWidth = 650; // Minimum to accommodate first two columns (250 + 400)
        MinHeight = 600;
        CanResize = false; // Make window non-resizable
    }
}
