namespace Highbyte.DotNet6502
{
    public static class MemoryHelpers
    {
        /// <summary>
        /// Fetches a byte from specified address.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static byte FetchByte(this Memory mem, ushort address)
        {
            return mem[address];
        }

        /// <summary>
        /// Same as FetchByte(this Memory mem, ushort address), with the only difference 
        /// that the address parameter is sent by ref, and increased 1 byte.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static byte FetchByte(this Memory mem, ref ushort address)
        {
            byte val = FetchByte(mem, address);
            address++;
            return val;
        }

        /// <summary>
        /// Fetches a word (aka ushort, aka UInt16) from specified address.
        /// The byte order is little endian, so the the least significant byte (lowbyte) is assumed to at address,
        /// and most significant byte is assumed to be at address + 1.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static ushort FetchWord(this Memory mem, ushort address)
        {
            byte byte1 = mem[address];
            byte byte2 = mem[(ushort)(address+1)];
            return ByteHelpers.ToLittleEndianWord(byte1, byte2);
        }

        /// <summary>
        /// Same as FetchByte(this Memory mem, ushort address), with the only difference 
        /// that the address parameter is sent by ref, and increased 2 bytes.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        public static ushort FetchWord(this Memory mem, ref ushort address)
        {
            var val = FetchWord(mem, address);
            address += 2;
            return val;
        }


        /// <summary>
        /// Writes the specified 8-bit byte to the specified address.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <param name="instruction"></param>
        public static void WriteByte(this Memory mem, ushort address, OpCodeId instruction)
        {
            WriteByte(mem, address, (byte)instruction);
        }

        /// <summary>
        /// Writes the specified 8-bit byte to the specified address.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        public static void WriteByte(this Memory mem, ushort address, byte data)
        {
            mem[address] = data;
        }

        /// <summary>
        /// Same as WriteByteToMemory(this Memory mem, ushort address, byte data), with the only difference 
        /// that the address parameter is sent by ref, and increased 1 byte.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <param name="instruction"></param>
        public static void WriteByte(this Memory mem, ref ushort address, OpCodeId instruction)
        {
            WriteByte(mem, ref address, (byte)instruction);
        }

        /// <summary>
        /// Same as WriteByteToMemory(this Memory mem, ushort address, byte data), with the only difference 
        /// that the address parameter is sent by ref, and increased 1 byte.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        public static void WriteByte(this Memory mem, ref ushort address, byte data)
        {
            WriteByte(mem, address, data);
            address++;
        }

        /// <summary>
        /// Writes the specified 16-bit word (aka ushort, aka UInt16) to the specified address.
        /// Uses Little-endian convention, and writes the low byte (least significant byte) first, then the high byte (the most significant byte).
        /// 
        /// If address is 0x4000, and data is 0xab12, the memory will look like this after being written.
        /// 0x4000: 0x12
        /// 0x4001: 0xab
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        public static void WriteWord(this Memory mem, ushort address, ushort data)
        {
            mem[address] = data.Lowbyte();
            mem[(ushort)(address+1)] = data.Highbyte();
        }

        /// <summary>
        /// Same as WriteWordToMemory(this Memory mem, ushort address, ushort data), with the only difference 
        /// that the address parameter is sent by ref, and increased 2 bytes.
        /// </summary>
        /// <param name="mem"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        public static void WriteWord(this Memory mem, ref ushort address, ushort data)
        {
            WriteWord(mem, address, data);
            address +=2;
        }
    }
}
