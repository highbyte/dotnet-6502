using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.AspNet.Generic;
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

        public static bool IsValidSystemUserConfig(SystemUserConfig systemUserConfig, out string validationError)
        {
            validationError = "";

            var userSettings = systemUserConfig.UserSettings;
            if (!userSettings.ContainsKey(USER_CONFIG_ROMS))
            {
                validationError = "Missing C64 ROMs. Press Config to upload.";
                return false;
            }

            var loadedRoms = (Dictionary<string, byte[]>)userSettings[USER_CONFIG_ROMS];
            List<string> missingRoms = new();
            foreach (var romName in C64Config.RequiredROMs)
            {
                if (!loadedRoms.ContainsKey(romName))
                    missingRoms.Add(romName);
            }

            if (missingRoms.Count > 0)
            {
                validationError = $"Missing ROMs: {string.Join(',', missingRoms)}";
                return false;
            }
            return true;
        }

        public static async Task<C64Config> BuildC64Config(SystemUserConfig systemUserConfig)
        {
            var httpClient = systemUserConfig.HttpClient;
            var uri = systemUserConfig.Uri;


            byte[] basicROMData;
            byte[] chargenROMData;
            byte[] kernalROMData;

            var userSettings = systemUserConfig.UserSettings;
            if (userSettings.ContainsKey(USER_CONFIG_ROMS))
            {
                var roms = (Dictionary<string, byte[]>)userSettings[USER_CONFIG_ROMS];
                // ROMs uploaded to client by user
                basicROMData = (byte[])roms[C64Config.BASIC_ROM_NAME];
                chargenROMData = (byte[])roms[C64Config.CHARGEN_ROM_NAME];
                kernalROMData = (byte[])roms[C64Config.KERNAL_ROM_NAME];
            }
            else
            {
                // Load ROMs from current website
                const string BASIC_ROM_URL = "ROM/basic.901226-01.bin";
                const string CHARGEN_ROM_URL = "ROM/characters.901225-01.bin";
                const string KERNAL_ROM_URL = "ROM/kernal.901227-03.bin";
                basicROMData = await GetROMFromUrl(httpClient, BASIC_ROM_URL);
                chargenROMData = await GetROMFromUrl(httpClient, CHARGEN_ROM_URL);
                kernalROMData = await GetROMFromUrl(httpClient, KERNAL_ROM_URL);
            }

            var c64Config = new C64Config
            {
                C64Model = "C64NTSC",   // C64NTSC, C64PAL
                Vic2Model = "NTSC",     // NTSC, NTSC_old, PAL
                // C64Model = "C64PAL",   // C64NTSC, C64PAL
                // Vic2Model = "PAL",     // NTSC, NTSC_old, PAL

                ROMDirectory = "",  // Set ROMDirectory to skip loading ROMs from file system (ROMDirectory + File property), instead read from the Data property
                ROMs = new List<ROM>
                {
                    new ROM
                    {
                        Name = C64Config.BASIC_ROM_NAME,
                        Data = basicROMData,
                        //Checksum = ""
                    },
                    new ROM
                    {
                        Name = C64Config.CHARGEN_ROM_NAME,
                        Data = chargenROMData,
                        //Checksum = ""
                    },
                    new ROM
                    {
                        Name = C64Config.KERNAL_ROM_NAME,
                        Data = kernalROMData,
                        //Checksum = ""
                    }
                }
            };
            c64Config.Validate();

            return c64Config;
        }

        private static async Task<byte[]> GetROMFromUrl(HttpClient httpClient, string url)
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
