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

        public async Task SetSelectedSystem(string systemName, HttpClient httpClient, Uri uri)
        {
            if (!Systems.Contains(systemName))
                throw new NotImplementedException($"System not implemented: {systemName}");

            switch (systemName)
            {
                case C64.SystemName:
                    var c64Config = await C64Setup.BuildC64Config(httpClient, uri);
                    SelectedSystem = C64Setup.BuildC64(c64Config);
                    SelectedRenderer = C64Setup.BuildC64Renderer(c64Config);
                    SelectedInputHandler = C64Setup.BuildC64InputHander(c64Config);
                    break;

                case GenericComputer.SystemName:
                    var genericConfig = await GenericComputerSetup.BuildGenericComputerConfig(httpClient, uri);
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
