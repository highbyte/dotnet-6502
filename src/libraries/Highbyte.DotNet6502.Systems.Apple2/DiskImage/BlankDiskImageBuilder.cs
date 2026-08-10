namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage;

/// <summary>
/// Builds an empty, formatted DOS 3.3 disk image — the emulated equivalent of handing someone a
/// freshly INITed diskette.
///
/// <para>The authentic way to get one is to boot DOS and type <c>INIT</c>, and that is what the
/// hardware and other emulators do. It does not work here: <c>INIT</c> writes 10-bit self-sync
/// bytes so its verify pass can bit-align to the field prologs afterwards, and this drive models
/// nibbles as plain bytes with no bit layer to express that in (the same gap that rules out
/// <c>.nib</c>/<c>.woz</c> media and copy protection). Measured: <c>INIT</c> writes every track
/// and then fails its own verification. So the filesystem is written here directly instead, which
/// is what disk-image tools do.</para>
///
/// <para>Only the volume structures are written. There is no DOS on the result, so it is a data
/// disk and will not boot — tracks 0-2 are reserved and left empty, exactly as a tool-made blank
/// is. Use it by booting a DOS disk first and then swapping this one in.</para>
/// </summary>
public static class BlankDiskImageBuilder
{
    /// <summary>Track holding the VTOC and the catalog, as DOS 3.3 always places them.</summary>
    public const int CatalogTrack = 17;

    /// <summary>First catalog sector. The chain runs downward from here to sector 1.</summary>
    private const int FirstCatalogSector = 15;

    /// <summary>Tracks DOS itself occupies. Reserved on every disk, whether or not DOS is on it.</summary>
    private const int ReservedTracks = 3;

    private const byte DosRelease = 3;
    private const int MaxTrackSectorPairs = 122;
    private const int CatalogEntriesPerSector = 7;

    /// <summary>
    /// A formatted, empty DOS 3.3 volume. Free sector count matches what a tool-made blank
    /// reports: 560 sectors total, less tracks 0-2 and the catalog track.
    /// </summary>
    /// <param name="volumeNumber">Volume number recorded in the VTOC. DOS's own operations
    /// default to "match any volume", so the conventional 254 suits anything.</param>
    public static byte[] CreateDos33(byte volumeNumber = Disk2.Disk2TrackNibblizer.DefaultVolume)
    {
        var image = new byte[DskParser.DiskImageSize];

        WriteVtoc(image, volumeNumber);
        WriteCatalogChain(image);

        return image;
    }

    private static void WriteVtoc(byte[] image, byte volumeNumber)
    {
        var vtoc = DskParser.SectorOffset(CatalogTrack, 0);

        image[vtoc + 0x01] = CatalogTrack;
        image[vtoc + 0x02] = FirstCatalogSector;
        image[vtoc + 0x03] = DosRelease;
        image[vtoc + 0x06] = volumeNumber;
        image[vtoc + 0x27] = MaxTrackSectorPairs;

        // Where DOS starts looking for space, and which way it walks. It allocates outward from
        // the catalog track, so starting just past it and heading up is what a real INIT leaves.
        image[vtoc + 0x30] = CatalogTrack + 1;
        image[vtoc + 0x31] = 1;

        image[vtoc + 0x34] = DskParser.Tracks;
        image[vtoc + 0x35] = DskParser.SectorsPerTrack;
        image[vtoc + 0x36] = 0x00;   // bytes per sector, low
        image[vtoc + 0x37] = 0x01;   // bytes per sector, high  (256)

        // Free-sector bitmap: four bytes per track, of which two are used. In the first byte bit 7
        // is sector 15 down to bit 0 for sector 8; in the second, bit 7 is sector 7 down to
        // sector 0. A set bit means free.
        for (var track = 0; track < DskParser.Tracks; track++)
        {
            var inUse = track < ReservedTracks || track == CatalogTrack;
            var entry = vtoc + 0x38 + (track * 4);
            image[entry + 0] = inUse ? (byte)0x00 : (byte)0xFF;
            image[entry + 1] = inUse ? (byte)0x00 : (byte)0xFF;
            image[entry + 2] = 0x00;
            image[entry + 3] = 0x00;
        }
    }

    /// <summary>
    /// The catalog is a chain of sectors running <em>downward</em> from sector 15 to sector 1,
    /// each pointing at the next and the last pointing nowhere. All entries are left blank.
    /// </summary>
    private static void WriteCatalogChain(byte[] image)
    {
        for (var sector = FirstCatalogSector; sector >= 1; sector--)
        {
            var offset = DskParser.SectorOffset(CatalogTrack, sector);
            var next = sector - 1;

            image[offset + 0x01] = next >= 1 ? (byte)CatalogTrack : (byte)0x00;
            image[offset + 0x02] = next >= 1 ? (byte)next : (byte)0x00;

            // Entries start at $0B, 35 bytes each, and a zero first byte means "never used".
            for (var entry = 0; entry < CatalogEntriesPerSector; entry++)
                image[offset + 0x0B + (entry * 35)] = 0x00;
        }
    }
}
