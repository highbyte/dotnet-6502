namespace Highbyte.DotNet6502.Utils;

/// <summary>
/// Helper methods for 16-bit unsigned words (ushort).
/// Handles little-endian memory layout.
/// </summary>
public static class WordHelpers
{
    /// <summary>
    /// Returns the high byte of specified word (aka ushort, aka UInt16)
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte Highbyte(this ushort data)
    {
        return (byte)(data >> 8 & 0xff);
    }

    /// <summary>
    /// Returns the low  byte of specified word (aka ushort, aka UInt16)
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte Lowbyte(this ushort data)
    {
        return (byte)(data & 0xff);
    }

    /// <summary>
    /// Sets the high byte of specified word (aka ushort, aka UInt16)
    /// </summary>
    /// <param name="data"></param>
    /// <param name="value"></param>
    public static void SetHighbyte(this ref ushort data, byte value)
    {
        data = (ushort)(data & 0x00ff | value << 8);
    }

    /// <summary>
    /// Sets the low byte of specified word (aka ushort, aka UInt16)
    /// </summary>
    /// <param name="data"></param>
    /// <param name="value"></param>
    public static void SetLowbyte(this ref ushort data, byte value)
    {
        data = (ushort)(data & 0xff00 | value);
    }

    /// <summary>
    /// Helps with writing bytes to memory in little endian systems (as 6502).
    /// 
    /// Returns the bytes from the specified word (aka ushort, aka UInt16) in reverse order.
    /// The lowbyte first, the highbyte second.
    /// 
    /// If data is
    ///      0xab12
    /// the returned array will be 
    ///     0x12,0xab
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte[] ToLittleEndianBytes(this ushort data)
    {
        return new byte[]
        {
            data.Lowbyte(),
            data.Highbyte()
        };
    }

    public static string ToHex(this ushort value, string hexPrefix = "0x", bool lowerCase = false)
    {
        if (lowerCase)
            return $"{hexPrefix}{value:x4}";
        else
            return $"{hexPrefix}{value:X4}";
    }

    public static string ToHexAndDecimal(this ushort value, string hexPrefix = "0x", bool lowerCase = false)
    {
        return $"{value.ToHex(hexPrefix, lowerCase)} ({value})";
    }

    public static string ToDecimalAndHex(this ushort value, string hexPrefix = "0x", bool lowerCase = false)
    {
        return $"{value} ({value.ToHex(hexPrefix, lowerCase)})";
    }

    /// <summary>
    /// Read a little endian word from stream.
    /// Return -1 if end of stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static int ReadWord(this MemoryStream stream)
    {
        byte[] wordBytes = new byte[2];
        var byte1 = stream.ReadByte();
        if (byte1 < 0)
            return -1;
        var byte2 = stream.ReadByte();
        if (byte2 < 0)
            return -1;
        return ByteHelpers.ToLittleEndianWord((byte)byte1, (byte)byte2);
    }
}
