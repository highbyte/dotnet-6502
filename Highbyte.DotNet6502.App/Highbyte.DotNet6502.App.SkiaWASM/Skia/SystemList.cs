using System.Net.Http;
using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public class SystemList
    {
        public HashSet<string> Systems = new();
        public ISystem? SelectedSystem { get; private set; }
        public IRenderer? SelectedRenderer { get; private set; }
        public IInputHandler? SelectedInputHandler { get; private set; }

        public SystemList()
        {
            Systems.Add(C64.SystemName);
            Systems.Add(GenericComputer.SystemName);
        }

        public async Task<(bool isOk, string valError)> IsSystemConfigOk(string systemName, SystemUserConfig systemUserConfig, BrowserContext browserContext)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            switch (systemName)
            {
                case C64.SystemName:

                    var userSettings = systemUserConfig.UserSettings;
#if DEBUG
                    // If running locally in Debug mode, try to load C64 ROMs from path "ROM" in current site.
                    // The wwwroot/ROM directory is not checked in to the GIT repo, so these have to be downloaded beforehand.
                    // By this you won't have to upload the ROMs manually from local disk to the browser each time you are testing it.
                    // 
                    try
                    {
                        Dictionary<string, byte[]> roms;
                        if (!userSettings.ContainsKey(C64Setup.USER_CONFIG_ROMS))
                        {
                            userSettings.Add(C64Setup.USER_CONFIG_ROMS, new Dictionary<string, byte[]>());
                            roms = (Dictionary<string, byte[]>)userSettings[C64Setup.USER_CONFIG_ROMS];

                            // Load ROMs from current website
                            const string BASIC_ROM_URL = "ROM/basic.901226-01.bin";
                            const string CHARGEN_ROM_URL = "ROM/characters.901225-01.bin";
                            const string KERNAL_ROM_URL = "ROM/kernal.901227-03.bin";
                            roms[C64Config.BASIC_ROM_NAME] = await C64Setup.GetROMFromUrl(browserContext.HttpClient, BASIC_ROM_URL);
                            roms[C64Config.CHARGEN_ROM_NAME] = await C64Setup.GetROMFromUrl(browserContext.HttpClient, CHARGEN_ROM_URL);
                            roms[C64Config.KERNAL_ROM_NAME] = await C64Setup.GetROMFromUrl(browserContext.HttpClient, KERNAL_ROM_URL);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Couldn't load C64 ROMS directly from current site in Debug mode");
                    }
#endif

                    if (!C64Setup.IsValidSystemUserConfig(systemUserConfig, out string validationError))
                        return (false, validationError);
                    break;

                case GenericComputer.SystemName:
                    break;
                default:
                    throw new NotImplementedException();
            }

            return (true, "");
        }

        public async Task SetSelectedSystem(string systemName, SystemUserConfig systemUserConfig, BrowserContext browserContext)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            switch (systemName)
            {
                case C64.SystemName:
                    var c64Config = await C64Setup.BuildC64Config(systemUserConfig, browserContext);
                    SelectedSystem = C64Setup.BuildC64(c64Config);
                    SelectedRenderer = C64Setup.BuildC64Renderer(c64Config);
                    SelectedInputHandler = C64Setup.BuildC64InputHander(c64Config);
                    break;

                case GenericComputer.SystemName:
                    var genericConfig = await GenericComputerSetup.BuildGenericComputerConfig(systemUserConfig, browserContext);
                    SelectedSystem = GenericComputerSetup.BuildGenericComputer(genericConfig);
                    SelectedRenderer = GenericComputerSetup.BuildGenericComputerRenderer(genericConfig);
                    SelectedInputHandler = GenericComputerSetup.BuildGenericComputerInputHander(genericConfig);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public SystemRunner GetSystemRunner(ISystem system, SkiaRenderContext skiaRenderContext, AspNetInputHandlerContext inputHandlerContext)
        {
            if (system is C64 c64)
            {
                return C64Setup.BuildSystemRunner(
                    c64,
                    (IRenderer<C64, SkiaRenderContext>)SelectedRenderer!,
                    (IInputHandler<C64, AspNetInputHandlerContext>)SelectedInputHandler!,
                    skiaRenderContext,
                    inputHandlerContext);
            }
            else if (system is GenericComputer genericComputer)
            {
                return GenericComputerSetup.BuildSystemRunner(
                    genericComputer,
                    (IRenderer<GenericComputer, SkiaRenderContext>)SelectedRenderer!,
                    (IInputHandler<GenericComputer, AspNetInputHandlerContext>)SelectedInputHandler!,
                    skiaRenderContext,
                    inputHandlerContext);
            }
            throw new NotImplementedException();
        }
    }
}
