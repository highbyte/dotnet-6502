using BlazorWasmSkiaTest.Skia;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace BlazorWasmSkiaTest.Pages
{
    public partial class Index
    {
        private EmulatorRenderer? _emulatorRenderer;

        protected override void OnInitialized()
        {
            var timer = new PeriodicAsyncTimer();
            _emulatorRenderer = new EmulatorRenderer(timer);
        }

        protected void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
        {
            _emulatorRenderer!.SetSize(e.Info.Width, e.Info.Height);
            if (e.Surface.Context is GRContext context && context != null)
            {
                // Set the context so all rendering happens in the same place
                _emulatorRenderer.SetContext(context);
            }
            _emulatorRenderer.Render(e.Surface.Canvas);
        }

        //private void BeforeUnload_BeforeUnloadHandler(object? sender, blazejewicz.Blazor.BeforeUnload.BeforeUnloadArgs e)
        //{
        //    _emulatorRenderer.Dispose();
        //}

        //public void Dispose()
        //{
        //    this.BeforeUnload.BeforeUnloadHandler -= BeforeUnload_BeforeUnloadHandler;
        //}
    }
}
