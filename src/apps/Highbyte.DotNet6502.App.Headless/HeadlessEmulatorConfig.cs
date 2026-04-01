namespace Highbyte.DotNet6502.App.Headless;

public class HeadlessEmulatorConfig
{
    public const string ConfigSectionName = "Highbyte.DotNet6502.HeadlessConfig";

    public string DefaultEmulator { get; set; } = "C64";
}
