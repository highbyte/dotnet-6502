using Highbyte.DotNet6502.Systems.Apple2.DiskImage;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

/// <summary>
/// The generated blank has to satisfy DOS, not just look plausible - so these assert the VTOC
/// fields DOS actually reads, and the catalog chain it walks.
/// </summary>
public class BlankDiskImageBuilderTests
{
    [Fact]
    public void The_Image_Is_A_140Kb_Volume_With_A_Vtoc_Dos_Recognises()
    {
        var image = BlankDiskImageBuilder.CreateDos33();
        Assert.Equal(DskParser.DiskImageSize, image.Length);

        var vtoc = DskParser.SectorOffset(BlankDiskImageBuilder.CatalogTrack, 0);
        Assert.Equal(17, image[vtoc + 0x01]);              // catalog track
        Assert.Equal(15, image[vtoc + 0x02]);              // first catalog sector
        Assert.Equal(3, image[vtoc + 0x03]);               // DOS release
        Assert.Equal(254, image[vtoc + 0x06]);             // volume
        Assert.Equal(DskParser.Tracks, image[vtoc + 0x34]);
        Assert.Equal(DskParser.SectorsPerTrack, image[vtoc + 0x35]);
        Assert.Equal(0x00, image[vtoc + 0x36]);
        Assert.Equal(0x01, image[vtoc + 0x37]);            // 256 bytes per sector
    }

    [Fact]
    public void Tracks_0_To_2_And_The_Catalog_Track_Are_Reserved_And_The_Rest_Free()
    {
        var image = BlankDiskImageBuilder.CreateDos33();
        var vtoc = DskParser.SectorOffset(BlankDiskImageBuilder.CatalogTrack, 0);

        var free = 0;
        for (var track = 0; track < DskParser.Tracks; track++)
        {
            var entry = vtoc + 0x38 + (track * 4);
            free += System.Numerics.BitOperations.PopCount((uint)image[entry + 0]);
            free += System.Numerics.BitOperations.PopCount((uint)image[entry + 1]);
        }

        // 35 tracks x 16 sectors, less DOS's three tracks and the catalog track.
        Assert.Equal((DskParser.Tracks - 4) * DskParser.SectorsPerTrack, free);
        Assert.Equal(496, free);
    }

    [Fact]
    public void The_Catalog_Chain_Runs_Down_To_Sector_1_And_Then_Stops()
    {
        var image = BlankDiskImageBuilder.CreateDos33();

        // Walk it exactly as DOS does, from the VTOC's pointer.
        var vtoc = DskParser.SectorOffset(BlankDiskImageBuilder.CatalogTrack, 0);
        int track = image[vtoc + 0x01], sector = image[vtoc + 0x02];

        var visited = 0;
        while (track != 0 && visited < 100)
        {
            var offset = DskParser.SectorOffset(track, sector);
            visited++;
            // Every entry slot is empty on a fresh disk.
            for (var entry = 0; entry < 7; entry++)
                Assert.Equal(0x00, image[offset + 0x0B + (entry * 35)]);
            track = image[offset + 0x01];
            sector = image[offset + 0x02];
        }

        Assert.Equal(15, visited);   // sectors 15 down to 1
    }

    [Fact]
    public void A_Custom_Volume_Number_Is_Recorded()
    {
        var image = BlankDiskImageBuilder.CreateDos33(volumeNumber: 42);
        var vtoc = DskParser.SectorOffset(BlankDiskImageBuilder.CatalogTrack, 0);
        Assert.Equal(42, image[vtoc + 0x06]);
    }
}
