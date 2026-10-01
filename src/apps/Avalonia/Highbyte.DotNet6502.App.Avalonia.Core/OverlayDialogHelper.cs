using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Embedding;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Highbyte.DotNet6502.App.Avalonia.Core.Views;

namespace Highbyte.DotNet6502.App.Avalonia.Core;

public class OverlayDialogHelper
{
    // Hosts may supply the visible area in Avalonia coordinates. With no override,
    // desktop dialogs retain their normal window-relative layout.
    public static readonly AttachedProperty<Rect?> VisibleViewportProperty =
        AvaloniaProperty.RegisterAttached<OverlayDialogHelper, TopLevel, Rect?>("VisibleViewport");

    public static readonly AttachedProperty<int> ModalOverlayCountProperty =
        AvaloniaProperty.RegisterAttached<OverlayDialogHelper, TopLevel, int>("ModalOverlayCount");

    private static readonly AttachedProperty<bool> HasModalTrackingProperty =
        AvaloniaProperty.RegisterAttached<OverlayDialogHelper, Panel, bool>("HasModalTracking");

    private readonly IApplicationLifetime? _applicationLifetime;

    public OverlayDialogHelper(IApplicationLifetime? applicationLifetime)
    {
        _applicationLifetime = applicationLifetime;
    }

    public Panel BuildOverlayDialogPanel(UserControl userControl)
    {
        const double dialogMargin = 20.0;

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
            Margin = new Thickness(dialogMargin), // Add margin from screen edges
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = userControl // Direct child, no ScrollViewer wrapper
        };

