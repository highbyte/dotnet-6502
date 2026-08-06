namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage.Download;

/// <summary>
/// One entry in the Apple II "Download &amp; Run programs" list: a downloadable DOS 3.3 disk
/// image and which catalog file to load from it.
///
/// Only RAM-resident programs belong here — the machine has no Disk II emulation, so a program
/// that reads the disk at runtime (DOS calls, level streaming) will not work.
/// </summary>
public class Apple2DownloadProgramInfo
{
    public Apple2DownloadProgramInfo(
        string displayName,
        string downloadUrl,
        string fileName = "*",
        string? zipEntryName = null)
    {
        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        FileName = fileName;
        ZipEntryName = zipEntryName;
    }

    public string DisplayName { get; }

    /// <summary>URL of the .dsk image (or a ZIP containing one, see <see cref="ZipEntryName"/>).</summary>
    public string DownloadUrl { get; }

    /// <summary>
    /// Catalog file to load and run; <c>"*"</c> picks the first runnable file (first Binary,
    /// else first Applesoft).
    /// </summary>
    public string FileName { get; }

    /// <summary>When the URL is a ZIP archive: the .dsk entry to extract.</summary>
    public string? ZipEntryName { get; }
}
