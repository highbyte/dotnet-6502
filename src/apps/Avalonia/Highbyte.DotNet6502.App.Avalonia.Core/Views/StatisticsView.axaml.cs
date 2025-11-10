using Avalonia;
using Avalonia.Controls;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class StatisticsView : UserControl
{
    // Access ViewModel through DataContext
    private StatisticsViewModel? ViewModel => DataContext as StatisticsViewModel;

    // Parameterless constructor for XAML compatibility
    public StatisticsView()
    {
        InitializeComponent();

        // Subscribe to DataContext changes
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // ViewModel is now available through DataContext binding
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Dispose the view model when the control is removed from the visual tree
        ViewModel?.Dispose();
    }
}
