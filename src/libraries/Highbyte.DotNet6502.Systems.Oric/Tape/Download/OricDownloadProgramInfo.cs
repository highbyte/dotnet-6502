namespace Highbyte.DotNet6502.Systems.Oric.Tape.Download;

/// <summary>
/// One entry in the Oric "Download &amp; Run programs" list: a byte-level TAP image, optionally
/// stored inside a ZIP archive.
/// </summary>
public sealed class OricDownloadProgramInfo
{
    public OricDownloadProgramInfo(
        string displayName,
        string downloadUrl,
        string? zipEntryName = null)
    {
        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        ZipEntryName = zipEntryName;
    }

    public string DisplayName { get; }
    public string DownloadUrl { get; }

    /// <summary>When the URL is a ZIP archive: the <c>.tap</c> entry to extract.</summary>
    public string? ZipEntryName { get; }
}
