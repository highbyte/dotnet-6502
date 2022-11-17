using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Commodore64;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public static class C64Setup
    {
        public const string USER_CONFIG_ROMS = "ROMS";

        public static C64 BuildC64(C64Config c64Config)
        {
            return C64.BuildC64(c64Config);
        }
        public static IRenderer<C64, SkiaRenderContext> BuildC64Renderer(C64Config c64Config)
        {
            var renderer = new C64SkiaRenderer();
            return renderer;
        }
        public static IInputHandler<C64, AspNetInputHandlerContext> BuildC64InputHander(C64Config c64Config)
        {
            var inputHandler = new C64AspNetInputHandler();
            return inputHandler;
        }

        public static SystemRunner BuildSystemRunner(
            C64 c64,
            IRenderer<C64, SkiaRenderContext> renderer,
            IInputHandler<C64, AspNetInputHandlerContext> inputHandler,
            SkiaRenderContext skiaRenderContext,
            AspNetInputHandlerContext inputHandlerContext)
        {
            renderer.Init(c64, skiaRenderContext);
            inputHandler.Init(c64, inputHandlerContext);

            var systemRunnerBuilder = new SystemRunnerBuilder<C64, SkiaRenderContext, AspNetInputHandlerContext>(c64);
            var systemRunner = systemRunnerBuilder
                .WithRenderer(renderer)
                .WithInputHandler(inputHandler)
                .Build();
            return systemRunner;
        }

        public static bool IsValidC64Config(C64Config c64Config, out string validationError)
        {
            validationError = "";

            var loadedRoms = c64Config.ROMs.Select(x => x.Name).ToList();
            List<string> missingRoms = new();
            foreach (var romName in C64Config.RequiredROMs)
            {
                if (!loadedRoms.Contains(romName))
                    missingRoms.Add(romName);
            }

            if (missingRoms.Count > 0)
            {
                validationError = $"Missing ROMs: {string.Join(", ", missingRoms)}.";
                validationError += " Press C64 Config to load ROMs";
                return false;
            }
            return true;
        }

        public static async Task<C64Config> BuildC64Config(Dictionary<string, byte[]>? roms)
        {
            var romList = new List<ROM>();
            if (roms != null)
            {
                if (roms.ContainsKey(C64Config.BASIC_ROM_NAME))
                {
                    romList.Add(new ROM
                    {
                        Name = C64Config.BASIC_ROM_NAME,
                        Data = roms[C64Config.BASIC_ROM_NAME],
                        //Checksum = ""
                    });
                }
                if (roms.ContainsKey(C64Config.CHARGEN_ROM_NAME))
                {
                    romList.Add(new ROM
                    {
                        Name = C64Config.CHARGEN_ROM_NAME,
                        Data = roms[C64Config.CHARGEN_ROM_NAME],
                        //Checksum = ""
                    });
                }
                if (roms.ContainsKey(C64Config.KERNAL_ROM_NAME))
                {
                    romList.Add(new ROM
                    {
                        Name = C64Config.KERNAL_ROM_NAME,
                        Data = roms[C64Config.KERNAL_ROM_NAME],
                        //Checksum = ""
                    });
                }
            }

            var c64Config = new C64Config
            {
                C64Model = "C64NTSC",   // C64NTSC, C64PAL
                Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
                // C64Model = "C64PAL",   // C64NTSC, C64PAL
                // Vic2Model = "PAL",     // NTSC, NTSC_old, PAL

                ROMDirectory = "",  // Set ROMDirectory to skip loading ROMs from file system (ROMDirectory + File property), instead read from the Data property
                ROMs = romList,
            };

            //c64Config.Validate();

            return c64Config;
        }

        public static async Task<byte[]> GetROMFromUrl(HttpClient httpClient, string url)
        {
            return await httpClient.GetByteArrayAsync(url);

            //var request = new HttpRequestMessage(HttpMethod.Get, url);
            ////request.SetBrowserRequestMode(BrowserRequestMode.NoCors);
            ////request.SetBrowserRequestCache(BrowserRequestCache.NoStore); //optional  

            ////var response = await HttpClient!.SendAsync(request);

            //var statusCode = response.StatusCode;
            //response.EnsureSuccessStatusCode();
            //byte[] responseRawData = await response.Content.ReadAsByteArrayAsync();
            //return responseRawData;
        }
    }
}
