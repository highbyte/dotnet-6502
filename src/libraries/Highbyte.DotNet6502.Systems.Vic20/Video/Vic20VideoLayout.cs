using Highbyte.DotNet6502.Systems.Vic20.Config;

namespace Highbyte.DotNet6502.Systems.Vic20.Video;

public readonly record struct Vic20VideoLayout(
    ushort ScreenStartAddress,
    ushort ColorStartAddress,
    ushort CharacterStartAddress,
    int Columns,
    int Rows,
    int CharacterHeight,
    byte BackgroundColor,
    byte BorderColor,
    byte AuxiliaryColor,
    bool ReverseScreen)
{
    public const ushort RegisterColumns = 0x9002;
    public const ushort RegisterRows = 0x9003;
    public const ushort RegisterAddress = 0x9005;
    public const ushort RegisterAuxiliaryColor = 0x900E;
    public const ushort RegisterBackgroundBorderColor = 0x900F;

    public static Vic20VideoLayout FromMemory(Memory mem, Vic20Config config)
    {
        var columnsRegister = mem[RegisterColumns];
        var rowsRegister = mem[RegisterRows];
        var addressRegister = mem[RegisterAddress];
        var auxiliaryColorRegister = mem[RegisterAuxiliaryColor];
        var backgroundBorderRegister = mem[RegisterBackgroundBorderColor];

        var columns = columnsRegister & 0x7F;
        if (columns == 0)
            columns = Vic20Config.Cols;

        var rows = (rowsRegister >> 1) & 0x3F;
        if (rows == 0)
            rows = Vic20Config.Rows;

        var characterHeight = (rowsRegister & 0x01) != 0 ? 16 : 8;

        return new Vic20VideoLayout(
            ScreenStartAddress: DecodeScreenStartAddress(columnsRegister, addressRegister),
            ColorStartAddress: DecodeColorStartAddress(columnsRegister),
            CharacterStartAddress: DecodeCharacterStartAddress(addressRegister),
            Columns: columns,
            Rows: rows,
            CharacterHeight: characterHeight,
            BackgroundColor: (byte)((backgroundBorderRegister >> 4) & 0x0F),
            BorderColor: (byte)(backgroundBorderRegister & 0x07),
            AuxiliaryColor: (byte)((auxiliaryColorRegister >> 4) & 0x0F),
            ReverseScreen: (backgroundBorderRegister & 0x08) == 0);
    }

    public static ushort DecodeScreenStartAddress(byte columnsRegister, byte addressRegister)
    {
        var screenBaseAddress = (ushort)(((addressRegister >> 4) & 0x07) << 10);
        if ((columnsRegister & 0x80) != 0)
            screenBaseAddress |= 0x0200;
        if ((addressRegister & 0x80) == 0)
            screenBaseAddress |= 0x8000;
        return screenBaseAddress;
    }

    public static ushort DecodeColorStartAddress(byte columnsRegister)
    {
        return (ushort)(0x9400 + (((columnsRegister & 0x80) != 0) ? 0x0200 : 0x0000));
    }

    public static ushort DecodeCharacterStartAddress(byte addressRegister)
    {
        var characterBaseAddress = (ushort)((addressRegister & 0x07) << 10);
        if ((addressRegister & 0x08) == 0)
            characterBaseAddress |= 0x8000;
        return characterBaseAddress;
    }

    public static byte EncodeColumnsRegister(ushort screenStartAddress, int columns)
    {
        var screenA9 = (byte)((screenStartAddress >> 9) & 0x01);
        return (byte)((columns & 0x7F) | (screenA9 << 7));
    }

    public static byte EncodeRowsRegister(int rows, bool doubleHeight = false)
    {
        return (byte)(((rows & 0x3F) << 1) | (doubleHeight ? 0x01 : 0x00));
    }

    public static byte EncodeAddressRegister(ushort screenStartAddress, ushort characterStartAddress)
    {
        var screenNibble = EncodeScreenNibble(screenStartAddress);
        var characterNibble = EncodeCharacterNibble(characterStartAddress);
        return (byte)((screenNibble << 4) | characterNibble);
    }

    private static byte EncodeScreenNibble(ushort screenStartAddress)
    {
        var inBlock0 = screenStartAddress < 0x8000;
        var upperAddressBits = (screenStartAddress >> 10) & 0x07;
        return (byte)((inBlock0 ? 0x08 : 0x00) | upperAddressBits);
    }

    private static byte EncodeCharacterNibble(ushort characterStartAddress)
    {
        var inBlock0 = characterStartAddress < 0x8000;
        var upperAddressBits = (characterStartAddress >> 10) & 0x07;
        return (byte)((inBlock0 ? 0x08 : 0x00) | upperAddressBits);
    }
}
