namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage;

/// <summary>
/// The order in which a 140 KB disk image file stores the sectors of each track.
///
/// <para>
/// The bytes on the disk are the same either way; what differs is which sector of the file holds
/// each physical position around the track. DOS 3.3 and ProDOS number their logical sectors
/// differently, and image files were dumped in whichever order the dumping system used.
/// </para>
/// </summary>
public enum DiskSectorOrder
{
    /// <summary>DOS 3.3 order, conventionally <c>.dsk</c> / <c>.do</c>.</summary>
    Dos = 0,

    /// <summary>ProDOS block order, conventionally <c>.po</c>.</summary>
    ProDos = 1,
}

/// <summary>
/// Works out how a disk image stores its sectors.
///
/// <para>
/// <b>Why by content and not by file extension.</b> Extensions are unreliable in practice: the
/// archive.org copy of "Dangerous Dave in the Deserted Pirate's Hideout" is named <c>.dsk</c> and is
/// ProDOS-ordered. Guessing from the name would mis-nibblize it into garbage, and the resulting
/// failure — a drive that spins with the read counter frozen — looks like a drive bug rather than a
/// misread image, which is an expensive thing to debug.
/// </para>
/// </summary>
public static class DiskSectorOrderDetector
{
    /// <summary>ProDOS block 2 holds the volume directory header; in a ProDOS-ordered file that is here.</summary>
    private const int ProdosVolumeDirectoryOffset = 0x400;

    /// <summary>Storage type nibble marking a volume directory header.</summary>
    private const byte VolumeDirectoryStorageType = 0x0F;

    /// <summary>DOS 3.3 keeps its VTOC at track 17 sector 0, which in a DOS-ordered file is here.</summary>
    private const int DosVtocOffset = 17 * DskParser.SectorsPerTrack * DskParser.BytesPerSector;

    /// <summary>
    /// Detects the sector order of a 140 KB image, falling back to <see cref="DiskSectorOrder.Dos"/>
    /// when neither filesystem is recognised — the historically commoner order, and what the drive
    /// assumed before this existed.
    /// </summary>
    public static DiskSectorOrder Detect(byte[] diskImageData)
    {
        ArgumentNullException.ThrowIfNull(diskImageData);

        if (LooksLikeProdosVolumeDirectory(diskImageData))
            return DiskSectorOrder.ProDos;

        return DiskSectorOrder.Dos;
    }

    /// <summary>
    /// True if a ProDOS volume directory header sits where a ProDOS-ordered image would keep it: a
    /// storage type of $F, a name of 1-15 characters, and that name actually being characters.
    /// Checking the name as well as the type matters — the type nibble alone is four bits and turns
    /// up by chance in game data often enough to matter.
    /// </summary>
    private static bool LooksLikeProdosVolumeDirectory(byte[] diskImageData)
    {
        if (diskImageData.Length < ProdosVolumeDirectoryOffset + 0x30)
            return false;

        var header = diskImageData[ProdosVolumeDirectoryOffset + 0x04];
        if ((byte)(header >> 4) != VolumeDirectoryStorageType)
            return false;

        var nameLength = header & 0x0F;
        if (nameLength is 0 or > 15)
            return false;

        for (var i = 0; i < nameLength; i++)
        {
            var c = diskImageData[ProdosVolumeDirectoryOffset + 0x05 + i];
            var isUpper = c is >= (byte)'A' and <= (byte)'Z';
            var isDigit = c is >= (byte)'0' and <= (byte)'9';
            if (!isUpper && !isDigit && c != (byte)'.')
                return false;
        }

        return true;
    }

    /// <summary>
    /// True if a DOS 3.3 VTOC sits where a DOS-ordered image would keep it. Not used by
    /// <see cref="Detect"/> (which defaults to DOS anyway) but useful to tests and diagnostics that
    /// want a positive identification rather than a fallback.
    /// </summary>
    public static bool LooksLikeDosVtoc(byte[] diskImageData)
    {
        ArgumentNullException.ThrowIfNull(diskImageData);
        if (diskImageData.Length < DosVtocOffset + 0x40)
            return false;

        var catalogTrack = diskImageData[DosVtocOffset + 0x01];
        var sectorsPerTrack = diskImageData[DosVtocOffset + 0x35];
        var bytesPerSectorLow = diskImageData[DosVtocOffset + 0x36];
        var bytesPerSectorHigh = diskImageData[DosVtocOffset + 0x37];

        return catalogTrack < DskParser.Tracks
            && sectorsPerTrack == DskParser.SectorsPerTrack
            && bytesPerSectorLow == 0x00
            && bytesPerSectorHigh == 0x01;   // 256, little-endian
    }
}
