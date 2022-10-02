using System;
using System.Diagnostics;
using System.IO;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Generic;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.SadConsoleHost;
using Highbyte.DotNet6502.SadConsoleHost.Commodore64;
using Highbyte.DotNet6502.SadConsoleHost.Generic;

namespace Highbyte.DotNet6502.SadConsoleHost
{
    public class EmulatorHost
    {
        private readonly SadConsoleConfig _sadConsoleConfig;
        private readonly GenericComputerConfig _genericComputerConfig;
        private static SadConsoleMain SadConsoleMain;

        public EmulatorHost(
            SadConsoleConfig sadConsoleConfig,
            GenericComputerConfig genericComputerConfig)
        {
            _sadConsoleConfig = sadConsoleConfig;
            _genericComputerConfig = genericComputerConfig;
        }

        public void Start()
        {

            SystemRunner systemRunner;
            int runEveryFrame;
            switch (_sadConsoleConfig.Emulator)
            {
                case "GenericComputer":
                    // Init emulator: Generic computer
                    var genericComputer = GenericComputerBuilder.SetupGenericComputerFromConfig(_genericComputerConfig);
                    systemRunner = GenericSadConsoleSystemRunnerBuilder.BuildSystemRunner(
                        genericComputer,
                        GetSadConsoleScreen,
                        _genericComputerConfig.Memory.Screen,
                        _genericComputerConfig.Memory.Input
                        );
                    runEveryFrame = _genericComputerConfig.RunEmulatorEveryFrame;
                    break;

                case "C64":
                    // Init emulator: C64
                    systemRunner = C64SadConsoleSystemRunnerBuilder.BuildSystemRunner(
                        GetSadConsoleScreen
                        );
                    runEveryFrame = 1;
                    break;
                default:
                    throw new Exception($"Unknown emulator name: {_sadConsoleConfig.Emulator}");
            }

            if (systemRunner.System is not ITextMode)
                throw new Exception("SadConsole host only supports running emulator systems that supports text mode.");

            // Create the main SadConsole class that is responsible for configuring and starting up SadConsole and running the emulator code every frame with our preferred configuration.
            SadConsoleMain = new SadConsoleMain(
                _sadConsoleConfig,
                systemRunner,
                runEveryFrame);

            // Start SadConsole. Will exit from this method after SadConsole window is closed.
            SadConsoleMain.Run();
        }

        private SadConsoleScreenObject GetSadConsoleScreen()
        {
            return SadConsoleMain.SadConsoleScreen;
        }


    }
}