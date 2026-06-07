using Highbyte.DotNet6502.Systems.Commodore64;
using Highbyte.DotNet6502.Systems.Commodore64.Config;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;
using Microsoft.Extensions.Logging.Abstractions;

namespace Highbyte.DotNet6502.Systems.Tests.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

/// <summary>
/// Tests for the shared <see cref="C64D64ContentLoader.LoadBytesAsync"/>
/// helper that is shared by the menu's download-and-run flow and the Avalonia automated-startup
/// participant. Verifies both branches (mount + direct-load) using a synthetic minimal D64 image.
/// </summary>
public class C64D64ContentLoaderTests
{
    // PRG payload (load address 0x0800 + NOP/NOP/RTS body). The first two bytes are the standard
    // PRG load-address header consumed by BinaryLoader.Load; the rest is the program body that
    // lands in C64 RAM starting at the load address.
    private const ushort PrgLoadAddress = 0x0800;
    private static readonly byte[] PrgFileBytes = new byte[]
    {
        0x00, 0x08,           // load address (little-endian) = 0x0800
        0xEA, 0xEA, 0x60,     // NOP, NOP, RTS — sentinel body
    };

    private const string TestFileName = "TEST";

    [Fact]
    public async Task LoadBytesAsync_DirectLoad_LoadsPrgBodyIntoMemory()
    {
        var c64 = BuildC64();
        var d64Bytes = BuildSyntheticD64();
        var programInfo = new C64DownloadProgramInfo(
            displayName: "synthetic",
            downloadUrl: string.Empty,
            directLoadPRGName: TestFileName,
            // Empty list so the helper does not paste any RunCommands during the test (we only
            // verify memory mutation here).
            runCommands: new List<string>());

        await C64D64ContentLoader.LoadBytesAsync(
            c64, d64Bytes, programInfo, issueRunCommands: true, NullLogger.Instance);

        Assert.Equal(0xEA, c64.Mem[PrgLoadAddress]);
        Assert.Equal(0xEA, c64.Mem[PrgLoadAddress + 1]);
        Assert.Equal(0x60, c64.Mem[PrgLoadAddress + 2]);

        // No disk should be mounted on the direct-load branch.
        var diskDrive = c64.IECBus.Devices.OfType<DiskDrive1541>().Single();
        Assert.False(diskDrive.IsDisketteInserted);
    }

    [Fact]
    public async Task LoadBytesAsync_DiskMount_AttachesDiskImageToDrive()
    {
        var c64 = BuildC64();
        var d64Bytes = BuildSyntheticD64();
        // DirectLoadPRGName=null → mount branch.
        var programInfo = new C64DownloadProgramInfo(
            displayName: "synthetic",
            downloadUrl: string.Empty,
            directLoadPRGName: null,
            runCommands: new List<string>());

        await C64D64ContentLoader.LoadBytesAsync(
            c64, d64Bytes, programInfo, issueRunCommands: false, NullLogger.Instance);

        var diskDrive = c64.IECBus.Devices.OfType<DiskDrive1541>().Single();
        Assert.True(diskDrive.IsDisketteInserted);

        // RAM should be untouched at the PRG load address (mount-only path doesn't write memory).
        Assert.Equal(0x00, c64.Mem[PrgLoadAddress]);
    }

    private static C64 BuildC64() => C64.BuildC64(new C64Config
    {
        LoadROMs = false,
        C64Model = "C64NTSC",
        Vic2Model = "NTSC",
    }, NullLoggerFactory.Instance);

    /// <summary>
    /// Build a minimal valid D64 byte image with one PRG file named <see cref="TestFileName"/>
    /// containing <see cref="PrgFileBytes"/>. Layout follows the standard 35-track / 683-sector
    /// CBM format: track 18 sector 0 = BAM (disk name/id), track 18 sector 1 = first directory
    /// sector with one entry pointing at the file's start track/sector.
    /// </summary>
    private static byte[] BuildSyntheticD64()
    {
        const int totalSectors = 683;
        const int sectorSize = 256;
        var image = new byte[totalSectors * sectorSize];   // 174848 bytes

        // ── BAM (track 18, sector 0) ─────────────────────────────────────────────────────
        var bamOffset = SectorOffset(18, 0);
        // disk name "TEST" at offset 0x90, padded with 0xa0 (shifted-space) for the remaining 12.
        for (int i = 0; i < 16; i++)
            image[bamOffset + 0x90 + i] = 0xa0;
        WriteAsciiUppercaseAsPetscii(image, bamOffset + 0x90, "TEST");
        // disk id "01" at offset 0xa2.
        WriteAsciiUppercaseAsPetscii(image, bamOffset + 0xa2, "01");

        // ── First directory sector (track 18, sector 1) ─────────────────────────────────
        var dirOffset = SectorOffset(18, 1);
        image[dirOffset + 0] = 0x00;   // no next directory sector
        image[dirOffset + 1] = 0xff;

        // Pick a file start track/sector inside the image (track 1, sector 0 = offset 0).
        // Track 1 only — directory entries reference track 0 to mean "no link".
        const byte fileStartTrack = 1;
        const byte fileStartSector = 0;

        // First directory entry begins at +2.
        const int entryOffset = 2;
        image[dirOffset + entryOffset + 0] = 0x82;   // PRG (file type 2) + closed bit (0x80)
        image[dirOffset + entryOffset + 1] = fileStartTrack;
        image[dirOffset + entryOffset + 2] = fileStartSector;
        // Filename (16 bytes starting at +3) padded with 0xa0.
        for (int i = 0; i < 16; i++)
            image[dirOffset + entryOffset + 3 + i] = 0xa0;
        WriteAsciiUppercaseAsPetscii(image, dirOffset + entryOffset + 3, TestFileName);
        // Blocks used (bytes 28-29, little-endian). One sector is plenty for our tiny PRG.
        image[dirOffset + entryOffset + 28] = 0x01;
        image[dirOffset + entryOffset + 29] = 0x00;

        // ── File sector (track 1, sector 0) ──────────────────────────────────────────────
        // First two bytes: next track / next sector. nextTrack=0 means "last sector"; in that
        // case nextSector is the count of payload bytes used in this sector.
        var fileOffset = SectorOffset(fileStartTrack, fileStartSector);
        image[fileOffset + 0] = 0x00;                                  // nextTrack = 0 (last)
        image[fileOffset + 1] = (byte)PrgFileBytes.Length;             // bytes used in this sector
        Array.Copy(PrgFileBytes, 0, image, fileOffset + 2, PrgFileBytes.Length);

        return image;
    }

    /// <summary>
    /// Byte offset for a given (1-based) track and (0-based) sector, summing the sectors-per-track
    /// table that the production parser uses. Mirrors <c>D64DiskImage.CalculateSectorOffset</c>.
    /// </summary>
    private static int SectorOffset(int track, int sector)
    {
        int offset = 0;
        for (int t = 1; t < track; t++)
            offset += Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.D64Parser.SectorsPerTrack[t] * 256;
        offset += sector * 256;
        return offset;
    }

    /// <summary>
    /// Writes uppercase ASCII letters (A-Z) and digits as raw bytes into the buffer. PETSCII codes
    /// 0x30-0x39 (digits) and 0x41-0x5A (uppercase) coincide with ASCII at the same codepoints,
    /// and the parser round-trips them via <c>PetsciiToAscii</c>.
    /// </summary>
    private static void WriteAsciiUppercaseAsPetscii(byte[] buffer, int offset, string text)
    {
        for (int i = 0; i < text.Length; i++)
            buffer[offset + i] = (byte)text[i];
    }
}
