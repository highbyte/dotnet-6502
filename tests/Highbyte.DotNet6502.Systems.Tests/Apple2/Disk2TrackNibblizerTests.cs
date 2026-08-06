using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Highbyte.DotNet6502.Systems.Apple2.DiskImage;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Disk2TrackNibblizerTests
{
    private static readonly byte[] s_addressProlog = { 0xD5, 0xAA, 0x96 };
    private static readonly byte[] s_dataProlog = { 0xD5, 0xAA, 0xAD };
    private static readonly byte[] s_fieldEpilog = { 0xDE, 0xAA, 0xEB };

    /// <summary>
    /// A disk image where every sector is tagged with its own track/sector number, so any
    /// interleave mistake shows up as the wrong tag.
    /// </summary>
    private static byte[] BuildTaggedDiskImage()
    {
        var image = new byte[DskParser.DiskImageSize];
        for (var track = 0; track < DskParser.Tracks; track++)
        {
            for (var sector = 0; sector < DskParser.SectorsPerTrack; sector++)
            {
                var offset = DskParser.SectorOffset(track, sector);
                image[offset] = (byte)track;
                image[offset + 1] = (byte)sector;
                for (var i = 2; i < DskParser.BytesPerSector; i++)
                    image[offset + i] = (byte)(track ^ sector ^ i);
            }
        }
        return image;
    }

    [Fact]
    public void BuildNibbleTracks_Produces_35_Tracks_Of_The_Standard_Size()
    {
        var tracks = Disk2TrackNibblizer.BuildNibbleTracks(BuildTaggedDiskImage());

        Assert.Equal(DskParser.Tracks, tracks.Length);
        Assert.All(tracks, t => Assert.Equal(Disk2TrackNibblizer.TrackSize, t.Length));
    }

    [Fact]
    public void BuildNibbleTracks_Rejects_Wrong_Image_Size()
    {
        Assert.Throws<InvalidDataException>(
            () => Disk2TrackNibblizer.BuildNibbleTracks(new byte[DskParser.DiskImageSize - 1]));
    }

    [Fact]
    public void Track_Contains_16_Address_Fields_In_Physical_Order()
    {
        const int track = 17;
        var trackData = Disk2TrackNibblizer.BuildNibbleTrack(BuildTaggedDiskImage(), track);

        var pos = 0;
        for (var expectedPhysical = 0; expectedPhysical < DskParser.SectorsPerTrack; expectedPhysical++)
        {
            pos = Disk2NibbleTestDecoder.FindAfter(trackData, pos, s_addressProlog);
            Assert.True(pos > 0, $"Address prolog {expectedPhysical} not found.");

            var (volume, addressTrack, addressSector) =
                Disk2NibbleTestDecoder.DecodeAddressField(trackData.AsSpan(pos, 8));
            Assert.Equal(Disk2TrackNibblizer.DefaultVolume, volume);
            Assert.Equal(track, addressTrack);
            Assert.Equal(expectedPhysical, addressSector);

            // Epilog directly after the 8 address bytes.
            Assert.Equal(s_fieldEpilog, trackData.AsSpan(pos + 8, 3).ToArray());
        }

        // No 17th address field.
        Assert.Equal(-1, Disk2NibbleTestDecoder.FindAfter(trackData, pos, s_addressProlog));
    }

    [Fact]
    public void Data_Fields_Carry_The_Dos_Interleaved_Logical_Sectors()
    {
        const int track = 5;
        var image = BuildTaggedDiskImage();
        var trackData = Disk2TrackNibblizer.BuildNibbleTrack(image, track);

        var pos = 0;
        for (var physical = 0; physical < DskParser.SectorsPerTrack; physical++)
        {
            pos = Disk2NibbleTestDecoder.FindAfter(trackData, pos, s_addressProlog);
            var (_, _, addressSector) = Disk2NibbleTestDecoder.DecodeAddressField(trackData.AsSpan(pos, 8));
            Assert.Equal(physical, addressSector);

            pos = Disk2NibbleTestDecoder.FindAfter(trackData, pos, s_dataProlog);
            Assert.True(pos > 0, $"Data prolog for physical sector {physical} not found.");

            var decoded = Disk2NibbleTestDecoder.DecodeSector(
                trackData.AsSpan(pos, Disk2NibbleCodec.EncodedDataSize));

            var expectedLogical = Disk2TrackNibblizer.PhysicalToDosSector[physical];
            var expectedOffset = DskParser.SectorOffset(track, expectedLogical);
            Assert.Equal(image.AsSpan(expectedOffset, DskParser.BytesPerSector).ToArray(), decoded);

            // Epilog directly after the encoded data.
            Assert.Equal(
                s_fieldEpilog,
                trackData.AsSpan(pos + Disk2NibbleCodec.EncodedDataSize, 3).ToArray());
            pos += Disk2NibbleCodec.EncodedDataSize;
        }
    }

    [Fact]
    public void Gaps_Are_Filled_With_Sync_Bytes()
    {
        var trackData = Disk2TrackNibblizer.BuildNibbleTrack(BuildTaggedDiskImage(), track: 0);

        // The track starts with gap 3 sync bytes before the first address field.
        for (var i = 0; i < Disk2TrackNibblizer.Gap3SyncBytes; i++)
            Assert.Equal(0xFF, trackData[i]);

        // Gap 2 sits between the address field's epilog and the data field's prolog.
        var gap2Start = Disk2TrackNibblizer.Gap3SyncBytes + 3 + 8 + 3;
        for (var i = gap2Start; i < gap2Start + Disk2TrackNibblizer.Gap2SyncBytes; i++)
            Assert.Equal(0xFF, trackData[i]);
        Assert.Equal(0xD5, trackData[gap2Start + Disk2TrackNibblizer.Gap2SyncBytes]);
    }

    /// <summary>
    /// The track holds exactly its 16 sectors: no wrap-around filler, so a reader that runs off
    /// the end lands on the next revolution's first sync gap. See the class comment on why the
    /// gap sizing is load-bearing for RWTS's drive-spinning check.
    /// </summary>
    [Fact]
    public void Track_Is_Exactly_16_Sectors_Long()
    {
        const int perSector = Disk2TrackNibblizer.Gap3SyncBytes + 14
            + Disk2TrackNibblizer.Gap2SyncBytes + 3 + Disk2NibbleCodec.EncodedDataSize + 3;

        Assert.Equal(perSector * DskParser.SectorsPerTrack, Disk2TrackNibblizer.TrackSize);

        // The track ends on the last sector's data epilog, and wraps straight into the first
        // sector's sync gap.
        var trackData = Disk2TrackNibblizer.BuildNibbleTrack(BuildTaggedDiskImage(), track: 0);
        Assert.Equal(new byte[] { 0xDE, 0xAA, 0xEB }, trackData[^3..]);
        Assert.Equal(0xFF, trackData[0]);
    }

    /// <summary>
    /// No run of identical bytes outside a sync gap is long enough to fool RWTS's
    /// "8 identical reads means the drive is stopped" check into triggering mid-field.
    /// </summary>
    [Fact]
    public void No_Field_Contains_A_Long_Run_Of_Identical_Bytes()
    {
        var trackData = Disk2TrackNibblizer.BuildNibbleTrack(BuildTaggedDiskImage(), track: 3);

        var runLength = 1;
        var longestRun = 1;
        for (var i = 1; i < trackData.Length; i++)
        {
            runLength = trackData[i] == trackData[i - 1] ? runLength + 1 : 1;
            longestRun = Math.Max(longestRun, runLength);
        }

        Assert.Equal(Disk2TrackNibblizer.Gap3SyncBytes, longestRun);
    }

    [Fact]
    public void PhysicalToDosSector_Is_The_Standard_2To1_Interleave()
    {
        var table = Disk2TrackNibblizer.PhysicalToDosSector.ToArray();
        Assert.Equal(new byte[] { 0, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 15 }, table);
        Assert.Equal(16, table.Distinct().Count());
    }
}
