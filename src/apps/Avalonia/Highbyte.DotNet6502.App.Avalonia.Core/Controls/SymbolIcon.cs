using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Highbyte.DotNet6502.App.Avalonia.Core.Controls;

public enum SymbolIconKind
{
    None,
    FilledCircle,
    HollowCircle,
    TriangleRight,
    TriangleDown,
    TriangleUp,
    Cross
}

public class SymbolIcon : Control
{
    public static readonly StyledProperty<SymbolIconKind> KindProperty =
        AvaloniaProperty.Register<SymbolIcon, SymbolIconKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<SymbolIcon, IBrush?>(nameof(Foreground), Brushes.White);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SymbolIcon, double>(nameof(StrokeThickness), 1.5d);

    public SymbolIconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static SymbolIcon()
    {
        AffectsRender<SymbolIcon>(KindProperty, ForegroundProperty, StrokeThicknessProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        const double defaultSize = 10;
        var width = double.IsInfinity(availableSize.Width) ? defaultSize : Math.Min(defaultSize, availableSize.Width);
        var height = double.IsInfinity(availableSize.Height) ? defaultSize : Math.Min(defaultSize, availableSize.Height);
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Kind == SymbolIconKind.None || Foreground is null)
            return;

        var rect = new Rect(Bounds.Size).Deflate(0.75);
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        switch (Kind)
        {
            case SymbolIconKind.FilledCircle:
                DrawFilledCircle(context, rect, Foreground);
                break;
            case SymbolIconKind.HollowCircle:
                DrawHollowCircle(context, rect, Foreground, StrokeThickness);
                break;
            case SymbolIconKind.TriangleRight:
                DrawFilledTriangle(context, Foreground, rect.TopLeft, rect.BottomLeft, new Point(rect.Right, rect.Center.Y));
                break;
            case SymbolIconKind.TriangleDown:
                DrawFilledTriangle(context, Foreground, rect.TopLeft, rect.TopRight, new Point(rect.Center.X, rect.Bottom));
                break;
            case SymbolIconKind.TriangleUp:
                DrawFilledTriangle(context, Foreground, new Point(rect.Left, rect.Bottom), new Point(rect.Right, rect.Bottom), new Point(rect.Center.X, rect.Top));
                break;
            case SymbolIconKind.Cross:
                DrawCross(context, rect, Foreground, StrokeThickness);
                break;
        }
    }

    private static void DrawFilledCircle(DrawingContext context, Rect rect, IBrush brush)
    {
        context.DrawEllipse(brush, null, rect.Center, rect.Width / 2, rect.Height / 2);
    }

    private static void DrawHollowCircle(DrawingContext context, Rect rect, IBrush brush, double strokeThickness)
    {
        var inset = strokeThickness / 2;
        var circleRect = rect.Deflate(inset);
        if (circleRect.Width <= 0 || circleRect.Height <= 0)
            return;

        context.DrawEllipse(null, new Pen(brush, strokeThickness), circleRect.Center, circleRect.Width / 2, circleRect.Height / 2);
    }

    private static void DrawFilledTriangle(DrawingContext context, IBrush brush, Point p1, Point p2, Point p3)
    {
        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            gc.BeginFigure(p1, true);
            gc.LineTo(p2);
            gc.LineTo(p3);
            gc.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    private static void DrawCross(DrawingContext context, Rect rect, IBrush brush, double strokeThickness)
    {
        var pen = new Pen(brush, strokeThickness, lineCap: PenLineCap.Round);
        context.DrawLine(pen, rect.TopLeft, rect.BottomRight);
        context.DrawLine(pen, new Point(rect.Right, rect.Top), new Point(rect.Left, rect.Bottom));
    }
}
