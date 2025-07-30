using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;

/// <summary>
/// Represents a D64 disk image with its directory information
/// </summary>
public class D64DiskImage
{
    public string DiskName { get; set; } = string.Empty;
    public byte[] DiskNamePetscii { get; set; } = Array.Empty<byte>();
    public string DiskId { get; set; } = string.Empty;
    public byte[] DiskIdPetscii { get; set; } = Array.Empty<byte>();
    public List<D64FileEntry> Files { get; set; } = new List<D64FileEntry>();
    public byte[] BamSector { get; set; } = Array.Empty<byte>();
    public byte[] RawDiskData { get; set; } = Array.Empty<byte>();
    public int TotalBlocks => Files.Sum(f => f.Blocks);
    public int FreeBlocks => CalculateFreeBlocks();

    private static readonly Dictionary<D64FileType, byte[]> FileTypePetsciiLookup = new()
    {
        { D64FileType.PRG, ConvertStringToPetscii("prg") },
        { D64FileType.SEQ, ConvertStringToPetscii("seq") },
        { D64FileType.REL, ConvertStringToPetscii("rel") },
        { D64FileType.USR, ConvertStringToPetscii("usr") },
        { D64FileType.CBM, ConvertStringToPetscii("cbm") },
        { D64FileType.DIR, ConvertStringToPetscii("dir") },
        { D64FileType.DEL, ConvertStringToPetscii("del") },
        { D64FileType.Unknown, ConvertStringToPetscii("???") },
    };

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Disk Name: \"{DiskName}\"");
        sb.AppendLine($"Disk ID: \"{DiskId}\"");
        sb.AppendLine();
        sb.AppendLine("Directory Listing:");
        sb.AppendLine(new string('=', 50));

        foreach (var file in Files)
        {
            sb.AppendLine(file.ToString());
        }

        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Total blocks used: {TotalBlocks}");

        return sb.ToString();
    }

    public byte[] DirectoryToPrgFormat()
    {
        var prgData = new List<byte>();

        // PRG load address (0x0801 = 2049, start of BASIC program area)
        prgData.Add(0x01);
        prgData.Add(0x08);

        var diskIdPetscii = DiskIdPetscii[0] != 160 ? DiskIdPetscii : new byte[] { 0x20, 0x20 };
        var dosVersionPetscii = ConvertStringToPetscii("2a");

        // Disk name should be 16 characters long in PETSCII, padded with spaces
        byte[] paddedDiskNamePetscii = new byte[16];
        Array.Fill(paddedDiskNamePetscii, Petscii.CharToPetscii[' ']);
        Array.Copy(DiskNamePetscii, 0, paddedDiskNamePetscii, 0, Math.Min(DiskNamePetscii.Length, 16));

        // Concat disk name and DOS version byte arrays
        byte[] fullDiskNamePetsciiText =
            ConvertStringToPetscii("\"")
            .Concat(paddedDiskNamePetscii)
            .Concat(ConvertStringToPetscii("\" "))
            .Concat(diskIdPetscii)
            .Concat(ConvertStringToPetscii(" "))
            .Concat(dosVersionPetscii)
            .ToArray();

        // Invert Petscii text
        var invertedfullDiskNamePetsciiText = ConvertPetsciiToInverted(fullDiskNamePetsciiText);
        //var invertedfullDiskNamePetsciiText = fullDiskNamePetsciiText;

        AddBasicLine(prgData, 0, invertedfullDiskNamePetsciiText);

        // File entries
        foreach (var file in Files)
        {

            // The Basic line number will be the number of blocks used by the file
            var lineNumber = file.Blocks;
            // Get count on how many base 10 digits is in lineNumber
            var lineNumberDigits = lineNumber == 0 ? 1 : (int)Math.Log10(lineNumber) + 1;
            //int lineNumberDigits = lineNumber == 0 ? 1 : (int)Math.Floor(Math.Log10(Math.Abs(lineNumber)) + 1);

            var paddingCountBeforeFileName = 4 - lineNumberDigits;

            // File name should be in quotes
            byte[] fileNameInQuotes =
                ConvertStringToPetscii("\"")
                .Concat(file.FileNamePetscii)
                .Concat(ConvertStringToPetscii("\""))
                .ToArray();

            var paddingCountAfterFileName = 19 - fileNameInQuotes.Length;

            // File type should be 3 characters long in PETSCII
            var fileTypePetscii = FileTypePetsciiLookup[file.FileType];

            // Extra info directly after file type (4 characters)
            var extraFileInfo = ConvertStringToPetscii("    ");
            if (file.FileDoesNotExist)
                extraFileInfo[0] = Petscii.CharToPetscii['<']; // Mark as deleted

            // Format file name 
            byte[] fullFileNamePetsciiText =
                ConvertStringToPetscii(new string(' ', paddingCountBeforeFileName))
                .Concat(fileNameInQuotes)
                .Concat(ConvertStringToPetscii(new string(' ', paddingCountAfterFileName)))
                .Concat(fileTypePetscii)
                .Concat(extraFileInfo)
                .ToArray();


            AddBasicLine(prgData, file.Blocks, fullFileNamePetsciiText);
            //AddBasicLine(prgData, file.Blocks, $" \"{file.FileName.PadRight(13)}\"    {fileTypeStr}");
        }

        // "BLOCKS FREE" line
        var freeBlocks = FreeBlocks;
        AddBasicLine(prgData, freeBlocks, ConvertStringToPetscii("blocks free.             ")); // Note: Seems to be 13 spaces after the text from real 1541.

        // End of program
        prgData.Add(0x00);
        prgData.Add(0x00);

        return prgData.ToArray();
    }

    private static byte[] ConvertStringToPetscii(string text)
    {
        var petsciiBytes = new List<byte>();
        foreach (char c in text)
        {
            if (Petscii.CharToPetscii.TryGetValue(c, out byte petsciiChar))
            {
                petsciiBytes.Add(petsciiChar);
            }
            else
            {
                // If character is not found, use a placeholder (e.g. '?')
                petsciiBytes.Add(Petscii.CharToPetscii['?']);
            }
        }
        return petsciiBytes.ToArray();
    }

    /// <summary>
    /// Adds a BASIC line to the PRG data
    /// </summary>
    private void AddBasicLine(List<byte> prgData, int lineNumber, byte[] petsciiText)
    {
        // Calculate next line address (approximate)
        var nextLineAddr = 0x0801 + prgData.Count + petsciiText.Length + 6;

        // Next line address (little-endian)
        prgData.Add((byte)(nextLineAddr & 0xFF));
        prgData.Add((byte)(nextLineAddr >> 8));

        // Line number (little-endian)
        prgData.Add((byte)(lineNumber & 0xFF));
        prgData.Add((byte)(lineNumber >> 8));

        // Line text (in PETSCII)
        foreach (byte b in petsciiText)
        {
            prgData.Add(b);
        }

        // End of line
        prgData.Add(0x00);
    }

    private byte[] ConvertPetsciiToInverted(byte[] petscII)
    {
        var inversed =
            new byte[] { 0x12 } // Reverse on
            .Concat(petscII)
            //.Concat(new byte[] { 0x92 }) // Reverse off - NOTE: This doesn't seem to be necessary, and will display a text "WAIT" at the end of line (dunno why).
            .ToArray();
        return inversed;
    }

    /// <summary>
    /// Calculate the number of free blocks by reading the BAM (Block Availability Map)
    /// </summary>
    private int CalculateFreeBlocks()
    {
        // Use calculation that matches VICE: 664 total capacity minus used blocks
        // Standard D64 has 664 blocks available for files (excluding directory track)
        return 664 - TotalBlocks;
    }

    public bool FileExists(string fileName)
    {
        return Files.Any(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the name of the first file in the disk image.
    /// Typically returns the first PRG file, falling back to any file type if no PRG files exist.
    /// </summary>
    /// <returns>The filename of the first file, or null if no files exist</returns>
    public string? GetFirstFileName()
    {
        // First try to find a PRG file
        var firstPrgFile = Files.FirstOrDefault(f => f.FileType == D64FileType.PRG && !f.FileDoesNotExist);
        if (firstPrgFile != null)
            return firstPrgFile.FileName;

        // If no PRG files, return the first file of any type
        var firstFile = Files.FirstOrDefault(f => !f.FileDoesNotExist);
        return firstFile?.FileName;
    }

    /// <summary>
    /// Read the byte contents of a specified file.
    /// </summary>
    public byte[] ReadFileContent(string fileName)
    {
        var fileEntry = Files.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"File not found: {fileName}");
        if (RawDiskData == null || RawDiskData.Length == 0)
            throw new InvalidOperationException("Disk data not loaded. Cannot read file content.");

        var content = new List<byte>();

        // Start from the track/sector specified in the directory entry
        var track = fileEntry.StartTrack;
        var sector = fileEntry.StartSector;

        while (track != 0)
        {
            int sectorOffset = CalculateSectorOffset(track, sector);

            // Ensure we don't read beyond the disk data
            if (sectorOffset >= RawDiskData.Length || sectorOffset + 256 > RawDiskData.Length)
            {
                throw new InvalidOperationException($"Sector offset {sectorOffset} is beyond disk data bounds.");
            }

            var sectorData = new byte[256];
            Array.Copy(RawDiskData, sectorOffset, sectorData, 0, 256);

            // Next track/sector locations are in the first two bytes
            var nextTrack = sectorData[0];
            var nextSector = sectorData[1];

            // If this is the last sector, only read up to the last byte used
            if (nextTrack == 0)
            {
                // Last sector - nextSector contains the number of bytes used in this sector
                var bytesUsed = nextSector;
                if (bytesUsed > 0 && bytesUsed <= 254)
                {
                    for (int i = 2; i < 2 + bytesUsed; i++)
                    {
                        content.Add(sectorData[i]);
                    }
                }
            }
            else
            {
                // Not the last sector - add all data bytes (2-255)
                for (int i = 2; i < 256; i++)
                {
                    content.Add(sectorData[i]);
                }
            }

            track = nextTrack;
            sector = nextSector;
        }

        return content.ToArray();
    }

    /// <summary>
    /// Calculate the byte offset for a given track and sector.
    /// </summary>
    private static int CalculateSectorOffset(int track, int sector)
    {
        int offset = 0;

        // Calculate offset by summing up all sectors in tracks before the target track
        for (int t = 1; t < track; t++)
        {
            if (D64Parser.SectorsPerTrack.TryGetValue(t, out int sectorsInTrack))
            {
                offset += sectorsInTrack * 256;
            }
        }

        // Add offset for the specific sector within the target track
        offset += sector * 256;

        return offset;
    }
}
