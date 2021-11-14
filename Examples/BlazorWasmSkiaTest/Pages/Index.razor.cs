using System.Reflection;
using BlazorWasmSkiaTest.Skia;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace BlazorWasmSkiaTest.Pages
{
    public partial class Index
    {
        private EmulatorRenderer? _emulatorRenderer;

        [Inject]
        protected HttpClient _httpClient { get; set; }

        protected async override void OnInitialized()
        {

            //SKTypeface typeFace = await LoadFont("../fonts/C64_Pro_Mono-STYLE.woff2");
            SKTypeface typeFace = await LoadEmbeddedFont("C64_Pro_Mono-STYLE.ttf");

            var timer = new PeriodicAsyncTimer();
            _emulatorRenderer = new EmulatorRenderer(timer, typeFace);
        }

        private async Task<SKTypeface> LoadFont(string fontUrl)
        {
            using (Stream file = await _httpClient.GetStreamAsync(fontUrl))
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                //byte[] bytes = memoryStream.ToArray();
                var typeFace = SKTypeface.FromStream(memoryStream);
                if (typeFace == null)
                    throw new ArgumentException($"Cannot load font as a Skia TypeFace. Url: {fontUrl}", nameof(fontUrl));
                return typeFace;
            }
        }

        private async Task<SKTypeface> LoadEmbeddedFont(string fullFontName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var resourceName = $"{"BlazorWasmSkiaTest.Resources.Fonts"}.{fullFontName}";
            using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    throw new ArgumentException($"Cannot load font from embedded resource. Resource: {resourceName}", nameof(fullFontName));

                var typeFace = SKTypeface.FromStream(resourceStream);
                if (typeFace == null)
                    throw new ArgumentException($"Cannot load font as a Skia TypeFace from embedded resource. Resource: {resourceName}", nameof(fullFontName));
                return typeFace;
            }
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
