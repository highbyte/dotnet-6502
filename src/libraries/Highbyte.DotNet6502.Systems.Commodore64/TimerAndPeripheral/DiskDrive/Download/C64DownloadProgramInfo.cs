namespace Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

public class C64DownloadProgramInfo
{
    public string DisplayName { get; set; }
    public string DownloadUrl { get; set; }
    public List<string> RunCommands { get; set; }
    public C64DownloadProgramType DownloadType { get; set; }
    public bool KeyboardJoystickEnabled { get; set; }
    public int KeyboardJoystickNumber { get; set; }
    public bool RequiresBitmap { get; set; }
    public bool AudioEnabled { get; set; }
    public string? DirectLoadPRGName { get; set; }
    public string C64Variant { get; set; }
    public bool AvailableInBrowser { get; set; }

    public C64DownloadProgramInfo(
        string displayName,
        string downloadUrl,
        List<string>? runCommands = null,
        C64DownloadProgramType downloadType = C64DownloadProgramType.D64,
        bool keyboardJoystickEnabled = false,
        int keyboardJoystickNumber = 2,
        bool requiresBitmap = false,
        bool audioEnabled = false,
        string? directLoadPRGName = null,
        string c64Variant = "C64NTSC",
        bool availableInBrowser = true)
    {
        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        DownloadType = downloadType;
        KeyboardJoystickEnabled = keyboardJoystickEnabled;
        KeyboardJoystickNumber = keyboardJoystickNumber;
        RequiresBitmap = requiresBitmap;
        AudioEnabled = audioEnabled;
        DirectLoadPRGName = directLoadPRGName;
        C64Variant = c64Variant;
        AvailableInBrowser = availableInBrowser;
        RunCommands = runCommands ?? BuildDefaultRunCommands(downloadType, directLoadPRGName);
    }

    private static List<string> BuildDefaultRunCommands(
        C64DownloadProgramType downloadType,
        string? directLoadPRGName)
        => downloadType == C64DownloadProgramType.Prg || !string.IsNullOrEmpty(directLoadPRGName)
            ? new List<string> { "run" }
            : new List<string> { "load\"*\",8,1", "run" };
}

public enum C64DownloadProgramType
{
    D64,
    D64Zip,
    Prg,
}
