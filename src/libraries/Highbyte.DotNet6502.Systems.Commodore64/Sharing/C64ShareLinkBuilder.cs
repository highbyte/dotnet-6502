using System.Text;
using Highbyte.DotNet6502.Systems.Commodore64.TimerAndPeripheral.DiskDrive.Download;

namespace Highbyte.DotNet6502.Systems.Commodore64.Sharing;

/// <summary>
/// Serializes a slice of the current C64 emulator state into a shareable URL that drives the
/// existing URL-query automated-startup contract (the inverse of the query parser in the Avalonia
/// Browser app / <c>C64AvaloniaStartupParticipant</c>).
/// </summary>
/// <remarks>
/// Pure and host-agnostic so it can be unit-tested and reused. The caller supplies the app base
/// URL (origin + path) and a typed <see cref="C64ShareLinkRequest"/>; this builds the full link.
/// It never embeds a CORS proxy — the running emulator applies that at fetch time.
/// </remarks>
public static class C64ShareLinkBuilder
{
    /// <summary>The query-parameter keys this builder emits — the public share-link contract.</summary>
    public static class QueryKeys
    {
        public const string System = "system";
        public const string SystemVariant = "systemVariant";
        public const string Start = "start";
        public const string WaitForSystemReady = "waitForSystemReady";
        public const string BasicText = "basicText";
        public const string RunBasic = "runBasic";
        public const string LoadPrgUrl = "loadPrgUrl";
        public const string LoadD64Url = "loadD64Url";
        public const string LoadCrtUrl = "loadCrtUrl";
        public const string D64Program = "d64Program";
        public const string DiskMount = "diskMount";
        public const string RunLoadedProgram = "runLoadedProgram";
        public const string AudioEnabled = "audioEnabled";
        public const string KeyboardJoystickEnabled = "keyboardJoystickEnabled";
        public const string KeyboardJoystickNumber = "keyboardJoystickNumber";
    }

    /// <summary>
    /// Builds the full shareable URL for <paramref name="request"/>, prefixed with
    /// <paramref name="baseUrl"/> (e.g. <c>https://host/path/</c>). Throws
    /// <see cref="ArgumentException"/> if the request is missing data required for its mode.
    /// </summary>
    public static string Build(string baseUrl, C64ShareLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = BuildQuery(request);
        var trimmedBase = baseUrl ?? string.Empty;
        return $"{trimmedBase}?{query}";
    }

    /// <summary>Builds just the query string (no leading <c>?</c>) for <paramref name="request"/>.</summary>
    public static string BuildQuery(C64ShareLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pairs = new List<(string Key, string Value)>
        {
            (QueryKeys.System, C64.SystemName),
        };

        if (!string.IsNullOrWhiteSpace(request.SystemVariant))
            pairs.Add((QueryKeys.SystemVariant, request.SystemVariant));

        // Every share mode starts the system. BASIC/PRG/D64 flows also wait for BASIC to be ready.
        // Cartridge images reset/boot the machine when attached, so they intentionally do not
        // require waitForSystemReady.
        pairs.Add((QueryKeys.Start, "1"));
        if (request.Mode != C64ShareMode.CartridgeImage)
            pairs.Add((QueryKeys.WaitForSystemReady, "1"));

        switch (request.Mode)
        {
            case C64ShareMode.CurrentBasic:
                AddBasicParams(pairs, request);
                break;
            case C64ShareMode.DownloadProgram:
                AddDownloadProgramParams(pairs, request);
                break;
            case C64ShareMode.CartridgeImage:
                AddCartridgeImageParams(pairs, request);
                break;
            default:
                throw new ArgumentException($"Unknown share mode '{request.Mode}'.", nameof(request));
        }

        if (request.IncludeSettings)
            AddSettingsParams(pairs, request);

        var sb = new StringBuilder();
        foreach (var (key, value) in pairs)
        {
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }
        return sb.ToString();
    }

    private static void AddBasicParams(List<(string, string)> pairs, C64ShareLinkRequest request)
    {
        if (string.IsNullOrEmpty(request.BasicText))
            throw new ArgumentException("CurrentBasic share mode requires BasicText.", nameof(request));

        pairs.Add((QueryKeys.BasicText, Base64UrlEncode(request.BasicText)));
        if (request.AutoRun)
            pairs.Add((QueryKeys.RunBasic, "1"));
    }

    private static void AddDownloadProgramParams(List<(string, string)> pairs, C64ShareLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DownloadUrl))
            throw new ArgumentException("DownloadProgram share mode requires DownloadUrl.", nameof(request));

        if (request.DownloadType == C64DownloadProgramType.Prg)
        {
            pairs.Add((QueryKeys.LoadPrgUrl, request.DownloadUrl));
        }
        else
        {
            // .d64 / .d64-in-zip: the automation path content-sniffs the bytes. Direct-load a
            // named PRG when one is given (incl. "*" = first file), otherwise mount the disk.
            pairs.Add((QueryKeys.LoadD64Url, request.DownloadUrl));
            if (!string.IsNullOrEmpty(request.DirectLoadPRGName))
                pairs.Add((QueryKeys.D64Program, request.DirectLoadPRGName));
            else
                pairs.Add((QueryKeys.DiskMount, "1"));
        }

        if (request.AutoRun)
            pairs.Add((QueryKeys.RunLoadedProgram, "1"));
    }

    private static void AddCartridgeImageParams(List<(string, string)> pairs, C64ShareLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CartridgeUrl))
            throw new ArgumentException("CartridgeImage share mode requires CartridgeUrl.", nameof(request));

        pairs.Add((QueryKeys.LoadCrtUrl, request.CartridgeUrl));
    }

    private static void AddSettingsParams(List<(string, string)> pairs, C64ShareLinkRequest request)
    {
        pairs.Add((QueryKeys.AudioEnabled, request.AudioEnabled ? "1" : "0"));
        pairs.Add((QueryKeys.KeyboardJoystickEnabled, request.KeyboardJoystickEnabled ? "1" : "0"));

        // The keyboard-joystick number implies "enabled" on the consuming side and also sets the
        // active joystick to the same port, so only emit it when the keyboard joystick is enabled.
        if (request.KeyboardJoystickEnabled)
            pairs.Add((QueryKeys.KeyboardJoystickNumber, request.KeyboardJoystickNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Encodes UTF-8 text as base64url (RFC 4648 §5, padding stripped).</summary>
    private static string Base64UrlEncode(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
