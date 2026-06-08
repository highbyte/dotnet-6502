using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var text = ViewModel?.GetStatsText();
        if (string.IsNullOrEmpty(text))
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
            return;

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateText(text));
        SafeAsyncHelper.Execute(() => clipboard.SetDataAsync(dataTransfer));
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ResetStats();
    }

    // Pause the periodic refresh while the pointer is over the panel, so values (and the layout)
    // don't change under the cursor — which would otherwise dismiss a tooltip being read.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (ViewModel != null)
            ViewModel.UpdatesPaused = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (ViewModel != null)
            ViewModel.UpdatesPaused = false;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Dispose the view model when the control is removed from the visual tree
        ViewModel?.Dispose();
    }
}
