using System.Globalization;

namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>
/// Resolves a raw native keyboard-layout identifier (from <see cref="KeyboardLayoutDetector"/>, or
/// fingerprinted by a browser host) — or an OS culture — to a <see cref="HostKeyboardLayout"/>.
///
/// The identifier formats are a property of the host platform, not of any emulated system, so this
/// lives beside the detector and is shared. Anything unrecognised resolves to <c>null</c> so the
/// caller can fall through to the next option in its own resolution chain.
/// </summary>
public static class HostKeyboardLayoutResolver
{
    private const string MacInputSourcePrefix = "com.apple.keylayout.";

    /// <summary>
    /// Maps a raw native layout id to a <see cref="HostKeyboardLayout"/>, or <c>null</c> when the
    /// id is empty or names a layout with no specific map. Recognises Windows KLIDs, macOS
    /// input-source ids, and a plain layout name (used by the browser host).
    /// </summary>
    public static HostKeyboardLayout? FromNativeLayoutId(string? nativeLayoutId)
    {
        if (string.IsNullOrWhiteSpace(nativeLayoutId))
            return null;
        nativeLayoutId = nativeLayoutId.Trim();

        // Windows: KLID is 8 hex digits; the low 4 are the language id (0409 = US, 041D = Swedish).
        if (nativeLayoutId.Length == 8 && nativeLayoutId.All(Uri.IsHexDigit))
        {
            return int.Parse(nativeLayoutId.Substring(4), NumberStyles.HexNumber) switch
            {
                0x041D => HostKeyboardLayout.Swedish,
                0x0409 => HostKeyboardLayout.US,
                _ => null,
            };
        }

        // macOS: input-source id "com.apple.keylayout.<Name>".
        if (nativeLayoutId.StartsWith(MacInputSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = nativeLayoutId.Substring(MacInputSourcePrefix.Length);
            if (name.Contains("Swedish", StringComparison.OrdinalIgnoreCase))
                return HostKeyboardLayout.Swedish;
            if (name.StartsWith("US", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ABC", StringComparison.OrdinalIgnoreCase))
                return HostKeyboardLayout.US;
            return null;
        }

        // Browser fingerprint / diagnostic token: a plain HostKeyboardLayout name.
        if (Enum.TryParse<HostKeyboardLayout>(nativeLayoutId, ignoreCase: true, out var layout))
            return layout;

        return null;
    }

    /// <summary>
    /// Maps an OS culture to a <see cref="HostKeyboardLayout"/> as a last-resort fallback when the
    /// physical layout cannot be detected. Inaccurate by nature (culture is the UI/region language,
    /// not the keyboard) — returns <c>null</c> for unmapped cultures.
    /// </summary>
    public static HostKeyboardLayout? FromCulture(CultureInfo culture)
    {
        return culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "sv" => HostKeyboardLayout.Swedish,
            "en" => HostKeyboardLayout.US,
            _ => null,
        };
    }
}
