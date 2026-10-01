namespace Highbyte.DotNet6502.Systems.Input;

/// <summary>Character mappings for the Commodore matrix and US ASCII keyboard positions.</summary>
public static class KeyboardTextMapping
{
    private static readonly Dictionary<char, string[]> CommodoreSymbols = new()
    {
        [' '] = ["space"], ['\r'] = ["return"], ['\b'] = ["delete"], ['\x1b'] = ["stop"],
        ['+'] = ["+"], ['-'] = ["-"], ['*'] = ["*"], ['/'] = ["/"],
        [':'] = [":"], [';'] = [";"], ['='] = ["="], ['.'] = ["."], [','] = [","],
        ['@'] = ["@"], ['£'] = ["lira"], ['↑'] = ["rightarrow"], ['←'] = ["leftarrow"],
        ['!'] = ["lshift", "1"], ['"'] = ["lshift", "2"], ['#'] = ["lshift", "3"],
        ['$'] = ["lshift", "4"], ['%'] = ["lshift", "5"], ['&'] = ["lshift", "6"],
        ['\''] = ["lshift", "7"], ['('] = ["lshift", "8"], [')'] = ["lshift", "9"],
        ['?'] = ["lshift", "/"], ['<'] = ["lshift", ","], ['>'] = ["lshift", "."],
        ['['] = ["lshift", ":"], [']'] = ["lshift", ";"],
    };

    private static readonly Dictionary<char, HostKey> AsciiPlainSymbols = new()
    {
        [' '] = HostKey.Space, ['\r'] = HostKey.Enter, ['\b'] = HostKey.Backspace,
        ['\x1b'] = HostKey.Escape, ['\t'] = HostKey.Tab,
        ['-'] = HostKey.Minus, ['='] = HostKey.Equal,
        ['['] = HostKey.BracketLeft, [']'] = HostKey.BracketRight, ['\\'] = HostKey.Backslash,
        [';'] = HostKey.Semicolon, ['\''] = HostKey.Quote,
        [','] = HostKey.Comma, ['.'] = HostKey.Period, ['/'] = HostKey.Slash, ['`'] = HostKey.Backquote,
    };

    private static readonly Dictionary<char, HostKey> AsciiShiftedSymbols = new()
    {
        ['!'] = HostKey.Digit1, ['@'] = HostKey.Digit2, ['#'] = HostKey.Digit3,
        ['$'] = HostKey.Digit4, ['%'] = HostKey.Digit5, ['^'] = HostKey.Digit6,
        ['&'] = HostKey.Digit7, ['*'] = HostKey.Digit8, ['('] = HostKey.Digit9, [')'] = HostKey.Digit0,
        ['_'] = HostKey.Minus, ['+'] = HostKey.Equal,
        ['{'] = HostKey.BracketLeft, ['}'] = HostKey.BracketRight, ['|'] = HostKey.Backslash,
        [':'] = HostKey.Semicolon, ['"'] = HostKey.Quote,
        ['<'] = HostKey.Comma, ['>'] = HostKey.Period, ['?'] = HostKey.Slash, ['~'] = HostKey.Backquote,
    };

    public static IReadOnlyList<string>? CommodoreKeys(char character)
    {
        // Unshifted letters follow the machine's active character set. Shifted letters
        // would produce graphics in its default uppercase/graphics mode.
        if (char.IsAsciiLetterOrDigit(character))
            return [char.ToLowerInvariant(character).ToString()];
        return CommodoreSymbols.GetValueOrDefault(character);
    }

    public static IReadOnlyList<HostKey>? AsciiKeys(char character, bool uppercaseOnly = false)
    {
        if (char.IsAsciiLetter(character))
        {
            var key = HostKey.KeyA + (char.ToUpperInvariant(character) - 'A');
            return char.IsAsciiLetterUpper(character) && !uppercaseOnly ? [HostKey.ShiftLeft, key] : [key];
        }
        if (char.IsAsciiDigit(character))
        {
            return [HostKey.Digit0 + (character - '0')];
        }
        if (AsciiPlainSymbols.TryGetValue(character, out var plain))
            return [plain];
        if (AsciiShiftedSymbols.TryGetValue(character, out var shifted))
            return [HostKey.ShiftLeft, shifted];
        return null;
    }
}