        return BuildOverlayDialogPanel(dialogContainer);
    }

    /// <summary>
    /// Hosts an already styled dialog using the same viewport-aware layout as
    /// standard dialogs, preserving its own size limits and appearance.
    /// </summary>
    public Panel BuildOverlayDialogPanel(Border dialogContainer)
    {
        var overlay = new ViewportOverlayPanel
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            ZIndex = 1000,
            Children = { dialogContainer }
        };
        var maximumWidth = dialogContainer.MaxWidth;
        var maximumHeight = dialogContainer.MaxHeight;

        // Cap dialog size to the visual root's client size so a tall/wide dialog cannot
        // grow past the viewport. This lets the dialog's inner ScrollViewer engage and
        // show a vertical scrollbar instead of pushing the OK/Cancel buttons off-screen.
        TopLevel? attachedTopLevel = null;
        EventHandler<SizeChangedEventArgs>? sizeHandler = null;
        EventHandler<AvaloniaPropertyChangedEventArgs>? viewportHandler = null;

        void ApplyTopLevelBounds()
        {
            if (attachedTopLevel == null)
                return;
            overlay.VisibleViewport = attachedTopLevel.GetValue(VisibleViewportProperty);
            var size = overlay.VisibleViewport?.Size ?? attachedTopLevel.ClientSize;
            dialogContainer.MaxWidth = Math.Min(maximumWidth,
                Math.Max(0, size.Width - dialogContainer.Margin.Left - dialogContainer.Margin.Right));
            dialogContainer.MaxHeight = Math.Min(maximumHeight,
                Math.Max(0, size.Height - dialogContainer.Margin.Top - dialogContainer.Margin.Bottom));
        }

        overlay.AttachedToVisualTree += (_, _) =>
        {
            attachedTopLevel = TopLevel.GetTopLevel(overlay);
            if (attachedTopLevel == null)
                return;
            sizeHandler = (_, _) => ApplyTopLevelBounds();
            attachedTopLevel.SizeChanged += sizeHandler;
            viewportHandler = (_, e) =>
            {
                if (e.Property == VisibleViewportProperty)
                    ApplyTopLevelBounds();
            };
            attachedTopLevel.PropertyChanged += viewportHandler;
            ApplyTopLevelBounds();
        };

        overlay.DetachedFromVisualTree += (_, _) =>
        {
            if (attachedTopLevel != null && sizeHandler != null)
                attachedTopLevel.SizeChanged -= sizeHandler;
            if (attachedTopLevel != null && viewportHandler != null)
                attachedTopLevel.PropertyChanged -= viewportHandler;
            attachedTopLevel = null;
            sizeHandler = null;
            viewportHandler = null;
        };

        return overlay;
    }

    private sealed class ViewportOverlayPanel : Panel
    {
        private Rect? _visibleViewport;

        public Rect? VisibleViewport
        {
            get => _visibleViewport;
            set
            {
                if (_visibleViewport == value)
                    return;
                _visibleViewport = value;
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_visibleViewport is not { } viewport)
                return base.MeasureOverride(availableSize);

            base.MeasureOverride(viewport.Size);
            // Browser overlays must not enlarge the natural app measurement and
            // feed back into automatic Fit sizing.
            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_visibleViewport is not { } viewport)
                return base.ArrangeOverride(finalSize);

            var root = TopLevel.GetTopLevel(this);
            var origin = root == null ? default : this.TranslatePoint(default, root) ?? default;
            var area = new Rect(viewport.X - origin.X, viewport.Y - origin.Y,
                viewport.Width, viewport.Height);
            foreach (var child in Children)
                child.Arrange(area);
            return finalSize;
        }
    }

    public Grid ShowOverlayDialogOnMainView(Panel overlayPanel)
    {
        var mainView = GetMainView() ?? throw new DotNet6502Exception("A MainView was not found. Cannot show overlay dialog.");
        return ShowOverlayDialog(overlayPanel, mainView);
    }

    public Grid ShowOverlayDialog(Panel overlayPanel, UserControl currentControl)
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
        TrackModalOverlay(overlayPanel);
        Grid.SetRowSpan(overlayPanel, displayOnGrid.RowDefinitions.Count > 0 ? displayOnGrid.RowDefinitions.Count : 1);
        Grid.SetColumnSpan(overlayPanel, displayOnGrid.ColumnDefinitions.Count > 0 ? displayOnGrid.ColumnDefinitions.Count : 1);
        displayOnGrid.Children.Add(overlayPanel);
    }

    private static void TrackModalOverlay(Panel overlayPanel)
    {
        // Install once so a panel can be closed and reopened without counting it twice.
        if (overlayPanel.GetValue(HasModalTrackingProperty))
            return;
        overlayPanel.SetValue(HasModalTrackingProperty, true);
        TopLevel? modalRoot = null;
        overlayPanel.AttachedToVisualTree += (_, _) =>
        {
            modalRoot = TopLevel.GetTopLevel(overlayPanel);
            if (modalRoot != null)
                modalRoot.SetValue(ModalOverlayCountProperty,
                    modalRoot.GetValue(ModalOverlayCountProperty) + 1);
        };
        overlayPanel.DetachedFromVisualTree += (_, _) =>
        {
            if (modalRoot != null)
                modalRoot.SetValue(ModalOverlayCountProperty,
                    Math.Max(0, modalRoot.GetValue(ModalOverlayCountProperty) - 1));
            modalRoot = null;
        };
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
        var root = TopLevel.GetTopLevel(currentControl);

        // When running in desktop, and currentControl is displayed on MainWindow, root is MainWindow (which contains MainView which contains Grid)
        if (root is Window window && window.Content is MainView mainView && mainView.Content is Grid grid)
            return grid;

        // Browser content can wrap MainView (for example, to measure the zoomable
        // viewport). Nested dialogs still belong on MainView's outer grid.
        if (root is EmbeddableControlRoot ecr)
        {
            var browserMainView = ecr.Content as MainView
                ?? ecr.FindDescendantOfType<MainView>();
            if (browserMainView?.Content is Grid browserGrid)
                return browserGrid;
        }

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
            mainView = desktop.MainWindow?.Content as MainView
                ?? desktop.MainWindow?.FindDescendantOfType<MainView>();
        }
        else if (_applicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            mainView = singleViewPlatform.MainView as MainView
                ?? singleViewPlatform.MainView?.FindDescendantOfType<MainView>();
        }
        return mainView;
    }
}
