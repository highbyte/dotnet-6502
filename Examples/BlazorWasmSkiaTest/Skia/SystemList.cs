using Highbyte.DotNet6502.Impl.AspNet.Generic;
using Highbyte.DotNet6502.Impl.Skia.Commodore64;
using Highbyte.DotNet6502.Impl.Skia.Generic;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;

namespace BlazorWasmSkiaTest.Skia
{
    public class SystemList
    {
        /// <summary>
        /// Systems that are available for running with a native Silk.Net & Skia host.
        /// </summary>
        public static HashSet<string> SystemNames = new();

        static SystemList()
        {
            SystemNames.Add(C64.SystemName);
            SystemNames.Add(GenericComputer.SystemName);
        }

        public Dictionary<string, ISystem> Systems = new();
        public Dictionary<ISystem, IRenderer> Renderers = new();
        public Dictionary<ISystem, IInputHandler> InputHandlers = new();

        public void BuildSystemLookups(
            C64Config? c64Config = null,
            GenericComputerConfig? genericComputerConfig = null)
        {
            if (c64Config != null)
            {
                var c64 = C64.BuildC64(c64Config);
                Systems.Add(c64.Name, c64);
                Renderers.Add(c64, new C64SkiaRenderer());
                InputHandlers.Add(c64, new NullInputHandler<C64>());
            }

            if (genericComputerConfig != null)
            {
                var genericComputer = GenericComputerBuilder.SetupGenericComputerFromConfig(genericComputerConfig);
                Systems.Add(genericComputer.Name, genericComputer);
                Renderers.Add(genericComputer, new GenericComputerSkiaRenderer(genericComputerConfig.Memory.Screen));
                InputHandlers.Add(genericComputer, new GenericComputerAspNetInputHandler(genericComputerConfig.Memory.Input));
            }
        }
    }
}
