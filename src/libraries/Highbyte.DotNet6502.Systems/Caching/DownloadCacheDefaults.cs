namespace Highbyte.DotNet6502.Systems.Caching;

/// <summary>
/// Shared defaults for download cache backends.
/// </summary>
public static class DownloadCacheDefaults
{
    /// <summary>Default cap on total cached content size (256 MiB).</summary>
    public const long MaxTotalBytes = 256L * 1024 * 1024;
}
