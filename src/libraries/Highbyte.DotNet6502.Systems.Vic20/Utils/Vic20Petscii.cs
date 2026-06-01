namespace Highbyte.DotNet6502.Systems.Vic20.Utils;

/// <summary>
/// PETSCII helpers shared by VIC-20 text features such as clipboard paste.
/// BASIC V2 on the VIC-20 uses the same PETSCII character encoding as the C64.
/// </summary>
public static class Vic20Petscii
{
    public static IReadOnlyDictionary<char, byte> CharToPetscii { get; } = new Dictionary<char, byte>
    {
        { (char)13, 0x0d },
        { ' ', 0x20 }, { '!', 0x21 }, { '"', 0x22 }, { '#', 0x23 }, { '$', 0x24 }, { '%', 0x25 }, { '&', 0x26 },
        { '\'', 0x27 }, { '(', 0x28 }, { ')', 0x29 }, { '*', 0x2a }, { '+', 0x2b }, { ',', 0x2c }, { '-', 0x2d },
        { '.', 0x2e }, { '/', 0x2f }, { '0', 0x30 }, { '1', 0x31 }, { '2', 0x32 }, { '3', 0x33 }, { '4', 0x34 },
        { '5', 0x35 }, { '6', 0x36 }, { '7', 0x37 }, { '8', 0x38 }, { '9', 0x39 }, { ':', 0x3a }, { ';', 0x3b },
        { '<', 0x3c }, { '=', 0x3d }, { '>', 0x3e }, { '?', 0x3f }, { '@', 0x40 }, { 'a', 0x41 }, { 'b', 0x42 },
        { 'c', 0x43 }, { 'd', 0x44 }, { 'e', 0x45 }, { 'f', 0x46 }, { 'g', 0x47 }, { 'h', 0x48 }, { 'i', 0x49 },
        { 'j', 0x4a }, { 'k', 0x4b }, { 'l', 0x4c }, { 'm', 0x4d }, { 'n', 0x4e }, { 'o', 0x4f }, { 'p', 0x50 },
        { 'q', 0x51 }, { 'r', 0x52 }, { 's', 0x53 }, { 't', 0x54 }, { 'u', 0x55 }, { 'v', 0x56 }, { 'w', 0x57 },
        { 'x', 0x58 }, { 'y', 0x59 }, { 'z', 0x5a }, { '[', 0x5b }, { '\\', 0x5c }, { '£', 0x5c }, { ']', 0x5d },
        { '^', 0x5e },
        { 'A', 0xc1 }, { 'B', 0xc2 }, { 'C', 0xc3 }, { 'D', 0xc4 }, { 'E', 0xc5 }, { 'F', 0xc6 }, { 'G', 0xc7 },
        { 'H', 0xc8 }, { 'I', 0xc9 }, { 'J', 0xca }, { 'K', 0xcb }, { 'L', 0xcc }, { 'M', 0xcd }, { 'N', 0xce },
        { 'O', 0xcf }, { 'P', 0xd0 }, { 'Q', 0xd1 }, { 'R', 0xd2 }, { 'S', 0xd3 }, { 'T', 0xd4 }, { 'U', 0xd5 },
        { 'V', 0xd6 }, { 'W', 0xd7 }, { 'X', 0xd8 }, { 'Y', 0xd9 }, { 'Z', 0xda },
    };
}
