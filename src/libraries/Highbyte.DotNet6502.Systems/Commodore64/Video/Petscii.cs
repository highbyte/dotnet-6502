namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

public static class Petscii
{
    /// <summary>
    /// PetscII codes for colors
    /// </summary>
    public enum Colors : byte
    {
        White = 0x05,
        Red = 0x1c,
        Green = 0x1e,
        Blue = 0x1f,
        Orange = 0x81,
        Black = 0x90,
        Brown = 0x95,
        LightRed = 0x96,
        DarkGray = 0x97,
        MediumGray = 0x98,
        LightGreen = 0x99,
        LightBlue = 0x9a,
        LightGray = 0x9b,
        Purple = 0x9c,
        Yellow = 0x9e,
        Cyan = 0x9f,
    };

    /// <summary>
    /// Common C# character to PetscII code.
    /// These are printable on a normal PC/Mac keyboard.
    /// For mapping of non-printable PC/Mac keys (ex: Enter, Shift, Up), use specific mapping for the input system implementation (Skia, SadConsole etc)
    /// </summary>
    /// <returns></returns>
    public static Dictionary<char, byte> CharToPetscii = new()
    {
        {(char)13,          0x0d},  // Return
        {' ',               0x20},
        {'!',               0x21},
        {'"',               0x22},
        {'#',               0x23},
        {'$',               0x24},
        {'%',               0x25},
        {'&',               0x26},
        {'\'',              0x27},
        {'(',               0x28},
        {')',               0x29},
        {'*',               0x2a},
        {'+',               0x2b},
        {',',               0x2c},
        {'-',               0x2d},
        {'.',               0x2e},
        {'/',               0x2f},
        {'0',               0x30},
        {'1',               0x31},
        {'2',               0x32},
        {'3',               0x33},
        {'4',               0x34},
        {'5',               0x35},
        {'6',               0x36},
        {'7',               0x37},
        {'8',               0x38},
        {'9',               0x39},
        {':',               0x3a},
        {';',               0x3b},
        {'<',               0x3c},
        {'=',               0x3d},
        {'>',               0x3e},
        {'?',               0x3f},
        {'@',               0x40},
        {'a',               0x41},
        {'b',               0x42},
        {'c',               0x43},
        {'d',               0x44},
        {'e',               0x45},
        {'f',               0x46},
        {'g',               0x47},
        {'h',               0x48},
        {'i',               0x49},
        {'j',               0x4a},
        {'k',               0x4b},
        {'l',               0x4c},
        {'m',               0x4d},
        {'n',               0x4e},
        {'o',               0x4f},
        {'p',               0x50},
        {'q',               0x51},
        {'r',               0x52},
        {'s',               0x53},
        {'t',               0x54},
        {'u',               0x55},
        {'v',               0x56},
        {'w',               0x57},
        {'x',               0x58},
        {'y',               0x59},
        {'z',               0x5a},
        {'[',               0x5b},
        {'\\',              0x5c},  // Map backslash to Pound sign
        {'£',               0x5c},  // Pound sign
        {']',               0x5d},
        {'^',               0x5e},

        {'A',               0xc1},
        {'B',               0xc2},
        {'C',               0xc3},
        {'D',               0xc4},
        {'E',               0xc5},
        {'F',               0xc6},
        {'G',               0xc7},
        {'H',               0xc8},
        {'I',               0xc9},
        {'J',               0xca},
        {'K',               0xcb},
        {'L',               0xcc},
        {'M',               0xcd},
        {'N',               0xce},
        {'O',               0xcf},
        {'P',               0xd0},
        {'Q',               0xd1},
        {'R',               0xd2},
        {'S',               0xd3},
        {'T',               0xd4},
        {'U',               0xd5},
        {'V',               0xd6},
        {'W',               0xd7},
        {'X',               0xd8},
        {'Y',               0xd9},
        {'Z',               0xda},

    };

    /// <summary>
    /// Maps C64 "PETSCII" codes to ASCII characters
    /// </summary>
    /// <returns></returns>
    // TODO
    // public static Dictionary<byte, byte> PETSCIIMap = new()
    // {
    //     { 0x00, 0x00},
    // };

    // Useful when rendering the C64 text screen with PC text characters
    public static byte C64ScreenCodeToPetscII(byte screenCode)
    {
        // Ref: http://sta.c64.org/cbm64scrtopet.html
        int petsciiCode = screenCode switch
        {
            >= 0 and <= 31 => screenCode + 64,
            >= 32 and <= 63 => screenCode,
            >= 64 and <= 93 => screenCode + 128,
            94 => 255,
            95 => 223,
            >= 96 and <= 127 => screenCode + 64,
            >= 128 and <= 159 => screenCode - 128,
            >= 160 and <= 191 => screenCode - 128,
            >= 192 and <= 223 => screenCode - 64,
            >= 224 and <= 254 => screenCode - 64,
            _ => throw new NotImplementedException(),
        };
        return (byte)petsciiCode;
    }

    public static byte PetscIIToAscII(byte petsciiCode)
    {
        // Ref: https://thec64community.online/thread/77/petscii-ascii-tool?page=1&scrollTo=438
        byte asciiCode = default!;
        // If the PETSCII character is A-Z, make it a-z (PETSCII 97-122, subtract 32)
        if (petsciiCode >= 97 && petsciiCode <= 122)
        {
            asciiCode = (byte)(petsciiCode - 32);
        }
        // If the PETSCII character is a-z, make it A-Z (PETSCII 65-90, add 32)
        else if (petsciiCode >= 65 && petsciiCode <= 90)
        {
            asciiCode = (byte)(petsciiCode + 32);
        }
        // If the PETSCII character is 192-223, subtract 96. Then subtract 32 if the resultant value is 97-122.                    
        else if (petsciiCode >= 192 && petsciiCode <= 223)
        {
            asciiCode = (byte)(petsciiCode - 96);
            if (asciiCode >= 97 && asciiCode <= 122)
                asciiCode = (byte)(asciiCode - 32);
        }
        else
        {
            asciiCode = petsciiCode;
        }

        return asciiCode;
    }
}
