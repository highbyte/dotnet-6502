using Highbyte.DotNet6502.Systems.Commodore64.Models;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config
{
    public class C64Config
    {
        public const string ConfigSectionName = "Highbyte.DotNet6502.C64";

        public const string KERNAL_ROM_NAME = "kernal";
        public const string BASIC_ROM_NAME = "basic";
        public const string CHARGEN_ROM_NAME = "chargen";
        public static List<string> RequiredROMs = new()
        {
            BASIC_ROM_NAME, CHARGEN_ROM_NAME, KERNAL_ROM_NAME
        };

        public List<ROM> ROMs { get; set; }
        public string ROMDirectory { get; set; }

        public string C64Model { get; set; }
        public string Vic2Model { get; set; }

        public C64Config()
        {
            // Defaults
            ROMs = new List<ROM>
            {
                new ROM
                {
                    Name = BASIC_ROM_NAME,
                    File = "basic",
                    Data = null,
                    Checksum = "79015323128650c742a3694c9429aa91f355905e",
                },
                new ROM
                {
                    Name = CHARGEN_ROM_NAME,
                    File = "chargen",
                    Data = null,
                    Checksum = "adc7c31e18c7c7413d54802ef2f4193da14711aa",
                },
                new ROM
                {
                    Name = KERNAL_ROM_NAME,
                    File = "kernal",
                    Data = null,
                    Checksum = "1d503e56df85a62fee696e7618dc5b4e781df1bb",
                },
            };
            ROMDirectory = "%USERPROFILE%/Documents/C64/VICE/C64";

            C64Model = "C64NTSC";
            Vic2Model = "NTSC";
        }

        public void Validate()
        {
            if (!C64ModelInventory.C64Models.ContainsKey(C64Model))
                throw new Exception($"Setting {nameof(C64Model)} value {C64Model} is not supported. Valid values are: {string.Join(',', C64ModelInventory.C64Models.Keys)}");
            var c64Model = C64ModelInventory.C64Models[C64Model];

            if (!c64Model.Vic2Models.Exists(x => x.Name == Vic2Model))
                throw new Exception($"Setting {nameof(Vic2Model)} value {Vic2Model} is not supported for the specified C64Variant. Valid values are: {string.Join(',', c64Model.Vic2Models.Select( x => x.Name))}");

            var allRequiredROMSconfigured = RequiredROMs.Intersect(ROMs.Select(x => x.Name)).Count() == RequiredROMs.Count();
            if (!allRequiredROMSconfigured)
                throw new Exception($"Setting {nameof(ROMs)} must contain at least all required ROMs: {string.Join(',', RequiredROMs)}");

            var romDir = PathHelper.ExpandOSEnvironmentVariables(ROMDirectory);
            if (!string.IsNullOrEmpty(romDir))
            {
                if (!Directory.Exists(romDir))
                    throw new Exception($"Setting {nameof(ROMDirectory)} value {romDir} does not contain an existing directory.");
            }
            foreach (var rom in ROMs)
            {
                rom.Validate();
            }
        }
    }
}
