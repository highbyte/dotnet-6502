namespace Highbyte.DotNet6502.Systems.Commodore64.Config
{
    public class C64Config
    {
        public const string ConfigSectionName = "Highbyte.DotNet6502.C64";

        public string ROMDirectory { get; set; }
        public string Vic2Variant { get; set; }

        public C64Config()
        {
            // Defaults
            ROMDirectory = "%USERPROFILE%/Documents/C64/VICE/C64";
            Vic2Variant = "NTSC";
        }

        public void Validate()
        {
            if (!C64Variants.Vic2Variants.ContainsKey(Vic2Variant))
                throw new Exception($"Setting {nameof(Vic2Variant)} value {Vic2Variant} is not supported. Valid values are: {string.Join(',', C64Variants.Vic2Variants.Keys)}");

            var romDir = Environment.ExpandEnvironmentVariables(ROMDirectory);
            if (!Directory.Exists(romDir))
                throw new Exception($"Setting {nameof(ROMDirectory)} value {romDir} does not contain an existing directory.");
        }
    }
}
