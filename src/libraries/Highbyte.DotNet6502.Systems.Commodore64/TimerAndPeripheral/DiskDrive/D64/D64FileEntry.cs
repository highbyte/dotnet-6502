
namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64;

public class D64FileEntry
{
    public string FileName { get; set; } = string.Empty;
    public byte[] FileNamePetscii { get; set; } = Array.Empty<byte>();

    public D64FileType FileType { get; set; } // Bits 0-3 of D64 file type
    public bool FileDoesNotExist { get; set; } = false; // Bit 6 of D64 file type
    public bool FileIsClosed { get; set; } = false; // Bit 7 of D64 file type
    public int Blocks { get; set; }
    public byte RawFileType { get; set; }
    
    // Track and sector where file data starts
    public byte StartTrack { get; set; }
    public byte StartSector { get; set; }

    public override string ToString()
    {
        return $"{Blocks,3} \"{FileName,-16}\" {FileType}";
    }
}

/// <summary>
/// Enumeration of D64 file types
/// </summary>
public enum D64FileType
{
    DEL, // Deleted
    SEQ, // Sequential
    PRG, // Program
    USR, // User
    REL, // Relative
    CBM, // CBM
    DIR, // Directory
    Unknown
}
