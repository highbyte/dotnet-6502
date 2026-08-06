namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage;

/// <summary>
/// A parsed DOS 3.3 disk image: the catalog plus the raw sector data, so file contents can be
/// read on demand. Produced by <see cref="DskParser.ParseDskFile(byte[])"/>.
/// </summary>
public class DskDiskImage
{
    public int Volume { get; init; }

    /// <summary>Catalog entries, in catalog order (deleted/unused entries excluded).</summary>
    public List<DskFileEntry> Files { get; init; } = new();

    /// <summary>The raw disk image bytes (DOS sector order).</summary>
    public byte[] RawDiskData { get; init; } = Array.Empty<byte>();

    public bool FileExists(string fileName)
        => Files.Any(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Name of the first file that can be loaded into RAM and run: the first Binary (B) file,
    /// or, when the disk has none, the first Applesoft (A) file. Null when neither exists.
    /// </summary>
    public string? GetFirstRunnableFileName()
    {
        var binary = Files.FirstOrDefault(f => f.FileType == DskFileType.Binary);
        if (binary != null)
            return binary.FileName;
        return Files.FirstOrDefault(f => f.FileType == DskFileType.ApplesoftBasic)?.FileName;
    }

    /// <summary>
    /// Reads a file's raw content: its data sectors concatenated, including any DOS file-type
    /// header (A: 2-byte length, B: 4-byte load address + length) and trailing sector padding.
    /// </summary>
    /// <exception cref="FileNotFoundException">No such file in the catalog.</exception>
    public byte[] ReadFileContent(string fileName)
    {
        var entry = Files.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"File not found in disk catalog: {fileName}");

        var content = new List<byte>();
        var listTrack = entry.TrackSectorListTrack;
        var listSector = entry.TrackSectorListSector;

        // Follow the chain of track/sector list sectors; each lists up to 122 data sectors.
        var visitedLists = 0;
        while (listTrack != 0 || listSector != 0)
        {
            if (++visitedLists > DskParser.MaxTrackSectorListsPerFile)
                throw new InvalidDataException($"Track/sector list chain of '{fileName}' does not terminate.");

            var listOffset = DskParser.SectorOffset(listTrack, listSector);
            var pairsEnd = false;
            for (var pair = 0; pair < DskParser.TrackSectorPairsPerList && !pairsEnd; pair++)
            {
                var dataTrack = RawDiskData[listOffset + 0x0C + (pair * 2)];
                var dataSector = RawDiskData[listOffset + 0x0C + (pair * 2) + 1];
                if (dataTrack == 0 && dataSector == 0)
                {
                    pairsEnd = true;
                    continue;
                }
                var dataOffset = DskParser.SectorOffset(dataTrack, dataSector);
                for (var i = 0; i < DskParser.BytesPerSector; i++)
                    content.Add(RawDiskData[dataOffset + i]);
            }

            listTrack = RawDiskData[listOffset + 0x01];
            listSector = RawDiskData[listOffset + 0x02];
        }

        return content.ToArray();
    }

    /// <summary>
    /// Reads an Applesoft (A) file as bare tokenized bytes: the 2-byte length header is used to
    /// trim sector padding and then stripped — the layout the BASIC loader expects.
    /// </summary>
    public byte[] ReadApplesoftFile(string fileName)
    {
        var raw = ReadFileContent(fileName);
        if (raw.Length < 3)
            throw new InvalidDataException($"'{fileName}' is too short to be an Applesoft file.");

        var length = raw[0] | (raw[1] << 8);
        if (length > raw.Length - 2)
            throw new InvalidDataException($"'{fileName}' has an invalid Applesoft length header.");

        return raw[2..(2 + length)];
    }

    /// <summary>
    /// Reads a Binary (B) file trimmed to its header's length, keeping the 4-byte header
    /// (load address + length) — the DOS 3.3 "B" layout the binary loader expects.
    /// </summary>
    public byte[] ReadBinaryFile(string fileName)
    {
        var raw = ReadFileContent(fileName);
        if (raw.Length < 5)
            throw new InvalidDataException($"'{fileName}' is too short to be a DOS 3.3 binary file.");

        var length = raw[2] | (raw[3] << 8);
        if (length == 0 || length > raw.Length - 4)
            throw new InvalidDataException($"'{fileName}' has an invalid binary length header.");

        return raw[..(4 + length)];
    }
}
