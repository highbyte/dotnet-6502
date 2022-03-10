using System.Reflection;
using BlazorWasmSkiaTest.Helpers;
using BlazorWasmSkiaTest.Skia;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace BlazorWasmSkiaTest.Pages
{
    public partial class Index
    {

        private const string DEFAULT_PRG_URL = "6502binaries/hostinteraction_scroll_text_and_cycle_colors.prg";
        private const int TextSize = 20;

        private EmulatorHelper? _emulatorHelper;
        private EmulatorRenderer? _emulatorRenderer;

        [Inject]
        public HttpClient? HttpClient { get; set; }

        [Inject]
        public NavigationManager? NavManager { get; set; }

        protected async override void OnInitialized()
        {
            var uri = NavManager!.ToAbsoluteUri(NavManager.Uri);
            // Load 6502 program binary
            var prgBytes = await Load6502Binary(uri);

            // Init 6502 emulator
            (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) = GetScreenSize(uri);

            _emulatorHelper = new EmulatorHelper(cols, rows, screenMemoryAddress, colorMemoryAddress);
            _emulatorHelper.InitDotNet6502Computer(prgBytes);

            // Init emulator renderer (Skia)
            var timer = new PeriodicAsyncTimer();
            //SKTypeface typeFace = await LoadFont("../fonts/C64_Pro_Mono-STYLE.woff2");
            SKTypeface typeFace = LoadEmbeddedFont("C64_Pro_Mono-STYLE.ttf");
            var skColorMaps = new SKPaintMaps(TextSize, typeFace, ColorMaps.C64ColorMap);
            _emulatorRenderer = new EmulatorRenderer(timer, skColorMaps, TextSize, _emulatorHelper);
        }

        private (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) GetScreenSize(Uri uri)
        {
            int? cols = null;
            int? rows = null;
            ushort? screenMemoryAddress = null;
            ushort? colorMemoryAddress = null;

            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("cols", out var colsParameter))
            {
                if (int.TryParse(colsParameter, out int colsParsed))
                    cols = colsParsed;
                else
                    cols = null;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("rows", out var rowsParameter))
            {
                if (int.TryParse(rowsParameter, out int rowsParsed))
                    rows = rowsParsed;
                else
                    rows = null;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("screenMem", out var screenMemParameter))
            {
                if (ushort.TryParse(screenMemParameter, out ushort screenMemParsed))
                    screenMemoryAddress = screenMemParsed;
                else
                    screenMemoryAddress = null;
            }
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("colorMem", out var colorMemParameter))
            {
                if (ushort.TryParse(colorMemParameter, out ushort colorMemParsed))
                    colorMemoryAddress = colorMemParsed;
                else
                    colorMemoryAddress = null;
            }

            return (cols, rows, screenMemoryAddress, colorMemoryAddress);

        }

        private async Task<byte[]> Load6502Binary(Uri uri)
        {
            byte[] prgBytes;
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("prgEnc", out var prgEnc))
            {
                // Query parameter prgEnc must be a valid Base64Url encoded string.
                // Examples on how to generate it from a compiled 6502 binary file:
                //      Linux: 
                //          base64 -w0 myprogram.prg | sed 's/+/-/g; s/\//_/g'

                //      https://www.base64encode.org/   
                //          - Encode files to Base64 format
                //          - Select file
                //          - Select options: BINARY, Perform URL-safe encoding (uses Base64Url format)
                //          - ENCODE
                //          - CLICK OR TAP HERE to download the encoded file
                //          - Use the contents in the generated file.
                //
                // Examples to generate a QR Code that will launch the program in the Base64URL string above:
                //      Linux:
                //          qrencode -s 3 -l L -o "myprogram.png" "http://localhost:5000/?prgEnc=THE_PROGRAM_ENCODED_AS_BASE64URL"
                prgBytes = Base64UrlDecode(prgEnc.ToString());
            }
            else if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("prgUrl", out var prgUrl))
            {
                prgBytes = await HttpClient!.GetByteArrayAsync(prgUrl.ToString());
            }
            else
            {
                prgBytes = await HttpClient!.GetByteArrayAsync(DEFAULT_PRG_URL);
            }

            return prgBytes;
        }

        /// <summary>
        /// Decode a Base64Url encoded string.
        /// The Base64Url standard is a bit different from normal Base64
        /// - Replaces '+' with '-'
        /// - Replaces '/' with '_'
        /// - Removes trailing '=' padding
        /// 
        /// This method does the above in reverse before decoding it as a normal Base64 string.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private byte[] Base64UrlDecode(string arg)
        {
            string s = arg;
            s = s.Replace('-', '+'); // 62nd char of encoding
            s = s.Replace('_', '/'); // 63rd char of encoding
            switch (s.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: s += "=="; break; // Two pad chars
                case 3: s += "="; break; // One pad char
                default:
                    throw new ArgumentException("Illegal base64url string!");
            }
            return Convert.FromBase64String(s); // Standard base64 decoder
        }

        private async Task<SKTypeface> LoadFont(string fontUrl)
        {
            using (Stream file = await HttpClient!.GetStreamAsync(fontUrl))
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

        private SKTypeface LoadEmbeddedFont(string fullFontName)
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
                // If we draw our own images (not directly on the canvas provided), make sure it's within the same context
                _emulatorRenderer.SetContext(context);
            }
            _emulatorRenderer.Render(e.Surface.Canvas);
        }
    }
}
