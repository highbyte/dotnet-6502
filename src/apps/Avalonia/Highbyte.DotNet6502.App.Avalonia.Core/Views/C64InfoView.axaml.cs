using Avalonia.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

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
}
