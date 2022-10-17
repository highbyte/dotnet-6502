using Highbyte.DotNet6502.Systems.Commodore64.Models;

namespace Highbyte.DotNet6502.Systems.Commodore64.Config
{
    public class C64Config
    {
        public const string ConfigSectionName = "Highbyte.DotNet6502.C64";

        public string ROMDirectory { get; set; }

        public string C64Model { get; set; }

        public string Vic2Model { get; set; }

        public C64Config()
        {
            // Defaults
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

            var romDir = Environment.ExpandEnvironmentVariables(ROMDirectory);
            if (!Directory.Exists(romDir))
                throw new Exception($"Setting {nameof(ROMDirectory)} value {romDir} does not contain an existing directory.");
        }
    }
}
