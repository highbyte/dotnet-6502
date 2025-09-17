// For possible future use in Windows Forms (via SkiaSharp.Views.Desktop) - not currently used.

//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using Highbyte.DotNet6502.Systems.Rendering;

//namespace Highbyte.DotNet6502.Impl.Skia;

///// Host-driven render loop using SKGLControl (OpenGL-backed).
///// Raises FrameTick on each PaintSurface. Optionally auto-invalidates
///// to keep rendering continuously, with optional target FPS throttling.
///
//public sealed class SkiaOnRenderLoopGLControl : IRenderLoop
//{
//    private readonly SKControl _control;
//    private readonly bool _continuous;
//    private readonly double? _targetFps;
//    private readonly Stopwatch _clock = Stopwatch.StartNew();
//    private readonly CancellationTokenSource _cts = new();
//    private TimeSpan _lastTick;

//    public SkiaOnRenderLoopGLControl(SKGLControl control, bool continuous = true, double? targetFps = null)
//    {
//        _control = control ?? throw new ArgumentNullException(nameof(control));
//        _continuous = continuous;
//        _targetFps = targetFps;

//        _control.PaintSurface += OnPaintSurface;

//        // Kick the first frame if running continuously
//        if (_continuous)
//            RequestRedraw();
//    }

//    public RenderTriggerMode Mode => RenderTriggerMode.HostFrameCallback;

//    public event EventHandler<TimeSpan>? FrameTick;

//    public void RequestRedraw()
//    {
//        if (_control.IsHandleCreated)
//        {
//            // On UI thread if possible; BeginInvoke is safe if called off-thread.
//            if (_control.InvokeRequired) _control.BeginInvoke(new Action(_control.Invalidate));
//            else _control.Invalidate();
//        }
//    }

//    private async void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
//    {
//        var now = _clock.Elapsed;
//        _lastTick = now;

//        FrameTick?.Invoke(this, now);

//        if (!_continuous || _cts.IsCancellationRequested)
//            return;

//        // If a target FPS is specified, delay before scheduling next frame.
//        if (_targetFps is double fps && fps > 0)
//        {
//            var frameDur = TimeSpan.FromSeconds(1.0 / fps);
//            // Measure time spent up to the end of this paint and sleep the difference.
//            var after = _clock.Elapsed;
//            var used = after - now;
//            var remaining = frameDur - used;
//            if (remaining > TimeSpan.Zero)
//            {
//                try { await Task.Delay(remaining, _cts.Token); }
//                catch (TaskCanceledException) { return; }
//            }
//        }

//        RequestRedraw();
//    }

//    public void Dispose()
//    {
//        _cts.Cancel();
//        _control.PaintSurface -= OnPaintSurface;
//        _cts.Dispose();
//    }
//}
