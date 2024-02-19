namespace Highbyte.DotNet6502;

/// <summary>
/// Helper methods for 8-bit unsigned bytes (byte) and byte arrays.
/// Handles little endian memory layout.
/// </summary>
public static class ByteHelpers
{
    /// <summary>
    /// Returns a word (aka ushort, aka UInt16) from a byte array.
    /// It uses the first and second bytes in the array, unless pos is specified with an offset.
    /// 
    /// It combines the two bytes, reverses the order to handle Little endian, and returns a word.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static ushort ToLittleEndianWord(this byte[] data, uint pos = 0) 
    {
        return ToLittleEndianWord(data[pos], data[(ushort)(pos+1)]);
    }

    /// <summary>
    /// Returns a word (aka ushort, aka UInt16) from two bytes (in the order they are stored in memory).
    /// It combines the two bytes, reverses the order to handle Little endian, and returns a word.
    /// byte1 is considered lowbyte, byte2 is considered highbyte
    /// </summary>
    /// <param name="data"></param>
    /// <param name="byte1"></param>
    /// <param name="byte2"></param>
    /// <returns></returns>
    public static ushort ToLittleEndianWord(byte byte1, byte byte2)
    {
        // Add the second byte (highbyte) shifted left 8 bits to the first byte (lowbyte)
        ushort word = (ushort)(byte1 | byte2 << 8);
        return word;
    }

    public static string ToHex(this byte value, string hexPrefix="0x", bool lowerCase=false)
    {
        if(lowerCase)
            return $"{hexPrefix}{value:x2}";
        else
            return $"{hexPrefix}{value:X2}";
    }

    public static string ToHexAndDecimal(this byte value, string hexPrefix="0x", bool lowerCase=false)
    {
        return $"{ToHex(value, hexPrefix, lowerCase)} ({value})";
    }

    public static string ToDecimalAndHex(this byte value, string hexPrefix="0x", bool lowerCase=false)
    {
        return $"{value} ({ToHex(value, hexPrefix, lowerCase)})";
    }

    /// <summary>
    /// Shifts the bits in an array of bytes to the right a specified number of position.
    /// </summary>
    /// <param name="bytes">The span of bytes to shift. Results will be stored in</param>
    /// <param name="numberOfBits">The number of bits to shift</param>
    /// <param name="shiftedBytes">A reference to a span of bytes where the resulted shifted bytes are stored. The span must be at least as long as the original bytes span.</param>
    /// <param name="carryBit">True if bit 0 in the right-most byte in the array was 1 before the last shift.</param>
    public static void ShiftRight(this Span<byte> bytes, ref Span<byte> shiftedBytes, int numberOfBits, out bool carryBit)
    {
        Span<byte> workingBytes = stackalloc byte[bytes.Length];
        bytes.CopyTo(workingBytes);

        carryBit = false;
        for (int i = 0; i < numberOfBits; i++)
        {
            ShiftRight(workingBytes, ref workingBytes, out carryBit);
        }
        workingBytes.CopyTo(shiftedBytes);
    }

    /// <summary>
    /// Shifts the bits in an array of bytes to the right on position.
    /// </summary>
    /// <param name="bytes">The byte array to shift.</param>
    /// <param name="shiftedBytes">A reference to a span of bytes where the resulted shifted bytes are stored. The span must be at least as long as the original bytes span.</param>
    /// <param name="carryBit">True if bit 0 in the right-most byte in the array was 1 before the last shift.</param>
    public static void ShiftRight(ReadOnlySpan<byte> bytes, ref Span<byte> shiftedBytes, out bool carryBit)
    {
        bytes.CopyTo(shiftedBytes);

        bool rightMostCarryFlag = false;
        int rightEnd = shiftedBytes.Length - 1;

        // Iterate through the elements of the array right to left.
        for (int index = rightEnd; index >= 0; index--)
        {
            // If the rightmost bit of the current byte is 1 then we have a carry.
            bool carryFlag = (shiftedBytes[index] & 0x01) > 0;
            if (index < rightEnd)
            {
                if (carryFlag == true)
                {
                    // Apply the carry to the leftmost bit of the current bytes neighbor to the right.
                    shiftedBytes[index + 1] = (byte)(shiftedBytes[index + 1] | 0x80);
                }
            }
            else
            {
                rightMostCarryFlag = carryFlag;
            }
            shiftedBytes[index] = (byte)(shiftedBytes[index] >> 1);
        }
        carryBit = rightMostCarryFlag;
    }

    public static bool IsDefinedAsOpCodeId(this byte opCodeId)
    {
        return Enum.IsDefined(typeof(OpCodeId), opCodeId);
    }
    public static OpCodeId ToOpCodeId(this byte opCodeId)
    {
        return (OpCodeId)opCodeId;
    }
}
