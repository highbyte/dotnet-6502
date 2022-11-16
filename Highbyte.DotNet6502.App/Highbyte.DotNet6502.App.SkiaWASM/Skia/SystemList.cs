using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace Highbyte.DotNet6502.App.SkiaWASM.Skia
{
    public class SystemList
    {
        public HashSet<string> Systems = new();

        public Dictionary<string, SystemUserConfig> SystemUserConfigs = new Dictionary<string, SystemUserConfig>();

        public const string CONFIG_KEY = "CONFIG";
        private readonly BrowserContext _browserContext;

        public SystemList(BrowserContext browserContext)
        {
            _browserContext = browserContext;
            Systems.Add(C64.SystemName);
            Systems.Add(GenericComputer.SystemName);
        }

        public async Task<SystemUserConfig> GetSystemUserConfig(string systemName)
        {
            if (!SystemUserConfigs.ContainsKey(systemName))
            {
                SystemUserConfig systemUserConfig = new SystemUserConfig();
                var userSettings = systemUserConfig.UserSettings;

                switch (systemName)
                {
                    case C64.SystemName:
                        Dictionary<string, byte[]>? roms = null;

//#if DEBUG
//                        // If running locally in Debug mode, try to load C64 ROMs from path "ROM" in current site.
//                        // The wwwroot/ROM directory is not checked in to the GIT repo, so these have to be downloaded beforehand.
//                        // By this you won't have to upload the ROMs manually from local disk to the browser each time you are testing it.
//                        // 
//                        try
//                        {
//                            roms = new Dictionary<string, byte[]>();
//                            userSettings.Add(C64Setup.USER_CONFIG_ROMS, roms);

//                            // Load ROMs from current website
//                            const string BASIC_ROM_URL = "ROM/basic.901226-01.bin";
//                            const string CHARGEN_ROM_URL = "ROM/characters.901225-01.bin";
//                            const string KERNAL_ROM_URL = "ROM/kernal.901227-03.bin";
//                            roms[C64Config.BASIC_ROM_NAME] = await C64Setup.GetROMFromUrl(_browserContext.HttpClient, BASIC_ROM_URL);
//                            roms[C64Config.CHARGEN_ROM_NAME] = await C64Setup.GetROMFromUrl(_browserContext.HttpClient, CHARGEN_ROM_URL);
//                            roms[C64Config.KERNAL_ROM_NAME] = await C64Setup.GetROMFromUrl(_browserContext.HttpClient, KERNAL_ROM_URL);
//                        }
//                        catch (Exception ex)
//                        {
//                            Console.WriteLine($"Couldn't load C64 ROMS directly from current site in Debug mode");
//                        }
//#endif
                        var c64Config = await C64Setup.BuildC64Config(roms);
                        userSettings[CONFIG_KEY] = c64Config;

                        break;

                    case GenericComputer.SystemName:

                        var genericConfig = await GenericComputerSetup.BuildGenericComputerConfig(_browserContext);
                        userSettings[CONFIG_KEY] = genericConfig;
                        break;
                }

                SystemUserConfigs[systemName] = systemUserConfig;
            }

            return SystemUserConfigs[systemName];
        }

        public async Task<(bool isOk, string valError)> IsSystemUserConfigOk(string systemName)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            var systemUserConfig = await GetSystemUserConfig(systemName);

            switch (systemName)
            {
                case C64.SystemName:
                    C64Config c64Config = (C64Config)systemUserConfig.UserSettings[CONFIG_KEY];
                    if (!C64Setup.IsValidC64Config(c64Config, out string validationError))
                        return (false, validationError);
                    break;

                case GenericComputer.SystemName:
                    break;

                default:
                    throw new NotImplementedException();
            }

            return (true, "");
        }

        public async Task<SystemData> GetSystemData(string systemName)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            var systemData = new SystemData();

            var systemUserConfig = await GetSystemUserConfig(systemName);

            switch (systemName)
            {
                case C64.SystemName:
                    var c64Config = (C64Config)systemUserConfig.UserSettings[CONFIG_KEY];
                    systemData.System = C64Setup.BuildC64(c64Config);
                    systemData.Renderer = C64Setup.BuildC64Renderer(c64Config);
                    systemData.InputHandler = C64Setup.BuildC64InputHander(c64Config);
                    break;

                case GenericComputer.SystemName:
                    var genericConfig = (GenericComputerConfig)systemUserConfig.UserSettings[CONFIG_KEY];
                    systemData.System = GenericComputerSetup.BuildGenericComputer(genericConfig);
                    systemData.Renderer = GenericComputerSetup.BuildGenericComputerRenderer(genericConfig);
                    systemData.InputHandler = GenericComputerSetup.BuildGenericComputerInputHander(genericConfig);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return systemData;
        }

        public async Task<SystemRunner> GetSystemRunner(
            ISystem system,
            SkiaRenderContext skiaRenderContext,
            AspNetInputHandlerContext inputHandlerContext)
        {
            var systemData = await GetSystemData(system.Name);

            if (system is C64 c64)
            {
                return C64Setup.BuildSystemRunner(
                    c64,
                    (IRenderer<C64, SkiaRenderContext>)systemData.Renderer!,
                    (IInputHandler<C64, AspNetInputHandlerContext>)systemData.InputHandler!,
                    skiaRenderContext,
                    inputHandlerContext);
            }
            else if (system is GenericComputer genericComputer)
            {
                return GenericComputerSetup.BuildSystemRunner(
                    genericComputer,
                    (IRenderer<GenericComputer, SkiaRenderContext>)systemData.Renderer!,
                    (IInputHandler<GenericComputer, AspNetInputHandlerContext>)systemData.InputHandler!,
                    skiaRenderContext,
                    inputHandlerContext);
            }
            throw new NotImplementedException();
        }
    }
}
