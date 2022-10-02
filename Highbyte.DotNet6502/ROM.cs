using System.Collections.Generic;
using System.IO;

namespace Highbyte.DotNet6502
{
    public class ROM
    {
        public string Name { get; set; }
        public string File { get; set; }
        public string Checksum { get; set; }

        private ROM() { }

        public static ROM NewROM(string name, string file, string checksum)
        {
            return new ROM
            {
                Name = name.Trim(),
                File = file.Trim(),
                Checksum = checksum
            };
        }

        public static Dictionary<string, byte[]> LoadROMS(string directory, ROM[] roms)
        {
            var romsData = new Dictionary<string, byte[]>();
            foreach (var rom in roms)
            {
                var romFilePath = Path.Combine(directory, rom.File);
                var fileData = System.IO.File.ReadAllBytes(romFilePath);
                // TODO: Verify checksum
                romsData.Add(rom.Name, fileData);
            }
            return romsData;
        }
    }
}