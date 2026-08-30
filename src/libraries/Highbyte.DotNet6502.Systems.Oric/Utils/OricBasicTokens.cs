namespace Highbyte.DotNet6502.Systems.Oric.Utils;

/// <summary>
/// Oric Extended BASIC 1.1 token table. Bytes $80-$F6 in a tokenized program stand for
/// these keywords; $F7-$FF are error-message codes and are not valid program tokens.
/// </summary>
public static class OricBasicTokens
{
    private static readonly string[] s_keywords =
    [
        "END", "EDIT", "STORE", "RECALL", "TRON", "TROFF", "POP", "PLOT",
        "PULL", "LORES", "DOKE", "REPEAT", "UNTIL", "FOR", "LLIST", "LPRINT",
        "NEXT", "DATA", "INPUT", "DIM", "CLS", "READ", "LET", "GOTO",
        "RUN", "IF", "RESTORE", "GOSUB", "RETURN", "REM", "HIMEM", "GRAB",
        "RELEASE", "TEXT", "HIRES", "SHOOT", "EXPLODE", "ZAP", "PING", "SOUND",
        "MUSIC", "PLAY", "CURSET", "CURMOV", "DRAW", "CIRCLE", "PATTERN", "FILL",
        "CHAR", "PAPER", "INK", "STOP", "ON", "WAIT", "CLOAD", "CSAVE",
        "DEF", "POKE", "PRINT", "CONT", "LIST", "CLEAR", "GET", "CALL",
        "!", "NEW", "TAB(", "TO", "FN", "SPC(", "@", "AUTO",
        "ELSE", "THEN", "NOT", "STEP", "+", "-", "*", "/",
        "^", "AND", "OR", ">", "=", "<", "SGN", "INT",
        "ABS", "USR", "FRE", "POS", "HEX$", "&", "SQR", "RND",
        "LN", "EXP", "COS", "SIN", "TAN", "ATN", "PEEK", "DEEK",
        "LOG", "LEN", "STR$", "VAL", "ASC", "CHR$", "PI", "TRUE",
        "FALSE", "KEY$", "SCRN", "POINT", "LEFT$", "RIGHT$", "MID$",
    ];

    public static IReadOnlyDictionary<byte, string> Tokens { get; } =
        s_keywords.Select((keyword, index) => new KeyValuePair<byte, string>((byte)(0x80 + index), keyword))
            .ToDictionary();
}
