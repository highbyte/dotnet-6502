using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage;

/// <summary>
/// Parser for DOS 3.3 disk images in DOS sector order (<c>.dsk</c>/<c>.do</c>):
/// 35 tracks × 16 sectors × 256 bytes = 143,360 bytes. Reads the VTOC (track 17 sector 0) and
/// follows the catalog sector chain to produce a <see cref="DskDiskImage"/>.
///
/// This is file-level access only — no Disk II hardware emulation. ProDOS-ordered (.po) and
/// nibble/flux images (.nib/.woz) are not supported.
/// </summary>
public static class DskParser
{
    public const int Tracks = 35;
    public const int SectorsPerTrack = 16;
    public const int BytesPerSector = 256;
    public const int DiskImageSize = Tracks * SectorsPerTrack * BytesPerSector;   // 143,360

    public const int VtocTrack = 17;
    public const int VtocSector = 0;

    /// <summary>Data-sector pairs per track/sector list sector.</summary>
    public const int TrackSectorPairsPerList = 122;

    /// <summary>Catalog entries per catalog sector.</summary>
    public const int CatalogEntriesPerSector = 7;
    public const int CatalogEntrySize = 0x23;
    public const int CatalogFirstEntryOffset = 0x0B;
    public const int FileNameLength = 30;

    /// <summary>Chain-walk guard: no legal structure chains more sectors than the disk has.</summary>
    public const int MaxCatalogSectors = Tracks * SectorsPerTrack;
    public const int MaxTrackSectorListsPerFile = Tracks * SectorsPerTrack;

    private const byte DeletedFileMarker = 0xFF;

    public static int SectorOffset(int track, int sector)
        => ((track * SectorsPerTrack) + sector) * BytesPerSector;

    /// <summary>Parses a DOS 3.3 disk image.</summary>
    /// <exception cref="InvalidDataException">The image has the wrong size or a broken catalog.</exception>
    public static DskDiskImage ParseDskFile(byte[] rawDiskData, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(rawDiskData);
        if (rawDiskData.Length != DiskImageSize)
            throw new InvalidDataException(
                $"Not a 140 KB DOS-ordered disk image: {rawDiskData.Length} bytes, expected {DiskImageSize}.");

        var vtocOffset = SectorOffset(VtocTrack, VtocSector);
        var catalogTrack = rawDiskData[vtocOffset + 0x01];
        var catalogSector = rawDiskData[vtocOffset + 0x02];
        var volume = rawDiskData[vtocOffset + 0x06];

        var files = new List<DskFileEntry>();
        var visitedCatalogSectors = 0;

        while (catalogTrack != 0 || catalogSector != 0)
        {
            if (catalogTrack >= Tracks || catalogSector >= SectorsPerTrack)
                throw new InvalidDataException(
                    $"Catalog chain points outside the disk (track {catalogTrack}, sector {catalogSector}).");
            if (++visitedCatalogSectors > MaxCatalogSectors)
                throw new InvalidDataException("Catalog sector chain does not terminate.");

            var sectorOffset = SectorOffset(catalogTrack, catalogSector);

            for (var i = 0; i < CatalogEntriesPerSector; i++)
            {
                var entryOffset = sectorOffset + CatalogFirstEntryOffset + (i * CatalogEntrySize);
                var listTrack = rawDiskData[entryOffset];

                if (listTrack == 0)
                    continue;   // never-used entry
                if (listTrack == DeletedFileMarker)
                    continue;   // deleted file

                var typeByte = rawDiskData[entryOffset + 2];
                var fileName = DecodeFileName(rawDiskData, entryOffset + 3);
                if (fileName.Length == 0)
                    continue;

                files.Add(new DskFileEntry
                {
                    FileName = fileName,
                    FileType = (DskFileType)(typeByte & 0x7F),
                    Locked = (typeByte & 0x80) != 0,
                    Sectors = rawDiskData[entryOffset + 0x21] | (rawDiskData[entryOffset + 0x22] << 8),
                    TrackSectorListTrack = listTrack,
                    TrackSectorListSector = rawDiskData[entryOffset + 1],
                });
            }

            catalogTrack = rawDiskData[sectorOffset + 0x01];
            catalogSector = rawDiskData[sectorOffset + 0x02];
        }

        logger?.LogInformation("Parsed DOS 3.3 disk image: volume {Volume}, {FileCount} catalog files.", volume, files.Count);

        return new DskDiskImage
        {
            Volume = volume,
            Files = files,
            RawDiskData = rawDiskData,
        };
    }

    /// <summary>Decodes a 30-byte high-bit-ASCII catalog file name, trimming trailing spaces.</summary>
    private static string DecodeFileName(byte[] data, int offset)
    {
        var chars = new char[FileNameLength];
        for (var i = 0; i < FileNameLength; i++)
            chars[i] = (char)(data[offset + i] & 0x7F);
        return new string(chars).TrimEnd(' ');
    }
}
