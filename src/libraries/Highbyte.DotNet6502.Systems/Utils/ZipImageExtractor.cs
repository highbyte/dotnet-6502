using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Highbyte.DotNet6502.Systems.Commodore64.Utils;

/// <summary>
/// Controls what to do when a ZIP archive contains more than one entry matching the requested
/// image-file extension.
/// </summary>
public enum ZipImageMultipleMatchBehavior
{
    /// <summary>Extract the first matching entry in the ZIP archive's entry order.</summary>
    UseFirst,

    /// <summary>Reject the ZIP archive as ambiguous.</summary>
    Throw,
}

/// <summary>
/// Helpers for turning downloaded bytes into raw image bytes, transparently handling the case
/// where the bytes are actually a ZIP archive containing a matching image file.
/// </summary>
public static class ZipImageExtractor
{
    // Local file header signature for a ZIP archive: "PK\x03\x04".
    private static readonly byte[] ZipLocalFileHeaderSignature = { 0x50, 0x4B, 0x03, 0x04 };

    /// <summary>True if the buffer starts with the ZIP local-file-header magic bytes.</summary>
    public static bool LooksLikeZip(ReadOnlySpan<byte> bytes)
        => bytes.Length >= ZipLocalFileHeaderSignature.Length
           && bytes[..ZipLocalFileHeaderSignature.Length].SequenceEqual(ZipLocalFileHeaderSignature);

    /// <summary>
    /// Returns raw image bytes from <paramref name="bytes"/>: if the buffer is a ZIP archive, a
    /// matching entry is extracted; otherwise the buffer is assumed to already be the image and is
    /// returned unchanged.
    /// </summary>
    public static byte[] EnsureImageBytes(
        byte[] bytes,
        string extension,
        ZipImageMultipleMatchBehavior multipleMatchBehavior,
        ILogger? logger = null,
        string? entryName = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (!LooksLikeZip(bytes))
        {
            if (!string.IsNullOrWhiteSpace(entryName))
                throw new InvalidOperationException("A ZIP entry was specified, but the supplied bytes are not a ZIP archive.");
            return bytes;
        }

        var normalizedExtension = NormalizeExtension(extension);
        logger?.LogInformation(
            "Image bytes are a ZIP archive ({ByteCount} bytes); extracting {Extension}.",
            bytes.Length,
            normalizedExtension);

        return ExtractImageFromZip(bytes, normalizedExtension, multipleMatchBehavior, logger, entryName);
    }

    /// <summary>Extracts a matching image entry from an in-memory ZIP archive.</summary>
    public static byte[] ExtractImageFromZip(
        byte[] zipBytes,
        string extension,
        ZipImageMultipleMatchBehavior multipleMatchBehavior,
        ILogger? logger = null,
        string? entryName = null)
    {
        ArgumentNullException.ThrowIfNull(zipBytes);

        using var zipStream = new MemoryStream(zipBytes, writable: false);
        return ExtractImageFromZip(zipStream, extension, multipleMatchBehavior, logger, entryName);
    }

    /// <summary>Extracts a matching image entry from a ZIP archive stream.</summary>
    public static byte[] ExtractImageFromZip(
        Stream zipStream,
        string extension,
        ZipImageMultipleMatchBehavior multipleMatchBehavior,
        ILogger? logger = null,
        string? entryName = null)
    {
        ArgumentNullException.ThrowIfNull(zipStream);

        var normalizedExtension = NormalizeExtension(extension);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var (imageEntry, matchCount) = string.IsNullOrWhiteSpace(entryName)
            ? FindEntryByExtension(archive, normalizedExtension, multipleMatchBehavior)
            : FindEntryByName(archive, normalizedExtension, entryName);

        if (imageEntry.Length > int.MaxValue)
            throw new InvalidOperationException(
                $"The ZIP entry '{imageEntry.FullName}' is too large to extract into memory ({imageEntry.Length} bytes).");

        if (matchCount > 1)
        {
            logger?.LogInformation(
                "Found {MatchCount} {Extension} files in ZIP; using first entry: {EntryName}.",
                matchCount,
                normalizedExtension,
                imageEntry.FullName);
        }
        else
        {
            logger?.LogInformation(
                "Found {Extension} file in ZIP: {EntryName}, size: {ByteCount} bytes.",
                normalizedExtension,
                imageEntry.FullName,
                imageEntry.Length);
        }

        // Pre-allocate with the exact uncompressed size and read the entry fully.
        var imageBytes = new byte[imageEntry.Length];
        using var entryStream = imageEntry.Open();
        entryStream.ReadExactly(imageBytes);

        logger?.LogInformation(
            "Extracted {Extension} file: {ByteCount} bytes.",
            normalizedExtension,
            imageBytes.Length);
        return imageBytes;
    }

    private static (ZipArchiveEntry Entry, int MatchCount) FindEntryByExtension(
        ZipArchive archive,
        string normalizedExtension,
        ZipImageMultipleMatchBehavior multipleMatchBehavior)
    {
        var matches = archive.Entries
            .Where(entry => entry.Name.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException($"No {normalizedExtension} file found in the ZIP archive");

        if (matches.Count > 1 && multipleMatchBehavior == ZipImageMultipleMatchBehavior.Throw)
        {
            var names = string.Join(", ", matches.Select(entry => entry.FullName));
            throw new InvalidOperationException(
                $"Multiple {normalizedExtension} files found in the ZIP archive; refusing to choose automatically: {names}");
        }

        return (matches[0], matches.Count);
    }

    private static (ZipArchiveEntry Entry, int MatchCount) FindEntryByName(
        ZipArchive archive,
        string normalizedExtension,
        string entryName)
    {
        var normalizedEntryName = NormalizeEntryName(entryName);
        if (!normalizedEntryName.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ZIP entry '{normalizedEntryName}' must be a {normalizedExtension} file.");
        }

        var entry = archive.GetEntry(normalizedEntryName)
            ?? throw new InvalidOperationException($"ZIP entry '{normalizedEntryName}' was not found in the archive.");

        if (string.IsNullOrEmpty(entry.Name))
            throw new InvalidOperationException($"ZIP entry '{normalizedEntryName}' is a directory, not a file.");

        return (entry, 1);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Image extension must be supplied.", nameof(extension));

        extension = extension.Trim();
        return extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : "." + extension;
    }

    private static string NormalizeEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("ZIP entry name must be supplied.", nameof(entryName));

        return entryName.Trim().Replace('\\', '/').TrimStart('/');
    }
}
