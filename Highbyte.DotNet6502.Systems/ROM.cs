namespace Highbyte.DotNet6502.Systems;

public class ROM
{
    /// <summary>
    /// Name of ROM
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Optional. File name of the ROM. If this is set, the ROM contents is read from the file instead of used from the Data property.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Optional. Binary data of the ROM. If this is set, the File property is ignored.
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Checksum of ROM.
    /// </summary>
    public string? Checksum { get; set; }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Name))
            throw new Exception($"Settings {nameof(Name)} must be set.");
        if (string.IsNullOrEmpty(File) && (Data == null || Data.Length == 0))
            throw new Exception($"One of settings {nameof(File)} and {nameof(Data)} must be set.");
        if (!string.IsNullOrEmpty(File) && (Data != null && Data.Length > 0))
            throw new Exception($"Both settings {nameof(File)} and {nameof(Data)} cannot be set.");
    }

    public static Dictionary<string, byte[]> LoadROMS(string directory, ROM[] roms)
    {
        var romsData = new Dictionary<string, byte[]>();
        foreach (var rom in roms)
        {

            byte[] fileData;
            if (!string.IsNullOrEmpty(rom.File))
            {
                var romFilePath = Path.Combine(directory, rom.File);
                romFilePath = PathHelper.ExpandOSEnvironmentVariables(romFilePath);
                fileData = System.IO.File.ReadAllBytes(romFilePath);
            }
            else
            {
                fileData = rom.Data!;
            }
            // TODO: Verify checksum
            romsData.Add(rom.Name, fileData);
        }
        return romsData;
    }

}
