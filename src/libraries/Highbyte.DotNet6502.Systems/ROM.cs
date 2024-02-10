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

    public static List<ROM> Clone(List<ROM> roms)
    {
        var romsCloned = new List<ROM>();
        foreach (var rom in roms)
            romsCloned.Add(rom.Clone());
        return romsCloned;
    }

    public ROM Clone()
    {
        return new ROM
        {
            Name = Name,
            File = File,
            Data = Data,
            Checksum = Checksum,
        };
    }

    public void Validate(string romDirectory)
    {
        if (!Validate(out List<string> validationErrors, romDirectory))
            throw new DotNet6502Exception($"Invalid ROM. Errors: {string.Join(',', validationErrors)}");
    }

    public bool Validate(out List<string> validationErrors, string romDirectory)
    {
        validationErrors = new List<string>();
        if (string.IsNullOrEmpty(Name))
            validationErrors.Add($"ROM {nameof(Name)} must be set.");
        if (string.IsNullOrEmpty(File) && (Data == null || Data.Length == 0))
            validationErrors.Add($"ROM {nameof(File)} and {nameof(Data)} must be set.");
        if (!string.IsNullOrEmpty(File) && (Data != null && Data.Length > 0))
            validationErrors.Add($"ROM {nameof(File)} and {nameof(Data)} cannot be set.");

        if (!string.IsNullOrEmpty(File))
        {
            var romFilePath = GetROMFilePath(romDirectory);
            if (!System.IO.File.Exists(romFilePath))
                validationErrors.Add($"ROM file does not exist: {romFilePath}");
        }
        return validationErrors.Count == 0;
    }

    public static Dictionary<string, byte[]> LoadROMS(string directory, ROM[] roms)
    {
        var romsData = new Dictionary<string, byte[]>();
        foreach (var rom in roms)
        {

            byte[] fileData;
            if (!string.IsNullOrEmpty(rom.File))
            {
                var romFilePath = rom.GetROMFilePath(directory);
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

    public string GetROMFilePath(string romDirectory)
    {
        if (File == null)
            throw new DotNet6502Exception($"Cannot get ROM file path if rom File is empty.");
        string romFilePath;
        if (!string.IsNullOrEmpty(romDirectory))
            romFilePath = Path.Combine(romDirectory, File);
        else
            romFilePath = File;
        return PathHelper.ExpandOSEnvironmentVariables(romFilePath);
    }
}
