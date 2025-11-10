namespace Highbyte.DotNet6502.Systems.Generic.Config;

public static class GenericComputerExampleConfigs
{
    private static readonly Dictionary<string, GenericComputerConfig> s_exampleConfigs = new();


    public static GenericComputerConfig GetExampleConfig(string exampleName, GenericComputerSystemConfig systemConfig)
    {
        var exampleConfig = GetExampleConfigClone(exampleName);
        if (!systemConfig.ExamplePrograms.ContainsKey(exampleName))
            throw new ArgumentException($"No example program with name '{exampleName}' exists in system config.");
        exampleConfig.ProgramBinaryFile = systemConfig.ExamplePrograms[exampleName];
        exampleConfig.RenderProviderType = systemConfig.RenderProviderType;
        return exampleConfig;
    }

    public static GenericComputerConfig GetExampleConfig(string exampleName, GenericComputerSystemConfig systemConfig, byte[] prgBytes)
    {
        var exampleConfig = GetExampleConfigClone(exampleName);
        exampleConfig.ProgramBinary = prgBytes;
        exampleConfig.RenderProviderType = systemConfig.RenderProviderType;
        return exampleConfig;
    }


    private static GenericComputerConfig GetExampleConfigClone(string exampleName)
    {
        if (!s_exampleConfigs.ContainsKey(exampleName))
            throw new ArgumentException($"No example config with name '{exampleName}' exists.");

        return (GenericComputerConfig)s_exampleConfigs[exampleName].Clone();
    }

    static GenericComputerExampleConfigs()
    {
        s_exampleConfigs.Add("None", new GenericComputerConfig
        {
            CPUCyclesPerFrame = 8000,
            ScreenRefreshFrequencyHz = 60,
            WaitForHostToAcknowledgeFrame = false,

            Memory = new EmulatorMemoryConfig
            {
                Screen = new EmulatorScreenConfig
                {
                    Cols = 40,
                    Rows = 25,
                    BorderCols = 3,
                    BorderRows = 3,
                    ScreenStartAddress = 0x0400,
                    ScreenColorStartAddress = 0xd800,

                    UseAscIICharacters = true,
                    DefaultBgColor = 0x06,     // 0x06 = Blue
                    DefaultFgColor = 0x0e,     // 0x0e = Light blue
                    DefaultBorderColor = 0x0b, // 0x06 = Blue
                },
                Input = new EmulatorInputConfig
                {
                    KeyPressedAddress = 0xd030,
                    KeyDownAddress = 0xd031,
                    KeyReleasedAddress = 0xd032,
                }
            }
        });

        s_exampleConfigs.Add("Scroll", new GenericComputerConfig
        {
            CPUCyclesPerFrame = 8000,
            ScreenRefreshFrequencyHz = 60,
            WaitForHostToAcknowledgeFrame = true,

            Memory = new EmulatorMemoryConfig
            {
                Screen = new EmulatorScreenConfig
                {
                    Cols = 40,
                    Rows = 25,
                    BorderCols = 3,
                    BorderRows = 3,
                    ScreenStartAddress = 0x0400,
                    ScreenColorStartAddress = 0xd800,

                    UseAscIICharacters = true,
                    DefaultBgColor = 0x06,     // 0x06 = Blue
                    DefaultFgColor = 0x0e,     // 0x0e = Light blue
                    DefaultBorderColor = 0x0b, // 0x06 = Blue
                },
                Input = new EmulatorInputConfig
                {
                    KeyPressedAddress = 0xd030,
                    KeyDownAddress = 0xd031,
                    KeyReleasedAddress = 0xd032,
                }
            }
        });


        s_exampleConfigs.Add("Snake", new GenericComputerConfig
        {
            ScreenRefreshFrequencyHz = 60,
            StopAtBRK = false,
            WaitForHostToAcknowledgeFrame = true,

            Memory = new EmulatorMemoryConfig
            {
                Screen = new EmulatorScreenConfig
                {
                    Cols = 32,
                    Rows = 32,
                    BorderCols = 3,
                    BorderRows = 3,
                    ScreenStartAddress = 0x0200,
                    ScreenColorStartAddress = 0xd800,   // Not used with this program

                    ScreenRefreshStatusAddress = 0xd000, // The 6502 code should set bit 1 here when it's done for current frame

                    DefaultBgColor = 0x00,     // 0x00 = Black
                    DefaultFgColor = 0x01,     // 0x01 = White
                    DefaultBorderColor = 0x0b, // 0x0b = Dark grey

                    UseAscIICharacters = false,
                    CharacterMap = new Dictionary<string, byte>
                            {
                                { "10", 32 },
                                { "13", 32 },
                                { "160", 219 },
                                { "224", 219 },
                            }
                },
                Input = new EmulatorInputConfig
                {
                    KeyPressedAddress = 0xd030,
                    KeyDownAddress = 0xd031,
                    KeyReleasedAddress = 0xd032,
                }
            }
        });

        s_exampleConfigs.Add("HelloWorld", new GenericComputerConfig
        {
            ScreenRefreshFrequencyHz = 60,
            StopAtBRK = false,
            WaitForHostToAcknowledgeFrame = true,

            Memory = new EmulatorMemoryConfig
            {
                Screen = new EmulatorScreenConfig
                {
                    Cols = 80,
                    Rows = 25,
                    BorderCols = 6,
                    BorderRows = 3,
                    ScreenStartAddress = 0x0400,
                    ScreenColorStartAddress = 0xd800,
                    ScreenBorderColorAddress = 0xd020,
                    ScreenBackgroundColorAddress = 0xd021,

                    ScreenRefreshStatusAddress = 0xd000, // The 6502 code should set bit 1 here when it's done for current frame

                    DefaultBgColor = 0x00,     // 0x00 = Black
                    DefaultFgColor = 0x01,     // 0x01 = White
                    DefaultBorderColor = 0x0b, // 0x0b = Dark grey

                    UseAscIICharacters = true,
                },
                Input = new EmulatorInputConfig
                {
                    KeyPressedAddress = 0xd030,
                    KeyDownAddress = 0xd031,
                    KeyReleasedAddress = 0xd032,
                }
            }
        });

    }
}

