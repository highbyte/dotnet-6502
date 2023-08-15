// Host app for running Highbyte.DotNet6502 emulator in a SadConsole Window
// 
// Generic 6502 example programs
// - Source (.asm)in: Examples/Assembler/Generic
// - Compiled with ACME cross-assembler: to Examples/Assembler/Generic/Build 
//
// C64 example programs
// - Source (.asm)in: Examples/Assembler/C64
// - Compiled with ACME cross-assembler: to Examples/Assembler/C64/Build 

using Highbyte.DotNet6502.Impl.SadConsole;
using Highbyte.DotNet6502.Systems.Generic.Config;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Microsoft.Extensions.Configuration;

IConfiguration Configuration;

// Get config options
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    // appsettings_scroll.json
    // appsettings_hello.json
    // appsettings_snake.json 
    // appsettings_c64.json
    .AddJsonFile("appsettings_c64.json");

Configuration = builder.Build();

var sadConsoleConfig = new SadConsoleConfig();
Configuration.GetSection(SadConsoleConfig.ConfigSectionName).Bind(sadConsoleConfig);

var genericComputerConfig = new GenericComputerConfig();
Configuration.GetSection(GenericComputerConfig.ConfigSectionName).Bind(genericComputerConfig);

var c64Config = new C64Config();
Configuration.GetSection(C64Config.ConfigSectionName).Bind(c64Config);

// Alternative way, build config via code instead of reading from appsettings.json
//var sadConsoleConfig = ConfigViaCode();

// Init EmulatorHost and run!
var emulatorHost = new EmulatorHost(
    sadConsoleConfig,
    genericComputerConfig,
    c64Config
    );
emulatorHost.Start();

//SadConsoleConfig ConfigViaCode()
//{
//    // Define how emulator memory should be layed out recarding screen output and keyboard input
//    var emulatorMemoryConfig = new EmulatorMemoryConfig
//    {
//        Screen = new EmulatorScreenConfig
//        {
//            // 6502 code running in emulator should have the same #rows & #cols as we setup in SadConsole
//            Cols = 80,
//            Rows = 25,

//            // If borders should be used. Currently only updateable with a color setting (see ScreenBackgroundColorAddress below)
//            BorderCols = 4,
//            BorderRows = 2,

//            // 6502 code must use these addresses as screen memory
//            ScreenStartAddress = 0x0400,   //80*25 = 2000(0x07d0) -> range 0x0400 - 0x0bcf
//            ScreenColorStartAddress = 0xd800,   //80*25 = 2000(0x07d0) -> range 0xd800 - 0xdfcf
//            ScreenRefreshStatusAddress = 0xd000,
//            ScreenBorderColorAddress = 0xd020,
//            ScreenBackgroundColorAddress = 0xd021,
//            DefaultBgColor = 0x00,     // 0x00 = Black
//            DefaultFgColor = 0x0f,     // 0x0f = Light grey
//            DefaultBorderColor = 0x0b,     // 0x0b = Dark grey
//        },

//        Input = new EmulatorInputConfig
//        {
//            KeyPressedAddress = 0xe000
//        }
//    };

//    var emulatorConfig = new EmulatorConfig
//    {
//        ProgramBinaryFile = "../../../../../../samples/Assembler/Generic/Build/hostinteraction_scroll_text_and_cycle_colors.prg",
//        Memory = emulatorMemoryConfig
//    };

//    // Configure overall SadConsole settings
//    var sadConsoleConfig = new SadConsoleConfig
//    {
//        WindowTitle = "SadConsole screen updated from program running in Highbyte.DotNet6502 emulator",
//        FontScale = 2
//    };

//    var emulatorHostOptions = new Options
//    {
//        SadConsoleConfig = sadConsoleConfig,
//        EmulatorConfig = emulatorConfig
//    };

//    return emulatorHostOptions;
//}
