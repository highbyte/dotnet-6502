using Highbyte.DotNet6502.Systems.Oric.Input;

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
        string? zipEntryName = null,
        OricJoystickInterface joystickInterface = OricJoystickInterface.None,
        bool keyboardJoystickEnabled = false,
        int keyboardJoystickNumber = 1)
    {
        if (keyboardJoystickNumber is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyboardJoystickNumber),
                keyboardJoystickNumber,
                "Oric joystick must be 1 or 2.");
        }

        DisplayName = displayName;
        DownloadUrl = downloadUrl;
        ZipEntryName = zipEntryName;
        JoystickInterface = joystickInterface;
        KeyboardJoystickEnabled = keyboardJoystickEnabled;
        KeyboardJoystickNumber = keyboardJoystickNumber;
    }

    public string DisplayName { get; }
    public string DownloadUrl { get; }

    /// <summary>When the URL is a ZIP archive: the <c>.tap</c> entry to extract.</summary>
    public string? ZipEntryName { get; }

    /// <summary>The printer-port joystick interface to expose while running the program.</summary>
    public OricJoystickInterface JoystickInterface { get; }

    /// <summary>Whether W/A/S/D and Space should drive the selected joystick port.</summary>
    public bool KeyboardJoystickEnabled { get; }

    /// <summary>The adapter port driven by both the host gamepad and keyboard joystick.</summary>
    public int KeyboardJoystickNumber { get; }
}
