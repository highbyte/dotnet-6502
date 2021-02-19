using System;
using System.Diagnostics;
using System.IO;
using SadConsole;
using Microsoft.Xna.Framework;
using Console = SadConsole.Console;
using Highbyte.DotNet6502;

namespace SadConsoleTest
{
    /// <summary>
    /// Example code running Highbyte.DotNet6502 emulator together with SadConsole Game library.
    /// 
    /// Source code for 6502 program (scroller and color cycler):
    /// - AssemblerSource/hostinteraction_scroll_text_and_cycle_colors.asm
    /// - Compiled with ACME cross-assembler (using VS Code extension vs64)
    /// </summary>
    public static class Program
    {
        // SadConsole screen setup
        private const string WindowTitle = "SadConsole screen updated from program running in Highbyte.DotNet6502 emulator";
        private const int Width = 80;
        private const int Height = 25;
        private const int BorderWidth = 2;
        private const int BorderHeight = 2;
        private static int FontScale = 2;
        private static Microsoft.Xna.Framework.Color DefaultFgColor = Microsoft.Xna.Framework.Color.Black;
        private static Microsoft.Xna.Framework.Color DefaultBgColor = Microsoft.Xna.Framework.Color.White;
        private static SadConsoleScreen SadConsoleScreen;

        // 6502 program binary to load into emulator
        const string prgFileName = "../../.cache/Examples/SadConsoleTest/AssemblerSource/hostinteraction_scroll_text_and_cycle_colors.prg";

        // Emulator <-> SadConsole interaction
        private static int UpdateEmulatorEveryXFrame = 1;
        private static SadConsoleEmulatorLoop SadConsoleEmulatorLoop;
        private static SadConsoleEmulatorRenderer SadConsoleEmulatorRenderer;

        static void Main()
        {
            // Init emulator
            var computer = SetupEmulator(prgFileName);
            var emulatorScreenConfig = SetupEmulatorScreenConfig();
            InitEmulatorScreenMemory(emulatorScreenConfig, computer.Mem);

            // Create SadConsole renderer for the screen memory contents in the emulator
            SadConsoleEmulatorRenderer = new SadConsoleEmulatorRenderer(
                GetSadConsoleScreen, 
                computer.Mem, 
                emulatorScreenConfig
            );

            // Create the main game loop class that invokes emulator and render to host screen
            SadConsoleEmulatorLoop = new SadConsoleEmulatorLoop(
                SadConsoleEmulatorRenderer, 
                computer,
                emulatorScreenConfig,
                updateEmulatorEveryXFrame: UpdateEmulatorEveryXFrame
            );

            // Setup the SadConsole engine and create the main window.
            SadConsole.Game.Create(Width * FontScale, Height * FontScale);

            // Hook the start event so we can add consoles to the system.
            SadConsole.Game.OnInitialize = InitSadConsole;

            // Hook the update event that happens each frame
            SadConsole.Game.OnUpdate = UpdateSadConsole;

            // Hook the "after render"
            //SadConsole.Game.OnDraw = Screen.DrawFrame;
            
            // Start the game.
            SadConsole.Game.Instance.Run();
            SadConsole.Game.Instance.Dispose();
        }

        private static void UpdateSadConsole(GameTime gameTime)
        {
            SadConsoleEmulatorLoop.SadConsoleUpdate(gameTime);
        }

        private static void InitSadConsole()
        {
            // TODO: Better way to map numeric scale value to SadConsole.Font.FontSizes enum?
            SadConsole.Font.FontSizes fontSize;
            switch(FontScale)
            {
                case 1:
                    fontSize = SadConsole.Font.FontSizes.One;
                    break;
                case 2:
                    fontSize = SadConsole.Font.FontSizes.Two;
                    break;
                case 3:
                    fontSize = SadConsole.Font.FontSizes.Three;
                    break;               
                default:
                    fontSize = SadConsole.Font.FontSizes.One;
                    break;
            }
            SadConsole.Global.FontDefault = SadConsole.Global.FontDefault.Master.GetFont(fontSize);

            // Set a custom ContainerConsole as the current screen, and it'll contain the actual consoles 
            SadConsoleScreen = new SadConsoleScreen(
                Width, 
                Height, 
                BorderHeight, 
                BorderWidth,
                DefaultFgColor,
                DefaultBgColor
                );
            SadConsole.Global.CurrentScreen = SadConsoleScreen;

            // Start with focus on screen console
            SadConsole.Global.FocusedConsoles.Set(SadConsoleScreen.ScreenConsole);

            SadConsole.Game.Instance.Window.Title = WindowTitle;
        }

        private static SadConsoleScreen GetSadConsoleScreen()
        {
            return SadConsoleScreen;
        }

        private static EmulatorScreenConfig SetupEmulatorScreenConfig()
        {
            return new EmulatorScreenConfig
            {
                // 6502 code running in emulator should have the same  #rows & #cols as we setup in SadConsole
                Cols = Width,   
                Rows = Height,

                // 6502 code must use these addresses as screen memory
                ScreenStartAddress = 0x0400,
                ScreenColorStartAddress = 0xd800,

                ScreenRefreshStatusAddress = 0xd000,
                //ScreenBorderColorAddress = 0xd020 // TODO: Border colors
                ScreenBackgroundColorAddress = 0xd021,

                DefaultBgColor = 0x00,  // 0x00 = Black, 0x06 = Blue, 0x0b = Dark grey
                DefaultFgColor = 0x0f   // 0x01 = White, 0x0e = Light blue, 0x0f = Light grey
            };
        }

        private static void InitEmulatorScreenMemory(EmulatorScreenConfig emulatorScreenConfig, Memory mem)
        {
            // One common bg color for entire screen, controlled by specific address
            mem[emulatorScreenConfig.ScreenBackgroundColorAddress] = emulatorScreenConfig.DefaultBgColor;

            ushort currentScreenAddress = emulatorScreenConfig.ScreenStartAddress;
            ushort currentColorAddress = emulatorScreenConfig.ScreenColorStartAddress;
            for (int row = 0; row < emulatorScreenConfig.Rows; row++)
            {
                for (int col = 0; col < emulatorScreenConfig.Cols; col++)
                {
                    mem[currentScreenAddress++] = 0x20;
                    mem[currentColorAddress++] = emulatorScreenConfig.DefaultFgColor;
                }
            }            
        }

        private static Computer SetupEmulator(string prgFileName)
        {
            Debug.WriteLine($"Loading 6502 machine code binary file.");
            Debug.WriteLine($"{prgFileName}");
            if(!File.Exists(prgFileName))
            {
                Debug.WriteLine($"File does not exist.");
                throw new Exception($"Cannot find 6502 binary file: {prgFileName}");
            }

            var mem = BinaryLoader.Load(
                prgFileName, 
                out ushort loadedAtAddress, 
                out int fileLength);

            // Initialize emulator with CPU, memory, and execution parameters
            var computerBuilder = new ComputerBuilder();
            computerBuilder
                .WithCPU()
                .WithStartAddress(loadedAtAddress)
                .WithMemory(mem)
                // .WithInstructionExecutedEventHandler( 
                //     (s, e) => Debug.WriteLine(OutputGen.GetLastInstructionDisassembly(e.CPU, e.Mem)))
                .WithExecOptions(options =>
                {
                    options.ExecuteUntilInstruction = OpCodeId.BRK; // Emulator will stop executing when a BRK instruction is reached.
                });
            var computer = computerBuilder.Build();

            return computer;
        }
    }
}
