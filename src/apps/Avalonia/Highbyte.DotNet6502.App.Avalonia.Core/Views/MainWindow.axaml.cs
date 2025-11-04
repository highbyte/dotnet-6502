using Avalonia.Controls;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
#if DEBUG
        // Temporarily disable the automatic opening of dev tools via F12 as it conflicts with emulator key input. Is enabled via another key in App.axaml.cs
        InitializeComponent(attachDevTools: false);
#else
        InitializeComponent();
#endif

        // Remove fixed Width since SizeToContent="Width" will handle it
        Height = 800;
        MinWidth = 650; // Minimum to accommodate first two columns (250 + 400)
        MinHeight = 600;
        CanResize = false; // Make window non-resizable
    }
}
