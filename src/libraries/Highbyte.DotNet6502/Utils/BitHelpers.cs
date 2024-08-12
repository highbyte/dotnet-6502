namespace Highbyte.DotNet6502.Utils;

public static class BitHelpers
{
    public static bool IsBitSet(this byte data, int bit)
    {
        return (data >> bit & 1) == 1;
    }

    public static bool IsBitSet(this byte data, StatusFlagBits bit)
    {
        return (data >> (int)bit & 1) == 1;
    }

    public static void SetBit(this ref byte data, int bit)
    {
        data.ChangeBit(bit, true);
    }
    public static void SetBit(this ref byte data, StatusFlagBits bit)
    {
        data.ChangeBit(bit, true);
    }
    public static void ClearBit(this ref byte data, int bit)
    {
        data.ChangeBit(bit, false);
    }
    public static void ClearBit(this ref byte data, StatusFlagBits bit)
    {
        data.ChangeBit(bit, false);
    }

    public static void ChangeBit(this ref byte data, int bit, bool state)
    {
        var bitValue = (byte)(1 << bit);
        if (state)
            // Set bit
            data = (byte)(data | bitValue);  // or
        else
            // Clear bit. TODO: Better way to clear a bit?
            data = (byte)(data & (255 ^ bitValue));  // and + xor
    }

    public static void ChangeBit(this ref byte data, StatusFlagBits bit, bool state)
    {
        var bitValue = (byte)(1 << (int)bit);
        if (state)
            // Set bit
            data = (byte)(data | bitValue);  // or
        else
            // Clear bit. TODO: Better way to clear a bit?
            data = (byte)(data & (255 ^ bitValue));  // and + xor
    }

    public static void ClearBits(this ref byte data, byte clearBits)
    {
        data = (byte)(data & ~clearBits);
    }
}
