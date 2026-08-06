using Highbyte.DotNet6502.Systems.Apple2.DiskImage;

namespace Highbyte.DotNet6502.Systems.Apple2.Disk2;

/// <summary>
/// Converts a DOS-ordered 16-sector disk image (<c>.dsk</c>/<c>.do</c>) into the per-track nibble
/// streams the Disk II controller feeds to the CPU — the standard emulator approach: nibblize
/// once on insert, then let RWTS (or a game's own loader) run unmodified against the stream.
///
/// Track layout per physical sector, matching what the DOS 3.3 formatter writes: 20 self-sync
/// bytes (gap 3), the address field (D5 AA 96 + 4-and-4 volume/track/sector/checksum +
/// DE AA EB), 5 self-sync bytes (gap 2), and the data field (D5 AA AD + 343 encoded bytes +
/// DE AA EB). Self-sync bytes are stored as plain $FF — the extra zero bits real hardware
/// appends only matter for bit-level (.nib/.woz) fidelity, which is out of scope.
///
/// The track buffer holds exactly its 16 sectors with no trailing filler, so a reader running
/// off the end wraps straight into the first sector's gap — the equivalent of gap 1 on a real
/// track. Gap sizes were measured against a real DOS 3.3 System Master boot (20/5, 16/16,
/// 12/12, 10/10 and 9/9 all boot in ~2.52 M cycles), so the authentic formatter values are
/// used here.
/// </summary>
public static class Disk2TrackNibblizer
{
    public const int Gap3SyncBytes = 20;
    public const int Gap2SyncBytes = 5;

    /// <summary>Address field: 3-byte prolog, 4 values in 4-and-4 encoding, 3-byte epilog.</summary>
    private const int AddressFieldSize = 3 + 8 + 3;

    /// <summary>Data field: 3-byte prolog, 343 encoded bytes, 3-byte epilog.</summary>
    private const int DataFieldSize = 3 + Disk2NibbleCodec.EncodedDataSize + 3;

    /// <summary>Nibble bytes per track: exactly 16 sectors, no wrap-around filler.</summary>
    public const int TrackSize =
        DskParser.SectorsPerTrack * (Gap3SyncBytes + AddressFieldSize + Gap2SyncBytes + DataFieldSize);

    private const byte SyncByte = 0xFF;

    /// <summary>
    /// Volume number written into every address field. DOS's own operations default to
    /// "match any volume", so a fixed conventional value is safe for standard images.
    /// </summary>
    public const byte DefaultVolume = 254;

    /// <summary>
    /// Which DOS (logical) sector occupies each physical position around a track. Address fields
    /// are numbered sequentially by physical position; RWTS applies this 2:1 software interleave
    /// when mapping the sector numbers it is asked for onto the disk — so a DOS-ordered image
    /// stores physical position <c>p</c>'s data at logical sector <c>PhysicalToDosSector[p]</c>.
    /// </summary>
    public static ReadOnlySpan<byte> PhysicalToDosSector => new byte[16]
    {
        0, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 15,
    };

    private static ReadOnlySpan<byte> AddressProlog => new byte[] { 0xD5, 0xAA, 0x96 };
    private static ReadOnlySpan<byte> DataProlog => new byte[] { 0xD5, 0xAA, 0xAD };
    private static ReadOnlySpan<byte> FieldEpilog => new byte[] { 0xDE, 0xAA, 0xEB };

    /// <summary>Nibblizes all 35 tracks of a DOS-ordered disk image.</summary>
    /// <exception cref="InvalidDataException">The image is not a 140 KB DOS-ordered image.</exception>
    public static byte[][] BuildNibbleTracks(byte[] diskImageData, byte volume = DefaultVolume)
    {
        ArgumentNullException.ThrowIfNull(diskImageData);
        if (diskImageData.Length != DskParser.DiskImageSize)
            throw new InvalidDataException(
                $"Not a 140 KB DOS-ordered disk image: {diskImageData.Length} bytes, expected {DskParser.DiskImageSize}.");

        var tracks = new byte[DskParser.Tracks][];
        for (var track = 0; track < DskParser.Tracks; track++)
            tracks[track] = BuildNibbleTrack(diskImageData, track, volume);
        return tracks;
    }

    /// <summary>Nibblizes one track of a DOS-ordered disk image.</summary>
    public static byte[] BuildNibbleTrack(byte[] diskImageData, int track, byte volume = DefaultVolume)
    {
        ArgumentNullException.ThrowIfNull(diskImageData);
        if (track is < 0 or >= DskParser.Tracks)
            throw new ArgumentOutOfRangeException(nameof(track));

        var trackData = new byte[TrackSize];
        Array.Fill(trackData, SyncByte);

        Span<byte> encoded = stackalloc byte[Disk2NibbleCodec.EncodedDataSize];
        var pos = 0;

        for (byte physicalSector = 0; physicalSector < DskParser.SectorsPerTrack; physicalSector++)
        {
            pos += Gap3SyncBytes;   // buffer is pre-filled with sync bytes

            pos = Write(trackData, pos, AddressProlog);
            trackData[pos++] = Disk2NibbleCodec.To44Lo(volume);
            trackData[pos++] = Disk2NibbleCodec.To44Hi(volume);
            trackData[pos++] = Disk2NibbleCodec.To44Lo((byte)track);
            trackData[pos++] = Disk2NibbleCodec.To44Hi((byte)track);
            trackData[pos++] = Disk2NibbleCodec.To44Lo(physicalSector);
            trackData[pos++] = Disk2NibbleCodec.To44Hi(physicalSector);
            var addressChecksum = (byte)(volume ^ (byte)track ^ physicalSector);
            trackData[pos++] = Disk2NibbleCodec.To44Lo(addressChecksum);
            trackData[pos++] = Disk2NibbleCodec.To44Hi(addressChecksum);
            pos = Write(trackData, pos, FieldEpilog);

            pos += Gap2SyncBytes;

            var logicalSector = PhysicalToDosSector[physicalSector];
            var sectorOffset = DskParser.SectorOffset(track, logicalSector);
            Disk2NibbleCodec.EncodeSector(
                diskImageData.AsSpan(sectorOffset, Disk2NibbleCodec.SectorSize), encoded);

            pos = Write(trackData, pos, DataProlog);
            pos = Write(trackData, pos, encoded);
            pos = Write(trackData, pos, FieldEpilog);
        }

        return trackData;
    }

    private static int Write(byte[] trackData, int pos, ReadOnlySpan<byte> data)
    {
        data.CopyTo(trackData.AsSpan(pos));
        return pos + data.Length;
    }
}
