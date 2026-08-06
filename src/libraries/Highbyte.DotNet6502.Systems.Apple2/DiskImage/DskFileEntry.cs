namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage;

/// <summary>One catalog entry of a DOS 3.3 disk image.</summary>
public class DskFileEntry
{
    /// <summary>File name, decoded from the high-bit ASCII catalog bytes, trailing spaces trimmed.</summary>
    public string FileName { get; init; } = string.Empty;

    public DskFileType FileType { get; init; }

    public bool Locked { get; init; }

    /// <summary>Size in sectors, as recorded in the catalog.</summary>
    public int Sectors { get; init; }

    /// <summary>Track of the file's first track/sector list.</summary>
    public int TrackSectorListTrack { get; init; }

    /// <summary>Sector of the file's first track/sector list.</summary>
    public int TrackSectorListSector { get; init; }
}

/// <summary>DOS 3.3 file types (the type byte's low bits; bit 7 is the lock flag).</summary>
public enum DskFileType
{
    Text = 0x00,
    IntegerBasic = 0x01,
    ApplesoftBasic = 0x02,
    Binary = 0x04,
    TypeS = 0x08,
    Relocatable = 0x10,
    TypeA = 0x20,
    TypeB = 0x40,
}
