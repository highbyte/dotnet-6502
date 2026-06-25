using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

namespace Highbyte.DotNet6502.Systems.Commodore64.Sharing;

/// <summary>What slice of C64 state a shareable link reproduces.</summary>
public enum C64ShareMode
{
    /// <summary>Paste the current BASIC program (optionally RUN it).</summary>
    CurrentBasic,

    /// <summary>Download &amp; run a program from a remote URL (.prg or .d64/.d64-in-zip).</summary>
    DownloadProgram,

    /// <summary>Download &amp; attach a .crt cartridge image from a remote URL.</summary>
    CartridgeImage,
}

/// <summary>
/// Typed input for <see cref="C64ShareLinkBuilder"/>: the data needed to build a shareable
/// startup link for one <see cref="C64ShareMode"/>.
/// </summary>
public sealed record C64ShareLinkRequest
{
    /// <summary>Which share mode this request serializes.</summary>
    public required C64ShareMode Mode { get; init; }

    /// <summary>C64 variant to start (e.g. <c>C64NTSC</c> / <c>C64PAL</c>); always emitted when set.</summary>
    public string? SystemVariant { get; init; }

    /// <summary>Queue RUN (BASIC) / run the loaded program after startup.</summary>
    public bool AutoRun { get; init; }

    /// <summary>When true, the runtime settings below are emitted into the link.</summary>
    public bool IncludeSettings { get; init; }

    /// <summary>Audio on/off (emitted only when <see cref="IncludeSettings"/>).</summary>
    public bool AudioEnabled { get; init; }

    /// <summary>Keyboard-joystick on/off (emitted only when <see cref="IncludeSettings"/>).</summary>
    public bool KeyboardJoystickEnabled { get; init; }

    /// <summary>Keyboard-joystick / active-joystick port 1 or 2 (emitted only when enabled).</summary>
    public int KeyboardJoystickNumber { get; init; } = 2;

    // --- CurrentBasic mode ---

    /// <summary>The BASIC source listing to paste (CurrentBasic mode).</summary>
    public string? BasicText { get; init; }

    // --- DownloadProgram mode ---

    /// <summary>Clean remote download URL — never proxied (DownloadProgram mode).</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Whether the download is a .prg or a .d64/.d64-in-zip (DownloadProgram mode).</summary>
    public C64DownloadProgramType DownloadType { get; init; }

    /// <summary>Direct-load this PRG from the .d64 (incl. <c>*</c> = first file); null = mount disk.</summary>
    public string? DirectLoadPRGName { get; init; }

    /// <summary>Optional exact ZIP entry name to extract for a zipped .d64 download.</summary>
    public string? D64ZipEntry { get; init; }

    // --- CartridgeImage mode ---

    /// <summary>Clean remote .crt cartridge-image URL — never proxied (CartridgeImage mode).</summary>
    public string? CartridgeUrl { get; init; }

    /// <summary>Optional exact ZIP entry name to extract for a zipped .crt download.</summary>
    public string? CartridgeZipEntry { get; init; }
}
