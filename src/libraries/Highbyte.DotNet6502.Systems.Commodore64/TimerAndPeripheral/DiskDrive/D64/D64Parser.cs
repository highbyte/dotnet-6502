using System.Text;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;

/// <summary>
/// Parser for Commodore 64 D64 disk image files
/// </summary>
public class D64Parser
{
    private const int TRACK_18_OFFSET = 0x16500; // Track 18 starts here
    private const int SECTOR_SIZE = 256;
    private const int DISK_NAME_OFFSET = 0x90;
    private const int DISK_ID_OFFSET = 0xa2;
    private const int ENTRIES_PER_SECTOR = 8;
    private const int ENTRY_SIZE = 32;
    
    // Track sector counts for D64 format
    public static readonly Dictionary<int, int> SectorsPerTrack = new Dictionary<int, int>
    {
        // Tracks 1-17: 21 sectors each
        { 1, 21 }, { 2, 21 }, { 3, 21 }, { 4, 21 }, { 5, 21 }, { 6, 21 }, { 7, 21 }, { 8, 21 },
        { 9, 21 }, { 10, 21 }, { 11, 21 }, { 12, 21 }, { 13, 21 }, { 14, 21 }, { 15, 21 }, { 16, 21 }, { 17, 21 },
        // Tracks 18-24: 19 sectors each
        { 18, 19 }, { 19, 19 }, { 20, 19 }, { 21, 19 }, { 22, 19 }, { 23, 19 }, { 24, 19 },
        // Tracks 25-30: 18 sectors each
        { 25, 18 }, { 26, 18 }, { 27, 18 }, { 28, 18 }, { 29, 18 }, { 30, 18 },
        // Tracks 31-35: 17 sectors each
        { 31, 17 }, { 32, 17 }, { 33, 17 }, { 34, 17 }, { 35, 17 }
    };

