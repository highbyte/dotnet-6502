using System.IO;
using Highbyte.DotNet6502.SadConsoleHost;
using Microsoft.Extensions.Configuration;

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
        private static IConfiguration Configuration;

        static void Main()
        {
            // Get config options
            var builder = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json");
            Configuration = builder.Build();

            var emulatorHostOptions = new Options();
            Configuration.GetSection(Options.ConfigSectionName).Bind(emulatorHostOptions);

            // Alternative way, build config via code instead of reading from appsettings.json
            //var emulatorHostOptions = ConfigViaCode();    

            // Init EmulatorHost and run!
            var emulatorHost = new EmulatorHost(emulatorHostOptions);
            emulatorHost.Start();
        }

    //     private static Options ConfigViaCode()
    //     {
    //         // Define how emulator memory should be layed out recarding screen output and keyboard input
    //         var emulatorMemoryConfig = new EmulatorMemoryConfig
    //         {
    //             Screen = new EmulatorScreenConfig
    //             {
    //                 // 6502 code running in emulator should have the same #rows & #cols as we setup in SadConsole
    //                 Cols        = 80,   
    //                 Rows        = 25,

    //                 // If borders should be used. Currently only updateable with a color setting (see ScreenBackgroundColorAddress below)
    //                 BorderCols  = 4,
    //                 BorderRows  = 2,

    //                 // 6502 code must use these addresses as screen memory
    //                 ScreenStartAddress              = 0x0400,   //80*25 = 2000(0x07d0) -> range 0x0400 - 0x0bcf
    //                 ScreenColorStartAddress         = 0xd800,   //80*25 = 2000(0x07d0) -> range 0xd800 - 0xdfcf
    //                 ScreenRefreshStatusAddress      = 0xd000,
    //                 ScreenBorderColorAddress        = 0xd020,
    //                 ScreenBackgroundColorAddress    = 0xd021,
    //                 DefaultBgColor                  = 0x00,     // 0x00 = Black
    //                 DefaultFgColor                  = 0x0f,     // 0x0f = Light grey
    //                 DefaultBorderColor              = 0x0b,     // 0x0b = Dark grey
    //             },

    //             Input = new EmulatorInputConfig
    //             {
    //                 KeyPressedAddress = 0xe000
    //             }
    //         };

    //         var emulatorConfig = new EmulatorConfig
    //         {
    //             ProgramBinaryFile = "../../.cache/Examples/SadConsoleTest/AssemblerSource/hostinteraction_scroll_text_and_cycle_colors.prg",
    //             Memory = emulatorMemoryConfig
    //         };

    //         // Configure overall SadConsole settings
    //         var sadConsoleConfig = new SadConsoleConfig
    //         {
    //             WindowTitle = "SadConsole screen updated from program running in Highbyte.DotNet6502 emulator",
    //             FontScale   = 2
    //         };

    //         var emulatorHostOptions = new Options 
    //         {
    //             SadConsoleConfig = sadConsoleConfig,
    //             EmulatorConfig = emulatorConfig
    //         };

    //         return emulatorHostOptions;
    //     }
    }
}