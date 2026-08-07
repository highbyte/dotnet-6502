namespace Highbyte.DotNet6502.Systems.Apple2.DiskImage.Download;

/// <summary>How a downloaded disk image is turned into a running program.</summary>
public enum Apple2DownloadRunMode
{
    /// <summary>
    /// Extract a file from the disk's DOS 3.3 catalog and inject it into RAM, with no drive
    /// involved. Only for RAM-resident programs: anything that reads the disk while running
    /// (DOS calls, level streaming) needs <see cref="BootDisk"/>.
    /// </summary>
    InjectFileIntoRam = 0,

    /// <summary>
    /// Put the image in the Disk II drive and boot it, exactly as the real machine would.
    /// Required for self-booting titles — most commercial games — which often have no DOS
    /// catalog at all, and for anything that reads the disk during play.
    /// </summary>
    BootDisk = 1,
}

/// <summary>
/// One entry in the Apple II "Download &amp; Run programs" list: a downloadable disk image and
/// how to run it. <see cref="RunMode"/> decides between injecting a catalog file into RAM and
/// booting the disk, so a single list can offer both kinds of title.
/// </summary>
public class Apple2DownloadProgramInfo
{
    public Apple2DownloadProgramInfo(
        string displayName,
        string downloadUrl,
        string fileName = "*",
        string? zipEntryName = null,
        Apple2DownloadRunMode runMode = Apple2DownloadRunMode.InjectFileIntoRam,
        bool keyboardJoystickEnabled = false)
    {
        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        FileName = fileName;
        ZipEntryName = zipEntryName;
        RunMode = runMode;
        KeyboardJoystickEnabled = keyboardJoystickEnabled;
    }

    public string DisplayName { get; }

    /// <summary>URL of the .dsk image (or a ZIP containing one, see <see cref="ZipEntryName"/>).</summary>
    public string DownloadUrl { get; }

    /// <summary>
    /// Catalog file to load and run; <c>"*"</c> picks the first runnable file (first Binary,
    /// else first Applesoft). Unused when <see cref="RunMode"/> is
    /// <see cref="Apple2DownloadRunMode.BootDisk"/>.
    /// </summary>
    public string FileName { get; }

    /// <summary>When the URL is a ZIP archive: the .dsk entry to extract.</summary>
    public string? ZipEntryName { get; }

    /// <summary>Whether the image is injected into RAM or booted in the drive.</summary>
    public Apple2DownloadRunMode RunMode { get; }

    /// <summary>Booting needs the optional Disk II boot ROM; injecting does not.</summary>
    public bool RequiresDisk2Rom => RunMode == Apple2DownloadRunMode.BootDisk;

    /// <summary>
    /// Whether to switch the keyboard joystick on for this program, as the C64 list does per entry.
    /// Set it for titles that read the game port, so they are playable without the user first
    /// having to work out that they need a joystick and where the setting lives.
    /// </summary>
    public bool KeyboardJoystickEnabled { get; }
}
