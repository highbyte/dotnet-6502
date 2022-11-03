using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Generic;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Generic;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Microsoft.AspNetCore.WebUtilities;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public static class GenericComputerSetup
    {
        private const string DEFAULT_PRG_URL = "6502binaries/hostinteraction_scroll_text_and_cycle_colors.prg";
        //private const string DEFAULT_PRG_URL = "6502binaries/snake6502.prg";

        public static GenericComputer BuildGenericComputer(GenericComputerConfig genericComputerConfig)
        {
            return GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig);
        }
        public static IRenderer<GenericComputer, SkiaRenderContext> BuildGenericComputerRenderer(GenericComputerConfig genericComputerConfig)
        {
            var renderer = new GenericComputerSkiaRenderer(genericComputerConfig.Memory.Screen);
            return renderer;
        }
        public static IInputHandler<GenericComputer, AspNetInputHandlerContext> BuildGenericComputerInputHander(GenericComputerConfig genericComputerConfig)
        {
            var inputHandler = new GenericComputerAspNetInputHandler(genericComputerConfig.Memory.Input);
            return inputHandler;
        }

        public static SystemRunner BuildSystemRunner(
            GenericComputer genericComputer,
            IRenderer<GenericComputer, SkiaRenderContext> renderer,
            IInputHandler<GenericComputer, AspNetInputHandlerContext> inputHandler,
            SkiaRenderContext skiaRenderContext,
            AspNetInputHandlerContext inputHandlerContext)
        {
            renderer.Init(genericComputer, skiaRenderContext);
            inputHandler.Init(genericComputer, inputHandlerContext);

            var systemRunnerBuilder = new SystemRunnerBuilder<GenericComputer, SkiaRenderContext, AspNetInputHandlerContext>(genericComputer);
            var systemRunner = systemRunnerBuilder
                .WithRenderer(renderer)
                .WithInputHandler(inputHandler)
                .Build();
            return systemRunner;
        }
        public static async Task<GenericComputerConfig> BuildGenericComputerConfig(SystemUserConfig systemUserConfig)
        {
            var httpClient = systemUserConfig.HttpClient;
            var uri = systemUserConfig.Uri;

            // Load 6502 program binary specified in url
            var prgBytes = await Load6502Binary(httpClient, uri);

            // Get screen size specified in url
            (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) = GetScreenSize(uri);

            cols = cols ?? 40;
            rows = rows ?? 25;
            screenMemoryAddress = screenMemoryAddress ?? 0x0400;
            colorMemoryAddress = colorMemoryAddress ?? 0xd800;

            var genericComputerConfig = new GenericComputerConfig
            {
                ProgramBinary = prgBytes,

                CPUCyclesPerFrame = 2500,
                ScreenRefreshFrequencyHz = 60,

                Memory = new EmulatorMemoryConfig
                {
                    Screen = new EmulatorScreenConfig
                    {
                        Cols = cols.Value,
                        Rows = rows.Value,
                        BorderCols = 3,
                        BorderRows = 3,
                        ScreenStartAddress = screenMemoryAddress.Value,
                        ScreenColorStartAddress = colorMemoryAddress.Value,

                        UseAscIICharacters = true,
                        DefaultBgColor = 0x00,     // 0x00 = Black (C64 scheme)
                        DefaultFgColor = 0x01,     // 0x0f = Light grey, 0x0e = Light Blue, 0x01 = White  (C64 scheme)
                        DefaultBorderColor = 0x0b, // 0x0b = Dark grey (C64 scheme)
                    },
                    Input = new EmulatorInputConfig
                    {
                        KeyPressedAddress = 0xd030,
                        KeyDownAddress = 0xd031,
                        KeyReleasedAddress = 0xd031,
                    }
                }
            };
            genericComputerConfig.Validate();

            return genericComputerConfig;
        }

        private static (int? cols, int? rows, ushort? screenMemoryAddress, ushort? colorMemoryAddress) GetScreenSize(Uri uri)
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

        private static async Task<byte[]> Load6502Binary(HttpClient httpClient, Uri uri)
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
                prgBytes = await httpClient.GetByteArrayAsync(prgUrl.ToString());
            }
            else
            {
                prgBytes = await httpClient.GetByteArrayAsync(DEFAULT_PRG_URL);
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
        private static byte[] Base64UrlDecode(string arg)
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

    }
}
