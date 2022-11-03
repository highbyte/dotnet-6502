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

        public const string USER_CONFIG_KERNAL_ROM = "KERNAL";
        public const string USER_CONFIG_BASIC_ROM = "BASIC";
        public const string USER_CONFIG_CHARGEN_ROM = "CHARGEN";

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

        public static bool IsValidSystemUserConfig(SystemUserConfig systemUserConfig)
        {
            var userSettings = systemUserConfig.UserSettings;
            if (!userSettings.ContainsKey(USER_CONFIG_ROMS))
                return false;

            var roms = (Dictionary<string, byte[]>)userSettings[USER_CONFIG_ROMS];

            bool validKernal =
                (
                roms.ContainsKey(USER_CONFIG_KERNAL_ROM)
                && roms[USER_CONFIG_KERNAL_ROM].Length > 0
                );
            bool validBasic =
                (
                roms.ContainsKey(USER_CONFIG_BASIC_ROM)
                && roms[USER_CONFIG_BASIC_ROM].Length > 0
                );
            bool validChargen =
                (
                roms.ContainsKey(USER_CONFIG_CHARGEN_ROM)
                && roms[USER_CONFIG_CHARGEN_ROM].Length > 0
                );

            return validKernal && validBasic && validChargen;
        }

        public static async Task<C64Config> BuildC64Config(SystemUserConfig systemUserConfig)
        {
            var httpClient = systemUserConfig.HttpClient;
            var uri = systemUserConfig.Uri;


            byte[] basicROMData;
            byte[] chargenROMData;
            byte[] kernalROMData;

            var userSettings = systemUserConfig.UserSettings;
            if (userSettings.ContainsKey(USER_CONFIG_KERNAL_ROM))
            {
                // ROMs uploaded to client by user
                basicROMData = (byte[])userSettings[USER_CONFIG_BASIC_ROM];
                chargenROMData = (byte[])userSettings[USER_CONFIG_CHARGEN_ROM];
                kernalROMData = (byte[])userSettings[USER_CONFIG_KERNAL_ROM];
            }
            else
            {
                // Load ROMs from website
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
                        Name = "basic",
                        Data = basicROMData,
                        //Checksum = ""
                    },
                    new ROM
                    {
                        Name = "chargen",
                        Data = chargenROMData,
                        //Checksum = ""
                    },
                    new ROM
                    {
                        Name = "kernal",
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
