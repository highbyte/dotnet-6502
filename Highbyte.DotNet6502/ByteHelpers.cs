using System;

namespace Highbyte.DotNet6502
{
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
        /// byte1 is considere lowbyte, byte2 is considered highbyte
        /// </summary>
        /// <param name="data"></param>
        /// <param name="byte1"></param>
        /// <param name="byte2"></param>
        /// <returns></returns>
        public static ushort ToLittleEndianWord(byte byte1, byte byte2) 
        {
            // First byte is lowbyte
            byte lowbyte = byte1;
            // Second byte is highbyte
            byte highbyte = byte2;

            // Add the second byte (highbyte) shifted left 8 bits to the first byte (lowbyte)
            ushort word = (ushort)(lowbyte | highbyte << 8);
            return word;
        }

        public static string ToHex(this byte value, string hexPrefix="0x")
        {
            return $"{hexPrefix}{value:X2}";
        }

        public static string ToHexAndDecimal(this byte value, string hexPrefix="0x")
        {
            return $"{ToHex(value, hexPrefix)} ({value})";
        }

        public static string ToDecimalAndHex(this byte value, string hexPrefix="0x")
        {
            return $"{value} ({ToHex(value, hexPrefix)})";
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
}
