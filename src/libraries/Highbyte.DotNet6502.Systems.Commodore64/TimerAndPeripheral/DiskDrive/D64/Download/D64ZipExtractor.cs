using Highbyte.DotNet6502.Systems.Commodore64.Utils;
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
    /// <summary>True if the buffer starts with the ZIP local-file-header magic bytes.</summary>
    public static bool LooksLikeZip(ReadOnlySpan<byte> bytes)
        => ZipImageExtractor.LooksLikeZip(bytes);

    /// <summary>
    /// Returns raw .d64 bytes from <paramref name="bytes"/>: if the buffer is a ZIP archive, the
    /// first .d64 entry is extracted; otherwise the buffer is assumed to already be a .d64 and is
    /// returned unchanged.
    /// </summary>
    public static byte[] EnsureD64Bytes(byte[] bytes, ILogger? logger = null, string? entryName = null)
        => ZipImageExtractor.EnsureImageBytes(
            bytes,
            ".d64",
            ZipImageMultipleMatchBehavior.UseFirst,
            logger,
            entryName);

    /// <summary>Extracts the first <c>.d64</c> entry from an in-memory ZIP archive.</summary>
    public static byte[] ExtractFirstD64FromZip(byte[] zipBytes, ILogger? logger = null, string? entryName = null)
        => ZipImageExtractor.ExtractImageFromZip(
            zipBytes,
            ".d64",
            ZipImageMultipleMatchBehavior.UseFirst,
            logger,
            entryName);

    /// <summary>Extracts the first <c>.d64</c> entry from a ZIP archive stream.</summary>
    public static byte[] ExtractFirstD64FromZip(Stream zipStream, ILogger? logger = null, string? entryName = null)
        => ZipImageExtractor.ExtractImageFromZip(
            zipStream,
            ".d64",
            ZipImageMultipleMatchBehavior.UseFirst,
            logger,
            entryName);
}
