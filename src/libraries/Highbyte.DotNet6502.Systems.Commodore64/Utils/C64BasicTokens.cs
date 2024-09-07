namespace Highbyte.DotNet6502.Systems.Commodore64.Utils;

/// <summary>
/// Tokens used by C64 Basic program in binary (token) format.
/// 
/// Based on: https://github.com/abbrev/prg-tools/blob/master/src/tokens.c
/// </summary>
public static class C64BasicTokens
{
    /// <summary>
    /// Dictionary that maps C64 token byte values to their string representation.
    /// </summary>
    public static Dictionary<byte, string> Tokens = new Dictionary<byte, string>
    {
        { 0x80, "END" },
        { 0x81, "FOR" },
        { 0x82, "NEXT" },
        { 0x83, "DATA" },
        { 0x84, "INPUT#" },
        { 0x85, "INPUT" },
        { 0x86, "DIM" },
        { 0x87, "READ" },

        { 0x88, "LET" },
        { 0x89, "GOTO" },
        { 0x8A, "RUN" },
        { 0x8B, "IF" },
        { 0x8C, "RESTORE" },
        { 0x8D, "GOSUB" },
        { 0x8E, "RETURN" },
        { 0x8F, "REM" },

        { 0x90, "STOP" },
        { 0x91, "ON" },
        { 0x92, "WAIT" },
        { 0x93, "LOAD" },
        { 0x94, "SAVE" },
        { 0x95, "VERIFY" },
        { 0x96, "DEF" },
        { 0x97, "POKE" },

        { 0x98, "PRINT#" },
        { 0x99, "PRINT" },
        { 0x9A, "CONT" },
        { 0x9B, "LIST" },
        { 0x9C, "CLR" },
        { 0x9D, "CMD" },
        { 0x9E, "SYS" },
        { 0x9F, "OPEN" },

        { 0xA0, "CLOSE" },
        { 0xA1, "GET" },
        { 0xA2, "NEW" },
        { 0xA3, "TAB(" },
        { 0xA4, "TO" },
        { 0xA5, "FN" },
        { 0xA6, "SPC(" },
        { 0xA7, "THEN" },

        { 0xA8, "NOT" },
        { 0xA9, "STEP" },
        { 0xAA, "+" },
        { 0xAB, "-" },
        { 0xAC, "*" },
        { 0xAD, "/" },
        { 0xAE, "^" },
        { 0xAF, "AND" },

        { 0xB0, "OR" },
        { 0xB1, ">" },
        { 0xB2, "=" },
        { 0xB3, "<" },
        { 0xB4, "SGN" },
        { 0xB5, "INT" },
        { 0xB6, "ABS" },
        { 0xB7, "USR" },

        { 0xB8, "FRE" },
        { 0xB9, "POS" },
        { 0xBA, "SQR" },
        { 0xBB, "RND" },
        { 0xBC, "LOG" },
        { 0xBD, "EXP" },
        { 0xBE, "COS" },
        { 0xBF, "SIN" },

        { 0xC0, "TAN" },
        { 0xC1, "ATN" },
        { 0xC2, "PEEK" },
        { 0xC3, "LEN" },
        { 0xC4, "STR$" },
        { 0xC5, "VAL" },
        { 0xC6, "ASC" },
        { 0xC7, "CHR$" },

        { 0xC8, "LEFT$" },
        { 0xC9, "RIGHT$" },
        { 0xCA, "MID$" },
        { 0xCB, "GO" },
        { 0xCC, "{cc}" },
        { 0xCD, "{cd}" },
        { 0xCE, "{ce}" },
        { 0xCF, "{cf}" },

        { 0xD0, "{d0}" },
        { 0xD1, "{d1}" },
        { 0xD2, "{d2}" },
        { 0xD3, "{d3}" },
        { 0xD4, "{d4}" },
        { 0xD5, "{d5}" },
        { 0xD6, "{d6}" },
        { 0xD7, "{d7}" },

        { 0xD8, "{d8}" },
        { 0xD9, "{d9}" },
        { 0xDA, "{da}" },
        { 0xDB, "{db}" },
        { 0xDC, "{dc}" },
        { 0xDD, "{dd}" },
        { 0xDE, "{de}" },
        { 0xDF, "{df}" },

        { 0xE0, "{e0}" },
        { 0xE1, "{e1}" },
        { 0xE2, "{e2}" },
        { 0xE3, "{e3}" },
        { 0xE4, "{e4}" },
        { 0xE5, "{e5}" },
        { 0xE6, "{e6}" },
        { 0xE7, "{e7}" },

        { 0xE8, "{e8}" },
        { 0xE9, "{e9}" },
        { 0xEA, "{ea}" },
        { 0xEB, "{eb}" },
        { 0xEC, "{ec}" },
        { 0xED, "{ed}" },
        { 0xEE, "{ee}" },
        { 0xEF, "{ef}" },

        { 0xF0, "{f0}" },
        { 0xF1, "{f1}" },
        { 0xF2, "{f2}" },
        { 0xF3, "{f3}" },
        { 0xF4, "{f4}" },
        { 0xF5, "{f5}" },
        { 0xF6, "{f6}" },
        { 0xF7, "{f7}" },

        { 0xF8, "{f8}" },
        { 0xF9, "{f9}" },
        { 0xFA, "{fa}" },
        { 0xFB, "{fb}" },
        { 0xFC, "{fc}" },
        { 0xFD, "{fd}" },
        { 0xFE, "{fe}" },
        { 0xFF, "{pi}" }
    };
}
