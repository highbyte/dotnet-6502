namespace Highbyte.DotNet6502.Systems;

/// <summary>
/// Where a single ROM image can be downloaded from, and how to turn the response into raw ROM
/// bytes.
///
/// Most ROMs are published as a bare file, so <see cref="Url"/> alone is enough. Some are only
/// distributed inside a ZIP archive — the Apple II character generator, for example, exists only
/// as an entry in a multi-file archive — in which case <see cref="ZipEntryName"/> names the entry
/// to extract.
/// </summary>
/// <param name="Url">The original source URL. Also the cache key, never a CORS-proxied variant.</param>
/// <param name="ZipEntryName">
/// Entry to extract when the download is a ZIP archive, e.g. <c>3410036.BIN</c>. Null for a bare
/// file. Naming the entry (rather than matching on extension) matters when the archive holds many
/// candidates.
/// </param>
/// <param name="FileName">
/// Name to save the ROM under when downloading to disk. Defaults to the ZIP entry's name, or the
/// file name in <see cref="Url"/> — deriving it from the URL alone would save a ZIP archive's name
/// for an extracted entry.
/// </param>
public sealed record RomDownloadSource(string Url, string? ZipEntryName = null, string? FileName = null)
{
    /// <summary>Whether the download must be unpacked before it is usable ROM data.</summary>
    public bool IsZipArchive => !string.IsNullOrWhiteSpace(ZipEntryName);

    /// <summary>File name to store the ROM under on disk.</summary>
    public string ResolveFileName()
    {
        if (!string.IsNullOrWhiteSpace(FileName))
            return FileName;

        if (!string.IsNullOrWhiteSpace(ZipEntryName))
            return Path.GetFileName(ZipEntryName.Replace('\\', '/'));

        return Path.GetFileName(new Uri(Url).LocalPath);
    }

    /// <summary>
    /// Cache key. A single archive can supply several different ROMs, so the entry name has to be
    /// part of the key — keying on the URL alone would make them collide.
    /// </summary>
    public string ResolveCacheKey()
        => IsZipArchive ? $"{Url}#{ZipEntryName}" : Url;

    /// <summary>Bare file extension of the resulting artifact, for cache metadata.</summary>
    public string ResolveExtension()
        => Path.GetExtension(ResolveFileName()).TrimStart('.').ToLowerInvariant();
}
