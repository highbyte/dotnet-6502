using Highbyte.DotNet6502.Impl.AspNet;
using Highbyte.DotNet6502.Impl.Skia;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
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

        public (bool isOk, string valError) IsSystemConfigOk(string systemName, SystemUserConfig systemUserConfig)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            switch (systemName)
            {
                case C64.SystemName:
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

        public async Task SetSelectedSystem(string systemName, SystemUserConfig systemUserConfig)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            switch (systemName)
            {
                case C64.SystemName:
                    var c64Config = await C64Setup.BuildC64Config(systemUserConfig);
                    SelectedSystem = C64Setup.BuildC64(c64Config);
                    SelectedRenderer = C64Setup.BuildC64Renderer(c64Config);
                    SelectedInputHandler = C64Setup.BuildC64InputHander(c64Config);
                    break;

                case GenericComputer.SystemName:
                    var genericConfig = await GenericComputerSetup.BuildGenericComputerConfig(systemUserConfig);
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
