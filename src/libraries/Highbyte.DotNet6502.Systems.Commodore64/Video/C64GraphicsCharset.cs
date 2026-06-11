namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Best-effort mapping of C64 <b>screen codes</b> to Unicode characters for the PETSCII block-graphics
/// glyphs (lines, corners, blocks, shades, card suits, etc.) that have no ASCII equivalent.
///
/// Used when rendering the C64 text screen with a plain font that has no C64/PETSCII glyphs (e.g. a
/// terminal), so screens that use graphic characters at least look <i>similar</i> instead of showing
/// garbage letters. It is not a pixel-perfect match.
///
/// Only valid for the built-in <b>uppercase/graphics</b> ROM charset (VIC charset base 0x1000, the
/// power-on default), where screen codes 0x40–0x7F are graphics. In the lowercase ROM charset those
/// codes are letters, and custom RAM charsets are arbitrary bitmaps — callers must not apply this map
/// there.
///
/// Where a glyph has a widely-supported Unicode equivalent (box drawing U+2500–257F, block elements
/// U+2580–259F, card suits, etc.) that is used. The handful of C64 glyphs that only map exactly to the
/// "Symbols for Legacy Computing" block (U+1FB00–U+1FBFF, poorly supported in terminal fonts) are
/// approximated with the nearest common character instead.
///
/// Glyph identities are from the standard C64 unshifted (uppercase/graphics) PETSCII set; see the
/// PETSCII reference at https://en.wikipedia.org/wiki/PETSCII.
/// </summary>
public static class C64GraphicsCharset
{
    // Indexed by screen code (0x00–0x7F). null = no graphics override (letter/digit/punctuation, or a
    // glyph better handled by the default screen-code -> PETSCII -> ASCII path).
    private static readonly string?[] s_screenCodeToUnicode = BuildMap();

    /// <summary>
    /// Returns the best-effort Unicode string for a graphic C64 screen code, or false if the screen
    /// code is not a graphics glyph (and should be rendered via the normal text mapping).
    /// </summary>
    public static bool TryGetUnicode(byte screenCode, out string unicode)
    {
        if (screenCode < s_screenCodeToUnicode.Length)
        {
            var mapped = s_screenCodeToUnicode[screenCode];
            if (mapped != null)
            {
                unicode = mapped;
                return true;
            }
        }
        unicode = string.Empty;
        return false;
    }

    private static string?[] BuildMap()
    {
        var map = new string?[0x80];

        // A few non-letter symbols outside the main graphics block (present in both ROM charsets).
        map[0x1C] = "£"; // £  pound
        map[0x1E] = "↑"; // ↑  up arrow
        map[0x1F] = "←"; // ←  left arrow

        // Main graphics block, screen codes 0x40–0x7F (uppercase/graphics charset).
        map[0x40] = "─"; // ─  horizontal line
        map[0x41] = "♠"; // ♠  spade
        map[0x42] = "│"; // │  vertical line (approx of U+1FB72)
        map[0x43] = "─"; // ─  horizontal line (approx of U+1FB78)
        map[0x44] = "─"; // ─  horizontal line (approx of U+1FB77)
        map[0x45] = "▔"; // ▔  upper line (approx of U+1FB76)
        map[0x46] = "│"; // │  vertical line (approx of U+1FB7A)
        map[0x47] = "▁"; // ▁  lower line (approx of U+1FB71)
        map[0x48] = "│"; // │  vertical line (approx of U+1FB74)
        map[0x49] = "╮"; // ╮  arc down-left
        map[0x4A] = "╰"; // ╰  arc up-right
        map[0x4B] = "╯"; // ╯  arc up-left
        map[0x4C] = "└"; // └  corner (approx of U+1FB7C)
        map[0x4D] = "╲"; // ╲  diagonal
        map[0x4E] = "╱"; // ╱  diagonal
        map[0x4F] = "┌"; // ┌  corner (approx of U+1FB7D)
        map[0x50] = "┐"; // ┐  corner (approx of U+1FB7E)
        map[0x51] = "●"; // ●  filled circle
        map[0x52] = "─"; // ─  horizontal line (approx of U+1FB7B)
        map[0x53] = "♥"; // ♥  heart
        map[0x54] = "│"; // │  vertical line (approx of U+1FB70)
        map[0x55] = "╭"; // ╭  arc down-right
        map[0x56] = "╳"; // ╳  diagonal cross
        map[0x57] = "○"; // ○  circle
        map[0x58] = "♣"; // ♣  club
        map[0x59] = "│"; // │  vertical line (approx of U+1FB75)
        map[0x5A] = "♦"; // ♦  diamond
        map[0x5B] = "┼"; // ┼  cross
        map[0x5C] = "▌"; // ▌  left half block (approx of U+1FB8C)
        map[0x5D] = "│"; // │  vertical line
        map[0x5E] = "π"; // π  pi
        map[0x5F] = "◥"; // ◥  upper-right triangle

        map[0x60] = " ";      //    blank (NBSP)
        map[0x61] = "▌"; // ▌  left half block
        map[0x62] = "▄"; // ▄  lower half block
        map[0x63] = "▔"; // ▔  upper one-eighth block
        map[0x64] = "▁"; // ▁  lower one-eighth block
        map[0x65] = "▏"; // ▏  left one-eighth block
        map[0x66] = "▒"; // ▒  medium shade
        map[0x67] = "▕"; // ▕  right one-eighth block
        map[0x68] = "▗"; // ▗  lower-right block (approx of U+1FB8F)
        map[0x69] = "◤"; // ◤  upper-left triangle
        map[0x6A] = "▕"; // ▕  right block (approx of U+1FB87)
        map[0x6B] = "├"; // ├  tee right
        map[0x6C] = "▗"; // ▗  lower-right quadrant
        map[0x6D] = "└"; // └  corner
        map[0x6E] = "┐"; // ┐  corner
        map[0x6F] = "▂"; // ▂  lower one-quarter block
        map[0x70] = "┌"; // ┌  corner
        map[0x71] = "┴"; // ┴  tee up
        map[0x72] = "┬"; // ┬  tee down
        map[0x73] = "┤"; // ┤  tee left
        map[0x74] = "▎"; // ▎  left one-quarter block
        map[0x75] = "▍"; // ▍  left three-eighths block
        map[0x76] = "▐"; // ▐  right half block (approx of U+1FB88)
        map[0x77] = "▀"; // ▀  upper block (approx of U+1FB82)
        map[0x78] = "▀"; // ▀  upper block (approx of U+1FB83)
        map[0x79] = "▃"; // ▃  lower three-eighths block
        map[0x7A] = "┘"; // ┘  corner (approx of U+1FB7F)
        map[0x7B] = "▖"; // ▖  lower-left quadrant
        map[0x7C] = "▝"; // ▝  upper-right quadrant
        map[0x7D] = "┘"; // ┘  corner
        map[0x7E] = "▘"; // ▘  upper-left quadrant
        map[0x7F] = "▚"; // ▚  upper-left + lower-right quadrants

        return map;
    }
}
