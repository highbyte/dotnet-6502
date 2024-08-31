using Highbyte.DotNet6502.Utils;

namespace Highbyte.DotNet6502.Systems;

public class ROM
{
    /// <summary>
    /// Name of ROM
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Optional. File name of the ROM. If this is set, the ROM contents is read from the file instead of used from the Data property.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Optional. Binary data of the ROM. If this is set, the File property is ignored.
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Valid SHA1 checksum dictionary for ROM (version descriptor, SHA 1checksum)
    /// </summary>
    public Dictionary<string, string> ValidVersionChecksums { get; set; } = new();

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
            ValidVersionChecksums = new Dictionary<string, string>(ValidVersionChecksums),
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
        if (!string.IsNullOrEmpty(File) && (Data != null && Data.Length > 0))
        {
            validationErrors.Add($"ROM {nameof(File)} and {nameof(Data)} cannot both be set.");
        }
        if (string.IsNullOrEmpty(File) && (Data == null || Data.Length == 0))
        {
            validationErrors.Add($"ROM {nameof(File)} or {nameof(Data)} must be set.");
        }

        bool fileExists = false;
        if (!string.IsNullOrEmpty(File))
        {
            var romFilePath = GetROMFilePath(romDirectory);
            if (!System.IO.File.Exists(romFilePath))
            {
                validationErrors.Add($"ROM file does not exist: {romFilePath}");
            }
            else
            {
                fileExists = true;
            }
        }

        var romData = GetRomData(romFileAssumedToExist: fileExists, romDirectory);
        if (romData != null)
        {
            var checksum = GetSHAChecksum(romData);
            if (!ValidVersionChecksums.Values.Contains(checksum))
            {
                validationErrors.Add($"{Name} ROM checksum error. Expected ver(s): {string.Join(',', ValidVersionChecksums.Keys)}");
                //validationErrors.Add($"{Name} ROM checksum error. Expected one of: {string.Join(',', ValidVersionChecksums.Values)}, Actual: {checksum}");
            }
        }

        return validationErrors.Count == 0;
    }

    public static Dictionary<string, byte[]> LoadROMS(string directory, ROM[] roms)
    {
        var romsData = new Dictionary<string, byte[]>();
        foreach (var rom in roms)
        {
            var romData = rom.GetRomData(romFileAssumedToExist: true, directory);
            romsData.Add(rom.Name, romData!);
        }
        return romsData;
    }

    private byte[]? GetRomData(bool romFileAssumedToExist, string? directory = null)
    {
        byte[]? romData;
        if (!string.IsNullOrEmpty(File))
        {
            if (romFileAssumedToExist)
            {
                var romFilePath = GetROMFilePath(directory);
                romData = System.IO.File.ReadAllBytes(romFilePath);
            }
            else
            {
                romData = null;
            }
        }
        else
        {
            romData = Data;
        }
        return romData;
    }

    public string GetROMFilePath(string? romDirectory)
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

    private string GetSHAChecksum(byte[] data)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
