using System.Globalization;
using Highbyte.DotNet6502.Systems.Input;

namespace Highbyte.DotNet6502.Systems.Commodore64.Input;

/// <summary>
/// Resolves a raw native keyboard-layout identifier (from <see cref="KeyboardLayoutDetector"/>,
/// or fingerprinted by a browser host) — or an OS culture — to a <see cref="C64KeyboardLayout"/>.
///
/// Only layouts the C64 keyboard mapping actually has a punctuation map for are recognised;
/// anything else resolves to <c>null</c> so the caller can fall through to the next option.
/// </summary>
public static class KeyboardLayoutResolver
{
    private const string MacInputSourcePrefix = "com.apple.keylayout.";

    /// <summary>
    /// Maps a raw native layout id to a <see cref="C64KeyboardLayout"/>, or <c>null</c> when the
    /// id is empty, unrecognised, or names a layout with no C64-specific map. Recognises Windows
    /// KLIDs, macOS input-source ids, and a plain layout name (used by the browser host).
    /// </summary>
    public static C64KeyboardLayout? FromNativeLayoutId(string? nativeLayoutId)
    {
        if (string.IsNullOrWhiteSpace(nativeLayoutId))
            return null;
        nativeLayoutId = nativeLayoutId.Trim();

        // Windows: KLID is 8 hex digits; the low 4 are the language id (0409 = US, 041D = Swedish).
        if (nativeLayoutId.Length == 8 && nativeLayoutId.All(Uri.IsHexDigit))
        {
            return int.Parse(nativeLayoutId.Substring(4), NumberStyles.HexNumber) switch
            {
                0x041D => C64KeyboardLayout.Swedish,
                0x0409 => C64KeyboardLayout.US,
                _ => null,
            };
        }

        // macOS: input-source id "com.apple.keylayout.<Name>".
        if (nativeLayoutId.StartsWith(MacInputSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = nativeLayoutId.Substring(MacInputSourcePrefix.Length);
            if (name.Contains("Swedish", StringComparison.OrdinalIgnoreCase))
                return C64KeyboardLayout.Swedish;
            if (name.StartsWith("US", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ABC", StringComparison.OrdinalIgnoreCase))
                return C64KeyboardLayout.US;
            return null;
        }

        // Browser fingerprint / diagnostic token: a plain C64KeyboardLayout name.
        if (Enum.TryParse<C64KeyboardLayout>(nativeLayoutId, ignoreCase: true, out var layout))
            return layout;

        return null;
    }

    /// <summary>
    /// Maps an OS culture to a <see cref="C64KeyboardLayout"/> as a last-resort fallback when the
    /// physical layout cannot be detected. Inaccurate by nature (culture is the UI/region
    /// language, not the keyboard) — returns <c>null</c> for unmapped cultures.
    /// </summary>
    public static C64KeyboardLayout? FromCulture(CultureInfo culture)
    {
        return culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "sv" => C64KeyboardLayout.Swedish,
            "en" => C64KeyboardLayout.US,
            _ => null,
        };
    }
}