    /// <summary>
    /// Parse a D64 disk image file from a file path
    /// </summary>
    /// <param name="filePath">Path to the D64 file</param>
    /// <returns>D64DiskImage object containing disk information and directory</returns>
    /// <exception cref="FileNotFoundException">Thrown when the D64 file is not found</exception>
    /// <exception cref="InvalidDataException">Thrown when the D64 file is invalid</exception>
    public static D64DiskImage ParseD64File(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"D64 file not found: {filePath}");
        var rawDiskData = File.ReadAllBytes(filePath);
        return ParseD64File(rawDiskData);
    }

    /// <summary>
    /// Parse a D64 disk image from a byte array
    /// </summary>
    /// <param name="rawDiskData">Raw bytes of the D64 file</param>
    /// <returns>D64DiskImage object containing disk information and directory</returns>
    /// <exception cref="InvalidDataException">Thrown when the D64 file is invalid</exception>
    public static D64DiskImage ParseD64File(byte[] rawDiskData)
    {
        try
        {
            return ParseD64FileInternal(rawDiskData);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Error parsing D64 file: {ex.Message}", ex);
        }
    }

    private static D64DiskImage ParseD64FileInternal(byte[] rawDiskData)
    {
        using var fileStream = new MemoryStream(rawDiskData);
        using var reader = new BinaryReader(fileStream);

        var diskImage = new D64DiskImage();
        // Store the raw disk data
        diskImage.RawDiskData = rawDiskData;

        // Read BAM (Block Availability Map) sector
        fileStream.Seek(TRACK_18_OFFSET, SeekOrigin.Begin);
        var bamSector = reader.ReadBytes(SECTOR_SIZE);

        // Extract disk name and ID
        var diskNameBytes = new byte[16];
        Array.Copy(bamSector, DISK_NAME_OFFSET, diskNameBytes, 0, 16);
        diskImage.DiskNamePetscii = diskNameBytes;
        diskImage.DiskName = PetsciiToAscii(diskNameBytes);

        var diskIdBytes = new byte[2];
        Array.Copy(bamSector, DISK_ID_OFFSET, diskIdBytes, 0, 2);
        diskImage.DiskIdPetscii = diskIdBytes;
        diskImage.DiskId = PetsciiToAscii(diskIdBytes);

        // Store BAM sector for free blocks calculation
        diskImage.BamSector = bamSector;

        // Read directory entries
        diskImage.Files = ReadDirectoryEntries(fileStream, reader);

        return diskImage;
    }

    /// <summary>
    /// Read directory entries from the D64 file
    /// </summary>
    private static List<D64FileEntry> ReadDirectoryEntries(MemoryStream fileStream, BinaryReader reader)
    {
        var files = new List<D64FileEntry>();
        var currentTrack = 18; // Directory is on track 18
        var currentSector = 1; // Start from sector 1

        while (currentTrack != 0)
        {
            // Calculate sector offset
            var sectorOffset = TRACK_18_OFFSET + (currentSector * SECTOR_SIZE);

            // Check bounds
            if (sectorOffset >= fileStream.Length)
            {
                break;
            }

            fileStream.Seek(sectorOffset, SeekOrigin.Begin);
            var sectorData = reader.ReadBytes(SECTOR_SIZE);

            // Ensure we have enough data
            if (sectorData.Length < 2)
            {
                break;
            }

            // First two bytes are track/sector of next directory sector
            var nextTrack = sectorData[0];
            var nextSector = sectorData[1];

            // Parse directory entries (8 entries per sector, 32 bytes each)
            for (int i = 0; i < ENTRIES_PER_SECTOR; i++)
            {
                var entryOffset = 2 + (i * ENTRY_SIZE);

                // Check bounds for directory entry - handle entries that cross sector boundaries
                if (entryOffset >= sectorData.Length)
                {
                    break;
                }

                byte[] entryData = new byte[ENTRY_SIZE];
                int bytesAvailable = sectorData.Length - entryOffset;

                if (bytesAvailable >= ENTRY_SIZE)
                {
                    // Entry fits completely within current sector
                    Array.Copy(sectorData, entryOffset, entryData, 0, ENTRY_SIZE);
                }
                else
                {
                    // Entry crosses sector boundary - read what we can and pad with zeros
                    Array.Copy(sectorData, entryOffset, entryData, 0, bytesAvailable);
                    // The rest remains zero-filled
                }

                var fileEntry = ParseDirectoryEntry(entryData);
                if (fileEntry != null)
                {
                    files.Add(fileEntry);
                }
            }

            // Move to next sector
            currentTrack = nextTrack;
            currentSector = nextSector;
        }

        return files;
    }

    /// <summary>
    /// Parse a single directory entry
    /// </summary>
    private static D64FileEntry? ParseDirectoryEntry(byte[] entry)
    {
        // Check if entry has enough data
        if (entry.Length < 30) // Need at least 30 bytes for valid entry
        {
            return null;
        }

        var fileType = entry[0];
        if (fileType == 0) // Empty entry
        {
            return null;
        }

        var blocks = BitConverter.ToInt16(entry, 28);

        // Filename (16 bytes starting at offset 3)
        var fileNameBytes = new byte[16];
        Array.Copy(entry, 3, fileNameBytes, 0, 16);

        // Remove any trailing padding values (160), they are not part of the file name
        int endIndex = Array.IndexOf(fileNameBytes, (byte)0xA0);
        if (endIndex == -1)
        {
            endIndex = fileNameBytes.Length;
        }
        var fileNameBytesTrimmed = new byte[endIndex];
        Array.Copy(fileNameBytes, fileNameBytesTrimmed, endIndex);

        var filenameAscii = PetsciiToAscii(fileNameBytesTrimmed);
        if (string.IsNullOrEmpty(filenameAscii))
        {
            return null;
        }

        // Extract starting track and sector (bytes 1 and 2 of directory entry)
        var startTrack = entry[1];
        var startSector = entry[2];

        return new D64FileEntry
        {
            FileName = filenameAscii,
            FileNamePetscii = fileNameBytesTrimmed,
            FileType = GetFileType((byte)(fileType & 0x07)), // The low 3 bits indicate the file type
            FileDoesNotExist = (fileType & 0x40) != 0, // Bit 6 indicates if file exists or not. 1 = does not exist, 0 = exists
            FileIsClosed = (fileType & 0x80) != 0, // Bit 7 indicates if file is closed. 1 = closed, 0 = open
            Blocks = blocks,
            RawFileType = fileType,
            StartTrack = startTrack,
            StartSector = startSector
        };
    }

    /// <summary>
    /// Convert PETSCII bytes to ASCII string
    /// </summary>
    private static string PetsciiToAscii(byte[] data)
    {
        var result = new StringBuilder();

        for (int i = 0; i < data.Length; i++)
        {
            var b = data[i];

            if (b == 0xa0) // Shifted space
            {
                result.Append(' ');
            }
            else if (b == 0x00) // End of string
            {
                break;
            }
            else if ((b >= 0x41 && b <= 0x5a) || // A-Z
                     (b >= 0x61 && b <= 0x7a) || // a-z
                     (b >= 0x30 && b <= 0x39) || // 0-9
                     (b >= 0x20 && b <= 0x2f))   // Common symbols
            {
                result.Append((char)b);
            }
            else
            {
                result.Append('?');
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Convert file type (lowest 3 bits of D64 file type value) to D64FileType enum
    /// </summary>
    private static D64FileType GetFileType(byte fileType)
    {
        return fileType switch
        {
            0x00 => D64FileType.DEL,
            0x01 => D64FileType.SEQ,
            0x02 => D64FileType.PRG,
            0x03 => D64FileType.USR,
            0x04 => D64FileType.REL,
            0x05 => D64FileType.CBM,
            0x06 => D64FileType.DIR,
            _ => D64FileType.Unknown
        };
    }
}
