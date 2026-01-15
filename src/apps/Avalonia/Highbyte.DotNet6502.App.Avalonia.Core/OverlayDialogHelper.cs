using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Embedding;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Highbyte.DotNet6502.App.Avalonia.Core.Views;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

internal class OverlayDialogHelper
{
    private readonly IApplicationLifetime? _applicationLifetime;

    public OverlayDialogHelper(IApplicationLifetime? applicationLifetime)
    {
        _applicationLifetime = applicationLifetime;
    }

    internal Panel BuildOverlayDialogPanel(UserControl userControl)
    {
        // Create a custom overlay with better modal behavior
        var overlay = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // More opaque overlay
            ZIndex = 1000
        };

        // Create a dialog container that looks like a proper modal
        var dialogContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),  // 1A202C, ViewDefaultBg
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 8,
                Blur = 25,
                Color = Color.FromArgb(128, 0, 0, 0)
            }),
            Margin = new Thickness(20), // Add margin from screen edges
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = userControl // Direct child, no ScrollViewer wrapper
        };

        overlay.Children.Add(dialogContainer);

        return overlay;
    }

    internal Grid ShowOverlayDialogOnMainView(Panel overlayPanel)
    {
        var mainView = GetMainView() ?? throw new DotNet6502Exception("A MainView was not found. Cannot show overlay dialog.");
        return ShowOverlayDialog(overlayPanel, mainView);
    }

    internal Grid ShowOverlayDialog(Panel overlayPanel, UserControl currentControl)
    {
        var displayOnGrid = GetGrid(currentControl) ?? throw new DotNet6502Exception("A Grid not found from current usercontrol. Cannot show overlay dialog.");
        ShowOverlayDialog(overlayPanel, displayOnGrid);
        return displayOnGrid;
    }


    /// <summary>
    /// Displays a panel overlay on top of a Grid control.
    /// 
    /// NOTE: It is required that the UserControl where the overlay panel is to be displayed has a Grid as its content container.
    /// 
    /// The reason a Grid is used as the container for displaying a panel overlay is this:
    /// 1.	Z-Index Layering: When you add multiple children to a Grid, they stack on top of each other (later children render above earlier ones). Combined with the ZIndex = 1000 set on the overlay panel, this ensures the dialog appears above all other content.
    /// 2.	Row/Column Spanning: The code uses Grid.SetRowSpan and Grid.SetColumnSpan to make the overlay stretch across the entire grid area, covering all existing content beneath it.
    /// 3.	No Layout Displacement: Unlike StackPanel or DockPanel, adding a child to a Grid doesn't push other children around—they simply overlap in the same cells.
    /// </summary>
    /// <param name="overlayPanel"></param>
    /// <param name="displayOnGrid"></param>
    private void ShowOverlayDialog(Panel overlayPanel, Grid displayOnGrid)
    {
        Grid.SetRowSpan(overlayPanel, displayOnGrid.RowDefinitions.Count > 0 ? displayOnGrid.RowDefinitions.Count : 1);
        Grid.SetColumnSpan(overlayPanel, displayOnGrid.ColumnDefinitions.Count > 0 ? displayOnGrid.ColumnDefinitions.Count : 1);
        displayOnGrid.Children.Add(overlayPanel);
    }

    /// <summary>
    /// Get the suitable Grid from currentControl to display an overlay panel dialog on.
    /// 
    /// Walk up to find the root, then get its content Grid
    /// If currentControl exists on MainWindow, root will be MainWindow.
    /// If currentControl exists in a separate Window (ex: C64ConfigDialog), root will be that Window (not MainWindow)
    /// If currentControl itself was opened as a Overlay from MainWindow, root will also be MainWindow.
    /// </summary>
    private Grid? GetGrid(UserControl currentControl)
    {
        var root = currentControl.GetVisualRoot();

        // When running in desktop, and currentControl is displayed on MainWindow, root is MainWindow (which contains MainView which contains Grid)
        if (root is Window window && window.Content is MainView mainView && mainView.Content is Grid grid)
            return grid;

        // When running in browser (WebAssembly) root is always EmbeddableControlRoot regardless if opened nested (overlay on overlay)
        if (root is EmbeddableControlRoot ecr && ecr.Content is MainView mv && mv.Content is Grid mvGrid)
            return mvGrid;

        // Fallback:
        // When running in desktop but not on MainWindow, find the UserControl's own content Grid
        return currentControl.Content as Grid;
    }

    /// <summary>
    /// Find the MainView user control that exists on the root Window (or single view platform as browser).
    /// Use this when a overplay panel is needed to be displayed from code that does not have direct access to the MainView (ex: from App.axaml.cs when showing error dialog)
    /// </summary>
    /// <returns></returns>
    private MainView? GetMainView()
    {
        MainView? mainView = null;
        if (_applicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainView = desktop.MainWindow?.Content as MainView;
        }
        else if (_applicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            mainView = singleViewPlatform.MainView as MainView;
        }
        return mainView;
    }
}
