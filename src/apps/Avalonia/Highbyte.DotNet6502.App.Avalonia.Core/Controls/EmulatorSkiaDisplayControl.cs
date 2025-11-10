//using Avalonia;
//using Avalonia.Media;
//using Avalonia.Platform;
//using Avalonia.Rendering.SceneGraph;
//using Avalonia.Skia;
//using Highbyte.DotNet6502.Systems.Rendering;

//namespace Highbyte.DotNet6502.App.Avalonia.Core.Controls;

///// <summary>
///// </summary>
//public class EmulatorSkiaDisplayControl : EmulatorDisplayControlBase
//{
//    private readonly IRenderCoordinator? _renderCoordinator;

//    static EmulatorSkiaDisplayControl()
//    {
//        //AffectsRender<EmulatorSkiaDisplayControl>(ScaleProperty, RendererProperty);
//        AffectsRender<EmulatorSkiaDisplayControl>(ScaleProperty);
//    }

//    public EmulatorSkiaDisplayControl(
//        IRenderCoordinator? renderCoordinator,
//        double scale,
//        bool focuable
//        ) : base()
//    {
//        _renderCoordinator = renderCoordinator;
//        Scale = scale;
//        Focusable = focuable;
//    }

//    public override async void Render(DrawingContext context)
//    {
//        if (_renderCoordinator == null) return;
//        await _renderCoordinator.FlushIfDirtyAsync();

//        // Use custom SkiaSharp drawing operation
//        var operation = new SkiaDrawOperation(this, Scale);
//        context.Custom(operation);
//    }

//    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
//    {
//        base.OnPropertyChanged(change);


//    }

//    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
//    {
//        base.OnAttachedToVisualTree(e);

//        // Set up callback for when new frames are ready
//        //Renderer?.SetNewFrameHasBeenDrawnCallback(RefreshDisplay);
//    }

//    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
//    {
//        base.OnDetachedFromVisualTree(e);
//    }

//    // Custom drawing operation for SkiaSharp rendering
//    private class SkiaDrawOperation : ICustomDrawOperation
//    {
//        //private readonly IAvaloniaDrawFrameRenderer _emulatorRenderer;
//        private readonly double _scale;

//        public SkiaDrawOperation(EmulatorSkiaDisplayControl control, double scale)
//        {
//            _scale = scale;
//            Bounds = new Rect(0, 0, control.DisplayWidth * scale, control.DisplayHeight * scale);
//        }

//        public Rect Bounds { get; }

//        public void Dispose() { }

//        public bool Equals(ICustomDrawOperation? other) => false;

//        public bool HitTest(Point p) => Bounds.Contains(p);

//        public void Render(ImmediateDrawingContext context)
//        {
//            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
//            if (leaseFeature == null) return;

//            using var lease = leaseFeature.Lease();
//            var canvas = lease.SkCanvas;

//            // Scale the canvas
//            canvas.Scale((float)_scale);

//            // Draw the generated bitmaps on to the canvas
//            // TODO
//            // var skiaRenderContext = new SkiaRenderContext(() => canvas, () => lease.GrContext);
//            // _emulatorRenderer.DrawFrame(skiaRenderContext);
//        }
//    }
//}
