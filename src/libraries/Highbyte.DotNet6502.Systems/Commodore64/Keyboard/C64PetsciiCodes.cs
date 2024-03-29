namespace Highbyte.DotNet6502.Systems.Commodore64.Keyboard;

/// <summary>
/// C64 keys with their corresponding PETSCII codes.
/// TODO: Is this needed?
/// </summary>
public enum C64PetsciiCodes : byte
{
    // Control codes
    Null = 0x00,
    Stop = 0x03,
    White = 0x05,
    DisableCShift = 0x08,
    EnableCShift = 0x09,
    Return = 0x0D,
    LoUpCharset = 0x0E,
    CursorDown = 0x11,
    ReverseOn = 0x12,
    Home = 0x13,
    Delete = 0x14,
    Red = 0x1C,
    CursorRight = 0x1D,
    Green = 0x1E,
    Blue = 0x1F,
    Orange = 0x81,
    Run = 0x83,
    F1 = 0x85,
    F2 = 0x86,
    F3 = 0x87,
    F4 = 0x88,
    F5 = 0x89,
    F6 = 0x8a,
    F7 = 0x8b,
    F8 = 0x8c,
    ShiftReturn = 0x8d,
    UpGfxCharset = 0x8e,
    Black = 0x90,
    CursorUp = 0x91,
    ReverseOff = 0x92,
    Clear = 0x93,
    Insert = 0x94,
    Brown = 0x95,
    Pink = 0x96,
    DarkGrey = 0x97,
    Grey = 0x98,
    LightGreen = 0x99,
    LightBlue = 0x9a,
    LightGrey = 0x9b,
    Purple = 0x9c,
    CursorLeft = 0x9d,
    Yellow = 0x9e,
    Cyan = 0x9f,

    // Printable characters
    Space = 0x20,
    ExclamationMark = 0x21,
    DoubleQuote = 0x22,
    Hash = 0x23,
    DollarSign = 0x24,
    PercentSign = 0x25,
    Ampersand = 0x26,
    SingleQuote = 0x27,
    LeftParenthesis = 0x28,
    RightParenthesis = 0x29,
    Asterisk = 0x2A,
    Plus = 0x2B,
    Comma = 0x2C,
    Minus = 0x2D,
    Period = 0x2E,
    Slash = 0x2F,
    Zero = 0x30,
    One = 0x31,
    Two = 0x32,
    Three = 0x33,
    Four = 0x34,
    Five = 0x35,
    Six = 0x36,
    Seven = 0x37,
    Eight = 0x38,
    Nine = 0x39,
    Colon = 0x3A,
    Semicolon = 0x3B,
    LessThan = 0x3C,
    Equal = 0x3D,
    GreaterThan = 0x3E,
    QuestionMark = 0x3F,
    At = 0x40,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    LeftBracket = 0x5B,
    Pound = 0x5C,
    RightBracket = 0x5D,
}
