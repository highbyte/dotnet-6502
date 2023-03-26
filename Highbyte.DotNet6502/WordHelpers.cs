namespace Highbyte.DotNet6502;

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
        return (byte) ((data>>8) & 0xff);
    }

    /// <summary>
    /// Returns the low  byte of specified word (aka ushort, aka UInt16)
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte Lowbyte(this ushort data) 
    {
        return (byte) (data & 0xff);
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
            Lowbyte(data),
            Highbyte(data)
        };
    }

    public static string ToHex(this ushort value, string hexPrefix="0x", bool lowerCase=false)
    {
        if(lowerCase)
            return $"{hexPrefix}{value:x4}";
        else
            return $"{hexPrefix}{value:X4}";
    }

    public static string ToHexAndDecimal(this ushort value, string hexPrefix="0x", bool lowerCase=false)
    {
        return $"{ToHex(value, hexPrefix, lowerCase)} ({value})";
    }

    public static string ToDecimalAndHex(this ushort value, string hexPrefix="0x", bool lowerCase=false)
    {
        return $"{value} ({ToHex(value, hexPrefix, lowerCase)})";
    }

}
