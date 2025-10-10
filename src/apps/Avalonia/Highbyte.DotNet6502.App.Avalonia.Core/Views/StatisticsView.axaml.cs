using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Highbyte.DotNet6502.App.Avalonia.Core.ViewModels;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Views;

public partial class StatisticsView : UserControl
{
    private StatisticsViewModel? _viewModel;

    public StatisticsView()
    {
        InitializeComponent();
        _viewModel = new StatisticsViewModel();
        DataContext = _viewModel;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        // Dispose the view model when the control is removed from the visual tree
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
