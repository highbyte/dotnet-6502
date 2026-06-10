using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.D64.Download;

/// <summary>
/// Helpers for turning downloaded bytes into raw .d64 disk-image bytes, transparently handling
/// the case where the bytes are actually a ZIP archive containing a .d64.
/// </summary>
/// <remarks>
/// Used by both the menu download flow (<see cref="D64Downloader"/>) and the URL-driven
/// automated-startup flow (which fetches bytes without knowing the declared download type, so it
/// must content-sniff). The two callers must agree on what "a .d64" looks like, hence the shared
/// helper.
/// </remarks>
public static class D64ZipExtractor
{
    // Local file header signature for a ZIP archive: "PK\x03\x04".
    private static readonly byte[] ZipLocalFileHeaderSignature = { 0x50, 0x4B, 0x03, 0x04 };

    /// <summary>True if the buffer starts with the ZIP local-file-header magic bytes.</summary>
    public static bool LooksLikeZip(ReadOnlySpan<byte> bytes)
        => bytes.Length >= ZipLocalFileHeaderSignature.Length
           && bytes[..ZipLocalFileHeaderSignature.Length].SequenceEqual(ZipLocalFileHeaderSignature);

    /// <summary>
    /// Returns raw .d64 bytes from <paramref name="bytes"/>: if the buffer is a ZIP archive, the
    /// first .d64 entry is extracted; otherwise the buffer is assumed to already be a .d64 and is
    /// returned unchanged.
    /// </summary>
    public static byte[] EnsureD64Bytes(byte[] bytes, ILogger? logger = null)
    {
        if (!LooksLikeZip(bytes))
            return bytes;

        logger?.LogInformation("Downloaded bytes are a ZIP archive ({ByteCount} bytes); extracting .d64.", bytes.Length);
        return ExtractFirstD64FromZip(bytes, logger);
    }

    /// <summary>Extracts the first <c>.d64</c> entry from an in-memory ZIP archive.</summary>
    public static byte[] ExtractFirstD64FromZip(byte[] zipBytes, ILogger? logger = null)
    {
        using var zipStream = new MemoryStream(zipBytes, writable: false);
        return ExtractFirstD64FromZip(zipStream, logger);
    }

    /// <summary>Extracts the first <c>.d64</c> entry from a ZIP archive stream.</summary>
    public static byte[] ExtractFirstD64FromZip(Stream zipStream, ILogger? logger = null)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var d64Entry = archive.Entries.FirstOrDefault(
                entry => entry.Name.EndsWith(".d64", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No .d64 file found in the ZIP archive");

        logger?.LogInformation("Found .d64 file in ZIP: {EntryName}, size: {ByteCount} bytes", d64Entry.Name, d64Entry.Length);

        // Pre-allocate with the exact uncompressed size and read the entry fully.
        var d64Bytes = new byte[d64Entry.Length];
        using var entryStream = d64Entry.Open();
        entryStream.ReadExactly(d64Bytes);

        logger?.LogInformation("Extracted .d64 file: {ByteCount} bytes", d64Bytes.Length);
        return d64Bytes;
    }
}
